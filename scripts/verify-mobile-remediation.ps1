[CmdletBinding()]
param(
    [string]$PostgresConnectionString =
        $env:PHASE6_POSTGRES_CONNECTION_STRING
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testsProject = 'SMEFLOWSystem.Tests/SMEFLOWSystem.Tests.csproj'
$infrastructureProject = 'SMEFLOWSystem.Infrastructure'
$startupProject = 'SMEFLOWSystem.WebAPI'
$previousMigration = '20260712123427_InitialPostgreSql'
$idempotencyMigration = '20260727035930_AddPunchIdempotency'
$originalKnownBugSetting = $env:RUN_KNOWN_BUG_TESTS
$originalPostgresSetting =
    $env:PHASE6_POSTGRES_CONNECTION_STRING

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "dotnet $($Arguments -join ' ')" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Get-MigrationSql {
    param(
        [Parameter(Mandatory)]
        [string]$FromMigration,
        [Parameter(Mandatory)]
        [string]$ToMigration
    )

    $output = & dotnet ef migrations script `
        $FromMigration `
        $ToMigration `
        --project $infrastructureProject `
        --startup-project $startupProject `
        --no-build `
        --no-transactions 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | Write-Output
        throw "Could not generate migration SQL."
    }

    return $output -join [Environment]::NewLine
}

Push-Location $repositoryRoot
try {
    Invoke-DotNet @(
        'build',
        'SMEFLOWSystem.sln',
        '--no-restore'
    )
    Invoke-DotNet @(
        'test',
        'SMEFLOWSystem.sln',
        '--no-build',
        '--no-restore'
    )

    $env:RUN_KNOWN_BUG_TESTS = '1'
    Invoke-DotNet @(
        'test',
        $testsProject,
        '--no-build',
        '--no-restore',
        '--filter',
        'Phase=0'
    )
    Invoke-DotNet @(
        'test',
        $testsProject,
        '--no-build',
        '--no-restore',
        '--filter',
        'Phase=7|Phase=8'
    )

    if ([string]::IsNullOrWhiteSpace(
            $PostgresConnectionString)) {
        Write-Warning (
            'PostgreSQL concurrency verification was skipped. ' +
            'Pass -PostgresConnectionString or set ' +
            'PHASE6_POSTGRES_CONNECTION_STRING to a disposable test database.'
        )
    }
    else {
        $env:PHASE6_POSTGRES_CONNECTION_STRING =
            $PostgresConnectionString
        Invoke-DotNet @(
            'test',
            $testsProject,
            '--no-build',
            '--no-restore',
            '--filter',
            'FullyQualifiedName~AttendanceIdempotency'
        )
    }

    $upSql = Get-MigrationSql `
        -FromMigration $previousMigration `
        -ToMigration $idempotencyMigration
    if (
        $upSql -notmatch 'ADD "ClientRequestId"' -or
        $upSql -notmatch 'CREATE UNIQUE INDEX'
    ) {
        throw 'Phase 6 migration Up SQL is missing expected operations.'
    }

    $downSql = Get-MigrationSql `
        -FromMigration $idempotencyMigration `
        -ToMigration $previousMigration
    if (
        $downSql -notmatch 'DROP INDEX' -or
        $downSql -notmatch 'DROP COLUMN "ClientRequestId"'
    ) {
        throw 'Phase 6 migration Down SQL is missing expected operations.'
    }

    Write-Host (
        'Mobile remediation verification completed successfully.'
    ) -ForegroundColor Green
}
finally {
    $env:RUN_KNOWN_BUG_TESTS = $originalKnownBugSetting
    $env:PHASE6_POSTGRES_CONNECTION_STRING =
        $originalPostgresSetting
    Pop-Location
}
