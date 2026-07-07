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

$packageIdentifier = 'Ryware.NonaCLI'
$packagePublisher = 'Ryware'
$packageName = 'Nona CLI'
$manifestVersion = '1.12.0'

if (-not ($Version -match '^[0-9A-Za-z][0-9A-Za-z\.\-]*$')) {
    throw "Version contains unsupported characters: $Version"
}

if (-not ($WinX64Checksum -match '^[A-Fa-f0-9]{64}$')) {
    throw "WinX64Checksum must be a 64-character SHA-256 hash."
}

if (-not ($WinArm64Checksum -match '^[A-Fa-f0-9]{64}$')) {
    throw "WinArm64Checksum must be a 64-character SHA-256 hash."
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

$resolvedOutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$manifestDirectory = Join-Path $resolvedOutputDirectory (Join-Path 'manifests' (Join-Path 'r' (Join-Path $packagePublisher (Join-Path 'NonaCLI' $Version))))
New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null

$versionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$manifestVersion.schema.json
PackageIdentifier: $packageIdentifier
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $manifestVersion
"@

$localeManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$manifestVersion.schema.json
PackageIdentifier: $packageIdentifier
PackageVersion: $Version
PackageLocale: en-US
Publisher: $packagePublisher
PublisherUrl: https://github.com/Ryware
PublisherSupportUrl: https://github.com/Ryware/nona-config/issues
Author: Ryware.dev
PackageName: $packageName
PackageUrl: https://github.com/Ryware/nona-config
License: Apache-2.0
LicenseUrl: https://github.com/Ryware/nona-config/blob/development/LICENSE.txt
ShortDescription: Cross-platform CLI for Nona Config administration and migrations.
Description: Nona CLI provides command-line access for Nona Config key management, authentication, saved defaults, and Firebase Remote Config migrations.
Moniker: nona-cli
Tags:
- cli
- nona-cli
- configuration
- feature-flags
- remote-config
- firebase
ReleaseNotesUrl: https://github.com/Ryware/nona-config/releases/tag/cli-v$Version
ManifestType: defaultLocale
ManifestVersion: $manifestVersion
"@

$installerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$manifestVersion.schema.json
PackageIdentifier: $packageIdentifier
PackageVersion: $Version
InstallerType: zip
NestedInstallerType: portable
Commands:
- nona
ReleaseDate: $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd'))
Installers:
- Architecture: x64
  InstallerUrl: $WinX64Url
  InstallerSha256: $($WinX64Checksum.ToUpperInvariant())
  NestedInstallerFiles:
  - RelativeFilePath: nona.exe
    PortableCommandAlias: nona
- Architecture: arm64
  InstallerUrl: $WinArm64Url
  InstallerSha256: $($WinArm64Checksum.ToUpperInvariant())
  NestedInstallerFiles:
  - RelativeFilePath: nona.exe
    PortableCommandAlias: nona
ManifestType: installer
ManifestVersion: $manifestVersion
"@

Write-Utf8NoBom -Path (Join-Path $manifestDirectory "$packageIdentifier.yaml") -Content $versionManifest
Write-Utf8NoBom -Path (Join-Path $manifestDirectory "$packageIdentifier.locale.en-US.yaml") -Content $localeManifest
Write-Utf8NoBom -Path (Join-Path $manifestDirectory "$packageIdentifier.installer.yaml") -Content $installerManifest

Write-Host "Created Winget manifests in $manifestDirectory"
