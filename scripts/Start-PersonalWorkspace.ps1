[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$originRecord = Join-Path $workspaceRoot 'PERSONAL_PROJECT_ORIGIN.md'
$projectFile = Join-Path $workspaceRoot 'CMCS_POE/CMCS_POE.csproj'
$builtApplication = Join-Path $workspaceRoot 'CMCS_POE/bin/Release/net10.0/CMCS_POE.dll'
$dataDirectory = Join-Path $workspaceRoot 'CMCS_POE/App_Data/Personal'

if (-not (Test-Path -LiteralPath $originRecord -PathType Leaf)) {
    throw 'The personal origin record is missing. This launcher is for CMCS-Personal only.'
}
if (-not (Test-Path -LiteralPath $builtApplication -PathType Leaf)) {
    throw 'The .NET 10 personal build is missing. Run scripts/Check-PersonalWorkspace.ps1 first.'
}

$environmentNames = @('DOTNET_ENVIRONMENT', 'ASPNETCORE_ENVIRONMENT', 'CMCS_PERSONAL_DATA_DIR', 'CMCS_ENABLE_LOCAL_ACCOUNTS', 'CMCS_DEMO_MODE', 'Demo__Enabled')
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location -LiteralPath $workspaceRoot
try {
    [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Development', 'Process')
    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
    [Environment]::SetEnvironmentVariable('CMCS_PERSONAL_DATA_DIR', $dataDirectory, 'Process')
    [Environment]::SetEnvironmentVariable('CMCS_ENABLE_LOCAL_ACCOUNTS', '1', 'Process')
    [Environment]::SetEnvironmentVariable('CMCS_DEMO_MODE', 'false', 'Process')
    [Environment]::SetEnvironmentVariable('Demo__Enabled', 'false', 'Process')

    Write-Host 'Starting the local personal website: http://127.0.0.1:5092' -ForegroundColor Cyan
    Write-Host 'Wait for the listening message, then open that address. Press Ctrl+C to stop.'
    Write-Host "Personal data directory: $dataDirectory"

    & dotnet run --project $projectFile --configuration Release --no-build --no-launch-profile --urls 'http://127.0.0.1:5092'
    if ($LASTEXITCODE -ne 0) { throw 'The personal website stopped with an error. Share the output.' }
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    Pop-Location
}
