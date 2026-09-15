@echo off
REM setup_skills.bat — переносимая установка graphify для этого репозитория (ZCode).
REM Ставит CLI graphify (если отсутствует) и пересобирает граф кода, если его ещё нет.
REM Идемпотентен: повторный запуск безопасен.

setlocal enabledelayedexpansion
set "REPO_ROOT=%~dp0.."
cd /d "%REPO_ROOT%"

echo ==> Настройка AI-скиллов репозитория: %REPO_ROOT%

REM --- 1. Скиллы проекта ---
set "SKILLS_DIR=%REPO_ROOT%\.zcode\skills"
if exist "%SKILLS_DIR%" (
    echo     [ok] Скиллы проекта найдены.
) else (
    echo     [warn] Папка .zcode\skills отсутствует. Запустите из корня суперпроекта.
)

REM --- 2. CLI graphify ---
where graphify >nul 2>nul
if %errorlevel%==0 (
    echo     [ok] graphify уже установлен: 
    graphify --version
    goto :GRAPH_OK
)

REM Поиск в user scripts (типичная установка pip на Windows)
set "USERSCRIPTS=%APPDATA%\Python\Python314\Scripts"
if exist "%USERSCRIPTS%\graphify.exe" (
    echo     [ok] graphify найден: %USERSCRIPTS%\graphify.exe
    set "PATH=%USERSCRIPTS%;%PATH%"
    goto :GRAPH_OK
)

REM --- Установка ---
echo ==> graphify CLI не найден. Пытаюсь установить...
where uv >nul 2>nul
if %errorlevel%==0 (
    echo     через uv tool (рекомендуется)...
    uv tool install graphifyy
    goto :INSTALLED
)
where pipx >nul 2>nul
if %errorlevel%==0 (
    echo     через pipx...
    pipx install graphifyy
    goto :INSTALLED
)
python -m pip --version >nul 2>nul
if %errorlevel%==0 (
    echo     через pip...
    python -m pip install graphifyy
    goto :INSTALLED
)

echo     [fatal] Не найден ни один менеджер Python-пакетов (uv, pipx, pip).
echo            Установите uv: https://docs.astral.sh/uv/ и повторите.
exit /b 1

:INSTALLED
echo     Убедитесь, что папка Scripts (например %APPDATA%\Python\Python314\Scripts)
echo     есть в PATH, затем выполните скрипт ещё раз для построения графа.
set "PATH=%USERSCRIPTS%;%PATH%"

:GRAPH_OK
where graphify >nul 2>nul
if %errorlevel%==0 goto :GRAPH_BUILD
echo     [fatal] graphify всё ещё не в PATH. Добавьте его в PATH и перезапустите.
exit /b 1

:GRAPH_BUILD
if exist "%REPO_ROOT%\graphify-out\graph.json" (
    echo     [ok] Граф кода уже существует (graphify-out\graph.json).
    echo         Для пересборки: graphify . --code-only --no-viz ^&^& graphify cluster-only .
) else (
    echo ==> Граф кода отсутствует. Строю локально (AST, без LLM-ключа)...
    graphify . --code-only --no-viz
    if %errorlevel% neq 0 exit /b 1
    graphify cluster-only .
    echo     [ok] Граф построен: graphify-out\graph.json + GRAPH_REPORT.md
)

echo.
echo ==> Готово. Что дальше:
echo  1. Откройте ZCode в корне суперпроекта: %REPO_ROOT%
echo  2. Скиллы проекта '/' меню: /graphify, /python-code-review, /dotnet-csharp, /code-reviewer
echo  3. Проверка запроса к графу: graphify query "архитектура модуля database"
echo  4. Перенос на другой ПК: git clone + этот скрипт. Подробнее: SKILLS.md в корне.

endlocal
exit /b 0