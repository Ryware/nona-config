$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $PSCommandPath
$ServerUrl = $env:SERVER_URL
$Spec = $null
$AdminPath = $env:ADMIN_PATH

function Show-Usage {
    Write-Host 'Usage:'
    Write-Host '  .\generate-clients.ps1'
    Write-Host '  .\generate-clients.ps1 --server-url http://localhost:18080'
    Write-Host '  .\generate-clients.ps1 --spec .\obj\openapi\WebApi.json --admin-path ..\nona-config-admin'
}

for ($i = 0; $i -lt $args.Count; $i++) {
    switch ($args[$i]) {
        { $_ -in @('--help', '-help', '-?') } {
            Show-Usage
            exit 0
        }
        { $_ -in @('--server-url', '-ServerUrl') } {
            if ($i + 1 -ge $args.Count) {
                throw "Missing value for $($args[$i])"
            }
            $i++
            $ServerUrl = $args[$i]
        }
        { $_ -in @('--spec', '-Spec') } {
            if ($i + 1 -ge $args.Count) {
                throw "Missing value for $($args[$i])"
            }
            $i++
            $Spec = $args[$i]
        }
        { $_ -in @('--admin-path', '-AdminPath') } {
            if ($i + 1 -ge $args.Count) {
                throw "Missing value for $($args[$i])"
            }
            $i++
            $AdminPath = $args[$i]
        }
        default {
            throw "Unknown argument: $($args[$i])"
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    $ServerUrl = 'http://localhost:5027'
}

if ([string]::IsNullOrWhiteSpace($AdminPath)) {
    $AdminPath = Join-Path $scriptDir '..\nona-config-admin'
}

$fetchSpec = [string]::IsNullOrWhiteSpace($Spec)
if ($fetchSpec) {
    $Spec = Join-Path $scriptDir 'openapi.json'
}

$lockDir = Join-Path $scriptDir '.nona-generate-clients.lock'
$lockAcquired = $false
$migratorOutput = Join-Path $scriptDir 'migrator/src/ConfigMigrator.Core/Generated'
$cliOutput = Join-Path $scriptDir 'cli/src/Nona.Cli/Core/Generated'
$adminOutput = Join-Path $AdminPath 'src/generated/api.ts'
$npxCommand = if (Get-Command npx.cmd -ErrorAction SilentlyContinue) { 'npx.cmd' } else { 'npx' }

function Acquire-Lock {
    $attempts = 0

    while ($true) {
        try {
            New-Item -ItemType Directory -Path $lockDir -ErrorAction Stop | Out-Null
            $script:lockAcquired = $true
            return
        }
        catch [System.IO.IOException] {
            $attempts++

            if ($attempts -ge 120) {
                throw "Timed out waiting for client generation lock: $lockDir"
            }

            Start-Sleep -Seconds 1
        }
    }
}

function Remove-Lock {
    if ($script:lockAcquired -and (Test-Path -LiteralPath $lockDir)) {
        Remove-Item -LiteralPath $lockDir -Force
    }
}

try {
    Acquire-Lock

    if ($fetchSpec) {
        Write-Host "Fetching OpenAPI spec from $ServerUrl/openapi/v1.json..."
        curl.exe -f -s "$ServerUrl/openapi/v1.json" -o "$Spec"
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
        Write-Host "  -> $Spec"
    }
    else {
        Write-Host "Using OpenAPI spec: $Spec"
    }

    Write-Host ''
    Write-Host 'Generating C# client for Migrator...'
    dotnet kiota generate `
        -l CSharp `
        -d "$Spec" `
        -c NonaMigrationApiClient `
        -n Nona.Migrator.Core.Generated `
        -o "$migratorOutput" `
        --co
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Write-Host '  -> migrator/src/ConfigMigrator.Core/Generated'

    Write-Host ''
    Write-Host 'Generating C# client for CLI...'
    dotnet kiota generate `
        -l CSharp `
        -d "$Spec" `
        -c NonaApiClient `
        -n Nona.Cli.Generated `
        -o "$cliOutput" `
        --co
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Write-Host '  -> cli/src/Nona.Cli/Core/Generated'

    Write-Host ''
    Write-Host 'Generating TypeScript types for admin UI...'
    & $npxCommand --yes openapi-typescript "$Spec" -o "$adminOutput"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Write-Host "  -> $adminOutput"

    Write-Host ''
    Write-Host 'Done. Review the changes and commit.'
}
finally {
    Remove-Lock
}
