param(
    [string[]]$Path = @("Build/WindowsDevelopment", "Build/Web")
)

$ErrorActionPreference = "Stop"

$SecretPattern = 'sk-(?=(?:[A-Za-z_-]*\d){3})[A-Za-z0-9_-]{20,}'
$SecretFoundExitCode = 1
$MissingBuildExitCode = 2

function Read-Payload {
    param([System.IO.FileInfo]$File)

    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    $source = [System.IO.MemoryStream]::new($bytes)
    $decoder = switch ($File.Extension) {
        ".br" { [System.IO.Compression.BrotliStream]::new($source, [System.IO.Compression.CompressionMode]::Decompress) }
        ".gz" { [System.IO.Compression.GZipStream]::new($source, [System.IO.Compression.CompressionMode]::Decompress) }
        default { $null }
    }

    if ($null -eq $decoder) {
        return $bytes
    }

    $target = [System.IO.MemoryStream]::new()
    $decoder.CopyTo($target)
    $decoder.Dispose()
    return $target.ToArray()
}

function Find-Secrets {
    param([byte[]]$Payload)

    $encodings = @(
        [System.Text.Encoding]::Latin1,
        [System.Text.Encoding]::Unicode
    )

    foreach ($encoding in $encodings) {
        foreach ($match in [regex]::Matches($encoding.GetString($Payload), $SecretPattern)) {
            $match.Value.Substring(0, 7) + "…"
        }
    }
}

$missing = $Path | Where-Object { -not (Test-Path $_) }
if ($missing) {
    Write-Host "build not found: $($missing -join ', ')"
    exit $MissingBuildExitCode
}

$scanned = 0
$findings = @()
foreach ($root in $Path) {
    foreach ($file in Get-ChildItem $root -Recurse -File) {
        $scanned++
        foreach ($secret in Find-Secrets (Read-Payload $file)) {
            $findings += "$($file.FullName): $secret"
        }
    }
}

if ($findings) {
    $findings | ForEach-Object { Write-Host $_ }
    Write-Host "$($findings.Count) secret(s) matching '$SecretPattern' in $scanned file(s)"
    exit $SecretFoundExitCode
}

Write-Host "no match for '$SecretPattern' in $scanned file(s) under $($Path -join ', ')"
exit 0
