@echo off
setlocal

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1"
if errorlevel 1 (
    echo.
    echo Installation failed. Review the error above.
    pause
    exit /b 1
)

echo.
echo Installation completed successfully.
pause
