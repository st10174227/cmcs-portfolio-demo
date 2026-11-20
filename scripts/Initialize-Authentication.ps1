[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'PERSONAL_PROJECT_ORIGIN.md'))) {
    throw 'Run this setup from the separate CMCS-Personal workspace.'
}
$project = Join-Path $workspaceRoot 'CMCS_POE/CMCS_POE.csproj'
$migrationFolder = Join-Path $workspaceRoot 'CMCS_POE/Migrations'
$dataDirectory = Join-Path $workspaceRoot 'CMCS_POE/App_Data/Personal'
$oldDataSetting = [Environment]::GetEnvironmentVariable('CMCS_PERSONAL_DATA_DIR', 'Process')
Push-Location -LiteralPath $workspaceRoot
try {
    [Environment]::SetEnvironmentVariable('CMCS_PERSONAL_DATA_DIR', $dataDirectory, 'Process')
    Write-Host 'Restoring the project-local Microsoft migration tool...' -ForegroundColor Cyan
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Local tool restore failed. Share the output.' }

    Write-Host 'Building the authentication code before generating its migration...' -ForegroundColor Cyan
    & dotnet build $project --configuration Release --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Authentication build failed. Share the output.' }

    $existing = @(Get-ChildItem -LiteralPath $migrationFolder -Filter '*_AddPersonalAccounts.cs' -File)
    if ($existing.Count -eq 0) {
        $checkpointName = 'account-migration-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
        $checkpoint = Join-Path (Split-Path -Parent $workspaceRoot) (Join-Path 'CMCS-Personal-Backups' $checkpointName)
        New-Item -ItemType Directory -Path $checkpoint -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $migrationFolder 'ApplicationDbContextModelSnapshot.cs') -Destination $checkpoint

        $efArguments = @(
            'tool', 'run', 'dotnet-ef', '--', 'migrations', 'add', 'AddPersonalAccounts',
            '--project', $project, '--startup-project', $project,
            '--context', 'CMCS_POE.Data.ApplicationDbContext', '--output-dir', 'Migrations',
            '--configuration', 'Release', '--no-build'
        )
        & dotnet @efArguments
        if ($LASTEXITCODE -ne 0) { throw 'The account migration could not be generated. Share the output.' }
        $existing = @(Get-ChildItem -LiteralPath $migrationFolder -Filter '*_AddPersonalAccounts.cs' -File)
    }
    if ($existing.Count -ne 1) { throw 'Expected exactly one personal-account migration.' }
    # Capture generated source so it can be reviewed and retained with the final repository.
    $export = Join-Path $workspaceRoot 'artifacts/AccountMigration'
    New-Item -ItemType Directory -Path $export -Force | Out-Null
    Copy-Item -LiteralPath $existing[0].FullName -Destination $export -Force
    $designer = $existing[0].FullName -replace '\.cs$', '.Designer.cs'
    Copy-Item -LiteralPath $designer -Destination $export -Force
    Copy-Item -LiteralPath (Join-Path $migrationFolder 'ApplicationDbContextModelSnapshot.cs') -Destination $export -Force
    $zip = Join-Path $workspaceRoot 'artifacts/CMCS-Account-Migration.zip'
    Compress-Archive -LiteralPath $export -DestinationPath $zip -Force
    Write-Host "Account migration source export: $zip"

    $upCode = ([IO.File]::ReadAllText($existing[0].FullName) -split 'protected override void Down')[0]
    if ($upCode -match '\b(Drop\w+|DeleteData|UpdateData|InsertData|Alter\w+|Rename\w+)\s*\(' -or $upCode -match '\.Sql\s*\(') {
        throw 'The generated migration includes changes beyond adding the account schema. No website data was migrated; share the migration export for review.'
    }


    & dotnet build $project --configuration Release --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'The generated migration did not build. Share the output.' }
    Write-Host 'Backing up the existing personal data file without applying the migration yet...' -ForegroundColor Cyan
    & dotnet (Join-Path $workspaceRoot 'CMCS_POE/bin/Release/net10.0/CMCS_POE.dll') --backup-personal-data
    if ($LASTEXITCODE -ne 0) { throw 'Personal data backup failed. The website has not been started.' }
}
finally {
    [Environment]::SetEnvironmentVariable('CMCS_PERSONAL_DATA_DIR', $oldDataSetting, 'Process')
    Pop-Location
}
Write-Host 'Account source is ready. Run the checker before starting the website.' -ForegroundColor Green
