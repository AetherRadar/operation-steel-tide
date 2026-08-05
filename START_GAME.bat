@echo off
setlocal
set "PROJECT=%~dp0"
if "%PROJECT:~-1%"=="\" set "PROJECT=%PROJECT:~0,-1%"
set "GODOT=%GODOT_MONO%"
if not defined GODOT for /f "delims=" %%G in ('where Godot_v4.6.3-stable_mono_win64.exe 2^>nul') do if not defined GODOT set "GODOT=%%G"
if not defined GODOT for /f "delims=" %%G in ('where godot4.exe 2^>nul') do if not defined GODOT set "GODOT=%%G"
if not defined GODOT for /f "delims=" %%G in ('where godot.exe 2^>nul') do if not defined GODOT set "GODOT=%%G"
if not defined GODOT set "GODOT=%USERPROFILE%\Downloads\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe"
set "STEEL_TIDE_SERVER=%PROJECT%\steel-tide-server.exe"
set "STEEL_TIDE_DATA=%PROJECT%\backend\data\state.json"
set "STEEL_TIDE_BACKEND_LOG=%PROJECT%\backend\backend.log"
set "STEEL_TIDE_BACKEND_ERROR=%PROJECT%\backend\backend-error.log"
set "STEEL_TIDE_BACKEND_PID_FILE=%PROJECT%\backend\backend.pid"
set "BACKEND_PID="
if not exist "%GODOT%" (
  echo Godot Mono was not found. Add it to PATH or set GODOT_MONO to the executable path.
  pause
  exit /b 1
)
if not exist "%STEEL_TIDE_SERVER%" (
  where go.exe >nul 2>&1
  if not errorlevel 1 (
    echo Building the Go mission service...
    pushd "%PROJECT%\backend"
    go build -o "%STEEL_TIDE_SERVER%" ./cmd/server
    popd
  )
)
if exist "%STEEL_TIDE_SERVER%" (
  if exist "%STEEL_TIDE_BACKEND_PID_FILE%" del /q "%STEEL_TIDE_BACKEND_PID_FILE%"
  powershell.exe -NoProfile -Command "$p=Start-Process -FilePath $env:STEEL_TIDE_SERVER -ArgumentList @('-addr','127.0.0.1:8787','-data',$env:STEEL_TIDE_DATA) -RedirectStandardOutput $env:STEEL_TIDE_BACKEND_LOG -RedirectStandardError $env:STEEL_TIDE_BACKEND_ERROR -WindowStyle Hidden -PassThru; Set-Content -LiteralPath $env:STEEL_TIDE_BACKEND_PID_FILE -Value $p.Id"
  if exist "%STEEL_TIDE_BACKEND_PID_FILE%" set /p BACKEND_PID=<"%STEEL_TIDE_BACKEND_PID_FILE%"
  powershell.exe -NoProfile -Command "Start-Sleep -Milliseconds 800"
) else (
  echo Go mission service not found; starting with the built-in offline mission fallback.
)
echo Starting Operation Steel Tide...
start "" /wait "%GODOT%" --path "%PROJECT%" --log-file "%PROJECT%\runtime.log"
set "EXIT_CODE=%ERRORLEVEL%"
if defined BACKEND_PID taskkill /PID %BACKEND_PID% /T /F >nul 2>&1
if exist "%STEEL_TIDE_BACKEND_PID_FILE%" del /q "%STEEL_TIDE_BACKEND_PID_FILE%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo The game exited with error code %EXIT_CODE%.
  echo See "%PROJECT%\runtime.log" for details.
  pause
)
endlocal & exit /b %EXIT_CODE%
