# AGENTS.md — контекст проекта для ИИ-агентов (ZCode)

## О проекте

IDABatchToolWinUI — **исполнение 2**: GUI на C# / WinUI 3 (WinAppSDK) для пакетного
анализа бинарных файлов (PE/ELF) через IDA Pro. Это самостоятельный репозиторий;
сестринский — `IDABatchTool` (исполнение 1, Python: пакет `ida_batch_tool`,
канонический классификатор, мосты).

Ключевое:
- `Pages/` — AnalysisPage, DiffPage (Pivot: «Бинарное»/«Git-сравнение»), SfaPage,
  SettingsPage; `Services/` — нативные порты Python-логики (DirectoriesDiffService —
  Myers, вывод сверен байт-в-байт с `diff_gui.py` исполнения 1);
  `Workers/`, `Models/`, `Controls/`, `Helpers/`.
- `_python/` — Python-мосты (report_bridge.py и др.), деплоятся в bin;
  канон мостов — в репозитории исполнения 1.
- `scripts/` — IDA-side: vendored diaphora, export_data.py.
- Python-мост для diff-подзадачи запрещён — Git-сравнение нативное (C#).

## Сборка и запуск

- Файла `.sln` нет; из корня репо: `dotnet build -c Debug -v quiet` или `dotnet run`.
- Релиз: поднять версию в 4 полях csproj (Version/FileVersion/AssemblyVersion/
  InformationalVersion) → дописать `version.txt` → `make-portable.ps1`.
  Перед скриптом закрыть запущенное приложение из `dist\portable` (лочит AppIcon.ico)
  и убедиться в отсутствии `idat.exe`. Архив: `dist\IDABatchToolWinUI-<версия>.zip`.

## Стандарты

- C#: nullable reference types, file-scoped namespaces, async/await +
  CancellationToken, DI.
- Комментарии и общение — на русском; код, имена, коммиты — на английском.
- Секреты — только в `.env`/переменных окружения, не в коде.

## Грабли (проверено на этом коде)

- Страницы кэшируются: подписки — в конструкторе; `x:Bind` без `Mode=OneWay`
  не обновляет значения.
- Пикеры — только `Microsoft.Windows.Storage.Pickers` (на WindowId); legacy-пикеры
  с FileTypeFilter падают с E_FAIL (WinAppSDK 2.4).
- Новые файлы/подпапки в `_python` требуют явной маски `<Content>` в csproj.
- Выравнивание форм: переносить атрибуты между вкладками только сверкой с эталоном.
