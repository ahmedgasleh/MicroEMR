# Run only against an explicitly identified isolated local/test deployment.
# Uses real server authentication/tenant resolution/permissions; never logs tokens or response bodies.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][uri]$ApiBaseUri,
    [Parameter(Mandatory = $true)][guid]$TenantUid,
    [Parameter(Mandatory = $true)][guid]$PatientUid,
    [Parameter(Mandatory = $true)][guid]$FamilyHistoryUid,
    [switch]$IsolatedTest
)

$ErrorActionPreference = 'Stop'
if (-not $IsolatedTest) { throw 'Confirm the target is an isolated local/test deployment with -IsolatedTest.' }
if (-not $ApiBaseUri.IsAbsoluteUri -or $ApiBaseUri.Scheme -ne 'https' -or
    $ApiBaseUri.UserInfo -or $ApiBaseUri.Query -or $ApiBaseUri.Fragment) {
    throw 'Supply an HTTPS API base URL without credentials, query or fragment. Keep certificate validation enabled.'
}
if ($TenantUid -eq [guid]::Empty -or $PatientUid -eq [guid]::Empty -or $FamilyHistoryUid -eq [guid]::Empty) {
    throw 'Supply actual nonempty test tenant, patient and family-history UIDs.'
}
Add-Type -AssemblyName System.Net.Http
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$http = [System.Net.Http.HttpClient]::new($handler)
$http.BaseAddress = [uri]($ApiBaseUri.AbsoluteUri.TrimEnd('/') + '/')
$http.Timeout = [TimeSpan]::FromSeconds(30)

function Send-Check([string]$Method, [string]$Path, [int]$Expected, $Body = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Path)
    $response = $null
    try {
        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 10 -Compress),
                [System.Text.Encoding]::UTF8, 'application/json')
        }
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        $actual = [int]$response.StatusCode
        if ($actual -ne $Expected) {
            throw "Expected HTTP $Expected, received $actual for $Method. Stop and inspect SQL; do not retry writes automatically."
        }
        if ($Expected -eq 200) {
            return ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
        }
        Write-Host "$Method denial: HTTP $actual (expected)."
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}

$secureToken = $null
$tokenPointer = [IntPtr]::Zero
try {
    $secureToken = Read-Host 'Reader A access token from the existing approved local/test OAuth workflow (input hidden)' -AsSecureString
    $tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    $http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
        'Bearer', [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer))
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
    $tokenPointer = [IntPtr]::Zero

    $context = Send-Check 'GET' 'api/context/tenant' 200
    if ([guid]$context.tenantUid -ne $TenantUid) { throw 'Authenticated tenant differs from the supplied test tenant.' }
    $permissions = @(Send-Check 'GET' 'api/permissions/me' 200)
    if ($permissions -notcontains 'Patients.View' -or $permissions -contains 'ClinicalData.Manage') {
        throw 'Reader A must have Patients.View and lack effective ClinicalData.Manage. No writes attempted.'
    }

    $route = "api/patients/$PatientUid/family-history"
    $resource = "$route/$FamilyHistoryUid"
    $entry = Send-Check 'GET' $resource 200
    if ([guid]$entry.patientUid -ne $PatientUid -or [guid]$entry.familyHistoryUid -ne $FamilyHistoryUid -or
        $entry.status -ne 'Active' -or $entry.conditionDescription -notlike 'TEST ONLY -*') {
        throw 'Select the actual active fictional TEST ONLY family-history fixture. No writes attempted.'
    }
    $before = @(Send-Check 'GET' ($route + '?status=All') 200) | ConvertTo-Json -Depth 10 -Compress
    $beforeEntry = $entry | ConvertTo-Json -Depth 10 -Compress
    $fromUtc = [DateTime]::UtcNow
    Write-Host ('Request window start UTC: ' + $fromUtc.ToString('o'))

    $create = @{ relationship = 'Mother'; conditionDescription = 'TEST ONLY - M7 denied create' }
    $update = @{
        relationship = $entry.relationship; conditionDescription = $entry.conditionDescription
        ageAtOnset = $entry.ageAtOnset; lifeStageCode = $entry.lifeStageCode; treatment = $entry.treatment
        notes = 'TEST ONLY - M7 forbidden update'; rowVersion = $entry.rowVersion
    }
    Send-Check 'POST' $route 403 $create
    Send-Check 'PUT' $resource 403 $update
    Send-Check 'POST' ($resource + '/archive') 403 @{ rowVersion = $entry.rowVersion }

    $after = @(Send-Check 'GET' ($route + '?status=All') 200) | ConvertTo-Json -Depth 10 -Compress
    $afterEntry = (Send-Check 'GET' $resource 200) | ConvertTo-Json -Depth 10 -Compress
    if ($before -cne $after -or $beforeEntry -cne $afterEntry) {
        throw 'Family-history values/list/version changed. Review the before/after SQL evidence.'
    }
    Write-Host ('Request window end UTC: ' + [DateTime]::UtcNow.ToString('o'))
    Write-Host 'PASS: real tenant/permission/read preflight; three API denials; unchanged API list and record.'
    Write-Host 'M7 SQL/audit closure still requires the guarded before/after read-only query in report 133. Web/UI checks are separate.'
}
finally {
    if ($tokenPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer) }
    $http.DefaultRequestHeaders.Authorization = $null
    $http.Dispose()
    $handler.Dispose()
    if ($null -ne $secureToken) { $secureToken.Dispose() }
}
