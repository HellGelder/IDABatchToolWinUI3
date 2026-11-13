@echo off
rem Запуск утилиты "ViPNet OSSL - белый перечень по импортам" без консоли.
setlocal
cd /d "%~dp0"
where pythonw >nul 2>&1
if errorlevel 1 (
    python "%~dp0vipnet_ossl_whitelist.py"
) else (
    start "" pythonw "%~dp0vipnet_ossl_whitelist.py"
)
endlocal
