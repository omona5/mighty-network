@echo off
setlocal
title Mighty Local Server - Start

set "REPO_ROOT=%~dp0"
set "SERVER_ROOT=%REPO_ROOT%server-node"
set "RUNTIME_ROOT=%REPO_ROOT%.local"

if not exist "%SERVER_ROOT%\node_modules\ws" (
    echo Server dependencies are missing. Run npm install in server-node first.
    pause
    exit /b 1
)

powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort 3000 -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if not errorlevel 1 (
    echo Local server is already listening on http://localhost:3000
    pause
    exit /b 0
)

if not exist "%RUNTIME_ROOT%" mkdir "%RUNTIME_ROOT%"
start "Mighty Local Server" /b "%ComSpec%" /d /c ""%REPO_ROOT%scripts\Run-LocalServer.cmd""

timeout /t 1 /nobreak > nul
powershell.exe -NoProfile -Command "try { $health = Invoke-RestMethod -Uri 'http://localhost:3000/health' -TimeoutSec 3; if ($health.status -eq 'ok') { exit 0 } } catch {}; exit 1"

echo.
if errorlevel 1 (
    echo Server could not be started. See .local\server.stderr.log for details.
) else (
    echo Local server is running. You can close this window; it keeps running in the background.
)
pause
endlocal
