#!/usr/bin/env bash
# setup_skills.sh — переносимая установка графити-инструментов для этого репозитория (ZCode).
# Ставит CLI graphify (если отсутствует) и пересобирает граф кода, если его ещё нет.
# Идемпотентен: повторный запуск безопасен.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "==> Настройка AI-скиллов репозитория: $REPO_ROOT"

# --- 1. Скиллы проекта ---
SKILLS_DIR="$REPO_ROOT/.zcode/skills"
if [ -d "$SKILLS_DIR" ]; then
  echo "    [ok] Скиллы проекта найдены: $(ls "$SKILLS_DIR" | tr '\n' ' ')"
else
  echo "    [warn] Папка .zcode/skills/ отсутствует. Запустите из корня суперпроекта (где лежит .zcode/)."
fi

# --- 2. CLI graphify ---
need_install=0
if command -v graphify >/dev/null 2>&1; then
  echo "    [ok] graphify уже установлен: $(graphify --version 2>&1 | head -1)"
elif [ -x "$HOME/AppData/Roaming/Python/Python314/Scripts/graphify.exe" ]; then
  echo "    [ok] graphify найден (Windows user scripts): $HOME/AppData/Roaming/Python/Python314/Scripts/graphify.exe"
  # Добавляем в PATH для текущей сессии
  export PATH="$HOME/AppData/Roaming/Python/Python314/Scripts:$PATH"
else
  need_install=1
fi

if [ "$need_install" -eq 1 ]; then
  echo "==> graphify CLI не найден. Пытаюсь установить..."
  if command -v uv >/dev/null 2>&1; then
    echo "    через uv tool (рекомендуется):"
    uv tool install graphifyy
  elif command -v pipx >/dev/null 2>&1; then
    echo "    через pipx:"
    pipx install graphifyy
  elif command -v pip >/dev/null 2>&1 || python -m pip --version >/dev/null 2>&1; then
    echo "    через pip (python -m pip install graphifyy):"
    python -m pip install graphifyy
  else
    echo "    [fatal] Не найден ни один менеджер Python-пакетов (uv, pipx, pip)."
    echo "            Установите uv: https://docs.astral.sh/uv/ (или pipx/pip) и повторите."
    exit 1
  fi
  echo "    Убедитесь, что директория скриптов (например ~/.local/bin или"
  echo "    ~/AppData/Roaming/Python/Python314/Scripts) есть в PATH, затем выполните скрипт ещё раз."
fi

# На случай установки через pip в Windows — добавим user scripts в PATH для сессии
SCRIPT_DIR_WIN="$HOME/AppData/Roaming/Python/Python314/Scripts"
if [ -x "$SCRIPT_DIR_WIN/graphify.exe" ]; then
  case ":$PATH:" in *":$SCRIPT_DIR_WIN:"*) ;; *) export PATH="$SCRIPT_DIR_WIN:$PATH" ;; esac
fi

if ! command -v graphify >/dev/null 2>&1; then
  echo "    [fatal] graphify всё ещё не в PATH. Добавьте его в PATH и перезапустите."
  exit 1
fi

# --- 3. Node.js (для MCP-сервера context7) ---
if command -v node >/dev/null 2>&1; then
  echo "    [ok] Node.js: $(node --version 2>&1 | head -1)"
else
  echo "    [warn] Node.js не найден. MCP-сервер context7 (документация библиотек) не подключится."
  echo "           Установите Node.js >= 18: https://nodejs.org/ — скиллы продолжат работать без него."
fi

# --- 4. Граф кода (пересборка только при отсутствии) ---
if [ -f "$REPO_ROOT/graphify-out/graph.json" ]; then
  echo "    [ok] Граф кода уже существует (graphify-out/graph.json)."
  echo "        Для пересборки запустите: graphify . --code-only --no-viz && graphify cluster-only ."
else
  echo "==> Граф кода отсутствует. Строю локально (AST, без LLM-ключа)..."
  graphify . --code-only --no-viz
  graphify cluster-only .
  echo "    [ok] Граф построен: graphify-out/graph.json + GRAPH_REPORT.md"
fi

echo
echo "==> Готово. Что дальше:"
echo "  1. Откройте ZCode в корне суперпроекта: $REPO_ROOT"
echo "  2. Скиллы проекта '/' меню: /graphify, /python-code-review, /dotnet-csharp, /code-reviewer"
echo "  3. Проверка запроса к графу: graphify query \"архитектура модуля database\""
echo
echo "  4. Скиллы из репозитория подхватываются автоматически (workspace .zcode/skills). Перенос на другой ПК:"
echo "     git clone + этот скрипт. Подробнее: SKILLS.md в корне репозитория."