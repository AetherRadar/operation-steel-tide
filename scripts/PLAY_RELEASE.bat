@echo off
setlocal
set "ROOT=%~dp0"
set "SERVER=%ROOT%steel-tide-server.exe"
set "GAME=%ROOT%OperationSteelTide.exe"
set "PID_FILE=%TEMP%\operation-steel-tide-backend-%RANDOM%.pid"
set "BACKEND_PID="

if not exist "%GAME%" (
  echo OperationSteelTide.exe is missing from this release folder.
  pause
  exit /b 1
)

if exist "%SERVER%" (
  powershell.exe -NoProfile -Command "$p=Start-Process -FilePath $env:SERVER -WorkingDirectory $env:ROOT -ArgumentList @('-addr','127.0.0.1:8787','-data','backend\data\state.json') -WindowStyle Hidden -PassThru; Start-Sleep -Milliseconds 600; $p.Refresh(); if (-not $p.HasExited) { Set-Content -LiteralPath $env:PID_FILE -Value $p.Id }"
  if exist "%PID_FILE%" set /p BACKEND_PID=<"%PID_FILE%"
)

start "" /wait "%GAME%" %*
set "EXIT_CODE=%ERRORLEVEL%"
if defined BACKEND_PID powershell.exe -NoProfile -Command "$p=Get-Process -Id $env:BACKEND_PID -ErrorAction SilentlyContinue; if ($p -and $p.Path -eq $env:SERVER) { Stop-Process -InputObject $p -Force }"
if exist "%PID_FILE%" del /q "%PID_FILE%"
endlocal & exit /b %EXIT_CODE%
