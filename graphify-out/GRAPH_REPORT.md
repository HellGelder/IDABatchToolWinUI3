# Graph Report - IDABatchToolWinUI3  (2026-11-13)

## Corpus Check
- 103 files · ~144,393 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 9 file(s) not represented in the graph (top: (none) 3, .props 1, .appxmanifest 1)

## Summary
- 1795 nodes · 3573 edges · 84 communities (77 shown, 3 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 112 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `a5b759d5`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Enrich
- SfaPage
- IdaRunner
- SettingsPage
- MonitorEntry
- .FindExecutables
- DirectoriesDiffService
- export_data.py
- AppConfig
- MainWindow
- system_modules.py
- ArtifactValidatorTests
- Page
- report_bridge.py
- .StartAnalysisAsync
- JobObject
- AppLog
- AnalysisPage
- Page
- DocSearchManager
- ProcessPulse
- Page
- DiffWorker
- .StartComparisonAsync
- Window
- .ComputeSimilarity
- McpDocClient
- DiffProcessRunner
- platform_classifier.py
- Page
- .MergeIntoJson
- vipnet_ossl_whitelist.py
- manpages_taxonomy.py
- README.md
- .ReadJson
- PairRowViewModel
- .GitStartAsync
- generate_analysis_report
- manpages_build.py
- FileItem
- IDABatchToolWinUI.Services
- DiffPage
- .GenerateModuleReportsAsync
- .RunPass
- RoutedEventArgs
- collect_imports
- IDABatchToolWinUI.csproj
- .WriteJson
- OutcomeTracker
- AnalysisIndexLayoutTest
- _DocCache
- ThemeHelper
- Window
- WhitelistApp
- .ConvertElement
- _SfaLog
- .ProcessPostPairAsync
- AnalysisWorker
- Path
- VersionCheckService
- Task
- ImportScan
- ThreadCardViewModel
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- IDABatchToolWinUI.Tests.csproj
- .ParseLatestVersion
- .ExportAsync
- VersionCheckState
- LibraryMatchingTest
- .RunAsync
- generate_analysis_index
- ProcessRunnerTests
- VersionConsistencyTests
- .RunScriptOnBaseAsync
- IDisposable
- App
- .OnAppWindowClosing
- .Create
- ProcessResult
- .SafeFireAndForget

## God Nodes (most connected - your core abstractions)
1. `DiffPage` - 62 edges
2. `IDABatchToolWinUI.Services` - 54 edges
3. `Page` - 43 edges
4. `SfaPage` - 42 edges
5. `AnalysisPage` - 41 edges
6. `run_generate()` - 39 edges
7. `DiffWorker` - 36 edges
8. `DirectoriesDiffService` - 32 edges
9. `HtmlGenWorker` - 28 edges
10. `SettingsPage` - 25 edges

## Surprising Connections (you probably didn't know these)
- `App` --inherits--> `Application`  [EXTRACTED]
  App.xaml.cs → App.xaml
- `App` --references--> `IConfigStore`  [EXTRACTED]
  App.xaml.cs → Services/ConfigStore.cs
- `DiffWorker` --references--> `AddAnalysisRunner`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/AddAnalysisRunner.cs
- `DiffWorker` --references--> `BinDiffService`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/BinDiffService.cs
- `DiffWorker` --references--> `DiaphoraService`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/DiaphoraService.cs

## Import Cycles
- None detected.

## Communities (84 total, 3 thin omitted)

### Community 0 - ".Enrich"
Cohesion: 0.19
Nodes (11): Action, Dictionary, HashSet, IEnumerable, List, Tag, DiffJsonEnricher, I1 (+3 more)

### Community 1 - "SfaPage"
Cohesion: 0.05
Nodes (40): Msg, Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag, Task (+32 more)

### Community 2 - "IdaRunner"
Cohesion: 0.12
Nodes (12): Action, DateTime, IEnumerable, IdaRunner, ErrorCallback, FileDoneCallback, FileStartCallback, IdatPath (+4 more)

### Community 3 - "SettingsPage"
Cohesion: 0.06
Nodes (30): Code, ContentDialogResult, LogPath, Action, Func, IEnumerable, RoutedEventArgs, Task (+22 more)

### Community 4 - "MonitorEntry"
Cohesion: 0.06
Nodes (41): Brush, DateTime, IEnumerable, IReadOnlyList, List, AnalysisMonitor, IsSessionActive, MaxWorkers (+33 more)

### Community 5 - ".FindExecutables"
Cohesion: 0.11
Nodes (13): IReadOnlyDictionary, PlatformDef, IEnumerable, PlatformDef, PlatformInfo, Keys, IEnumerable, List (+5 more)

### Community 6 - "DirectoriesDiffService"
Cohesion: 0.08
Nodes (26): A, B, chunk, Kind, excluded, files, FileStream, isBinary (+18 more)

### Community 7 - "export_data.py"
Cohesion: 0.07
Nodes (43): Any, _compute_file_hashes(), _decompile_function(), _elf_flag_names(), _elf_meta_template(), export_to_json(), callback(), _extract_framework_name() (+35 more)

### Community 8 - "AppConfig"
Cohesion: 0.07
Nodes (19): Dictionary, AppConfig, BindiffExecutable, DefaultInputDir, IdaExecutable, LogLevel, MaxIda, Theme (+11 more)

### Community 9 - "MainWindow"
Cohesion: 0.14
Nodes (11): AnalysisPage, DiffPage, Dictionary, Task, MainWindow, NavigationView, NavigationViewSelectionChangedEventArgs, SfaPage (+3 more)

### Community 10 - "system_modules.py"
Cohesion: 0.05
Nodes (45): _build_category_key_map(), get_module_category(), get_module_category_and_description(), _manpages_lib_dict(), Логика классификации и группировка модулей по категориям. Linux / Android…, Возвращает категорию и описание категории (используется в отчётах). Поиск по…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся… (+37 more)

### Community 11 - "ArtifactValidatorTests"
Cohesion: 0.20
Nodes (3): ArtifactValidator, Fact, ArtifactValidatorTests

### Community 12 - "Page"
Cohesion: 0.06
Nodes (52): BindiffInfoButton, BindiffTip, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, CurrentFileLabel, DiffErrorTextBox (+44 more)

### Community 13 - "report_bridge.py"
Cohesion: 0.06
Nodes (64): _back_link(), _build_internal_set(), build_sfa_index(), _cancel_requested(), _collect_export_jsons(), emit_error(), emit_progress(), emit_result() (+56 more)

### Community 14 - ".StartAnalysisAsync"
Cohesion: 0.25
Nodes (3): Action, Func, Task

### Community 15 - "JobObject"
Cohesion: 0.25
Nodes (6): DllImport, IntPtr, Process, IO_COUNTERS, JobObject, Current

### Community 16 - "AppLog"
Cohesion: 0.20
Nodes (6): Exception, IEnumerable, IReadOnlyList, List, AppLog, LogDirectories

### Community 17 - "AnalysisPage"
Cohesion: 0.23
Nodes (4): DispatcherQueueTimer, List, AnalysisPage, IsBusy

### Community 18 - "Page"
Cohesion: 0.09
Nodes (29): BrowseDirButton, CancelButton, CleanupCheck, DeleteJsonCheck, DetailsButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox (+21 more)

### Community 19 - "DocSearchManager"
Cohesion: 0.08
Nodes (20): _decode_bytes(), DocSearchManager, _find_npx(), _get_mcp_client(), _kill_process_tree(), _normalize_func_name(), _parse_npx_results(), Общий клиент MCP. Создаётся один раз: пул поиска состоит из нескольких потоков,… (+12 more)

### Community 20 - "ProcessPulse"
Cohesion: 0.13
Nodes (15): Bytes, IoCounters, Mtime, CancellationToken, CancellationTokenSource, DateTime, DllImport, IntPtr (+7 more)

### Community 21 - "Page"
Cohesion: 0.10
Nodes (25): Page, SfaBrowseDirButton, SfaCancelButton, SfaCleanupCheck, SfaDeleteJsonCheck, SfaErrorLogTextBox, SfaGenerateHtmlButton, SfaInputDirTextBox (+17 more)

### Community 22 - "DiffWorker"
Cohesion: 0.11
Nodes (18): IdenticalPairsService, CancellationToken, CancellationTokenSource, IReadOnlyList, Task, DiffWorker, AddOutputDir, BindiffPath (+10 more)

### Community 23 - ".StartComparisonAsync"
Cohesion: 0.22
Nodes (5): DirScanResult, Dictionary, IEnumerable, List, DirScanResult

### Community 24 - "Window"
Cohesion: 0.24
Nodes (9): RowsList, SessionText, SummaryText, ThreadCards, ThreadsPanel, Window, ItemsControl, StackPanel (+1 more)

### Community 25 - ".ComputeSimilarity"
Cohesion: 0.24
Nodes (6): Reason, HexdumpDiffService, Similarity, Skipped, Fact, HexdumpDiffServiceTests

### Community 26 - "McpDocClient"
Cohesion: 0.11
Nodes (17): _mcp_load_cached_session(), _mcp_save_cached_session(), _mcp_session_cache_path(), _McpConnection, McpDocClient, Клиент Microsoft Learn MCP: постоянное соединение, запрос на функцию. Замена…, POST к MCP по keep-alive соединению текущего потока. TLS-рукопожатие на каждый…, (session_id, tool) из кэша learn-cli - один раз на клиент. (+9 more)

### Community 27 - "DiffProcessRunner"
Cohesion: 0.11
Nodes (19): Action, CancellationToken, Process, ProcessStartInfo, Regex, Task, TimeSpan, DiffProcessRunner (+11 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.10
Nodes (14): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, Классификатор импортированных модулей с детальными описаниями., linux_soname_descriptions(), soname и короткая форма → описание библиотеки (анализ ПО). Ключи - glibc- и…, BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier (+6 more)

### Community 29 - "Page"
Cohesion: 0.13
Nodes (23): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, CheckManPagesButton, CheckUtilsButton, IdatPathTextBox (+15 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - "vipnet_ossl_whitelist.py"
Cohesion: 0.15
Nodes (20): _details(), _esc(), main(), _module_table(), _parse_whitelist(), _pe_exports(), ViPNet OSSL — белый перечень по таблицам импорта. Утилита пакета IDA Batch…, Формирует HTML-отчёт. (+12 more)

### Community 32 - "manpages_taxonomy.py"
Cohesion: 0.09
Nodes (29): category_name_ru(), is_syscall_section(), lang_rank(), Таксономия man-pages - единственная таблица соответствия для классификации…, Русское название категории man-pages для интерфейса. Категории man-pages…, Порядок секции man для выбора страницы (неизвестные - в конец)., Порядок языка: русская страница предпочитается английской., Базовая секция man: «2const»/«2type» → «2», «3type» → «3». (+21 more)

### Community 33 - "README.md"
Cohesion: 0.08
Nodes (25): Git-сравнение (исходники), HTML‑отчёты, IDA Batch Tool - WinUI 3 (исполнение 2), Python‑часть (мосты и скрипты), Release‑публикация и переносимая поставка, Анализ СФ (системных функций), Архитектура, Быстрый старт (из исходников) (+17 more)

### Community 34 - ".ReadJson"
Cohesion: 0.30
Nodes (8): Action, CancellationToken, List, Task, AddAnalysisRunner, StageFile, DiffPair, Stem

### Community 35 - "PairRowViewModel"
Cohesion: 0.14
Nodes (11): PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair, PidText (+3 more)

### Community 36 - ".GitStartAsync"
Cohesion: 0.14
Nodes (6): DependencyObject, CancellationToken, CancellationTokenSource, ScrollViewer, TextBox, UiLog

### Community 37 - "generate_analysis_report"
Cohesion: 0.08
Nodes (27): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _api_to_release() (+19 more)

### Community 38 - "manpages_build.py"
Cohesion: 0.17
Nodes (15): _collect_pages(), _library_tokens(), main(), render_one(), _names_and_summary(), Сборка встроенной БД man-pages для исполнения 2 (IDABatchToolWinUI). Собирает…, Короткие имена библиотек из секции LIBRARY/БИБЛИОТЕКА. Токены в скобках…, Рендер roff → HTML через pandoc (ман-ридер, stdin/stdout). (+7 more)

### Community 39 - "FileItem"
Cohesion: 0.05
Nodes (32): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, FileItem (+24 more)

### Community 40 - "IDABatchToolWinUI.Services"
Cohesion: 0.06
Nodes (19): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Controls, IDABatchToolWinUI.Tests, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff (+11 more)

### Community 41 - "DiffPage"
Cohesion: 0.13
Nodes (9): Page, HeaderCheckBox, Cur, DispatcherQueueTimer, Queue, RoutedEventArgs, DiffPage, IsBusy (+1 more)

### Community 42 - ".GenerateModuleReportsAsync"
Cohesion: 0.36
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".RunPass"
Cohesion: 0.22
Nodes (7): Action, CancellationToken, Func, List, Task, DiffPassRunner, Outcomes

### Community 44 - "RoutedEventArgs"
Cohesion: 0.20
Nodes (5): PlatformInfoBar, ThreadsWarningBar, RoutedEventArgs, TaskManagerWindow, InfoBar

### Community 45 - "collect_imports"
Cohesion: 0.12
Nodes (15): collect_imports(), run_one(), _db_files_for(), _imports_via_ida(), _iter_units(), normalize_symbol(), Приводит импортируемое имя к виду словарей. Снимает ``__imp_`` (MSVC), версию…, Обработанная база (или осиротевший export.json). (+7 more)

### Community 46 - "IDABatchToolWinUI.csproj"
Cohesion: 0.29
Nodes (6): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0)

### Community 47 - ".WriteJson"
Cohesion: 0.20
Nodes (9): Dictionary, HashSet, SqliteConnection, DiffJson, Dictionary, Fact, List, SqliteConnection (+1 more)

### Community 48 - "OutcomeTracker"
Cohesion: 0.29
Nodes (6): ConcurrentDictionary, OutcomeTracker, Failed, Succeeded, Fact, OutcomeTrackerTests

### Community 49 - "AnalysisIndexLayoutTest"
Cohesion: 0.16
Nodes (4): AnalysisIndexLayoutTest, ModuleStatusTagsTest, Тесты компоновки сводного индекса анализа ПО (решения владельца 2026-11-10).…, Правило колонки «Тег»: системность и принадлежность папке.

### Community 51 - "ThemeHelper"
Cohesion: 0.23
Nodes (7): ElementTheme, List, Window, ThemeHelper, CurrentElementTheme, CurrentTheme, WeakReference

### Community 52 - "Window"
Cohesion: 0.16
Nodes (14): AppTitleBar, NavAnalysis, NavDiff, NavFrame, NavSettings, NavSettingsBadge, NavSfa, NavView (+6 more)

### Community 53 - "WhitelistApp"
Cohesion: 0.24
Nodes (4): find_idat(), Ищет idat64.exe/idat.exe: параметр, config.yaml, env, Program Files, PATH., Окно настройки и запуска анализа импортов., WhitelistApp

### Community 54 - ".ConvertElement"
Cohesion: 0.29
Nodes (4): Dictionary, Task, JsonDictExtensions, JsonElement

### Community 55 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов. Базы старой…, Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".ProcessPostPairAsync"
Cohesion: 0.22
Nodes (8): Action, CancellationToken, Task, BinDiffService, Action, CancellationToken, Task, DiffPairProcessor

### Community 57 - "AnalysisWorker"
Cohesion: 0.33
Nodes (4): CancellationTokenSource, IReadOnlyList, Task, AnalysisWorker

### Community 58 - "Path"
Cohesion: 0.18
Nodes (8): Path, LinuxReportsTest, Системность функций и теги модулей на синтетическом экспорте., Синтетический ELF-экспорт: системные, библиотечные и сырые вызовы., _write_export(), main(), _open_tar(), Распаковка man-страниц из архива корпуса в плоский каталог man*/. Поддержка:…

### Community 61 - "VersionCheckService"
Cohesion: 0.20
Nodes (8): AssemblyInformationalVersionAttribute, CancellationToken, Regex, Task, TimeSpan, VersionCheckService, CurrentVersion, Version

### Community 62 - "Task"
Cohesion: 0.21
Nodes (4): Action, Func, IReadOnlyList, Task

### Community 63 - "ImportScan"
Cohesion: 0.24
Nodes (4): ImportedSymbol, ImportScan, Функция, импортируемая хотя бы одной базой., (модуль, импортов, белых, вне перечня, прочих) по убыванию импортов.

### Community 64 - "ThreadCardViewModel"
Cohesion: 0.18
Nodes (6): INotifyPropertyChanged, ThreadCardViewModel, ModuleText, ProgressText, StateText, Title

### Community 65 - "AGENTS.md — контекст проекта для ИИ-агентов (ZCode)"
Cohesion: 0.29
Nodes (6): AGENTS.md — контекст проекта для ИИ-агентов (ZCode), Грабли (проверено на этом коде), Зоны MOTW / SmartScreen (правило сборки поставки), О проекте, Сборка и запуск, Стандарты

### Community 66 - "IDABatchToolWinUI.Tests.csproj"
Cohesion: 0.40
Nodes (4): xunit.v3 (4.0.2), net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk

### Community 67 - ".ParseLatestVersion"
Cohesion: 0.27
Nodes (4): Fact, InlineData, Theory, VersionCheckServiceTests

### Community 68 - ".ExportAsync"
Cohesion: 0.31
Nodes (6): Action, CancellationToken, Reason, Task, DiaphoraService, Ok

### Community 69 - "VersionCheckState"
Cohesion: 0.27
Nodes (4): VersionCheckResult, VersionCheckState, NoticeRead, Result

### Community 71 - ".RunAsync"
Cohesion: 0.33
Nodes (6): Action, CancellationToken, Process, ProcessStartInfo, Task, ProcessRunner

### Community 72 - "generate_analysis_index"
Cohesion: 0.09
Nodes (26): get_category_description(), Словесное описание категории (пусто для неизвестной категории)., module_library_key(), Короткое имя библиотеки man-pages для модуля ('' - определить нельзя)., _category_description(), _classify_module(), _collect_own_system_libs(), generate_analysis_index() (+18 more)

### Community 75 - "ProcessRunnerTests"
Cohesion: 0.49
Nodes (4): Fact, ProcessStartInfo, Task, ProcessRunnerTests

### Community 77 - ".RunScriptOnBaseAsync"
Cohesion: 0.56
Nodes (4): CancellationToken, Dictionary, IReadOnlyList, Task

### Community 78 - "IDisposable"
Cohesion: 0.32
Nodes (3): IDisposable, Fact, AppLogTests

### Community 79 - "App"
Cohesion: 0.29
Nodes (5): Application, Window, App, ConfigStore, LaunchActivatedEventArgs

### Community 80 - ".OnAppWindowClosing"
Cohesion: 0.29
Nodes (4): AppWindow, AppWindowClosingEventArgs, Task, TimeSpan

### Community 81 - ".Create"
Cohesion: 0.33
Nodes (6): IO_COUNTERS, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, UIntPtr

### Community 82 - "ProcessResult"
Cohesion: 0.40
Nodes (3): TimeSpan, ProcessResult, Success

### Community 83 - ".SafeFireAndForget"
Cohesion: 0.33
Nodes (4): Action, Exception, Task, TaskExtensions

## Knowledge Gaps
- **188 isolated node(s):** `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile`, `MatchedFunctions`, `Unmatched1` (+183 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 605 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Path` connect `Path` to `manpages_build.py`, `FileItem`, `collect_imports`, `WhitelistApp`, `vipnet_ossl_whitelist.py`?**
  _High betweenness centrality (0.374) - this node is a cross-community bridge._
- **Why does `FileItem` connect `FileItem` to `IDABatchToolWinUI.Services`, `AnalysisPage`, `Path`, `SfaPage`?**
  _High betweenness centrality (0.361) - this node is a cross-community bridge._
- **Why does `LinuxReportsTest` connect `Path` to `system_modules.py`?**
  _High betweenness centrality (0.223) - this node is a cross-community bridge._
- **What connects `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile` to the rest of the system?**
  _188 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.051189400782896716 - nodes in this community are weakly interconnected._
- **Should `IdaRunner` be split into smaller, more focused modules?**
  _Cohesion score 0.11764705882352941 - nodes in this community are weakly interconnected._
- **Should `SettingsPage` be split into smaller, more focused modules?**
  _Cohesion score 0.0554954954954955 - nodes in this community are weakly interconnected._