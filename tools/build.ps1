param(
    [ValidateSet("Windows Development", "Windows Release", "Web")]
    [string]$BuildProfile = "Windows Development",
    [string]$OutputPath = "",
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"

$RetryableExitCodes = @(6, 7)
$MaxAttempts = 2

$DefaultOutputPaths = @{
    "Windows Development" = "Build/WindowsDevelopment/GFT.exe"
    "Windows Release"     = "Build/WindowsRelease/GFT.exe"
    "Web"                 = "Build/Web"
}

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    if (-not $OutputPath) {
        $OutputPath = $DefaultOutputPaths[$BuildProfile]
    }

    $arguments = @(
        "--non-interactive", "--format", "json",
        "build", ".",
        "--profile", $BuildProfile,
        "--output-path", $OutputPath
    )

    if ($AllowDirty) {
        $arguments += "--allow-dirty-build"
    }

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        & unity @arguments
        $exitCode = $LASTEXITCODE

        $isRetryable = $RetryableExitCodes -contains $exitCode
        if (-not $isRetryable -or $attempt -eq $MaxAttempts) {
            exit $exitCode
        }

        Write-Warning "unity exited with $exitCode (operation or service failure); retrying once"
    }
}
finally {
    Pop-Location
}
