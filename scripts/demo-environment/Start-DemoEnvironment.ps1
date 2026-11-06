param([ValidateSet('Authenticate','Stage','Activate','Seed','Verify','Cutback')][string]$Phase='Authenticate')
$ErrorActionPreference='Stop'
# No bearer passes through this wrapper, arguments or persistent files.
# Authenticate is read-only. Mutation phases retain the existing explicit consent gates.
& dotnet run --project (Join-Path $PSScriptRoot 'OAuthLauncher/OAuthLauncher.csproj') -- $Phase (Join-Path $PSScriptRoot 'Prepare-DemoEnvironment.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Demo launcher stopped. See the phase-specific status above. No credential details logged.' }
