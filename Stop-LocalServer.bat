@echo off
setlocal
title Mighty Local Server - Stop

powershell.exe -NoProfile -Command "$listener = Get-NetTCPConnection -State Listen -LocalPort 3000 -ErrorAction SilentlyContinue | Select-Object -First 1; if (-not $listener) { Write-Host 'No local server is listening on port 3000.'; exit 0 }; $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue; if (-not $process -or $process.ProcessName -ne 'node') { Write-Error ('Port 3000 is owned by ' + $process.ProcessName + '; it was not stopped.'); exit 1 }; Stop-Process -Id $process.Id -Force; Write-Host ('Local server stopped (PID ' + $process.Id + ').')"

echo.
pause
endlocal
