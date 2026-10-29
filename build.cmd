@echo off
setlocal
echo [EnRAGE] Building NemesisMod.dll (Release net48)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0decompiled\NemesisMod\build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] NemesisMod build failed with exit code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)
copy /Y "%~dp0decompiled\NemesisMod\bin\Release\net48\NemesisMod.dll" "%~dp0scripts\NemesisMod.dll" >nul
echo [EnRAGE] Successfully updated scripts\NemesisMod.dll

echo [NpcAiTalk] Building NpcAiTalk.dll (Release net48)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0decompiled\NpcAiTalkMod\build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] NpcAiTalk build failed with exit code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)
copy /Y "%~dp0decompiled\NpcAiTalkMod\bin\Release\net48\NpcAiTalk.dll" "%~dp0scripts\NpcAiTalk.dll" >nul
copy /Y "%~dp0config_templates\NpcAiTalk.ini" "%~dp0scripts\NpcAiTalk.ini" >nul
echo [NpcAiTalk] Successfully updated scripts\NpcAiTalk.dll and scripts\NpcAiTalk.ini
exit /b 0
