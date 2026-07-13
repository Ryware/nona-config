$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $PSCommandPath
$webApiProject = Join-Path $scriptDir 'core/src/WebApi/WebApi.csproj'
$openApiDirectory = if ([string]::IsNullOrWhiteSpace($env:OPENAPI_DIR)) {
    Join-Path $scriptDir 'obj/openapi'
}
else {
    $env:OPENAPI_DIR
}
$Spec = if ([string]::IsNullOrWhiteSpace($env:SPEC)) {
    Join-Path $openApiDirectory 'WebApi.json'
}
else {
    $env:SPEC
}
$ServerUrl = $env:SERVER_URL
$AdminPath = $env:ADMIN_PATH
$buildSpec = $true
$fetchSpec = $false
$generateAdmin = $true
$restoreTools = $true

function Show-Usage {
    Write-Host 'Usage:'
    Write-Host '  .\generate-clients.ps1 [options]'
    Write-Host ''
    Write-Host 'Options:'
    Write-Host '  --admin-path PATH        Path to admin. Default: .\admin'
    Write-Host '  --server-url URL         Fetch OpenAPI from URL/openapi/v1.json instead of building it'
    Write-Host '  --spec PATH              Use an existing OpenAPI document instead of building one'
    Write-Host '  --skip-admin             Generate only backend C# clients'
    Write-Host '  --skip-tool-restore      Do not run dotnet tool restore before generation'
    Write-Host '  --help                   Show this help'
}

for ($i = 0; $i -lt $args.Count; $i++) {
    switch ($args[$i]) {
        { $_ -in @('--help', '-help', '-?') } {
            Show-Usage
            exit 0
        }
        { $_ -in @('--admin-path', '-AdminPath') } {
            if ($i + 1 -ge $args.Count) {
                throw "Missing value for $($args[$i])"
            }
            $i++
            $AdminPath = $args[$i]
        }
        { $_ -in @('--server-url', '-ServerUrl') } {
            if ($i + 1 -ge $args.Count) {
                throw "Missing value for $($args[$i])"
            }
            $i++
            $ServerUrl = $args[$i]
            $buildSpec = $false
            $fetchSpec = $true
        }
        { $_ -in @('--spec', '-Spec') } {
            if ($i + 1 -ge $args.Count) {
                throw "Missing value for $($args[$i])"
            }
            $i++
            $Spec = $args[$i]
            $buildSpec = $false
            $fetchSpec = $false
        }
        { $_ -in @('--skip-admin', '-SkipAdmin') } {
            $generateAdmin = $false
        }
        { $_ -in @('--skip-tool-restore', '-SkipToolRestore') } {
            $restoreTools = $false
        }
        default {
            throw "Unknown argument: $($args[$i])"
        }
    }
}

if ([string]::IsNullOrWhiteSpace($AdminPath)) {
    $AdminPath = Join-Path $scriptDir 'admin'
}

$lockDir = Join-Path $scriptDir '.nona-generate-clients.lock'
$lockAcquired = $false
$migratorOutput = Join-Path $scriptDir 'migrator/src/ConfigMigrator.Core/Generated'
$cliOutput = Join-Path $scriptDir 'cli/src/Nona.Cli/Core/Generated'
$adminOutput = Join-Path $AdminPath 'src/generated/api.ts'

function Invoke-Checked {
    param([scriptblock]$Command)

    & $Command
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Require-File {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Expected $Description at $Path, but it does not exist."
    }
}

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

    if ($restoreTools) {
        Write-Host 'Restoring .NET tools...'
        Invoke-Checked { dotnet tool restore }
    }

    if ($buildSpec) {
        Write-Host "Building OpenAPI spec from $webApiProject..."
        New-Item -ItemType Directory -Force -Path $openApiDirectory | Out-Null
        $env:Storage__Type = 'InMemory'
        Invoke-Checked {
            dotnet build "$webApiProject" `
                /p:OpenApiGenerateDocuments=true `
                "/p:OpenApiDocumentsDirectory=$openApiDirectory"
        }
        Require-File -Path $Spec -Description 'generated OpenAPI spec'
        Write-Host "  -> $Spec"
    }
    elseif ($fetchSpec) {
        if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
            $ServerUrl = 'http://localhost:5027'
        }

        Write-Host "Fetching OpenAPI spec from $ServerUrl/openapi/v1.json..."
        $specParent = Split-Path -Parent $Spec
        if (-not [string]::IsNullOrWhiteSpace($specParent)) {
            New-Item -ItemType Directory -Force -Path $specParent | Out-Null
        }

        if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
            Invoke-Checked { curl.exe -f -s "$ServerUrl/openapi/v1.json" -o "$Spec" }
        }
        else {
            Invoke-WebRequest -Uri "$ServerUrl/openapi/v1.json" -OutFile "$Spec"
        }
        Write-Host "  -> $Spec"
    }
    else {
        Require-File -Path $Spec -Description 'OpenAPI spec'
        Write-Host "Using OpenAPI spec: $Spec"
    }

    Write-Host ''
    Write-Host 'Generating C# client for Migrator...'
    Invoke-Checked {
        dotnet kiota generate `
            -l CSharp `
            -d "$Spec" `
            -c NonaMigrationApiClient `
            -n Nona.Migrator.Core.Generated `
            -o "$migratorOutput" `
            --co
    }
    Write-Host '  -> migrator/src/ConfigMigrator.Core/Generated'

    Write-Host ''
    Write-Host 'Generating C# client for CLI...'
    Invoke-Checked {
        dotnet kiota generate `
            -l CSharp `
            -d "$Spec" `
            -c NonaApiClient `
            -n Nona.Cli.Generated `
            -o "$cliOutput" `
            --co
    }
    Write-Host '  -> cli/src/Nona.Cli/Core/Generated'

    if ($generateAdmin) {
        if (-not (Test-Path -LiteralPath $AdminPath -PathType Container)) {
            throw "Admin path not found: $AdminPath. Pass --admin-path PATH or --skip-admin."
        }

        $adminOutputParent = Split-Path -Parent $adminOutput
        New-Item -ItemType Directory -Force -Path $adminOutputParent | Out-Null

        Write-Host ''
        Write-Host 'Generating TypeScript types for admin UI...'
        $npmCommand = if (Get-Command npm.cmd -ErrorAction SilentlyContinue) {
            'npm.cmd'
        }
        elseif (Get-Command npm -ErrorAction SilentlyContinue) {
            'npm'
        }
        else {
            $null
        }

        if ($npmCommand -and (Test-Path -LiteralPath (Join-Path $AdminPath 'package.json') -PathType Leaf)) {
            Invoke-Checked { & $npmCommand --prefix "$AdminPath" exec --yes -- openapi-typescript "$Spec" -o "$adminOutput" }
        }
        else {
            $npxCommand = if (Get-Command npx.cmd -ErrorAction SilentlyContinue) { 'npx.cmd' } else { 'npx' }
            Invoke-Checked { & $npxCommand --yes openapi-typescript "$Spec" -o "$adminOutput" }
        }
        Write-Host "  -> $adminOutput"
    }

    Write-Host ''
    Write-Host 'Done. Review the changes and commit the generated files.'
}
finally {
    Remove-Lock
}
