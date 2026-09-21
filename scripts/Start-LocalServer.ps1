param(
    [int]$Port = 3000
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$serverRoot = Join-Path $repoRoot 'server-node'
$runtimeRoot = Join-Path $repoRoot '.local'
$pidFile = Join-Path $runtimeRoot 'server.pid'
$stdoutLog = Join-Path $runtimeRoot 'server.stdout.log'
$stderrLog = Join-Path $runtimeRoot 'server.stderr.log'

if (-not (Test-Path -LiteralPath (Join-Path $serverRoot 'node_modules\ws'))) {
    Write-Host 'Installing server dependencies...'
    & npm install --prefix $serverRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null

if (Test-Path -LiteralPath $pidFile) {
    $existingPid = 0
    $pidText = [string](Get-Content -LiteralPath $pidFile -Raw)
    [void][int]::TryParse($pidText.Trim(), [ref]$existingPid)
    if ($existingPid -gt 0 -and (Get-Process -Id $existingPid -ErrorAction SilentlyContinue)) {
        Write-Host "Local server is already running (PID $existingPid)."
        Write-Host "Health: http://localhost:$Port/health"
        exit 0
    }
    Remove-Item -LiteralPath $pidFile -Force
}

# Win32_Process avoids Start-Process's environment-table compatibility issue
# on machines that contain both Path and PATH entries.
$commandLine = 'cmd.exe /d /s /c "set PORT=' + $Port + '&& node server.js 1> ""' + $stdoutLog + '"" 2> ""' + $stderrLog + '"""'
$created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
    CommandLine = $commandLine
    CurrentDirectory = $serverRoot
}
if ($created.ReturnValue -ne 0 -or $created.ProcessId -le 0) {
    throw "Unable to start the local server (Win32_Process result: $($created.ReturnValue))."
}
$serverProcess = Get-Process -Id $created.ProcessId -ErrorAction Stop

Set-Content -LiteralPath $pidFile -Value $serverProcess.Id

$deadline = (Get-Date).AddSeconds(10)
do {
    Start-Sleep -Milliseconds 250
    try {
        $health = Invoke-RestMethod -Uri "http://localhost:$Port/health" -TimeoutSec 1
        if ($health.status -eq 'ok') {
            Write-Host "Local server started (PID $($serverProcess.Id))."
            Write-Host "WebSocket: ws://localhost:$Port"
            Write-Host "Test page: http://localhost:$Port"
            exit 0
        }
    } catch {
        if ($serverProcess.HasExited) { break }
    }
} while ((Get-Date) -lt $deadline)

Write-Error "Server did not become healthy. Check $stderrLog"
exit 1
