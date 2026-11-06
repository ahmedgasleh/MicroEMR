param([ValidateSet('Plan','Stage','Activate','Cutback')][string]$Phase='Plan')
. (Join-Path $PSScriptRoot 'Common.ps1')
if($Phase -eq 'Plan'){
    [ordered]@{Operation='PREPARATION ONLY';Strategy='Fresh same-tenant database; original retained';Source=$Plan.database;Stage=$Plan.stageDatabase;Archive=$Plan.archiveDatabase;CutbackHold=$Plan.failedDatabase;SeedCounts=$Plan.counts;PreservationTables=$Plan.preserveTables;ExecutionConsent='Separate phase-specific owner authorization required'}|ConvertTo-Json -Depth 8
    return
}
Require-Consent $Phase
$statePath=Join-Path $Evidence 'replacement.json'
function State {Require (Test-Path -LiteralPath $statePath) 'Replacement receipt missing.';Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json}
function Save($state){Save-Json 'replacement.json' $state}
function Same($a,$b){(Digest ($a|ConvertTo-Json -Depth 30 -Compress)) -ceq (Digest ($b|ConvertTo-Json -Depth 30 -Compress))}
function Capture-Storage {
    $storage='D:\MicroEMR-Data\patient-files';Require (Test-Path -LiteralPath $storage -PathType Container) 'Existing storage root unavailable.'
    $destination=Join-Path $Evidence 'preserved-storage';Require (-not(Test-Path -LiteralPath $destination)) 'Do not overwrite storage preservation.'
    Copy-Item -LiteralPath $storage -Destination $destination -Recurse
    $files=@(Get-ChildItem -LiteralPath $storage -File -Recurse|ForEach-Object{
        $relative=$_.FullName.Substring($storage.Length).TrimStart('\');$copy=Join-Path $destination $relative
        Require ((Get-FileHash -LiteralPath $_.FullName).Hash -eq (Get-FileHash -LiteralPath $copy).Hash) 'Storage preservation mismatch.'
        [ordered]@{RelativePath=$relative;Length=$_.Length;Hash=(Get-FileHash -LiteralPath $copy).Hash}
    });Save-Json 'preserved-storage.json' $files
}
function Preserve-SqlAccess($source,$stage){
    # Recreate only the same database access for the same existing server principals
    # in the new store. No ALTER LOGIN, password, Auth or platform writes.
    $principals=Query $source "SELECT d.name,d.type,d.sid,SUSER_SNAME(d.sid) LoginName FROM sys.database_principals d WHERE d.principal_id>4 AND d.type IN ('S','U','G') AND d.name NOT LIKE '##%'"
    foreach($p in $principals.Rows){Require (-not [string]::IsNullOrWhiteSpace([string]$p.LoginName)) 'Contained/unresolved database principal requires explicit review.'
        $name=Quote $p.name;$login=Quote $p.LoginName;Exec $stage "IF DATABASE_PRINCIPAL_ID(N'$($p.name)') IS NULL CREATE USER $name FOR LOGIN $login;"}
    $roles=Query $source "SELECT name FROM sys.database_principals WHERE type='R' AND is_fixed_role=0 AND name<>'public'"
    foreach($r in $roles.Rows){$name=Quote $r.name;Exec $stage "IF DATABASE_PRINCIPAL_ID(N'$($r.name)') IS NULL CREATE ROLE $name AUTHORIZATION dbo;"}
    $members=Query $source 'SELECT r.name RoleName,u.name UserName FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id=m.role_principal_id JOIN sys.database_principals u ON u.principal_id=m.member_principal_id WHERE u.principal_id>4'
    foreach($m in $members.Rows){Exec $stage ('IF NOT EXISTS(SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(@role) AND member_principal_id=DATABASE_PRINCIPAL_ID(@member)) ALTER ROLE '+(Quote $m.RoleName)+' ADD MEMBER '+(Quote $m.UserName)+';') @{'@role'=[string]$m.RoleName;'@member'=[string]$m.UserName}}
    $permissionSql="SELECT p.state,p.permission_name,p.class,grantee.name Grantee,OBJECT_SCHEMA_NAME(p.major_id) ObjectSchema,OBJECT_NAME(p.major_id) ObjectName,SCHEMA_NAME(p.major_id) SchemaName,COL_NAME(p.major_id,p.minor_id) ColumnName,p.minor_id FROM sys.database_permissions p JOIN sys.database_principals grantee ON grantee.principal_id=p.grantee_principal_id WHERE grantee.principal_id>4 OR grantee.name='public'"
    function Permission-Key($g){return (@([string]$g.state,[string]$g.permission_name,[string]$g.class,[string]$g.Grantee,[string]$g.ObjectSchema,[string]$g.ObjectName,[string]$g.SchemaName,[string]$g.ColumnName,[string]$g.minor_id)|ConvertTo-Json -Compress)}
    $existing=@{};foreach($g in (Query $stage $permissionSql).Rows){$existing[(Permission-Key $g)]=$true}
    $grants=Query $source $permissionSql
    foreach($g in $grants.Rows){
        # Engine-supplied system-object grants already exist; never try to re-GRANT them.
        if($existing.ContainsKey((Permission-Key $g))){continue}
        Require ($g.class -in @(0,1,3) -and $g.state -in @('G','W','D') -and ($g.minor_id -eq 0 -or ($g.class -eq 1 -and -not [string]::IsNullOrWhiteSpace([string]$g.ColumnName)))) 'Unsupported permission must be reviewed; do not broaden grants.'
        Require ($g.class -ne 1 -or $g.ObjectSchema -ne 'sys') 'Missing engine-supplied system permission; stop rather than alter system objects.'
        Require ($g.permission_name -match '^[A-Z ]+$') 'Unexpected permission name.'
        $scope=if($g.class -eq 1){' ON OBJECT::'+(Quote $g.ObjectSchema)+'.'+(Quote $g.ObjectName)}elseif($g.class -eq 3){' ON SCHEMA::'+(Quote $g.SchemaName)}else{''}
        if($g.minor_id -gt 0){$scope+=' ('+(Quote $g.ColumnName)+')'}
        $verb=if($g.state -eq 'D'){'DENY'}else{'GRANT'};$option=if($g.state -eq 'W'){' WITH GRANT OPTION'}else{''}
        Exec $stage ($verb+' '+$g.permission_name+$scope+' TO '+(Quote $g.Grantee)+$option+';')
    }
}
function SqlAccess($c){
    [ordered]@{
        Principals=[string](Scalar $c "SELECT COALESCE((SELECT name,type,CASE WHEN type='R' THEN '' ELSE CONVERT(varchar(170),sid,1) END Sid FROM sys.database_principals WHERE principal_id>4 AND type IN ('S','U','G','R') ORDER BY name FOR JSON PATH),N'[]')")
        Roles=[string](Scalar $c 'SELECT COALESCE((SELECT r.name RoleName,u.name UserName FROM sys.database_role_members m JOIN sys.database_principals r ON r.principal_id=m.role_principal_id JOIN sys.database_principals u ON u.principal_id=m.member_principal_id WHERE u.principal_id>4 ORDER BY r.name,u.name FOR JSON PATH),N''[]'')')
        Permissions=[string](Scalar $c "SELECT COALESCE((SELECT u.name Grantee,p.state,p.permission_name,p.class,CASE p.class WHEN 1 THEN OBJECT_SCHEMA_NAME(p.major_id)+'.'+OBJECT_NAME(p.major_id) WHEN 3 THEN SCHEMA_NAME(p.major_id) ELSE '' END Scope,p.minor_id FROM sys.database_permissions p JOIN sys.database_principals u ON u.principal_id=p.grantee_principal_id WHERE u.principal_id>4 OR u.name='public' ORDER BY u.name,p.class,Scope,p.permission_name,p.minor_id FOR JSON PATH),N'[]')")
    }
}
function Repair-ReviewedStage {
    # Bounded recovery of the observed access-copy failure; never recreate or clear a partial store.
    $state=State
    Require ($state.Phase -eq 'Preparing' -and $state.BackupVerified -and -not $state.ParityVerified -and -not $state.Activated) 'Partial stage is outside the reviewed recovery state.'
    Require ($state.OriginalDatabaseId -eq 44 -and $state.StageDatabaseId -eq 0) 'Partial stage receipt identity differs from the reviewed failure.'
    Require ($state.PlanDigest -ceq (Digest (Get-Content (Join-Path $PSScriptRoot 'demo-plan.json') -Raw))) 'Plan changed; no partial-stage recovery.'
    $failure=Get-Content -LiteralPath (Join-Path $Evidence 'phase-failure.json') -Raw|ConvertFrom-Json
    Require ($failure.Phase -eq 'Stage' -and $failure.Stack -match 'at Preserve-SqlAccess,') 'Partial stage has a different failure; stop for review.'
    Assert-Api
    $source=Connect-Clinical $Plan.database;$stage=Connect-Clinical $Plan.stageDatabase;$master=Connect-Clinical 'master'
    try{
        Require ((Assert-Identity $source) -eq 44 -and (Assert-Identity $stage) -eq 45) 'Reviewed original/stage database identities changed.'
        foreach($db in @($Plan.archiveDatabase,$Plan.failedDatabase)){Require ((Scalar $master 'SELECT COALESCE(DB_ID(@db),0)' @{'@db'=$db}) -eq 0) 'Archive/hold name occupied; no recovery.'}
        Require (Same $state.Preservation (Preservation $source)) 'Original preservation changed.'
        Require (Same $state.Preservation (Preservation $stage)) 'Staged data/configuration parity differs.'
        Require (Same $state.Platform (Platform-Snapshot)) 'Platform access changed.'
        Require (Same $state.SqlAccess (SqlAccess $source)) 'Original SQL access changed.'
        Require ((Scalar $stage 'SELECT COUNT(*) FROM dbo.Patient') -eq 0) 'Partial stage contains patients.'
        $manifest=Get-Content (Join-Path $Repo 'db\tenant-clinical\manifest.json') -Raw|ConvertFrom-Json
        Require ($manifest[-1].migrationId -eq $Plan.requiredMigration -and (Scalar $stage 'SELECT COUNT(*) FROM dbo.SchemaMigration') -eq $manifest.Count) 'Stage migration boundary differs.'
        foreach($migration in $manifest){
            $sql=Get-Content -LiteralPath (Join-Path (Join-Path $Repo 'db') $migration.script) -Raw
            $hash=Digest $sql.TrimStart([char]0xFEFF).Replace("`r`n","`n").Replace("`r","`n")
            Require ((Scalar $stage 'SELECT COUNT(*) FROM dbo.SchemaMigration WHERE MigrationId=@id AND ScriptHash=@hash' @{'@id'=$migration.migrationId;'@hash'=$hash}) -eq 1) 'Stage migration hash differs.'
        }
        $storage='D:\MicroEMR-Data\patient-files';$preserved=Join-Path $Evidence 'preserved-storage'
        Require ((Test-Path -LiteralPath $storage -PathType Container) -and (Test-Path -LiteralPath $preserved -PathType Container)) 'Storage preservation unavailable.'
        $sourceFiles=@(Get-ChildItem -LiteralPath $storage -File -Recurse);$savedFiles=@(Get-ChildItem -LiteralPath $preserved -File -Recurse)
        Require ($sourceFiles.Count -eq $savedFiles.Count) 'Storage file count differs.'
        foreach($file in $sourceFiles){$copy=Join-Path $preserved $file.FullName.Substring($storage.Length).TrimStart('\');Require ((Test-Path -LiteralPath $copy -PathType Leaf) -and (Get-FileHash -LiteralPath $file.FullName).Hash -ceq (Get-FileHash -LiteralPath $copy).Hash) 'Storage bytes differ.'}
        $apiRoot=Join-Path $Repo 'src\MicroEMR.Api';$apiCopy=Join-Path $Evidence 'preserved-api'
        foreach($relative in @('appsettings.json','appsettings.Development.json','Properties\launchSettings.json')){
            Require ((Get-FileHash -LiteralPath (Join-Path $apiRoot $relative)).Hash -ceq (Get-FileHash -LiteralPath (Join-Path $apiCopy ([IO.Path]::GetFileName($relative)))).Hash) 'Preserved API configuration changed.'
        }
        $output=Join-Path $apiRoot 'bin\Debug\net10.0';$savedOutput=Join-Path $apiCopy 'output'
        $liveFiles=@(Get-ChildItem -LiteralPath $output -File -Recurse);$savedFiles=@(Get-ChildItem -LiteralPath $savedOutput -File -Recurse)
        Require ($liveFiles.Count -eq $savedFiles.Count) 'Preserved API output file count changed.'
        foreach($file in $liveFiles){$copy=Join-Path $savedOutput $file.FullName.Substring($output.Length).TrimStart('\');Require ((Test-Path -LiteralPath $copy -PathType Leaf) -and (Get-FileHash -LiteralPath $file.FullName).Hash -ceq (Get-FileHash -LiteralPath $copy).Hash) 'Preserved API output bytes changed.'}
        Require ($state.Backup -match '^C:\\Program Files\\Microsoft SQL Server\\MSSQL15\.TELEMED52\\MSSQL\\Backup\\MicroEMR_LocalDev_Fresh_demo_[a-f0-9]{32}\.bak$') 'Backup outside reviewed location.'
        Exec $source 'RESTORE VERIFYONLY FROM DISK=@p WITH CHECKSUM,STOP_ON_ERROR;' @{'@p'=[string]$state.Backup}
        $owner=Scalar $master 'SELECT COUNT(*) FROM sys.databases a JOIN sys.databases b ON a.owner_sid=b.owner_sid WHERE a.name=@source AND b.name=@stage' @{'@source'=$Plan.database;'@stage'=$Plan.stageDatabase}
        Require ($owner -eq 1) 'Original/stage database owners differ.'
        # Keep the original failure receipt and state intact as recovery evidence.
        Require (-not(Test-Path (Join-Path $Evidence 'reviewed-stage-recovery.json'))) 'Recovery already attempted; inspect its receipt.'
        Save-Json 'reviewed-stage-recovery.json' ([ordered]@{OriginalState=$state;Failure=$failure;ReviewedStageDatabaseId=45;StartedAtUtc=[DateTime]::UtcNow.ToString('o')})
        # The old pipeline serializer omitted the inventory entirely for an empty storage root.
        if(-not(Test-Path (Join-Path $Evidence 'preserved-storage.json'))){
            $inventory=@($sourceFiles|ForEach-Object{[ordered]@{RelativePath=$_.FullName.Substring($storage.Length).TrimStart('\');Length=$_.Length;Hash=(Get-FileHash -LiteralPath $_.FullName).Hash}})
            Save-Json 'preserved-storage.json' $inventory
        }
        Preserve-SqlAccess $source $stage
        Require (Same $state.SqlAccess (SqlAccess $stage)) 'Recovered SQL access parity failed; no activation.'
        Require (Same $state.Preservation (Preservation $stage)) 'Recovered data/configuration parity failed.'
        Require (Same $state.Platform (Platform-Snapshot)) 'Platform access changed during recovery.'
        $state.StageDatabaseId=45;$state.ParityVerified=$true;$state.Phase='Staged';Save $state
        Write-Output 'Reviewed retained stage recovered; backup, preservation and exact SQL-access parity gates passed. No activation performed.'
    }finally{$master.Dispose();$stage.Dispose();$source.Dispose()}
}
if($Phase -eq 'Stage' -and (Test-Path -LiteralPath $statePath)){
    Repair-ReviewedStage
    return
}
if($Phase -eq 'Stage'){
    Require (-not(Test-Path -LiteralPath $statePath)) 'An existing/partial run must be reviewed; do not create another store.'
    Assert-Api # Fail BEFORE backups or stage creation if legitimate runtime authentication is unavailable.
    $source=Connect-Clinical $Plan.database;$master=Connect-Clinical 'master'
    try{
        $originalId=Assert-Identity $source
        $actor=Query $source 'SELECT u.UserId FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId WHERE u.UserId=@actor AND u.AuthSubjectId=@subject AND u.IsActive=1 AND p.IsActive=1 AND p.ProviderUid=@provider' @{'@actor'=$Plan.actorId;'@subject'=$Plan.actorSubject;'@provider'=[guid]$Plan.providerUid}
        Require ($actor.Rows.Count -eq 1) 'Approved existing actor/provider mapping is unavailable.'
        foreach($db in @($Plan.stageDatabase,$Plan.archiveDatabase,$Plan.failedDatabase)){Require ((Scalar $master 'SELECT COALESCE(DB_ID(@db),0)' @{'@db'=$db}) -eq 0) 'Planned staging/archive name already exists.'}
        $state=[ordered]@{Version=$Plan.version;PlanDigest=(Digest (Get-Content (Join-Path $PSScriptRoot 'demo-plan.json') -Raw));Phase='Preparing';OriginalDatabaseId=$originalId;StageDatabaseId=0;Preservation=(Preservation $source);Platform=(Platform-Snapshot);SqlAccess=(SqlAccess $source);BackupVerified=$false;ParityVerified=$false;Activated=$false}
        Save $state
        Capture-Storage
        $apiRoot=Join-Path $Repo 'src\MicroEMR.Api';$appFiles=Join-Path $Evidence 'preserved-api';Require (-not(Test-Path -LiteralPath $appFiles)) 'Do not overwrite API preservation.'
        New-Item -ItemType Directory -Path $appFiles|Out-Null
        Copy-Item -LiteralPath (Join-Path $apiRoot 'bin\Debug\net10.0') -Destination (Join-Path $appFiles 'output') -Recurse
        Copy-Item -LiteralPath (Join-Path $apiRoot 'appsettings.json'),(Join-Path $apiRoot 'appsettings.Development.json'),(Join-Path $apiRoot 'Properties\launchSettings.json') -Destination $appFiles
        $backup='C:\Program Files\Microsoft SQL Server\MSSQL15.TELEMED52\MSSQL\Backup\MicroEMR_LocalDev_Fresh_demo_'+[guid]::NewGuid().ToString('N')+'.bak'
        $exists=Query $master 'SELECT * FROM sys.dm_os_file_exists(@p)' @{'@p'=$backup};Require ($exists.Rows[0].file_exists -eq 0 -and $exists.Rows[0].parent_directory_exists -eq 1) 'Backup destination unsafe.'
        Exec $source ('BACKUP DATABASE '+(Quote $Plan.database)+' TO DISK=@p WITH COPY_ONLY,CHECKSUM,STOP_ON_ERROR;') @{'@p'=$backup}
        Exec $source 'RESTORE VERIFYONLY FROM DISK=@p WITH CHECKSUM,STOP_ON_ERROR;' @{'@p'=$backup}
        $state.Backup=$backup;$state.BackupVerified=$true;Save $state
        $collation=[string](Scalar $master 'SELECT collation_name FROM sys.databases WHERE name=@db' @{'@db'=$Plan.database})
        Require ($collation -match '^[A-Za-z0-9_]+$') 'Source collation unavailable.'
        Exec $master ('CREATE DATABASE '+(Quote $Plan.stageDatabase)+' COLLATE '+$collation+';')
        $stage=Connect-Clinical $Plan.stageDatabase
        try{
            $manifest=Get-Content (Join-Path $Repo 'db\tenant-clinical\manifest.json') -Raw|ConvertFrom-Json
            Require ($manifest[-1].migrationId -eq $Plan.requiredMigration) 'Reviewed migration boundary changed.'
            Require ($manifest[0].migrationId -eq '0000-tenant-metadata') 'Expected provisioning metadata bootstrap missing.'
            # Existing runner bootstraps metadata and tenant identity before migration replay.
            $metadata=Get-Content -LiteralPath (Join-Path (Join-Path $Repo 'db') $manifest[0].script) -Raw
            foreach($batch in (Split-Batches $metadata)){Exec $stage $batch}
            Exec $stage 'INSERT dbo.TenantDatabaseIdentity(TenantUid,TenantKey) VALUES(@uid,@key)' @{'@uid'=[guid]$Plan.tenantUid;'@key'=$Plan.tenantKey}
            foreach($migration in $manifest){
                $path=[IO.Path]::GetFullPath((Join-Path (Join-Path $Repo 'db') $migration.script));Require ($path.StartsWith((Join-Path $Repo 'db')+'\')) 'Migration outside repository assets.'
                $sql=Get-Content -LiteralPath $path -Raw
                Require ($sql -notmatch '(?im)^\s*(USE\s|(?:CREATE|ALTER|DROP)\s+DATABASE\s)') 'Cross-database migration requires review.'
                $tx=$stage.BeginTransaction();try{
                    foreach($batch in (Split-Batches $sql)){Exec $stage $batch @{} $tx}
                    Exec $stage 'INSERT dbo.SchemaMigration(MigrationId,SchemaVersion,ScriptHash,AppliedBy) VALUES(@id,@version,@hash,@by)' @{'@id'=$migration.migrationId;'@version'=$migration.schemaVersion;'@hash'=(Digest $sql.TrimStart([char]0xFEFF).Replace("`r`n","`n").Replace("`r","`n"));'@by'=$env:COMPUTERNAME} $tx
                    $tx.Commit()
                }catch{if($tx.Connection){$tx.Rollback()};throw}
            }
            # All DML below targets the NEW stage. The original is SELECT-only.
            # Copy in live FK order; no constraints are disabled, no rows deleted.
            $remaining=[Collections.Generic.List[string]]::new();foreach($table in $Plan.preserveTables){$remaining.Add($table)};$copied=@{}
            while($remaining.Count){$progress=$false
                foreach($table in @($remaining.ToArray())){
                    $parents=@((Query $source 'SELECT OBJECT_NAME(referenced_object_id) ParentTable FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(@t)' @{'@t'='dbo.'+$table}).Rows|ForEach-Object{[string]$_.ParentTable})
                    Require (@($parents|Where-Object {$_ -ne $table -and $_ -notin $Plan.preserveTables}).Count -eq 0) "Preservation dependency outside allowlist for $table"
                    if(@($parents|Where-Object {$_ -ne $table -and -not $copied.ContainsKey($_)}).Count){continue}
                    $cols=Columns $source $table;$targetCols=Columns $stage $table
                    Require ((@($cols.Rows|ForEach-Object{$_.name}) -join '|') -ceq (@($targetCols.Rows|ForEach-Object{$_.name}) -join '|')) "Schema mismatch for $table"
                    $keys=Keys $source $table;Require ($keys.Count -gt 0) "Missing preservation key $table"
                    $all=@($cols.Rows|ForEach-Object{[string]$_.name});$identities=@($cols.Rows|Where-Object is_identity|ForEach-Object{$_.name});$fields=($all|ForEach-Object{Quote $_}) -join ','
                    $on=($keys|ForEach-Object{'t.'+(Quote $_)+'=s.'+(Quote $_)}) -join ' AND '
                    $assign=@($all|Where-Object {$_ -notin $keys -and $_ -notin $identities}|ForEach-Object{'t.'+(Quote $_)+'=s.'+(Quote $_)}) -join ','
                    $match=if($assign){' WHEN MATCHED THEN UPDATE SET '+$assign}else{''}
                    $q=Quote $table;$values=($all|ForEach-Object{'s.'+(Quote $_)}) -join ','
                    if($identities.Count){Exec $stage "SET IDENTITY_INSERT dbo.$q ON;"}
                    try{Exec $stage ("MERGE dbo.$q AS t USING "+(Quote $Plan.database)+".dbo.$q AS s ON $on"+$match+" WHEN NOT MATCHED BY TARGET THEN INSERT ($fields) VALUES ($values);")}
                    finally{if($identities.Count){Exec $stage "SET IDENTITY_INSERT dbo.$q OFF;"}}
                    $copied[$table]=$true;[void]$remaining.Remove($table);$progress=$true
                };Require $progress 'Preservation FK cycle requires review.'
            }
            Preserve-SqlAccess $source $stage
            $owner=[string](Scalar $master 'SELECT SUSER_SNAME(owner_sid) FROM sys.databases WHERE name=@db' @{'@db'=$Plan.database})
            Require (-not [string]::IsNullOrWhiteSpace($owner)) 'Existing database owner cannot be preserved.'
            Exec $master ('ALTER AUTHORIZATION ON DATABASE::'+(Quote $Plan.stageDatabase)+' TO '+(Quote $owner)+';')
            Require (Same $state.Preservation (Preservation $stage)) 'Staging configuration/identity parity failed; no activation.'
            Require (Same $state.SqlAccess (SqlAccess $stage)) 'Staging SQL access parity failed; no activation.'
            Require (Same $state.Platform (Platform-Snapshot)) 'Platform access changed; no activation.'
            Require ((Scalar $stage 'SELECT COUNT(*) FROM dbo.Patient') -eq 0) 'Fresh stage contains patients.'
            $state.StageDatabaseId=Assert-Identity $stage;$state.ParityVerified=$true;$state.Phase='Staged';Save $state
        }finally{$stage.Dispose()}
    }finally{$master.Dispose();$source.Dispose()};return
}
$state=State
Require ($state.PlanDigest -ceq (Digest (Get-Content (Join-Path $PSScriptRoot 'demo-plan.json') -Raw))) 'Approved plan changed; do not resume.'
Require ($state.BackupVerified -and $state.ParityVerified) 'Backup/preservation gates incomplete.'
if($Phase -eq 'Activate'){
    Require (-not $state.Activated) 'Already activated; do not repeat database renames.'
    $current=Connect-Clinical $Plan.database;$stage=Connect-Clinical $Plan.stageDatabase
    try{Require ((Assert-Identity $current) -eq $state.OriginalDatabaseId) 'Original database changed.';Require (Same $state.Preservation (Preservation $current)) 'Preserved source configuration changed; restage.';Require (Same $state.Preservation (Preservation $stage)) 'Stage parity changed.';Require (Same $state.Platform (Platform-Snapshot)) 'Platform access changed.'}finally{$current.Dispose();$stage.Dispose()}
}
$apiRoot=Join-Path $Repo 'src\MicroEMR.Api';$api=@(Get-Process -Name MicroEMR.Api -ErrorAction SilentlyContinue)
Require ($api.Count -le 1) 'Multiple API hosts.'
if($api.Count){Require ($api[0].Path -eq (Join-Path $apiRoot 'bin\Debug\net10.0\MicroEMR.Api.exe')) 'Unexpected API host.'}
if($Phase -eq 'Activate'){Require ($api.Count -eq 1) 'Expected running API before activation.'}
$other=@(Get-Process -Name MicroEMR.Web,MicroEMR.Auth -ErrorAction Stop|Select-Object Id,Path)
if($api.Count){Stop-Process -Id $api[0].Id;Require ($api[0].WaitForExit(20000)) 'API did not stop.'}
[Data.SqlClient.SqlConnection]::ClearAllPools();$master=Connect-Clinical 'master'
try{
    if($Phase -eq 'Activate'){
        Require ((Scalar $master 'SELECT COALESCE(DB_ID(@name),0)' @{'@name'=$Plan.archiveDatabase}) -eq 0) 'Archive name occupied.'
        # Never force-kill SQL sessions. Active connections make rename fail closed.
        Exec $master ('ALTER DATABASE '+(Quote $Plan.database)+' MODIFY NAME = '+(Quote $Plan.archiveDatabase)+';')
        $state.Phase='OriginalArchived';Save $state
        Exec $master ('ALTER DATABASE '+(Quote $Plan.stageDatabase)+' MODIFY NAME = '+(Quote $Plan.database)+';')
        $state.Activated=$true;$state.Phase='Activated';Save $state
    }else{
        $currentId=[int](Scalar $master 'SELECT COALESCE(DB_ID(@name),0)' @{'@name'=$Plan.database})
        $archiveId=[int](Scalar $master 'SELECT COALESCE(DB_ID(@name),0)' @{'@name'=$Plan.archiveDatabase})
        Require ($archiveId -eq $state.OriginalDatabaseId) 'Cutback archive identity differs.'
        if($currentId){Require ($currentId -eq $state.StageDatabaseId) 'Cutback would displace an unexpected database.';Require ((Scalar $master 'SELECT COALESCE(DB_ID(@name),0)' @{'@name'=$Plan.failedDatabase}) -eq 0) 'Cutback hold exists.';Exec $master ('ALTER DATABASE '+(Quote $Plan.database)+' MODIFY NAME = '+(Quote $Plan.failedDatabase)+';')}
        Exec $master ('ALTER DATABASE '+(Quote $Plan.archiveDatabase)+' MODIFY NAME = '+(Quote $Plan.database)+';')
        $state.Phase='CutBack';$state.Activated=$false;Save $state
    }
}finally{$master.Dispose()}
Start-Process -FilePath dotnet -ArgumentList @('run','--no-build','--no-restore','--launch-profile','https') -WorkingDirectory $apiRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Evidence ($Phase+'-api.log')) -RedirectStandardError (Join-Path $Evidence ($Phase+'-api-error.log'))|Out-Null
foreach($hostInfo in $other){Require ((Get-Process -Id $hostInfo.Id).Path -eq $hostInfo.Path) 'Web/Auth changed.'}
$healthy=$false
for($attempt=0;$attempt -lt 15;$attempt++){
    $code=& curl.exe --silent --show-error --max-time 3 --output NUL --write-out '%{http_code}' ($Plan.api+'/health/platform')
    if($LASTEXITCODE -eq 0 -and $code -eq '200'){$healthy=$true;break};Start-Sleep -Seconds 1
}
Require $healthy 'API health failed after name transition; stop and review cutback.'
$active=Connect-Clinical $Plan.database
try{
    $expectedId=if($Phase -eq 'Activate'){$state.StageDatabaseId}else{$state.OriginalDatabaseId}
    Require ((Assert-Identity $active) -eq $expectedId) 'Post-transition database identity differs.'
    Require (Same $state.Preservation (Preservation $active)) 'Post-transition preservation differs.'
    Require (Same $state.Platform (Platform-Snapshot)) 'Platform access changed.'
}finally{$active.Dispose()}
Assert-Api
Write-Output 'Database transition, health, preserved configuration/platform and existing authenticated actor checks passed. No original database or file was deleted.'
