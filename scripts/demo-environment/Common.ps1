$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
$script:Repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$script:Plan=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'demo-plan.json') -Raw | ConvertFrom-Json
$script:Evidence=Join-Path $script:Repo '.tmp\demo-environment-v1'
function Require($condition,$message){if(-not $condition){throw $message}}
Require ($Plan.database -ceq 'MicroEMR_LocalDev_Fresh' -and $Plan.tenantKey -ceq 'local-dev-fresh' -and $Plan.tenantUid -ceq 'f94c6ecd-4417-483e-8a94-0507cf8384c2' -and $Plan.server -ceq '192.168.50.1,50013' -and $Plan.api -ceq 'https://localhost:7003') 'Plan target outside authorization.'
foreach($name in @($Plan.stageDatabase,$Plan.archiveDatabase,$Plan.failedDatabase)){Require ($name -match '^MicroEMR_LocalDev_Fresh_(DemoStage|PreDemo|DemoHold)_\d{8}$') 'Unreviewed database name.'}
function Save-Json($name,$value){New-Item -ItemType Directory -Path $Evidence -Force|Out-Null;ConvertTo-Json -InputObject $value -Depth 40|Set-Content -LiteralPath (Join-Path $Evidence $name) -Encoding utf8}
function Secret($store,$key){
    $node=Get-Content -LiteralPath (Join-Path $env:APPDATA "Microsoft\UserSecrets\$store\secrets.json") -Raw|ConvertFrom-Json
    function Find($item,$remaining){
        $exact=$item.PSObject.Properties[$remaining];if($exact){return [string]$exact.Value}
        $parts=$remaining.Split(':');for($n=$parts.Length-1;$n -ge 1;$n--){$head=($parts[0..($n-1)] -join ':');$child=$item.PSObject.Properties[$head];if($child){return Find $child.Value ($parts[$n..($parts.Length-1)] -join ':')}}
        throw 'Established configuration entry unavailable; no credentials printed.'
    };Find $node $key
}
function Connect-Clinical($database){
    Require ($database -in @($Plan.database,$Plan.stageDatabase,$Plan.archiveDatabase,$Plan.failedDatabase,'master')) 'Database outside reviewed plan.'
    $b=[Data.SqlClient.SqlConnectionStringBuilder]::new((Secret 'MicroEMR.DatabaseTool-local-development' 'TenantDatabaseSecrets:development:MicroEMR_LocalDev_Fresh'))
    Require ($b.DataSource -eq $Plan.server -and $b.InitialCatalog -eq $Plan.database) 'Configured development target differs.'
    $b['Initial Catalog']=$database;$b['Application Name']='MicroEMR.OwnerApprovedDemoPreparation';$c=[Data.SqlClient.SqlConnection]::new($b.ConnectionString);$c.Open();return $c
}
function Query($c,$sql,$parameters=@{},$transaction=$null){
    $q=$c.CreateCommand();$q.CommandTimeout=300;$q.CommandText=$sql;if($transaction){$q.Transaction=$transaction}
    foreach($key in $parameters.Keys){[void]$q.Parameters.AddWithValue($key,$parameters[$key])}
    try{$a=[Data.SqlClient.SqlDataAdapter]::new($q);$t=[Data.DataTable]::new();[void]$a.Fill($t);return ,$t}finally{$q.Dispose()}
}
function Exec($c,$sql,$parameters=@{},$transaction=$null){
    $q=$c.CreateCommand();$q.CommandTimeout=300;$q.CommandText=$sql;if($transaction){$q.Transaction=$transaction}
    foreach($key in $parameters.Keys){[void]$q.Parameters.AddWithValue($key,$parameters[$key])};try{[void]$q.ExecuteNonQuery()}finally{$q.Dispose()}
}
function Scalar($c,$sql,$parameters=@{}){$t=Query $c $sql $parameters;Require ($t.Rows.Count -gt 0) 'Expected SQL scalar missing.';return $t.Rows[0][0]}
function Quote($name){Require (-not [string]::IsNullOrWhiteSpace($name) -and $name.Length -le 128 -and $name -notmatch "['\r\n\x00]") 'Unsafe SQL identifier.';return '['+$name.Replace(']',']]')+']'}
function Digest($text){$s=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($s.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-','')}finally{$s.Dispose()}}
function Assert-Identity($c){
    $t=Query $c 'SELECT DB_ID() DatabaseId,TenantUid,TenantKey FROM dbo.TenantDatabaseIdentity'
    Require ($t.Rows.Count -eq 1 -and [string]$t.Rows[0].TenantUid -eq $Plan.tenantUid -and $t.Rows[0].TenantKey -eq $Plan.tenantKey) 'Wrong tenant identity.'
    Require ((Scalar $c "SELECT COUNT(*) FROM dbo.SchemaMigration WHERE MigrationId=@id" @{'@id'=$Plan.requiredMigration}) -eq 1) '0106 not installed.'
    return [int]$t.Rows[0].DatabaseId
}
function Platform-Snapshot {
    try{$value=Secret 'MicroEMR.DatabaseTool-local-development' 'ConnectionStrings:PlatformDatabase'}catch{$value=Secret 'MicroEMR.Api-local-development' 'ConnectionStrings:PlatformDatabase'}
    $b=[Data.SqlClient.SqlConnectionStringBuilder]::new($value);Require ($b.DataSource -eq $Plan.server -and $b.InitialCatalog -eq 'MicroEMR_Platform') 'Unexpected platform target.'
    $c=[Data.SqlClient.SqlConnection]::new($b.ConnectionString);$c.Open();try{
        $m=Query $c 'EXEC dbo.TenantDatabase_GetByTenantUid @TenantUid=@id' @{'@id'=[guid]$Plan.tenantUid}
        Require ($m.Rows.Count -eq 1 -and $m.Rows[0].DatabaseName -eq $Plan.database -and $m.Rows[0].DatabaseStatus -eq 'Active') 'Catalog mapping differs.'
        $members=Query $c 'EXEC dbo.PlatformMembership_ListByTenant @TenantUid=@id' @{'@id'=[guid]$Plan.tenantUid}
        $profiles=Query $c 'EXEC dbo.AccessProfile_List @TenantUid=@id' @{'@id'=[guid]$Plan.tenantUid}
        # Capture complete access-profile/override contents with trusted operator access.
        # Do not silently fall back to counts if SELECT is unavailable.
        $access=Query $c 'SELECT (SELECT * FROM dbo.UserTenantAccessProfile WHERE TenantUid=@id ORDER BY UserId FOR JSON PATH,INCLUDE_NULL_VALUES) Assignments,(SELECT * FROM dbo.UserPermissionOverride WHERE TenantUid=@id ORDER BY UserId,PermissionKey FOR JSON PATH,INCLUDE_NULL_VALUES) Overrides' @{'@id'=[guid]$Plan.tenantUid}
        $permissions=@();foreach($p in $profiles.Rows){$v=Query $c 'EXEC dbo.AccessProfile_Get @TenantUid=@tenant,@AccessProfileUid=@profile' @{'@tenant'=[guid]$Plan.tenantUid;'@profile'=$p.AccessProfileUid};$permissions+=@($v.Rows|ForEach-Object{[ordered]@{Profile=[string]$_.AccessProfileUid;Permission=[string]$_.PermissionKey}})}
        $effective=Query $c 'EXEC dbo.AccessProfile_GetEffective @TenantUid=@tenant,@UserId=@user' @{'@tenant'=[guid]$Plan.tenantUid;'@user'=$Plan.actorSubject}
        Require (@($effective.Rows|Where-Object MembershipStatus -ne Active).Count -eq 0) 'Seed membership is not active.'
        $keys=@($effective.Rows|ForEach-Object{[string]$_.PermissionKey})
        foreach($required in @('Patients.View','Patients.Edit','Scheduling.View','Scheduling.Manage','Encounters.View','Encounters.Edit','ClinicalData.Manage','Prescriptions.Prescribe','Referrals.View','Referrals.Manage','Documents.View','Documents.Manage','Tasks.View','Tasks.Manage','Templates.Use','ClinicSettings.Manage','Providers.View')){Require ($required -in $keys) "Existing actor lacks $required; no grant will be added."}
        $profileRows=Scalar $c 'SELECT COALESCE((SELECT AccessProfileUid,Name,Description,IsBuiltIn,IsActive,CreatedBy,CreatedAt,UpdatedBy,UpdatedAt FROM dbo.AccessProfile WHERE TenantUid=@id ORDER BY AccessProfileUid FOR JSON PATH,INCLUDE_NULL_VALUES),N''[]'')' @{'@id'=[guid]$Plan.tenantUid}
        return [ordered]@{Mapping=@($m.Rows|ForEach-Object{[string]$_.TenantUid+'|'+$_.DatabaseName+'|'+$_.SecretReference+'|'+$_.DatabaseStatus});Memberships=@($members.Rows|ForEach-Object{[string]$_.UserId+'|'+$_.MembershipStatus+'|'+$_.RoleName+'|'+$_.IsDefaultTenant}|Sort-Object);Profiles=$permissions;ProfileMetadata=[string]$profileRows;Assignments=[string]$access.Rows[0].Assignments;Overrides=[string]$access.Rows[0].Overrides;ActorPermissions=$keys}
    }finally{$c.Dispose()}
}
function Columns($c,$table){return ,(Query $c 'SELECT c.name,c.is_identity FROM sys.columns c WHERE c.object_id=OBJECT_ID(@name) AND c.is_computed=0 AND c.system_type_id<>189 ORDER BY c.column_id' @{'@name'='dbo.'+$table})}
function Keys($c,$table){return @((Query $c 'SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(@name) AND i.is_primary_key=1 ORDER BY ic.key_ordinal' @{'@name'='dbo.'+$table}).Rows|ForEach-Object{[string]$_.name})}
function Preservation($c){
    $snapshot=[ordered]@{}
    foreach($table in $Plan.preserveTables){$cols=Columns $c $table;Require ($cols.Rows.Count -gt 0) "Missing preservation table $table";$keys=Keys $c $table;Require ($keys.Count -gt 0) "No preservation key for $table";$names=($cols.Rows|ForEach-Object{Quote $_.name}) -join ',';$order=($keys|ForEach-Object{Quote $_}) -join ',';$json=Scalar $c ("SELECT COALESCE((SELECT $names FROM dbo."+(Quote $table)+" ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]')");$snapshot[$table]=Digest ([string]$json)}
    $snapshot['TenantIdentity']=Digest ([string](Scalar $c 'SELECT COALESCE((SELECT TenantUid,TenantKey FROM dbo.TenantDatabaseIdentity FOR JSON PATH),N''[]'')'))
    return $snapshot
}
function Require-Consent($phase){Require ($env:MICROEMR_DEMO_EXECUTION_CONSENT -ceq ('OWNER_APPROVED_DEMO_V1_'+$phase.ToUpperInvariant())) 'Explicit phase-specific owner consent is missing.'}
function Split-Batches($sql){
    # Same string/comment-aware GO rules as the existing SqlBatchParser; no GO repeats.
    $inString=$false;$block=$false;$buffer=[Text.StringBuilder]::new();$batches=[Collections.Generic.List[string]]::new()
    foreach($line in ($sql -split '\r?\n')){
        if(-not $inString -and -not $block -and $line -match '^\s*GO\s+\d+') {throw 'GO repeat is unsupported.'}
        if(-not $inString -and -not $block -and $line -match '^\s*GO\s*(--.*)?$'){if($buffer.ToString().Trim()){$batches.Add($buffer.ToString());[void]$buffer.Clear()};continue}
        [void]$buffer.AppendLine($line)
        for($i=0;$i -lt $line.Length;$i++){
            $next=if($i+1 -lt $line.Length){$line[$i+1]}else{[char]0};$ch=$line[$i]
            if($block){if($ch -eq '*' -and $next -eq '/'){$block=$false;$i++};continue}
            if($inString){if($ch -eq "'" -and $next -eq "'"){$i++}elseif($ch -eq "'"){$inString=$false};continue}
            if($ch -eq '-' -and $next -eq '-'){break};if($ch -eq '/' -and $next -eq '*'){$block=$true;$i++}elseif($ch -eq "'"){$inString=$true}
        }
    };Require (-not $inString -and -not $block) 'Unterminated SQL string/comment.';if($buffer.ToString().Trim()){$batches.Add($buffer.ToString())};return $batches.ToArray()
}
function Api($method,$path,$body=$null){
    Require ($path.StartsWith('/api/')) 'Non-API request refused.'
    Require (-not [string]::IsNullOrWhiteSpace($env:MICROEMR_DEMO_BEARER)) 'No legitimate deployed API session is available.'
    $headers=@{Authorization='Bearer '+$env:MICROEMR_DEMO_BEARER;Accept='application/json'}
    $args=@{Method=$method;Uri=($Plan.api+$path);Headers=$headers;ErrorAction='Stop'}
    if($null -ne $body){$args.ContentType='application/json';$args.Body=($body|ConvertTo-Json -Depth 30 -Compress)}
    Invoke-RestMethod @args
}
function Assert-Api {
    $context=Api GET '/api/context/tenant';Require ($context.tenantUid -eq $Plan.tenantUid -and $context.tenantKey -eq $Plan.tenantKey) 'Authenticated tenant mismatch.'
    # The API has validated this bearer; decode only to bind the run to its expected subject.
    $p=$env:MICROEMR_DEMO_BEARER.Split('.')[1].Replace('-','+').Replace('_','/');$p=$p.PadRight(($p.Length+3)-(($p.Length+3)%4),'=')
    $claims=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p))|ConvertFrom-Json;Require ($claims.sub -eq $Plan.actorSubject) 'Resume/seed subject differs from approved existing actor.'
}
