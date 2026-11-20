[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$originRecord = Join-Path $workspaceRoot 'PERSONAL_PROJECT_ORIGIN.md'
$projectFile = Join-Path $workspaceRoot 'CMCS_POE/CMCS_POE.csproj'
$checksProject = Join-Path $workspaceRoot 'CMCS.PersonalChecks/CMCS.PersonalChecks.csproj'

if (-not (Test-Path -LiteralPath $originRecord -PathType Leaf)) {
    throw 'The personal origin record is missing. Run this from the extracted CMCS-Personal folder.'
}
if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "Application project not found: $projectFile"
}
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found in PATH. Paste this message here so we can check your installed SDK.'
}

Write-Host 'Installed .NET SDKs:' -ForegroundColor Cyan
& dotnet --list-sdks
if ($LASTEXITCODE -ne 0) { throw 'Unable to read installed .NET SDKs.' }

Write-Host "`nInstalled .NET runtimes:" -ForegroundColor Cyan
& dotnet --list-runtimes
if ($LASTEXITCODE -ne 0) { throw 'Unable to read installed .NET runtimes.' }

if (@(Get-ChildItem -LiteralPath (Join-Path $workspaceRoot 'CMCS_POE/Migrations') -Filter '*_AddPersonalAccounts.cs' -File).Count -ne 1) {
    throw 'Run scripts/Initialize-Authentication.ps1 to generate the account migration before checking this update.'
}

Push-Location -LiteralPath $workspaceRoot
try {
    Write-Host 'Building the personal website copy...' -ForegroundColor Cyan
    & dotnet build $projectFile --configuration Release --nologo --no-incremental -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Website build failed. Paste the build errors here.' }

    Write-Host 'Running personal regression checks with isolated temporary fixtures...' -ForegroundColor Cyan
    & dotnet build $checksProject --configuration Release --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Personal check project did not build. Paste the output here.' }
    & dotnet run --project $checksProject --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Personal regression checks failed. Paste the output here.' }
}
finally {
    Pop-Location
}

Write-Host 'Build and regression checks passed. Browser acceptance is a separate step.' -ForegroundColor Green
Write-Host 'No Git commands were run and the website was not started.'
