@echo off
setlocal
set "ROOT=%~dp0"
set "SERVER=%ROOT%steel-tide-server.exe"
set "GAME=%ROOT%OperationSteelTide.exe"
set "DATA=%ROOT%backend\data\state.json"
set "PID_FILE=%TEMP%\operation-steel-tide-backend-%RANDOM%.pid"
set "BACKEND_PID="

if not exist "%GAME%" (
  echo OperationSteelTide.exe is missing from this release folder.
  pause
  exit /b 1
)

if exist "%SERVER%" (
  powershell.exe -NoProfile -Command "$p=Start-Process -FilePath $env:SERVER -ArgumentList @('-addr','127.0.0.1:8787','-data',$env:DATA) -WindowStyle Hidden -PassThru; Set-Content -LiteralPath $env:PID_FILE -Value $p.Id"
  if exist "%PID_FILE%" set /p BACKEND_PID=<"%PID_FILE%"
  powershell.exe -NoProfile -Command "Start-Sleep -Milliseconds 600"
)

start "" /wait "%GAME%"
set "EXIT_CODE=%ERRORLEVEL%"
if defined BACKEND_PID taskkill /PID %BACKEND_PID% /T /F >nul 2>&1
if exist "%PID_FILE%" del /q "%PID_FILE%"
endlocal & exit /b %EXIT_CODE%
