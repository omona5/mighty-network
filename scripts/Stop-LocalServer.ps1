$repoRoot = Split-Path -Parent $PSScriptRoot
$pidFile = Join-Path $repoRoot '.local\server.pid'

if (-not (Test-Path -LiteralPath $pidFile)) {
    Write-Host 'No managed local server is running.'
    exit 0
}

$serverPid = 0
$pidText = [string](Get-Content -LiteralPath $pidFile -Raw)
[void][int]::TryParse($pidText.Trim(), [ref]$serverPid)
$serverProcess = Get-Process -Id $serverPid -ErrorAction SilentlyContinue
if ($serverPid -gt 0 -and $serverProcess) {
    Stop-Process -Id $serverPid
    $serverProcess.WaitForExit(5000)
    Write-Host "Local server stopped (PID $serverPid)."
} else {
    Write-Host "Stale server PID removed ($serverPid)."
}

Remove-Item -LiteralPath $pidFile -Force
