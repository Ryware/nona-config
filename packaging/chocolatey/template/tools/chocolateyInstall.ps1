$ErrorActionPreference = 'Stop'

$toolsDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$packageName = 'nona-cli'
$archivePath = Join-Path $toolsDirectory 'nona-cli.zip'
$isArm64 = $env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64'

if ($isArm64) {
    $downloadUrl = '{{WIN_ARM64_URL}}'
    $checksum = '{{WIN_ARM64_CHECKSUM}}'
}
else {
    $downloadUrl = '{{WIN_X64_URL}}'
    $checksum = '{{WIN_X64_CHECKSUM}}'
}

Get-ChocolateyWebFile `
    -PackageName $packageName `
    -FileFullPath $archivePath `
    -Url $downloadUrl `
    -Checksum $checksum `
    -ChecksumType 'sha256'

Get-ChocolateyUnzip -FileFullPath $archivePath -Destination $toolsDirectory -PackageName $packageName
Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue

$executablePath = Join-Path $toolsDirectory 'nona.exe'
if (-not (Test-Path $executablePath)) {
    throw "Expected CLI executable was not found at $executablePath"
}
