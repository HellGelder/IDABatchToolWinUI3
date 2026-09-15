# AGENTS.md — контекст проекта для ИИ-агентов (ZCode)

## О проекте

IDABatchTool — инструмент пакетного анализа бинарных файлов (PE/ELF) через IDA Pro.
Репозиторий — **суперпроект** с двумя вложенными git-репозиториями:

- `IDABatchTool/` — Python-часть (пакет `ida_batch_tool/`: archive_handler, classifier,
  config, database, discovery, ida, reporting, ui; `scripts/` — экспорт IDA, diaphora;
  `main.py`, `main_gui.py`, `config.yaml`).
- `IDABatchToolWinUI/` — C# WinUI 3 приложение (App.xaml, MainWindow, Pages/).

Рабочий каталог для ZCode — **корень суперпроекта** (здесь лежат `.zcode/`, `SKILLS.md`,
`graphify-out/`).

## Скиллы проекта

Скиллы живут в `.zcode/skills/` и доступны через `/`-меню (см. `SKILLS.md`):

- `/graphify` — knowledge-graph кодовой базы (экономия токенов).
- `/python-code-review` — Python 3.11+ (mypy strict, pytest, async).
- `/dotnet-csharp` — C#/.NET 8+ (ASP.NET Core, EF Core, async, DI).
- `/code-reviewer` — структурированное ревью кода.

## Правило экономии токенов (graphify-first)

Перед чтением файлов кода для ответа на вопрос об архитектуре, связях или содержимом
проекта — **сначала выполни `graphify query "<вопрос>"`** (граф уже построен и закоммичен
в `graphify-out/graph.json`). Отвечай по результату запроса, цитируя `source_location`.
Читай файлы целиком только когда граф не даёт ответа. Это экономит токены.

## Стандарты (кратко)

- Python: PEP 8, аннотации типов, Google-style docstrings, pytest.
- C#: nullable reference types, file-scoped namespaces, async/await + CancellationToken, DI.
- Комментарии и общение — на русском; код, имена, коммиты — на английском.
- Секреты — только в `.env`/переменных окружения, не в коде.