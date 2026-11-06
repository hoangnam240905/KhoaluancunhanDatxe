@echo off
setlocal EnableDelayedExpansion
title CustomerApp - Android Emulator
cd /d "%~dp0CustomerApp"
echo ========================================================
echo   KHOI DONG MAY AO ANDROID VA CHAY CUSTOMER APP
echo ========================================================
echo [1/2] Dang bat may ao Android Pixel_5 (neu chua mo)...
call flutter emulators --launch Pixel_5
echo Dang cho may ao Android khoi dong...
set "ADB=%ANDROID_HOME%\platform-tools\adb.exe"
if not exist "%ADB%" set "ADB=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"
if exist "%ADB%" (
    "%ADB%" -s emulator-5554 wait-for-device
    set "booted="
    for /l %%i in (1,1,60) do (
        for /f "delims=" %%s in ('"%ADB%" -s emulator-5554 shell getprop sys.boot_completed 2^>nul') do set "booted=%%s"
        if "!booted!"=="1" goto :emulator_ready
        timeout /t 1 /nobreak >nul
    )
    echo Khong the cho may ao Android khoi dong trong 60 giay.
    pause
    exit /b 1
)
timeout /t 15 /nobreak >nul
:emulator_ready
if exist "%ADB%" (
    "%ADB%" shell settings put global window_animation_scale 0
    "%ADB%" shell settings put global transition_animation_scale 0
    "%ADB%" shell settings put global animator_duration_scale 0
)
echo [2/2] Dang deploy va khoi chay CustomerApp len Android...
flutter run -d emulator-5554
pause
