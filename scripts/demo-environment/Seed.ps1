param([ValidateSet('Plan','Execute','Verify')][string]$Mode='Plan')
. (Join-Path $PSScriptRoot 'Common.ps1')
if($Mode -eq 'Plan'){$Plan|Select-Object version,anchorLocalDate,counts,unsignedFinalizations,allowSigning,actorId,providerUid|ConvertTo-Json -Depth 6;return}
if($Mode -eq 'Execute'){Require-Consent 'Seed'}else{Require-Consent 'Verify'}
$state=Get-Content -LiteralPath (Join-Path $Evidence 'replacement.json') -Raw|ConvertFrom-Json
Require ($state.Activated -and $state.Phase -in @('Activated','Seeded','VerifiedWithDeferrals')) 'Replacement is not activated.'
Require ($state.PlanDigest -ceq (Digest (Get-Content (Join-Path $PSScriptRoot 'demo-plan.json') -Raw))) 'Manifest changed after staging.'
$db=Connect-Clinical $Plan.database
try{Require ((Assert-Identity $db) -eq $state.StageDatabaseId) 'Refusing to seed/verify the original or another database.'}finally{$db.Dispose()}
Require ((Digest ((Platform-Snapshot)|ConvertTo-Json -Depth 30 -Compress)) -ceq (Digest ($state.Platform|ConvertTo-Json -Depth 30 -Compress))) 'Membership/profile preservation differs.'
Assert-Api
$journal=Join-Path $Evidence 'seed';if($Mode -eq 'Execute'){New-Item -ItemType Directory -Path $journal -Force|Out-Null}
function Done($key){$path=Join-Path $journal ($key+'.done.json');Require (Test-Path -LiteralPath $path) "Seed receipt missing: $key";return (Get-Content -LiteralPath $path -Raw|ConvertFrom-Json).result}
function Job($key,$method,$path,$body=$null){
    Require ($key -match '^[A-Za-z0-9-]+$') 'Unsafe receipt name.'
    $done=Join-Path $journal ($key+'.done.json');$pending=Join-Path $journal ($key+'.pending.json')
    $digestBody=$body;if($body -is [Collections.IDictionary]){$digestBody=$body.Clone();$digestBody.Remove('rowVersion')}
    $requestDigest=Digest (($method+' '+$path+' ')+($digestBody|ConvertTo-Json -Depth 30 -Compress))
    if(Test-Path -LiteralPath $done){$r=Get-Content -LiteralPath $done -Raw|ConvertFrom-Json;Require ($r.requestDigest -ceq $requestDigest) "Completed request changed: $key";return $r.result}
    Require (-not(Test-Path -LiteralPath $pending)) "Uncertain prior write $key; reconcile its audit/owned record, never blindly repeat."
    Require ($Mode -eq 'Execute') 'Verification cannot create missing records.'
    [ordered]@{key=$key;method=$method;path=$path;requestDigest=$requestDigest;startedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath $pending -Encoding utf8
    $result=Api $method $path $body
    [ordered]@{key=$key;requestDigest=$requestDigest;completedUtc=[DateTime]::UtcNow.ToString('o');result=$result}|ConvertTo-Json -Depth 40|Set-Content -LiteralPath $done -Encoding utf8
    Remove-Item -LiteralPath $pending
    return $result
}
function LocalTime($day,$hour,$minute){$date=[DateTime]::SpecifyKind(([DateTime]::ParseExact($Plan.anchorLocalDate,'yyyy-MM-dd',[Globalization.CultureInfo]::InvariantCulture)).AddDays($day).AddHours($hour).AddMinutes($minute),[DateTimeKind]::Unspecified);[TimeZoneInfo]::ConvertTimeToUtc($date,[TimeZoneInfo]::FindSystemTimeZoneById($Plan.windowsTimeZone)).ToString('o')}
function Assert-SeedAnchor($today){
    if($today -eq $Plan.anchorLocalDate){return}
    # Owner explicitly approved retaining this already-staged historical manifest on 2026-11-06.
    $approvedHistoricalRun=($Plan.anchorLocalDate -ceq '2026-11-04' -and
        $state.PlanDigest -ceq '634F34F2234A48D9F2BCB47B4B45DED3E0E05D19AA610C6A0439B60D14FA47E7' -and
        $state.OriginalDatabaseId -eq 44 -and $state.StageDatabaseId -eq 45 -and
        $state.Activated -and $state.BackupVerified -and $state.ParityVerified)
    Require $approvedHistoricalRun 'Anchor is no longer today and this is not the owner-approved historical run; revise/reapprove before staging.'
}
if($Mode -eq 'Execute'){
    $today=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,$Plan.windowsTimeZone).ToString('yyyy-MM-dd')
    if(-not @(Get-ChildItem -LiteralPath $journal -Filter '*.done.json' -ErrorAction SilentlyContinue).Count){Assert-SeedAnchor $today}
    Require (-not $Plan.allowSigning) 'This seed never fabricates or performs signatures.'
    foreach($p in $Plan.patients){
        $patient=Job ($p.alias+'-patient') POST '/api/patients' @{firstName=$p.firstName;lastName=$p.lastName;dateOfBirth=$p.dateOfBirth;sexAtBirth=$p.sexAtBirth;phoneNumber=$p.phone;email=$p.email;addressLine1=$p.addressLine1;city=$p.city;province=$p.province;countryCode=$p.countryCode;healthCardNumber=$null;healthCardVersion=$null}
        Require (-not [string]::IsNullOrWhiteSpace($patient.patientUid)) 'Create did not return a patient identity.';$id=$patient.patientUid
        $null=Job ($p.alias+'-problem') POST "/api/patients/$id/problems" @{problemName=$p.scenario;problemDescription=$p.note}
        $null=Job ($p.alias+'-history') POST "/api/patients/$id/clinical-history" @{historyType='Medical';description=$p.note}
        $null=Job ($p.alias+'-medication') POST "/api/patients/$id/medications" @{medicationName=$p.medication;classification='Ongoing';route='Oral';notes='Synthetic medication history for demonstration; not a treatment recommendation.'}
        if($p.allergen){$null=Job ($p.alias+'-allergy') POST "/api/patients/$id/allergies" @{allergenName=$p.allergen;allergenType='Unknown';reaction='Fictional historical rash';severity='Mild';notes='Synthetic positive history; no implied no-known-allergies assertion for other patients.'}}
    }
    for($n=0;$n -lt 18;$n++){
        $p=$Plan.patients[$n%12];$id=(Done ($p.alias+'-patient')).patientUid
        $day=if($n -lt 4){0}elseif($n -ge 16){-1}else{1};$slot=if($n -ge 16){$n-16}else{$n};$hour=9+[math]::Floor($slot/2);$minute=($slot%2)*30
        $resource=if($n%2){$Plan.secondaryResourceUid}else{$Plan.primaryResourceUid}
        $start=LocalTime $day $hour $minute;$end=([DateTime]::Parse($start)).AddMinutes(25).ToUniversalTime().ToString('o')
        $a=Job ('appointment-'+$n) POST '/api/scheduling/appointments' @{patientUid=$id;primaryResourceUid=$resource;roomResourceUid=$Plan.roomResourceUid;startDateTimeUtc=$start;endDateTimeUtc=$end;appointmentType='Cardiology';reason=$p.scenario;notes=('DEMO-v1 '+$p.alias);isAdHoc=$false;isCritical=$false}
        if($n -ge 16){$null=Job ('cancel-'+$n) POST "/api/scheduling/appointments/$($a.appointmentUid)/cancel" @{cancelReason='Synthetic patient requested a different date.'}}
    }
    for($n=0;$n -lt 6;$n++){
        $p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid
        if($n -lt 4){$a=Done ('appointment-'+$n);$enc=Job ($p.alias+'-encounter') POST "/api/scheduling/appointments/$($a.appointmentUid)/start-encounter"}
        else{$enc=Job ($p.alias+'-encounter') POST "/api/patients/$id/encounters" @{encounterDateUtc=(LocalTime 0 8 $n);encounterType='Cardiology follow-up';reasonForVisit=$p.scenario;templateUid=$Plan.encounterTemplateUid}}
        $current=Api GET "/api/patient-encounters/$($enc.encounterUid)"
        Require ($current.patientUid -eq $id -and $current.status -eq 'Open') 'Encounter patient/status differs.'
        $section=if($current.templateVersionUid){'encounterNote'}else{'notes'};$field=if($current.templateVersionUid){'note'}else{$null}
        $null=Job ($p.alias+'-contribution') POST "/api/patients/$id/encounters/$($enc.encounterUid)/contributions" @{sectionKey=$section;fieldKey=$field;content=$p.note;rowVersion=$current.rowVersion}
        $null=Job ($p.alias+'-vitals') POST "/api/patients/$id/vitals" @{recordedAt=(LocalTime 0 8 $n);bloodPressureSystolic=(128+$n*3);bloodPressureDiastolic=78;heartRate=(68+$n*2);heightCm=175;weightKg=(76+$n);notes='SYNTHETIC demo measurements'}
    }
    for($n=0;$n -lt 5;$n++){
        $p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid
        $rx=Job ($p.alias+'-prescription') POST "/api/patients/$id/prescriptions" @{productName=$p.medication;genericName=$p.medication;productDisplayText=($p.medication+' - SYNTHETIC training order');route='Oral';frequencyCode='ONCE_DAILY';directions='Synthetic teaching order only - do not dispense or use for treatment.';quantity=30;quantityUnit='tablet';initialDaysSupply=30;refillQuantity=30;refillQuantityUnit='tablet';refillDaysSupply=30;authorizedRepeats=1;prescribedDate=$Plan.anchorLocalDate;indication=$p.scenario}
        if($n -lt $Plan.unsignedFinalizations){$current=Api GET "/api/patients/$id/prescriptions/$($rx.prescriptionUid)";$null=Job ($p.alias+'-finalize') POST "/api/patients/$id/prescriptions/$($rx.prescriptionUid)/finalize" @{rowVersion=$current.rowVersion;pickupReviewed=$true}}
    }
    for($n=0;$n -lt 4;$n++){
        $p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid
        $null=Job ($p.alias+'-referral') POST "/api/patients/$id/referrals" @{referringProviderUid=$Plan.providerUid;recipientName='Dr Rowan Example (fictional)';recipientOrganization='Fictional Cardiology Centre';recipientPhone='416-555-0199';reason=$p.scenario;clinicalSummary=$p.note}
        $null=Job ($p.alias+'-task') POST "/api/patients/$id/tasks" @{taskTitle=('DEMO follow-up '+$p.alias);taskDescription=$p.note;taskType='Follow-up';taskPriority='Normal';assignedTo=$Plan.actorId;dueAt=(LocalTime 7 10 $n)}
        # Multipart upload through the real API; no storage/repository shortcuts.
        $key=$p.alias+'-file';$pending=Join-Path $journal ($key+'.pending.json');$done=Join-Path $journal ($key+'.done.json')
        if(-not(Test-Path -LiteralPath $done)){
            Require (-not(Test-Path -LiteralPath $pending)) "Uncertain previous upload $key; reconcile before retry."
            @{key=$key;startedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath $pending
            Add-Type -AssemblyName System.Net.Http;$client=[Net.Http.HttpClient]::new();$form=[Net.Http.MultipartFormDataContent]::new()
            try{
                $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$env:MICROEMR_DEMO_BEARER)
                $bytes=[Text.Encoding]::UTF8.GetBytes('SYNTHETIC DEMONSTRATION ONLY: '+$p.alias+' '+$p.scenario+'. No real patient information or images.')
                $file=[Net.Http.ByteArrayContent]::new($bytes);$file.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('text/plain');$form.Add($file,'file',($p.alias+'-demo.txt'));$form.Add([Net.Http.StringContent]::new('Report'),'category');$form.Add([Net.Http.StringContent]::new('Synthetic '+$p.alias+' report'),'title')
                $response=$client.PostAsync(($Plan.api+"/api/patients/$id/files"),$form).GetAwaiter().GetResult();Require $response.IsSuccessStatusCode 'Upload failed; receipt remains uncertain.'
                $result=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
                @{result=$result;expectedBytes=[Convert]::ToBase64String($bytes);expectedDigest=(Digest ([Text.Encoding]::UTF8.GetString($bytes)))}|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $done -Encoding utf8
                Remove-Item -LiteralPath $pending
            }finally{$form.Dispose();$client.Dispose()}
        }
    }
    foreach($n in @(4,5)){
        $p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid
        $values=@{reasonText=$p.scenario;historyText=$p.note;findingsText='Synthetic findings recorded with the demonstration vitals.';investigationsText='Fictional outside report reviewed.';impressionText=$p.scenario;recommendationsText='Demonstration follow-up plan; not clinical advice.';medicationText='Synthetic medication list reviewed.';followUpText='Demonstration follow-up in four weeks.'}
        $doc=Job ($p.alias+'-consultation') POST "/api/patients/$id/documents" @{templateUid=$Plan.consultationTemplateUid;documentType='Consultation Report';title=('Synthetic consultation '+$p.alias);structuredDataJson=(@{schemaVersion=1;values=$values}|ConvertTo-Json -Depth 6 -Compress)}
        $current=Api GET "/api/patient-documents/$($doc.documentUid)"
        $null=Job ($p.alias+'-recipients') PUT "/api/patients/$id/documents/$($doc.documentUid)/consultation-recipients" @{rowVersion=$current.rowVersion;recipients=@(@{recipientOrder=1;recipientType='TO';providerUid=$Plan.consultationRecipientProviderUid})}
    }
    $state.Phase='Seeded';Save-Json 'replacement.json' $state
}
& (Join-Path $PSScriptRoot 'Verify.ps1')
