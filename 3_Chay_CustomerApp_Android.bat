@echo off
title CustomerApp - Android Emulator
cd /d "%~dp0CustomerApp"
echo ========================================================
echo   KHOI DONG MAY AO ANDROID VA CHAY CUSTOMER APP
echo ========================================================
echo [1/2] Dang bat may ao Android Pixel_9a (neu chua mo)...
call flutter emulators --launch Pixel_9a
echo [2/2] Dang deploy va khoi chay CustomerApp len Android...
flutter run -d emulator-5554
pause

