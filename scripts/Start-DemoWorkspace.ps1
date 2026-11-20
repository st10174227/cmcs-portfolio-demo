[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'PERSONAL_PROJECT_ORIGIN.md') -PathType Leaf)) {
    throw 'This launcher belongs to the separate CMCS-Personal workspace.'
}
$project = Join-Path $workspaceRoot 'CMCS_POE/CMCS_POE.csproj'
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'CMCS_POE/bin/Release/net10.0/CMCS_POE.dll'))) {
    throw 'Run scripts/Check-PersonalWorkspace.ps1 before launching the demo.'
}
$names = @('DOTNET_ENVIRONMENT', 'ASPNETCORE_ENVIRONMENT', 'CMCS_DEMO_MODE', 'CMCS_DEMO_DATA_DIR', 'Demo__Enabled', 'Demo__DataDirectory')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
Push-Location -LiteralPath $workspaceRoot
try {
    [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Development', 'Process')
    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
    [Environment]::SetEnvironmentVariable('CMCS_DEMO_MODE', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('Demo__Enabled', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('CMCS_DEMO_DATA_DIR', (Join-Path $workspaceRoot 'CMCS_POE/App_Data/Demo'), 'Process')
    [Environment]::SetEnvironmentVariable('Demo__DataDirectory', (Join-Path $workspaceRoot 'CMCS_POE/App_Data/Demo'), 'Process')
    Write-Host 'Starting the visitor demo: http://127.0.0.1:5093/' -ForegroundColor Cyan
    Write-Host 'Choose a workspace card on the homepage. No password is required. Ctrl+C stops this demo.'
    Write-Host 'This local address is for your PC. Public hosting is a separate deployment step.'
    & dotnet run --project $project --configuration Release --no-build --no-launch-profile --urls 'http://127.0.0.1:5093'
    if ($LASTEXITCODE -ne 0) { throw 'The demo stopped with an error. Share the output.' }
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    Pop-Location
}
