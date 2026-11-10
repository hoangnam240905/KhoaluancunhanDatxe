@echo off
title Recommender Service (FastAPI :8001)
cd /d "%~dp0Recommender"

echo ========================================================
echo   KHOI DONG DICH VU GOI Y XE THONG MINH (FASTAPI :8001)
echo ========================================================

set "PY_EXE=%LOCALAPPDATA%\Programs\Python\Python311\python.exe"
if not exist "%PY_EXE%" set "PY_EXE=python"

echo Dang kiem tra / cai dat goi phu thuoc (fastapi, uvicorn)...
"%PY_EXE%" -m pip install -r requirements.txt

echo.
echo Dang khoi dong Recommender Service tai http://127.0.0.1:8001 ...
"%PY_EXE%" -m uvicorn app:app --host 127.0.0.1 --port 8001 --reload

pause
