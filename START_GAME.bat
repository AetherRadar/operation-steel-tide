@echo off
setlocal
set "LAUNCHER=%~dp0scripts\Start-Game.ps1"

if not exist "%LAUNCHER%" (
  echo Launcher helper is missing: "%LAUNCHER%"
  pause
  exit /b 1
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%LAUNCHER%" %*
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo Operation Steel Tide launcher exited with code %EXIT_CODE%.
  pause
)

endlocal & exit /b %EXIT_CODE%
