param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$WinX64Url,

    [Parameter(Mandatory = $true)]
    [string]$WinX64Checksum,

    [Parameter(Mandatory = $true)]
    [string]$WinArm64Url,

    [Parameter(Mandatory = $true)]
    [string]$WinArm64Checksum,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'out')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Replace-TemplateTokens {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [hashtable]$Values
    )

    $content = Get-Content -Path $Path -Raw
    foreach ($entry in $Values.GetEnumerator()) {
        $token = '{{' + [string]$entry.Key + '}}'
        $content = $content.Replace($token, [string]$entry.Value)
    }

    Set-Content -Path $Path -Value $content -NoNewline
}

$templateDirectory = Join-Path $PSScriptRoot 'template'
if (-not (Test-Path $templateDirectory)) {
    throw "Chocolatey template directory not found: $templateDirectory"
}

if (-not ($Version -match '^[0-9A-Za-z\.\-]+$')) {
    throw "Version contains unsupported characters: $Version"
}

if (-not ($WinX64Checksum -match '^[A-Fa-f0-9]{64}$')) {
    throw "WinX64Checksum must be a 64-character SHA-256 hash."
}

if (-not ($WinArm64Checksum -match '^[A-Fa-f0-9]{64}$')) {
    throw "WinArm64Checksum must be a 64-character SHA-256 hash."
}

$resolvedOutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("nona-cli-choco-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stagingDirectory | Out-Null
Copy-Item -Path (Join-Path $templateDirectory '*') -Destination $stagingDirectory -Recurse

$values = @{
    VERSION = $Version
    WIN_X64_URL = $WinX64Url
    WIN_X64_CHECKSUM = $WinX64Checksum.ToLowerInvariant()
    WIN_ARM64_URL = $WinArm64Url
    WIN_ARM64_CHECKSUM = $WinArm64Checksum.ToLowerInvariant()
}

Get-ChildItem -Path $stagingDirectory -File -Recurse | ForEach-Object {
    Replace-TemplateTokens -Path $_.FullName -Values $values
}

$nuspecPath = Join-Path $stagingDirectory 'nona-cli.nuspec'
if (-not (Test-Path $nuspecPath)) {
    throw "Expected nuspec at $nuspecPath"
}

try {
    & choco pack $nuspecPath --outputdirectory $resolvedOutputDirectory | Write-Host
}
finally {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

$packagePath = Join-Path $resolvedOutputDirectory ("nona-cli.{0}.nupkg" -f $Version)
if (-not (Test-Path $packagePath)) {
    throw "Chocolatey package was not created: $packagePath"
}

Write-Host "Created $packagePath"
