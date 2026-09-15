# Скиллы и AI-инструменты проекта (ZCode)

Здесь живут **переносимые AI-скиллы** проекта. Они закоммичены в git, поэтому при клонировании
репозитория на новый ПК (или после отката системы) всё подхватывается автоматически — без
ручной установки.

## Что есть

| Скилл | Команда | Назначение | Источник | Лицензия |
|---|---|---|---|---|
| graphify | `/graphify` | Knowledge-graph кодовой базы: `query`/`path`/`explain` вместо чтения файлов — экономия токенов | [Graphify-Labs/graphify](https://github.com/Graphify-Labs/graphify) | Apache-2.0 |
| python-code-review | `/python-code-review` | Написание type-safe Python 3.11+ (mypy strict, pytest, async, dataclasses) | [Jeffallan/claude-skills](https://github.com/Jeffallan/claude-skills) | MIT |
| dotnet-csharp | `/dotnet-csharp` | C#/.NET 8+ (ASP.NET Core, EF Core, async, DI, Result-паттерн) | [Jeffallan/claude-skills](https://github.com/Jeffallan/claude-skills) | MIT |
| code-reviewer | `/code-reviewer` | Структурированное ревью кода (баги, безопасность, N+1, архитектура) | [Jeffallan/claude-skills](https://github.com/Jeffallan/claude-skills) | MIT |

### MCP-сервер (контекстная документация)

| Сервер | Что даёт | Источник | Установка |
|---|---|---|---|
| context7 | Актуальная версия-специфичная документация библиотек (Python, .NET и др.) прямо в контекст — вместо устаревших знаний и чтения целых доков | [Upstash Context7](https://github.com/upstash/context7) | Авто через `.zcode/config.json` (требует Node.js ≥18) |

Каждый скилл — папка `.zcode/skills/<name>/` с `SKILL.md`, опциональными `references/`
и `ATTRIBUTION.md` (p и source/license/commit).

## MCP-сервер context7 (документация библиотек)

Подключён через `.zcode/config.json` (workspace-scope, авто-доверенный). Агент вызывает
его инструменты (`context7_query`) когда нужна актуальная документация библиотеки —
например версия API пакета или сигнатура метода. Это экономит токены: вместо чтения
целых доков в контекст попадает только нужный фрагмент.

Требования: **Node.js ≥ 18** (проверка: `node --version`). Если Node нет — удалите блок
`mcp` из `.zcode/config.json`, MCP просто не подключится, остальные скиллы продолжат
работать.

## Как это работает

- ZCode сканирует `<repo>/.zcode/skills/*/SKILL.md` при старте (workspace-level). Скиллы
  появляются в `/`-меню и могут вызываться моделью автоматически по `description`.
- **Приоритет загрузки** (выше = весомее): user `~/.zcode/skills` → workspace
  `<repo>/.zcode/skills` → плагины. Скиллы проекта имеют уникальные имена, чтобы не
  перекрывались пользовательскими.
- Для работы скиллов достаточно **открыть ZCode в корне суперпроекта** (где лежит
  `.zcode/`). У скиллов из вложенных git-репозиториев (`IDABatchTool/`, `IDABatchToolWinUI/`)
  не будет видимого `.zcode/skills` — все скиллы живут в корне.

## Перенос на новый ПК

```bash
git clone <этот репозиторий>
cd IDABatchTool
scripts/setup_skills.sh        # (Windows: scripts\setup_skills.bat)
```

Скрипт:
1. Проверяет скиллы в `.zcode/skills/` (чат структура уже в git — ничего ставить не надо);
2. Ставит CLI `graphify` (через `uv` → `pipx` → `pip`), если его нет;
3. Если `graphify-out/graph.json` ещё нет — строит граф локально (AST, без LLM-ключа):
   `graphify . --code-only --no-viz && graphify cluster-only .`
4. Выводит инструкцию.

## Коммитимый граф — экономия токенов с первого запуска

`graphify-out/graph.json`, `GRAPH_REPORT.md` и `graph.html` **закоммичены** (это переносимая
карта кодовой базы: 5596 узлов, 12073 рёбер, 238 сообществ). На новом ПК граф не нужно
пересобирать — он уже в репозитории.

Правило использования (см. также `.zcode/skills/graphify/SKILL.md`):
> Перед чтением файлов кода сначала задай вопрос графу: `graphify query "<вопрос>"`.
> Отвечай по результату — это экономит токены (подграф из нескольких узлов вместо целых файлов).

Пересборка графа после изменений:

```bash
graphify . --code-only --no-viz && graphify cluster-only .
git add graphify-out/graph.json graphify-out/GRAPH_REPORT.md graphify-out/graph.html && git commit
```

> LLM-ключ **не нужен**: граф строится локально через tree-sitter AST (`--code-only`).
> Семантическая экстракция doc/PDF/изображений — опционально, требует API-ключ (не настроено).

Нюансы:
- `.gitignore` игнорирует кэш graphify (`graphify-out/cache/`, `manifest.json`,
  `.graphify_*`) — это машино-специфичные файлы (абсолютные пути, mtime).
- `graphify-out/.graphify_root` хранит абсолютный путь корня — на другом ПК пересоберите
  граф, если он понадобится инкрементальным обновлением (`graphify . --update`).

## Как добавить/удалить скилл

**Добавить** (vendor-копия):
1. Скопировать папку скилла в `.zcode/skills/<name>/`;
2. Проверить лицензию исходника (только permissive: MIT/Apache/BSD);
3. Адаптировать frontmatter под ZCode: `name`, `description` (≤1024 симв.), `license`,
   `metadata` — **без** полей `allowed-tools`/`hooks` (ZCode их не поддерживает в скиллах);
4. Создать `ATTRIBUTION.md` (источник, лицензия, коммит);
5. Закоммитить.

**Удалить** — просто удалить папку и закоммитить.

## Отладка

- Скилл не виден в `/`-меню → проверьте `name` и `description` в frontmatter (ZCode
  сбрасывает скилл без них); проверьте длину description (лимит 1024 симв.).
- `graphify: command not found` → добавьте папку Scripts в PATH (см. вывод setup-скрипта)
  или переустановите CLI.
- После правки SKILL.md — в ZCode: Settings → Skills → Refresh.