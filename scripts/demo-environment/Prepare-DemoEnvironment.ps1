param([ValidateSet('Plan','Stage','Activate','Seed','Verify','Cutback')][string]$Phase='Plan')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$lock=$null
try{
    if($Phase -ne 'Plan'){
        Require-Consent $Phase
        New-Item -ItemType Directory -Path $Evidence -Force|Out-Null
        $lock=[IO.File]::Open((Join-Path $Evidence 'operation.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::Write,[IO.FileShare]::None)
    }
    if($Phase -in @('Plan','Stage','Activate','Cutback')){& (Join-Path $PSScriptRoot 'Replace.ps1') -Phase $Phase}
    elseif($Phase -eq 'Seed'){& (Join-Path $PSScriptRoot 'Seed.ps1') -Mode Execute}
    else{& (Join-Path $PSScriptRoot 'Seed.ps1') -Mode Verify}
}catch{
    # Persist only code location, never raw exception messages, requests or credentials.
    if($lock){
        try{Save-Json 'phase-failure.json' ([ordered]@{Phase=$Phase;FailedAtUtc=[DateTime]::UtcNow.ToString('o');Script=[IO.Path]::GetFileName($_.InvocationInfo.ScriptName);Line=$_.InvocationInfo.ScriptLineNumber;Stack=$_.ScriptStackTrace})}catch{}
    }
    throw 'Preparation phase failed; inspect .tmp/demo-environment-v1/phase-failure.json and existing phase receipts. No credential details logged.'
}finally{if($lock){$lock.Dispose()}}
