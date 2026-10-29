# Graph Report - IDABatchToolWinUI3  (2026-10-29)

## Corpus Check
- 96 files · ~118,946 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 7 file(s) not represented in the graph (top: (none) 3, .props 1, .appxmanifest 1)

## Summary
- 1617 nodes · 3275 edges · 67 communities (62 shown, 3 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 115 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `185307fb`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Enrich
- SfaPage
- IdaRunner
- SettingsPage
- TaskManagerWindow
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
- .RunAsync
- Page
- DiffWorker
- Task
- Window
- .ComputeSimilarity
- McpDocClient
- DiffProcessRunner
- platform_classifier.py
- Page
- .MergeIntoJson
- .WarnAsync
- generate_analysis_index
- README.md
- .ReadJson
- PairRowViewModel
- .GitStartAsync
- describe_section
- manpages_bridge.py
- FileItem
- .BeginSession
- DiffPage
- .RunModuleComparisonAsync
- .RunPass
- RoutedEventArgs
- .GitGenerateReportAsync
- IDABatchToolWinUI.csproj
- .WriteJson
- AnalysisWorker
- _is_sfa_module
- _DocCache
- IDABatchToolWinUI.Services
- _file_info_entries
- DiffJson
- _SfaLog
- .ProcessPostPairAsync
- MonitorEntry
- VersionConsistencyTests
- .EnsureEnvironmentAsync
- AnalysisMonitor
- ThreadCardViewModel
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- IDABatchToolWinUI.Tests.csproj
- ThemeHelper
- TaskThreadCard

## God Nodes (most connected - your core abstractions)
1. `DiffPage` - 62 edges
2. `IDABatchToolWinUI.Services` - 52 edges
3. `Page` - 43 edges
4. `SfaPage` - 42 edges
5. `AnalysisPage` - 41 edges
6. `run_generate()` - 40 edges
7. `DiffWorker` - 36 edges
8. `DirectoriesDiffService` - 32 edges
9. `HtmlGenWorker` - 28 edges
10. `SettingsPage` - 27 edges

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

## Communities (67 total, 3 thin omitted)

### Community 0 - ".Enrich"
Cohesion: 0.20
Nodes (11): Action, Dictionary, HashSet, IEnumerable, List, Tag, DiffJsonEnricher, I1 (+3 more)

### Community 1 - "SfaPage"
Cohesion: 0.05
Nodes (38): Msg, Page, Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag (+30 more)

### Community 2 - "IdaRunner"
Cohesion: 0.09
Nodes (20): IReadOnlyList, ArtifactCleanup, Action, CancellationToken, DateTime, Dictionary, IEnumerable, IReadOnlyList (+12 more)

### Community 3 - "SettingsPage"
Cohesion: 0.08
Nodes (18): Action, Func, IEnumerable, RoutedEventArgs, Task, SettingsPage, Count, ContentDialog (+10 more)

### Community 4 - "TaskManagerWindow"
Cohesion: 0.14
Nodes (10): MonitorFileStatus, Cancelled, Done, Failed, Pending, Running, DispatcherQueueTimer, IReadOnlyList (+2 more)

### Community 5 - ".FindExecutables"
Cohesion: 0.12
Nodes (13): InlineData, IReadOnlyDictionary, PlatformDef, IEnumerable, PlatformDef, PlatformInfo, Keys, IEnumerable (+5 more)

### Community 6 - "DirectoriesDiffService"
Cohesion: 0.08
Nodes (26): A, B, chunk, Kind, excluded, files, FileStream, isBinary (+18 more)

### Community 7 - "export_data.py"
Cohesion: 0.07
Nodes (43): Any, _compute_file_hashes(), _decompile_function(), _elf_flag_names(), _elf_meta_template(), export_to_json(), callback(), _extract_framework_name() (+35 more)

### Community 8 - "AppConfig"
Cohesion: 0.07
Nodes (20): Dictionary, AppConfig, BindiffExecutable, DefaultInputDir, IdaExecutable, LogLevel, ManPagesDbPath, MaxIda (+12 more)

### Community 9 - "MainWindow"
Cohesion: 0.06
Nodes (30): AnalysisPage, AppWindow, AppWindowClosingEventArgs, DiffPage, AppTitleBar, NavAnalysis, NavDiff, NavFrame (+22 more)

### Community 10 - "system_modules.py"
Cohesion: 0.08
Nodes (36): _build_category_key_map(), get_module_category(), get_module_category_and_description(), Возвращает категорию и описание категории (используется в отчётах). Поиск по…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся…, module_name_aliases(), normalize_module_name() (+28 more)

### Community 11 - "ArtifactValidatorTests"
Cohesion: 0.20
Nodes (3): ArtifactValidator, Fact, ArtifactValidatorTests

### Community 12 - "Page"
Cohesion: 0.06
Nodes (53): BindiffInfoButton, BindiffTip, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, CurrentFileLabel, DiffErrorTextBox (+45 more)

### Community 13 - "report_bridge.py"
Cohesion: 0.06
Nodes (64): _back_link(), _build_internal_set(), _cancel_requested(), _collect_export_jsons(), emit_error(), emit_progress(), emit_result(), _extract_dll() (+56 more)

### Community 14 - ".StartAnalysisAsync"
Cohesion: 0.25
Nodes (3): Action, Func, Task

### Community 15 - "JobObject"
Cohesion: 0.11
Nodes (17): Application, Window, App, ConfigStore, IO_COUNTERS, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, LaunchActivatedEventArgs (+9 more)

### Community 16 - "AppLog"
Cohesion: 0.10
Nodes (13): IDisposable, Exception, IEnumerable, IReadOnlyList, List, AppLog, LogDirectories, Action (+5 more)

### Community 17 - "AnalysisPage"
Cohesion: 0.23
Nodes (4): DispatcherQueueTimer, List, AnalysisPage, IsBusy

### Community 18 - "Page"
Cohesion: 0.09
Nodes (29): BrowseDirButton, CancelButton, CleanupCheck, DeleteJsonCheck, DetailsButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox (+21 more)

### Community 19 - "DocSearchManager"
Cohesion: 0.08
Nodes (20): _decode_bytes(), DocSearchManager, _find_npx(), _get_mcp_client(), _kill_process_tree(), _normalize_func_name(), _parse_npx_results(), Общий клиент MCP. Создаётся один раз: пул поиска состоит из нескольких потоков,… (+12 more)

### Community 20 - ".RunAsync"
Cohesion: 0.07
Nodes (28): Bytes, IoCounters, Mtime, CancellationToken, CancellationTokenSource, DateTime, DllImport, IntPtr (+20 more)

### Community 21 - "Page"
Cohesion: 0.10
Nodes (25): Page, SfaBrowseDirButton, SfaCancelButton, SfaCleanupCheck, SfaDeleteJsonCheck, SfaErrorLogTextBox, SfaGenerateHtmlButton, SfaInputDirTextBox (+17 more)

### Community 22 - "DiffWorker"
Cohesion: 0.10
Nodes (18): IdenticalPairsService, CancellationToken, CancellationTokenSource, IReadOnlyList, Task, DiffWorker, AddOutputDir, BindiffPath (+10 more)

### Community 23 - "Task"
Cohesion: 0.22
Nodes (4): Action, Func, IReadOnlyList, Task

### Community 24 - "Window"
Cohesion: 0.24
Nodes (9): RowsList, SessionText, SummaryText, ThreadCards, ThreadsPanel, Window, ItemsControl, StackPanel (+1 more)

### Community 25 - ".ComputeSimilarity"
Cohesion: 0.24
Nodes (6): Reason, HexdumpDiffService, Similarity, Skipped, Fact, HexdumpDiffServiceTests

### Community 26 - "McpDocClient"
Cohesion: 0.11
Nodes (17): _mcp_load_cached_session(), _mcp_save_cached_session(), _mcp_session_cache_path(), _McpConnection, McpDocClient, Уникальный id JSON-RPC-запроса внутри клиента (потокобезопасно)., Полный handshake и повтор поиска (кэш сессии протух). Рукопожатие…, Достаёт result из SSE-ответа (``data: {json}``). (+9 more)

### Community 27 - "DiffProcessRunner"
Cohesion: 0.15
Nodes (14): Action, CancellationToken, Process, ProcessStartInfo, Regex, Task, TimeSpan, DiffProcessRunner (+6 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.09
Nodes (15): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, Логика классификации и группировка модулей по категориям., Классификатор импортированных модулей с детальными описаниями., Словари для Linux-модулей. Правила поддержки словарей: 1. Каждый модуль…, Словари для macOS / iOS модулей. Правила поддержки словарей: 1. Каждый модуль…, BasePlatformClassifier, classify_module(), CompositeClassifier (+7 more)

### Community 29 - "Page"
Cohesion: 0.13
Nodes (24): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, BrowseManPagesButton, CheckManPagesButton, CheckUtilsButton (+16 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - ".WarnAsync"
Cohesion: 0.20
Nodes (9): ContentDialogResult, ContentDialog, IReadOnlyList, Task, TextBox, Window, UiDialogs, MainWindow (+1 more)

### Community 32 - "generate_analysis_index"
Cohesion: 0.12
Nodes (16): get_category_description(), Словесное описание категории (пусто для неизвестной категории)., _category_description(), _classify_full(), _classify_module(), _classify_with_context(), generate_analysis_index(), _is_internal_module() (+8 more)

### Community 33 - "README.md"
Cohesion: 0.08
Nodes (25): Git-сравнение (исходники), HTML‑отчёты, IDA Batch Tool - WinUI 3 (исполнение 2), Python‑часть (мосты и скрипты), Release‑публикация и переносимая поставка, Анализ СФ (системных функций), Архитектура, Быстрый старт (из исходников) (+17 more)

### Community 34 - ".ReadJson"
Cohesion: 0.30
Nodes (8): Action, CancellationToken, List, Task, AddAnalysisRunner, StageFile, DiffPair, Stem

### Community 35 - "PairRowViewModel"
Cohesion: 0.15
Nodes (11): PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair, PidText (+3 more)

### Community 36 - ".GitStartAsync"
Cohesion: 0.18
Nodes (5): DependencyObject, CancellationTokenSource, ScrollViewer, TextBox, UiLog

### Community 37 - "describe_section"
Cohesion: 0.16
Nodes (13): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _elf_sections() (+5 more)

### Community 38 - "manpages_bridge.py"
Cohesion: 0.18
Nodes (16): done(), err(), _extract_sections(), import_archive(), main(), on_progress(), _parse_roff_text(), prog() (+8 more)

### Community 39 - "FileItem"
Cohesion: 0.05
Nodes (33): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, FileItem (+25 more)

### Community 40 - ".BeginSession"
Cohesion: 0.35
Nodes (4): IEnumerable, IReadOnlyList, Fact, MonitorTests

### Community 41 - "DiffPage"
Cohesion: 0.14
Nodes (11): DirScanResult, Cur, Dictionary, DispatcherQueueTimer, IEnumerable, List, Queue, DiffPage (+3 more)

### Community 42 - ".RunModuleComparisonAsync"
Cohesion: 0.30
Nodes (5): CancellationToken, IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".RunPass"
Cohesion: 0.33
Nodes (4): CancellationToken, Func, List, Task

### Community 44 - "RoutedEventArgs"
Cohesion: 0.20
Nodes (5): PlatformInfoBar, ThreadsWarningBar, RoutedEventArgs, TaskManagerWindow, InfoBar

### Community 46 - "IDABatchToolWinUI.csproj"
Cohesion: 0.29
Nodes (6): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0)

### Community 47 - ".WriteJson"
Cohesion: 0.28
Nodes (6): Dictionary, Dictionary, Fact, List, SqliteConnection, DiffParserTests

### Community 48 - "AnalysisWorker"
Cohesion: 0.33
Nodes (4): CancellationTokenSource, IReadOnlyList, Task, AnalysisWorker

### Community 49 - "_is_sfa_module"
Cohesion: 0.20
Nodes (10): build_sfa_index(), _collect_own_system_libs(), _is_sfa_module(), _normalize_module_key(), Модуль входит в отчёты СФ: категория «Системные библиотеки ОС». Отчёты СФ…, Размер файла в байтах; 0, если файл исчез или недоступен. exists() + stat() в…, Канонический ключ модуля - как в исполнении 1…, Строит index-БД системных функций из JSON-файлов (аналог… (+2 more)

### Community 52 - "IDABatchToolWinUI.Services"
Cohesion: 0.06
Nodes (19): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Controls, IDABatchToolWinUI.Tests, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff (+11 more)

### Community 53 - "_file_info_entries"
Cohesion: 0.50
Nodes (4): _api_to_release(), _file_info_entries(), Сопоставляет уровень Android API кодовому имени релиза (как в исполнении 1)., Карточка «Информация о файле» (аналог ELFReportGenerator._build_file_info…

### Community 54 - "DiffJson"
Cohesion: 0.18
Nodes (7): Dictionary, HashSet, SqliteConnection, Task, DiffJson, JsonDictExtensions, JsonElement

### Community 55 - "_SfaLog"
Cohesion: 0.25
Nodes (6): _manpages_autosync(), _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов., Скачивает архив man-pages и импортирует его в ``db_path``. Для Linux/Android…, Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".ProcessPostPairAsync"
Cohesion: 0.11
Nodes (19): Action, CancellationToken, Task, BinDiffService, Action, CancellationToken, Reason, Task (+11 more)

### Community 57 - "MonitorEntry"
Cohesion: 0.13
Nodes (14): DateTime, MonitorEntry, CpuTime, EndedAt, FileName, Key, MemoryMb, OsThreads (+6 more)

### Community 62 - ".EnsureEnvironmentAsync"
Cohesion: 0.15
Nodes (11): Code, LogPath, DispatcherQueue, Func, Task, PythonEnvironmentUi, IReadOnlyList, Task (+3 more)

### Community 63 - "AnalysisMonitor"
Cohesion: 0.28
Nodes (4): List, AnalysisMonitor, IsSessionActive, MaxWorkers

### Community 64 - "ThreadCardViewModel"
Cohesion: 0.18
Nodes (6): INotifyPropertyChanged, ThreadCardViewModel, ModuleText, ProgressText, StateText, Title

### Community 65 - "AGENTS.md — контекст проекта для ИИ-агентов (ZCode)"
Cohesion: 0.33
Nodes (5): AGENTS.md — контекст проекта для ИИ-агентов (ZCode), Грабли (проверено на этом коде), О проекте, Сборка и запуск, Стандарты

### Community 66 - "IDABatchToolWinUI.Tests.csproj"
Cohesion: 0.40
Nodes (4): xunit.v3 (4.0.2), net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk

### Community 68 - "ThemeHelper"
Cohesion: 0.23
Nodes (7): ElementTheme, List, Window, ThemeHelper, CurrentElementTheme, CurrentTheme, WeakReference

### Community 70 - "TaskThreadCard"
Cohesion: 0.22
Nodes (9): Brush, TaskThreadCard, BusyVisibility, CardBorder, FileName, FreeVisibility, IsBusy, Title (+1 more)

## Knowledge Gaps
- **184 isolated node(s):** `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile`, `MatchedFunctions`, `Unmatched1` (+179 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 523 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IDABatchToolWinUI.Services` connect `IDABatchToolWinUI.Services` to `IdaRunner`, `SettingsPage`, `TaskManagerWindow`, `.FindExecutables`, `DirectoriesDiffService`, `FileItem`, `AppConfig`, `MainWindow`, `.RunModuleComparisonAsync`, `ArtifactValidatorTests`, `ThemeHelper`, `.GitStartAsync`, `JobObject`, `AppLog`, `.RunAsync`, `.EnsureEnvironmentAsync`, `.WarnAsync`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **Why does `DiffPage` connect `DiffPage` to `ThreadCardViewModel`, `SfaPage`, `.GitStartAsync`, `AppConfig`, `MainWindow`, `.RunModuleComparisonAsync`, `Page`, `.GitGenerateReportAsync`, `IDABatchToolWinUI.Services`, `DiffWorker`, `Task`?**
  _High betweenness centrality (0.125) - this node is a cross-community bridge._
- **Why does `DiffWorker` connect `DiffWorker` to `.ReadJson`, `DiffPage`, `AppLog`, `IDABatchToolWinUI.Services`, `Task`, `.ProcessPostPairAsync`, `DiffProcessRunner`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **What connects `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile` to the rest of the system?**
  _184 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.054612054612054615 - nodes in this community are weakly interconnected._
- **Should `IdaRunner` be split into smaller, more focused modules?**
  _Cohesion score 0.09024390243902439 - nodes in this community are weakly interconnected._
- **Should `SettingsPage` be split into smaller, more focused modules?**
  _Cohesion score 0.08078431372549019 - nodes in this community are weakly interconnected._