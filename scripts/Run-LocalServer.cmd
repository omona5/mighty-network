@echo off
setlocal
set "PORT=3000"
set "REPO_ROOT=%~dp0.."

cd /d "%REPO_ROOT%\server-node"
node server.js 1> "%REPO_ROOT%\.local\server.stdout.log" 2> "%REPO_ROOT%\.local\server.stderr.log"
