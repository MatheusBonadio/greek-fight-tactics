param(
    [ValidateSet("EditMode", "PlayMode", "All")]
    [string]$Mode = "All",
    [string]$OutputDirectory = "artifacts",
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

$TestsFailedExitCode = 8
$RetryableExitCodes = @(6, 7)
$MaxAttempts = 2

function Invoke-UnityWithRetry {
    param([string[]]$Arguments)

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        & unity @Arguments | Out-Host
        $exitCode = $LASTEXITCODE

        $isRetryable = $RetryableExitCodes -contains $exitCode
        if (-not $isRetryable -or $attempt -eq $MaxAttempts) {
            return [int]$exitCode
        }

        Write-Warning "unity exited with $exitCode (operation or service failure); retrying once"
    }
}

function Invoke-TestMode {
    param([string]$TestMode)

    $resultsPath = Join-Path $OutputDirectory "$($TestMode.ToLower()).xml"
    if (Test-Path -LiteralPath $resultsPath) {
        Remove-Item -LiteralPath $resultsPath
    }

    $arguments = @(
        "--non-interactive", "--format", "json",
        "test", ".",
        "--mode", $TestMode,
        "--output", $resultsPath
    )

    if ($Filter) {
        $arguments += @("--filter", $Filter)
    }

    if ($TestMode -eq "EditMode") {
        $arguments += @("--", "-nographics")
    }

    $exitCode = Invoke-UnityWithRetry -Arguments $arguments
    if ($exitCode -eq 0 -and -not (Test-Path -LiteralPath $resultsPath)) {
        Write-Warning "$TestMode finished without writing $resultsPath"
        return 1
    }

    return $exitCode
}

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

    $modes = if ($Mode -eq "All") { @("EditMode", "PlayMode") } else { @($Mode) }
    $finalExitCode = 0

    foreach ($testMode in $modes) {
        $exitCode = Invoke-TestMode -TestMode $testMode
        if ($exitCode -eq $TestsFailedExitCode) {
            Write-Host "$testMode tests failed" -ForegroundColor Red
        }

        if ($exitCode -ne 0 -and $finalExitCode -eq 0) {
            $finalExitCode = $exitCode
        }
    }

    exit $finalExitCode
}
finally {
    Pop-Location
}
