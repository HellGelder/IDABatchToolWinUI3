# Graph Report - IDABatchToolWinUI3  (2026-10-29)

## Corpus Check
- 95 files · ~115,695 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 7 file(s) not represented in the graph (top: (none) 3, .props 1, .appxmanifest 1)

## Summary
- 1576 nodes · 3174 edges · 77 communities (69 shown, 6 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 115 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `67eab671`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Enrich
- SfaPage
- IdaRunner
- SettingsPage
- TaskManagerWindow
- IdenticalPairsService
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
- DiffPage
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
- .StartComparisonAsync
- .GenerateModuleReportsAsync
- .RunPass
- OutcomeTracker
- .RunSafe
- IDABatchToolWinUI.csproj
- .WriteJson
- AnalysisWorker
- .ExtractArchiveAsync
- _DocCache
- .RunAsync
- IDABatchToolWinUI.Services
- generate_analysis_report
- DiffJson
- _SfaLog
- .ProcessPostPairAsync
- MonitorEntry
- PythonEnvironment
- VersionConsistencyTests
- .EnsureEnvironmentAsync
- AnalysisMonitor
- ThreadCardViewModel
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- IDABatchToolWinUI.Tests.csproj
- ProcessRunnerTests
- ThemeHelper
- ManPagesSyncWorker
- TaskThreadCard
- .ExportAsync
- BinDiffService
- inline_vendor
- .ExportPairAsync
- Task
- .ApplyToDialog

## God Nodes (most connected - your core abstractions)
1. `DiffPage` - 62 edges
2. `IDABatchToolWinUI.Services` - 51 edges
3. `Page` - 43 edges
4. `AnalysisPage` - 41 edges
5. `SfaPage` - 41 edges
6. `DiffWorker` - 36 edges
7. `run_generate()` - 36 edges
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

## Communities (77 total, 6 thin omitted)

### Community 0 - ".Enrich"
Cohesion: 0.20
Nodes (11): Action, Dictionary, HashSet, IEnumerable, List, Tag, DiffJsonEnricher, I1 (+3 more)

### Community 1 - "SfaPage"
Cohesion: 0.05
Nodes (39): Msg, Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag, Task (+31 more)

### Community 2 - "IdaRunner"
Cohesion: 0.13
Nodes (16): Action, CancellationToken, DateTime, Dictionary, IEnumerable, IReadOnlyList, Task, IdaRunner (+8 more)

### Community 3 - "SettingsPage"
Cohesion: 0.16
Nodes (9): Action, Func, IEnumerable, RoutedEventArgs, Task, SettingsPage, Count, SizeMb (+1 more)

### Community 4 - "TaskManagerWindow"
Cohesion: 0.14
Nodes (10): MonitorFileStatus, Cancelled, Done, Failed, Pending, Running, DispatcherQueueTimer, IReadOnlyList (+2 more)

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
Nodes (32): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, _build_category_key_map(), get_module_category(), get_module_category_and_description(), Логика классификации и группировка модулей по категориям., Возвращает категорию и описание категории (используется в отчётах). Поиск по…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся… (+24 more)

### Community 11 - "ArtifactValidatorTests"
Cohesion: 0.22
Nodes (3): ArtifactValidator, Fact, ArtifactValidatorTests

### Community 12 - "Page"
Cohesion: 0.06
Nodes (52): BindiffInfoButton, BindiffTip, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, CurrentFileLabel, DiffErrorTextBox (+44 more)

### Community 13 - "report_bridge.py"
Cohesion: 0.07
Nodes (51): done(), _build_internal_set(), build_sfa_index(), _cancel_requested(), _collect_export_jsons(), emit_error(), emit_progress(), emit_result() (+43 more)

### Community 14 - ".StartAnalysisAsync"
Cohesion: 0.23
Nodes (4): Action, Func, List, Task

### Community 15 - "JobObject"
Cohesion: 0.11
Nodes (17): Application, Window, App, ConfigStore, IO_COUNTERS, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, LaunchActivatedEventArgs (+9 more)

### Community 16 - "AppLog"
Cohesion: 0.11
Nodes (12): Exception, IEnumerable, IReadOnlyList, List, AppLog, LogDirectories, Action, Exception (+4 more)

### Community 17 - "AnalysisPage"
Cohesion: 0.18
Nodes (5): DispatcherQueueTimer, RoutedEventArgs, TaskManagerWindow, AnalysisPage, IsBusy

### Community 18 - "Page"
Cohesion: 0.08
Nodes (32): BrowseDirButton, CancelButton, CleanupCheck, DeleteJsonCheck, DetailsButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox (+24 more)

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
Cohesion: 0.12
Nodes (17): CancellationToken, CancellationTokenSource, IReadOnlyList, Task, DiffWorker, AddOutputDir, BindiffPath, DiaphoraPath (+9 more)

### Community 23 - "DiffPage"
Cohesion: 0.15
Nodes (7): Page, Cur, DispatcherQueueTimer, Queue, DiffPage, IsBusy, TextBlock

### Community 24 - "Window"
Cohesion: 0.24
Nodes (9): RowsList, SessionText, SummaryText, ThreadCards, ThreadsPanel, Window, ItemsControl, StackPanel (+1 more)

### Community 25 - ".ComputeSimilarity"
Cohesion: 0.24
Nodes (6): Reason, HexdumpDiffService, Similarity, Skipped, Fact, HexdumpDiffServiceTests

### Community 26 - "McpDocClient"
Cohesion: 0.11
Nodes (17): _mcp_load_cached_session(), _mcp_save_cached_session(), _mcp_session_cache_path(), _McpConnection, McpDocClient, HTTPS к learn.microsoft.com с коротким таймаутом установки соединения.…, Путь к кэшу сессии learn-cli (%LOCALAPPDATA%\\mslearn\\Cache\\...)., sessionId и имя docsSearch-тула из кэша learn-cli (TTL 24 ч там же). learn-cli… (+9 more)

### Community 27 - "DiffProcessRunner"
Cohesion: 0.16
Nodes (14): Action, CancellationToken, Process, ProcessStartInfo, Regex, Task, TimeSpan, DiffProcessRunner (+6 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.16
Nodes (9): Классификатор импортированных модулей с детальными описаниями., BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier, MacOSClassifier, Изолированные классификаторы по платформам. Без пустой строки в расширениях., WindowsClassifier (+1 more)

### Community 29 - "Page"
Cohesion: 0.13
Nodes (24): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, BrowseManPagesButton, CheckManPagesButton, CheckUtilsButton (+16 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - ".WarnAsync"
Cohesion: 0.21
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
Cohesion: 0.14
Nodes (11): PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair, PidText (+3 more)

### Community 36 - ".GitStartAsync"
Cohesion: 0.13
Nodes (7): DependencyObject, CancellationToken, CancellationTokenSource, IReadOnlyList, ScrollViewer, TextBox, UiLog

### Community 37 - "describe_section"
Cohesion: 0.16
Nodes (13): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _elf_sections() (+5 more)

### Community 38 - "manpages_bridge.py"
Cohesion: 0.22
Nodes (13): err(), _extract_sections(), import_archive(), main(), on_progress(), _parse_roff_text(), prog(), Самодостаточная синхронизация man-pages для исполнения 2 (IDABatchToolWinUI).… (+5 more)

### Community 39 - "FileItem"
Cohesion: 0.05
Nodes (37): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, IDisposable (+29 more)

### Community 40 - ".BeginSession"
Cohesion: 0.35
Nodes (4): IEnumerable, IReadOnlyList, Fact, MonitorTests

### Community 41 - ".StartComparisonAsync"
Cohesion: 0.26
Nodes (5): DirScanResult, Dictionary, IEnumerable, List, DirScanResult

### Community 42 - ".GenerateModuleReportsAsync"
Cohesion: 0.36
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".RunPass"
Cohesion: 0.22
Nodes (7): Action, CancellationToken, Func, List, Task, DiffPassRunner, Outcomes

### Community 44 - "OutcomeTracker"
Cohesion: 0.26
Nodes (7): ConcurrentDictionary, OutcomeTracker, Failed, Succeeded, Total, Fact, OutcomeTrackerTests

### Community 46 - "IDABatchToolWinUI.csproj"
Cohesion: 0.29
Nodes (6): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0)

### Community 47 - ".WriteJson"
Cohesion: 0.28
Nodes (6): Dictionary, Dictionary, Fact, List, SqliteConnection, DiffParserTests

### Community 48 - "AnalysisWorker"
Cohesion: 0.33
Nodes (4): CancellationTokenSource, IReadOnlyList, Task, AnalysisWorker

### Community 51 - ".RunAsync"
Cohesion: 0.19
Nodes (9): Action, CancellationToken, Process, ProcessStartInfo, Task, TimeSpan, ProcessResult, Success (+1 more)

### Community 52 - "IDABatchToolWinUI.Services"
Cohesion: 0.05
Nodes (19): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Controls, IDABatchToolWinUI.Tests, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff (+11 more)

### Community 53 - "generate_analysis_report"
Cohesion: 0.14
Nodes (15): _api_to_release(), _back_link(), _file_info_entries(), generate_analysis_report(), generate_diff_index(), generate_diff_report(), normalize_display_name(), Рендерит шаблон templates/<name> движком Jinja2 с авто-escape. (+7 more)

### Community 54 - "DiffJson"
Cohesion: 0.18
Nodes (7): Dictionary, HashSet, SqliteConnection, Task, DiffJson, JsonDictExtensions, JsonElement

### Community 55 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов., Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".ProcessPostPairAsync"
Cohesion: 0.36
Nodes (5): Action, CancellationToken, Task, DiffPairProcessor, ExportJsonService

### Community 57 - "MonitorEntry"
Cohesion: 0.13
Nodes (14): DateTime, MonitorEntry, CpuTime, EndedAt, FileName, Key, MemoryMb, OsThreads (+6 more)

### Community 58 - "PythonEnvironment"
Cohesion: 0.22
Nodes (5): PythonHelper, IReadOnlyList, PythonEnvironment, PythonEnvResult, AllOk

### Community 62 - ".EnsureEnvironmentAsync"
Cohesion: 0.26
Nodes (7): Code, LogPath, DispatcherQueue, Func, Task, PythonEnvironmentUi, Task

### Community 63 - "AnalysisMonitor"
Cohesion: 0.30
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

### Community 67 - "ProcessRunnerTests"
Cohesion: 0.49
Nodes (4): Fact, ProcessStartInfo, Task, ProcessRunnerTests

### Community 68 - "ThemeHelper"
Cohesion: 0.23
Nodes (7): ElementTheme, List, Window, ThemeHelper, CurrentElementTheme, CurrentTheme, WeakReference

### Community 69 - "ManPagesSyncWorker"
Cohesion: 0.38
Nodes (3): Process, Task, ManPagesSyncWorker

### Community 70 - "TaskThreadCard"
Cohesion: 0.22
Nodes (9): Brush, TaskThreadCard, BusyVisibility, CardBorder, FileName, FreeVisibility, IsBusy, Title (+1 more)

### Community 71 - ".ExportAsync"
Cohesion: 0.39
Nodes (6): Action, CancellationToken, Reason, Task, DiaphoraService, Ok

### Community 72 - "BinDiffService"
Cohesion: 0.38
Nodes (4): Action, CancellationToken, Task, BinDiffService

### Community 73 - "inline_vendor"
Cohesion: 0.29
Nodes (7): format_percent(), inline_vendor(), _jinja_env(), _marked_js(), Доля [0..1] в проценты с адаптивной точностью. Обычно 1 знак после запятой;…, Встроенный marked.min.js (без внешних файлов)., Содержимое вендорного asset из vendor/ для инлайн-встраивания. Встраивание идёт…

### Community 74 - ".ExportPairAsync"
Cohesion: 0.33
Nodes (4): CancellationToken, Task, Primary, Secondary

### Community 75 - "Task"
Cohesion: 0.47
Nodes (3): Action, Func, Task

## Knowledge Gaps
- **184 isolated node(s):** `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile`, `MatchedFunctions`, `Unmatched1` (+179 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 509 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IDABatchToolWinUI.Services` connect `IDABatchToolWinUI.Services` to `SfaPage`, `IdaRunner`, `TaskManagerWindow`, `ThemeHelper`, `DirectoriesDiffService`, `FileItem`, `AppConfig`, `MainWindow`, `.GenerateModuleReportsAsync`, `.GitStartAsync`, `OutcomeTracker`, `JobObject`, `AppLog`, `.ExtractArchiveAsync`, `.RunAsync`, `ProcessPulse`, `PythonEnvironment`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **Why does `DiffPage` connect `DiffPage` to `ThreadCardViewModel`, `.GitStartAsync`, `AppConfig`, `MainWindow`, `.StartComparisonAsync`, `.GenerateModuleReportsAsync`, `Page`, `.RunSafe`, `Task`, `OutcomeTracker`, `IDABatchToolWinUI.Services`, `DiffWorker`?**
  _High betweenness centrality (0.131) - this node is a cross-community bridge._
- **Why does `DiffWorker` connect `DiffWorker` to `.ReadJson`, `.ExportAsync`, `BinDiffService`, `.StartComparisonAsync`, `FileItem`, `.RunPass`, `OutcomeTracker`, `IDABatchToolWinUI.Services`, `DiffPage`, `.ProcessPostPairAsync`, `DiffProcessRunner`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **What connects `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile` to the rest of the system?**
  _184 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.052393857271906055 - nodes in this community are weakly interconnected._
- **Should `IdaRunner` be split into smaller, more focused modules?**
  _Cohesion score 0.13105413105413105 - nodes in this community are weakly interconnected._
- **Should `TaskManagerWindow` be split into smaller, more focused modules?**
  _Cohesion score 0.1380952380952381 - nodes in this community are weakly interconnected._