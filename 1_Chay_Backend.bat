@echo off
title Backend API Server (Port 5199)
cd /d "%~dp0"
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
echo ========================================================
echo   DANG KHOI DONG BACKEND API SERVER TAI PORT 5199
echo ========================================================
dotnet run --project .\Backend\Backend.csproj
pause
