param(
    [Parameter(Mandatory)]
    [string]$InstallDirectory
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$PinnedVersion = "1.0.0-beta.8"
$PinnedSha256 = "f2b2571cd5e9d9975be3cab0b7e3931a9f6193c42c2a491f7c30d72861349993"
$DownloadUrl = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/$PinnedVersion/unity-windows-x64.exe"
$ChecksumMismatchExitCode = 3
$VersionMismatchExitCode = 4

New-Item -ItemType Directory -Force $InstallDirectory | Out-Null
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "unity-cli-$([guid]::NewGuid().ToString('N')).exe"

Invoke-WebRequest -Uri $DownloadUrl -OutFile $staging -UseBasicParsing

$actualSha256 = (Get-FileHash -LiteralPath $staging -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSha256 -ne $PinnedSha256) {
    Remove-Item -LiteralPath $staging -Force
    Write-Host "unity cli $PinnedVersion checksum mismatch: expected $PinnedSha256, got $actualSha256"
    exit $ChecksumMismatchExitCode
}

$binary = Join-Path $InstallDirectory "unity.exe"
Move-Item -LiteralPath $staging -Destination $binary -Force

$reportedVersion = & $binary --version
if ($reportedVersion -ne $PinnedVersion) {
    Write-Host "unity cli reports $reportedVersion, expected $PinnedVersion"
    exit $VersionMismatchExitCode
}

if ($env:GITHUB_PATH) {
    Add-Content -LiteralPath $env:GITHUB_PATH -Value $InstallDirectory
}

Write-Host "unity cli $reportedVersion installed at $binary"
exit 0
