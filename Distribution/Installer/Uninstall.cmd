@echo off
setlocal

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1" -Uninstall
if errorlevel 1 (
    echo.
    echo Uninstallation failed. Review the error above.
    pause
    exit /b 1
)

echo.
echo Uninstallation completed successfully.
pause
