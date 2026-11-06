# Invoked by Seed.ps1 after explicit Execute/Verify consent, never at preparation time.
Require ($Mode -in @('Execute','Verify')) 'Use the entry point; no independent unauthenticated verification.'
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$action){try{& $action;$checks.Add(@{name=$name;status='PASS'})}catch{$checks.Add(@{name=$name;status='FAIL';reason=$_.Exception.Message});Save-Json 'verification.json' $checks.ToArray();throw}}
function Download($method,$path,$body=$null){
    Add-Type -AssemblyName System.Net.Http;$client=[Net.Http.HttpClient]::new()
    try{
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$env:MICROEMR_DEMO_BEARER)
        if($method -eq 'POST'){$content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 30 -Compress),[Text.Encoding]::UTF8,'application/json');try{$r=$client.PostAsync(($Plan.api+$path),$content).GetAwaiter().GetResult()}finally{$content.Dispose()}}
        else{$r=$client.GetAsync(($Plan.api+$path)).GetAwaiter().GetResult()}
        Require $r.IsSuccessStatusCode "Binary request failed: $path"
        return ,$r.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    }finally{$client.Dispose()}
}
function Pdf($name,$path,$patient,$body=$null){
    $method=if($null -eq $body){'GET'}else{'POST'};$bytes=Download $method $path $body
    Require ([Text.Encoding]::ASCII.GetString($bytes,0,[math]::Min(5,$bytes.Length)) -eq '%PDF-') 'Invalid PDF header.'
    $file=Join-Path $Evidence ($name+'.pdf');[IO.File]::WriteAllBytes($file,$bytes)
    & dotnet fsi --exec (Join-Path $PSScriptRoot 'Verify-Pdf.fsx') $file $patient $script:ClinicText
    Require ($LASTEXITCODE -eq 0) 'PDF text/branding assertion failed.'
}
$db=Connect-Clinical $Plan.database
try{
    Check 'Tenant, migration and activated database identity' {Require ((Assert-Identity $db) -eq $state.StageDatabaseId) 'Wrong active store.';Assert-Api}
    Check 'Preserved identities/configuration/logo and platform access' {
        $before=Digest ($state.Preservation|ConvertTo-Json -Depth 30 -Compress);$after=Digest ((Preservation $db)|ConvertTo-Json -Depth 30 -Compress);Require ($before -ceq $after) 'Preservation differs.'
        Require ((Digest ($state.Platform|ConvertTo-Json -Depth 30 -Compress)) -ceq (Digest ((Platform-Snapshot)|ConvertTo-Json -Depth 30 -Compress))) 'Platform changed.'
    }
    Check 'Original file/blob bytes remain intact' {
        $storage='D:\MicroEMR-Data\patient-files'
        foreach($file in (Get-Content -LiteralPath (Join-Path $Evidence 'preserved-storage.json') -Raw|ConvertFrom-Json)){
            $path=[IO.Path]::GetFullPath((Join-Path $storage $file.RelativePath));Require ($path.StartsWith($storage+'\')) 'Storage receipt escaped the preserved root.'
            Require ((Get-Item -LiteralPath $path).Length -eq $file.Length -and (Get-FileHash -LiteralPath $path).Hash -eq $file.Hash) 'Original stored bytes changed.'
        }
    }
    Check 'Seed counts and lifecycle states' {
        $expected=@{Patient=12;ScheduleAppointment=18;PatientEncounter=6;PatientPrescription=5;PatientReferral=4;PatientFile=4;PatientDocument=2}
        foreach($table in $expected.Keys){Require ((Scalar $db ('SELECT COUNT(*) FROM dbo.'+(Quote $table))) -eq $expected[$table]) "Unexpected $table count"}
        Require ((Scalar $db "SELECT COUNT(*) FROM dbo.PatientEncounter WHERE Status='Open'") -eq 6) 'Unexpected signed/closed encounter.'
        Require ((Scalar $db "SELECT COUNT(*) FROM dbo.ScheduleAppointment WHERE AppointmentStatus='Seen'") -eq 4) 'Expected four legitimate start transitions.'
        Require ((Scalar $db "SELECT COUNT(*) FROM dbo.ScheduleAppointment WHERE AppointmentStatus='Cancelled'") -eq 2) 'Expected two cancellation histories.'
        Require ((Scalar $db "SELECT COUNT(*) FROM dbo.PatientPrescription WHERE Status='Finalized'") -eq $Plan.unsignedFinalizations) 'Unsigned finalization count differs.'
    }
    $configuration=Api GET '/api/clinic-configuration';$script:ClinicText=if($configuration.legalName){$configuration.legalName}else{$configuration.clinicName}
    foreach($p in $Plan.patients){
        $id=(Done ($p.alias+'-patient')).patientUid
        Check ($p.alias+' search/chart/source retrieval') {
            $search=Api GET ('/api/patients?searchText='+[Uri]::EscapeDataString($p.lastName)+'&pageNumber=1&pageSize=50')
            Require (($search|ConvertTo-Json -Depth 20).Contains($id)) 'Search did not return seeded identity.'
            $chart=Api GET "/api/patients/$id";Require ($chart.patientUid -eq $id -and $chart.lastName -eq $p.lastName) 'Wrong chart.'
            foreach($section in @('problems','medications','clinical-history')){$rows=Api GET "/api/patients/$id/$section";Require (@($rows).Count -gt 0) "Missing $section"}
            $allergies=Api GET "/api/patients/$id/allergies";if($p.allergen){Require (($allergies|ConvertTo-Json -Depth 20).Contains($p.allergen)) 'Missing allergy.'}
        }
    }
    Check 'Appointment API and saved calendar data' {
        $start=[Uri]::EscapeDataString((LocalTime -2 0 0));$end=[Uri]::EscapeDataString((LocalTime 3 0 0))
        $appointments=Api GET "/api/scheduling/appointments?startUtc=$start&endUtc=$end"
        # The existing calendar read intentionally omits the two Cancelled appointments.
        Require (@($appointments).Count -eq 16) 'Calendar API active appointment count differs.'
        foreach($n in 0..15){Require ((Done ('appointment-'+$n)).appointmentUid -in @($appointments|ForEach-Object{$_.appointmentUid})) 'Seeded active appointment missing from calendar API.'}
    }
    for($n=0;$n -lt 6;$n++){$p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid;$enc=Done ($p.alias+'-encounter')
        Check ($p.alias+' encounter/vitals') {$record=Api GET "/api/patient-encounters/$($enc.encounterUid)";Require ($record.patientUid -eq $id -and $record.status -eq 'Open') 'Encounter ownership/status differs.';Require (($record|ConvertTo-Json -Depth 30).Contains($p.note)) 'Saved note contribution missing.';Require (@(Api GET "/api/patients/$id/vitals").Count -eq 1) 'Vitals missing.'}
        if($n -ge 4){Check ($p.alias+' Open encounter PDF') {$record=Api GET "/api/patient-encounters/$($enc.encounterUid)";Pdf ($p.alias+'-encounter') "/api/patient-encounters/$($enc.encounterUid)/pdf-preview" $p.lastName @{structuredDataJson=$record.structuredDataJson}}}
    }
    for($n=0;$n -lt 5;$n++){$p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid;$rx=Done ($p.alias+'-prescription')
        Check ($p.alias+' prescription') {$record=Api GET "/api/patients/$id/prescriptions/$($rx.prescriptionUid)";Require ($record.patientUid -eq $id -and $record.productName -eq $p.medication) 'Prescription differs.'}
        if($n -lt $Plan.unsignedFinalizations){Check ($p.alias+' prescription PDF') {Pdf ($p.alias+'-prescription') "/api/patients/$id/prescriptions/$($rx.prescriptionUid)/artifact" $p.lastName}}
    }
    for($n=0;$n -lt 4;$n++){$p=$Plan.patients[$n];$id=(Done ($p.alias+'-patient')).patientUid;$ref=Done ($p.alias+'-referral');$f=Done ($p.alias+'-file')
        Check ($p.alias+' referral preview') {$record=Api GET "/api/patients/$id/referrals/$($ref.referralUid)";Require ($record.patientUid -eq $id) 'Referral patient differs.';Pdf ($p.alias+'-referral') "/api/patients/$id/referrals/$($ref.referralUid)/letter/preview" $p.lastName}
        Check ($p.alias+' document list/download/bytes/audit') {
            $list=Api GET "/api/patients/$id/files";Require (@($list|Where-Object fileUid -eq $f.fileUid).Count -eq 1) 'Uploaded file missing from list.'
            $auditBefore=Scalar $db "SELECT COUNT(*) FROM dbo.AuditLog WHERE ActionName='PatientFileDownloaded' AND ResourceUid=@file AND UserId=@actor" @{'@file'=[guid]$f.fileUid;'@actor'=$Plan.actorId}
            $bytes=Download GET "/api/patients/$id/files/$($f.fileUid)/content";$expected=[Convert]::FromBase64String((Get-Content -LiteralPath (Join-Path $journal ($p.alias+'-file.done.json')) -Raw|ConvertFrom-Json).expectedBytes)
            Require ([Linq.Enumerable]::SequenceEqual([byte[]]$bytes,[byte[]]$expected)) 'Downloaded bytes differ.'
            $auditAfter=Scalar $db "SELECT COUNT(*) FROM dbo.AuditLog WHERE ActionName='PatientFileDownloaded' AND ResourceUid=@file AND UserId=@actor" @{'@file'=[guid]$f.fileUid;'@actor'=$Plan.actorId};Require ($auditAfter -eq $auditBefore+1) 'Download audit not observed.'
            $other=(Done ($Plan.patients[($n+1)%12].alias+'-patient')).patientUid
            $denied=$false;try{$null=Api GET "/api/patients/$other/files/$($f.fileUid)"}catch{$response=$_.Exception.Response;if($response){$denied=([int]$response.StatusCode -in @(403,404))}};Require $denied 'Wrong-patient denial not verified.'
        }
    }
    foreach($n in @(4,5)){$p=$Plan.patients[$n];$doc=Done ($p.alias+'-consultation')
        Check ($p.alias+' consultation PDF') {$record=Api GET "/api/patient-documents/$($doc.documentUid)";Require ($record.patientUid -eq (Done ($p.alias+'-patient')).patientUid) 'Consultation patient differs.';Pdf ($p.alias+'-consultation') "/api/patient-documents/$($doc.documentUid)/pdf-preview" $p.lastName @{structuredDataJson=$record.structuredDataJson}}
    }
    Check 'CPP and Immunization Summary PDFs' {
        $p=$Plan.patients[0];$id=(Done ($p.alias+'-patient')).patientUid;$options=Api GET "/api/patients/$id/cpp/print/options"
        Require (@($options.clinicians|Where-Object clinicianUid -eq $Plan.providerUid).Count -eq 1) 'Approved CPP clinician is unavailable.'
        Pdf 'cpp' "/api/patients/$id/cpp/print" $p.lastName @{clinicianUid=$Plan.providerUid;categories=@($options.categories|ForEach-Object{$_.key});medicationScope='Ongoing'}
        Pdf 'immunization' "/api/patients/$id/immunizations/summary/pdf" $p.lastName
    }
    Check 'Clinical writes attributed and audited' {Require ((Scalar $db 'SELECT COUNT(*) FROM dbo.AuditLog WHERE UserId=@actor AND PatientId IS NOT NULL' @{'@actor'=$Plan.actorId}) -gt 0) 'No clinical audit evidence.'}
    $checks.Add(@{name='Second authenticated tenant denial';status='DEFERRED';reason='Requires a separately qualified existing second-tenant session; no account/role provisioning or fabricated claim.'})
    $checks.Add(@{name='Browser calendar presentation and all existing user logins';status='DEFERRED';reason='API/SQL preservation checks do not prove Web sessions. Reuse existing normal-login browser harness once available; no owner clinical testing.'})
    $state.Phase='VerifiedWithDeferrals';Save-Json 'replacement.json' $state;Save-Json 'verification.json' $checks.ToArray()
    Save-Json 'verification-summary.json' @{passed=@($checks.ToArray()|Where-Object status -eq PASS).Count;failed=@($checks.ToArray()|Where-Object status -eq FAIL).Count;deferred=@($checks.ToArray()|Where-Object status -eq DEFERRED).Count;allRuntimeChecksPassed=$false}
}finally{$db.Dispose()}
$checks|ForEach-Object{[pscustomobject]$_}|Format-Table -AutoSize
