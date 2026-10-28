@echo off
setlocal
echo [EnRAGE] Building NemesisMod.dll (Release net48)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0decompiled\NemesisMod\build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed with exit code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)
copy /Y "%~dp0decompiled\NemesisMod\bin\Release\net48\NemesisMod.dll" "%~dp0scripts\NemesisMod.dll" >nul
echo [EnRAGE] Successfully updated scripts\NemesisMod.dll
exit /b 0
