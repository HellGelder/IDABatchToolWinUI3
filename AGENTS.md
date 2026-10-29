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
- Тесты (xUnit v3, MTP): из `tests\IDABatchToolWinUI.Tests` — `dotnet run` (или exe
  из bin). `dotnet test` тоже включён (TestingPlatformDotnetTestSupport), но в среде
  агента ZCode падает: NamedPipeClientStream до MSBuild-узла получает
  UnauthorizedAccessException — там запускать только через `dotnet run`.
- Релиз: поднять версию в `Directory.Build.props` (единый источник, 4 поля) →
  синхронно `Version` в `Package.appxmanifest` и `version` в `app.manifest` →
  дописать `version.txt`. Расхождение ловит `make-portable.ps1` (assert) и
  тест `VersionConsistencyTests`. Перед скриптом закрыть запущенное приложение
  из поставки (лочит AppIcon.ico) и убедиться в отсутствии `idat.exe`.
  Поставка кладётся **вне репозитория**: `..\dist` (правило зон — ниже);
  архив: `..\dist\IDABatchToolWinUI-<версия>-<rid>.zip`.

## Зоны MOTW / SmartScreen (правило сборки поставки)

Окно «Не удаётся проверить издателя» при запуске неподписанного exe — это
проверка зоны Attachment Manager, а не код приложения. Причины и правила
(выяснено 2026-10-29, экспериментально; сломало 1.0.0.32):

- **Ничего не скачивать внутрь репозитория.** На текущей Windows (Dev Drive,
  скрытый MOTW-хранилище) файлы, скачанные `Invoke-WebRequest`/браузером в
  папку, помечают **саму папку** зоной Internet; всё создаваемое внутри
  (bin, dist, новые файлы) наследует зону. Метка невидима для
  `Get-Item -Stream`/`dir /R`, `Unblock-File` её не снимает.
- Кэши скачиваний — в `%LOCALAPPDATA%\IDABatchTool\...` (кэш корпусов
  man-pages: `%LOCALAPPDATA%\IDABatchTool\manpages-corpus`), и сразу после
  скачивания — `Unblock-File` (см. `scripts/build-manpages.ps1`).
- Поставка собирается только вне репозитория: `make-portable.ps1` кладёт
  результат в `..\dist`. В конце сборки скрипт проверяет зоны exe и zip
  (assert: `[System.Security.Policy.Zone]::CreateFromUrl` == MyComputer).
- Лончер поставки запускает приложение только через CreateProcess
  (`UseShellExecute = false`): ShellExecute вызывает проверку зоны даже без
  MOTW. Не возвращать `UseShellExecute = true`.
- В корень поставки кладётся `unblock-portable.ps1` — снятие MOTW рекурсивно
  после распаковки на целевой машине (один запуск, против окна SmartScreen
  от переноса через браузер/облако).
- Радикальное решение для «ноль окон» — подпись exe: самоподписанный
  сертификат CodeSigning уже создан (`CN=IDA Batch Tool`,
  `Cert:\CurrentUser\My`, отпечаток BF7D93DE00D5FF4854B069FF406EA421FDBA129C,
  экспорт `%TEMP%\idabatchtool.cer` может быть уже удалён — пересоздать
  `Export-Certificate`); доверие на целевой машине — импорт .cer в
  Root+TrustedPublisher один раз.
- Быстрая диагностика зоны файла/папки:
  `[System.Security.Policy.Zone]::CreateFromUrl("file:///путь").SecurityZone`
  (MyComputer=чисто, Internet=источник окна).
- Граф знаний: `make-portable.ps1` при полной сборке обновляет малый граф
  (`graphify-out/`: свой код без вендоренной diaphora; `-SkipGraph` — пропустить,
  отсутствие graphify — предупреждение). Полный граф с diaphora —
  только по запросу: `$env:GRAPHIFY_OUT='graphify-out-full'; graphify . --code-only;
  Remove-Item Env:GRAPHIFY_OUT`. В git — только `GRAPH_REPORT.md` и `manifest.json`.

## Стандарты

- C#: nullable reference types, file-scoped namespaces, async/await +
  CancellationToken, DI.
- Комментарии и общение — на русском; код, имена, коммиты — на английском.
- Секреты — только в `.env`/переменных окружения, не в коде.

## Грабли (проверено на этом коде)

- ps1 с кириллицей — сохранять в UTF-8 **с BOM**: PowerShell 5.1 без BOM читает
  файл как ANSI, парсинг ломается на mojibake (в repo все ps1 с BOM).
- Страницы кэшируются: подписки — в конструкторе; `x:Bind` без `Mode=OneWay`
  не обновляет значения.
- Пикеры — только `Microsoft.Windows.Storage.Pickers` (на WindowId); legacy-пикеры
  с FileTypeFilter падают с E_FAIL (WinAppSDK 2.4).
- Новые файлы/подпапки в `_python` требуют явной маски `<Content>` в csproj.
- Выравнивание форм: переносить атрибуты между вкладками только сверкой с эталоном.
