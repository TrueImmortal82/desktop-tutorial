@echo off
chcp 65001 >nul
setlocal

cd /d "%~dp0"

set "APP=%~dp0WindowsAdminShortcuts.exe"

if not exist "%APP%" (
    echo.
    echo Ошибка: файл WindowsAdminShortcuts.exe не найден.
    echo Помести этот BAT-файл в одну папку с приложением.
    echo.
    pause
    exit /b 1
)

start "" "%APP%"
exit /b 0
