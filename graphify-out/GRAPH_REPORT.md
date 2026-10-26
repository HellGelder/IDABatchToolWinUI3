# Graph Report - IDABatchToolWinUI3  (2026-10-26)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 1467 nodes · 2971 edges · 61 communities (57 shown, 2 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 118 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e670f875`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .ReadJson
- SfaPage
- AnalysisPage
- SettingsPage
- MonitorEntry
- IDABatchToolWinUI.Services
- DirectoriesDiffService
- export_data.py
- AppConfig
- MainWindow
- system_modules.py
- report_bridge.py
- Page
- run_generate
- ManPagesSyncWorker
- JobObject
- AppLog
- IdaRunner
- Page
- DocSearchManager
- ProcessPulse
- Page
- DiffWorker
- DiffPage
- TextBlock
- .ComputeSimilarity
- McpDocClient
- DiffProcessRunner
- platform_classifier.py
- Page
- .MergeIntoJson
- FileItem
- generate_analysis_index
- OutcomeTracker
- .RunAsync
- PairRowViewModel
- Task
- describe_section
- manpages_bridge.py
- TreemapControl
- .GitGenerateReportAsync
- .StartComparisonAsync
- .GenerateModuleReportsAsync
- .ExportAsync
- ThreadCardViewModel
- AnalysisStatus
- IDABatchToolWinUI.csproj
- ProcessRunnerTests
- .RunPass
- PlatformInfo
- _DocCache
- .ExportPairAsync
- RoutedEventArgs
- InputScannerTests
- BinDiffService
- _SfaLog
- .SafeFireAndForget
- ProgressBar
- PseudocodeTip

## God Nodes (most connected - your core abstractions)
1. `DiffPage` - 62 edges
2. `IDABatchToolWinUI.Services` - 51 edges
3. `Page` - 43 edges
4. `AnalysisPage` - 39 edges
5. `SfaPage` - 38 edges
6. `DiffWorker` - 36 edges
7. `run_generate()` - 36 edges
8. `DirectoriesDiffService` - 32 edges
9. `SettingsPage` - 27 edges
10. `Page` - 24 edges

## Surprising Connections (you probably didn't know these)
- `DiffPage` --references--> `Total`  [EXTRACTED]
  Pages/DiffPage.xaml.cs → Services/OutcomeTracker.cs
- `DiffWorker` --references--> `AddAnalysisRunner`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/AddAnalysisRunner.cs
- `DiffWorker` --references--> `DiffPairProcessor`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/DiffPairProcessor.cs
- `DiffWorker` --references--> `DiffPair`  [EXTRACTED]
  Workers/DiffWorker.cs → Models/FileItem.cs
- `MainWindow` --references--> `SfaPage`  [EXTRACTED]
  MainWindow.xaml.cs → Pages/SfaPage.xaml.cs

## Import Cycles
- None detected.

## Communities (61 total, 2 thin omitted)

### Community 0 - ".ReadJson"
Cohesion: 0.06
Nodes (37): Action, CancellationToken, List, Task, AddAnalysisRunner, StageFile, Dictionary, Dictionary (+29 more)

### Community 1 - "SfaPage"
Cohesion: 0.05
Nodes (33): Page, Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag, Task (+25 more)

### Community 2 - "AnalysisPage"
Cohesion: 0.07
Nodes (16): Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Task, TaskManagerWindow, AnalysisPage (+8 more)

### Community 3 - "SettingsPage"
Cohesion: 0.07
Nodes (21): ContentDialog, Action, Func, IEnumerable, RoutedEventArgs, Task, SettingsPage, Task (+13 more)

### Community 4 - "MonitorEntry"
Cohesion: 0.05
Nodes (41): Brush, DateTime, IEnumerable, IReadOnlyList, List, AnalysisMonitor, IsSessionActive, MaxWorkers (+33 more)

### Community 5 - "IDABatchToolWinUI.Services"
Cohesion: 0.05
Nodes (21): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Controls, IDABatchToolWinUI.Tests, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff (+13 more)

### Community 6 - "DirectoriesDiffService"
Cohesion: 0.08
Nodes (26): A, B, chunk, Kind, excluded, files, FileStream, isBinary (+18 more)

### Community 7 - "export_data.py"
Cohesion: 0.07
Nodes (43): Any, _compute_file_hashes(), _decompile_function(), _elf_flag_names(), _elf_meta_template(), export_to_json(), callback(), _extract_framework_name() (+35 more)

### Community 8 - "AppConfig"
Cohesion: 0.09
Nodes (18): Dictionary, AppConfig, BindiffExecutable, DefaultInputDir, IdaExecutable, LogLevel, ManPagesDbPath, MaxIda (+10 more)

### Community 9 - "MainWindow"
Cohesion: 0.06
Nodes (30): AnalysisPage, AppWindow, AppWindowClosingEventArgs, DiffPage, AppTitleBar, NavAnalysis, NavDiff, NavFrame (+22 more)

### Community 10 - "system_modules.py"
Cohesion: 0.08
Nodes (32): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, _build_category_key_map(), get_module_category(), get_module_category_and_description(), Логика классификации и группировка модулей по категориям., Возвращает категорию и описание категории (используется в отчётах). Поиск по…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся… (+24 more)

### Community 11 - "report_bridge.py"
Cohesion: 0.09
Nodes (37): _api_to_release(), _back_link(), _cancel_requested(), _extract_dll(), _file_info_entries(), format_percent(), generate_analysis_report(), generate_diff_index() (+29 more)

### Community 12 - "Page"
Cohesion: 0.09
Nodes (35): BindiffInfoButton, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, DiffErrorTextBox, DiffProgressRing, EngineBindiff (+27 more)

### Community 13 - "run_generate"
Cohesion: 0.07
Nodes (36): done(), _build_internal_set(), build_sfa_index(), _collect_export_jsons(), emit_error(), emit_progress(), emit_result(), fail() (+28 more)

### Community 14 - "ManPagesSyncWorker"
Cohesion: 0.09
Nodes (15): Code, LogPath, DispatcherQueue, Func, Task, PythonEnvironmentUi, PythonHelper, IReadOnlyList (+7 more)

### Community 15 - "JobObject"
Cohesion: 0.11
Nodes (17): Application, Window, App, ConfigStore, IO_COUNTERS, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, LaunchActivatedEventArgs (+9 more)

### Community 16 - "AppLog"
Cohesion: 0.15
Nodes (9): IDisposable, Exception, IEnumerable, IReadOnlyList, List, AppLog, LogDirectories, Fact (+1 more)

### Community 17 - "IdaRunner"
Cohesion: 0.13
Nodes (16): Action, CancellationToken, DateTime, Dictionary, IEnumerable, IReadOnlyList, Task, IdaRunner (+8 more)

### Community 18 - "Page"
Cohesion: 0.10
Nodes (24): BrowseDirButton, CancelButton, DeleteJsonCheck, DetailsButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox, Page (+16 more)

### Community 19 - "DocSearchManager"
Cohesion: 0.08
Nodes (19): _decode_bytes(), DocSearchManager, _find_npx(), _get_mcp_client(), _kill_process_tree(), _normalize_func_name(), _parse_npx_results(), Декодирует байты из subprocess с автоопределением кодировки. На Windows npx… (+11 more)

### Community 20 - "ProcessPulse"
Cohesion: 0.13
Nodes (15): Bytes, IoCounters, Mtime, CancellationToken, CancellationTokenSource, DateTime, DllImport, IntPtr (+7 more)

### Community 21 - "Page"
Cohesion: 0.11
Nodes (23): CleanupCheck, MaxIdaSlider, PseudocodeCheck, GitFullDiffCheckBox, GitModulesCheckBox, GitSourcesOnlyCheckBox, HeaderCheckBox, Page (+15 more)

### Community 22 - "DiffWorker"
Cohesion: 0.12
Nodes (17): CancellationToken, CancellationTokenSource, IReadOnlyList, Task, DiffWorker, AddOutputDir, BindiffPath, DiaphoraPath (+9 more)

### Community 23 - "DiffPage"
Cohesion: 0.15
Nodes (7): Cur, DispatcherQueueTimer, IReadOnlyList, Queue, DiffPage, IsBusy, TextBlock

### Community 24 - "TextBlock"
Cohesion: 0.09
Nodes (22): ProcessStatusText, CurrentFileLabel, GitModulesProgress, GitStatusText, GitThreadsItems, MapStatusLabel, StagesPanel, ItemsControl (+14 more)

### Community 25 - ".ComputeSimilarity"
Cohesion: 0.24
Nodes (6): Reason, HexdumpDiffService, Similarity, Skipped, Fact, HexdumpDiffServiceTests

### Community 26 - "McpDocClient"
Cohesion: 0.14
Nodes (13): _mcp_load_cached_session(), _mcp_save_cached_session(), _mcp_session_cache_path(), McpDocClient, Путь к кэшу сессии learn-cli (%LOCALAPPDATA%\\mslearn\\Cache\\...)., sessionId и имя docsSearch-тула из кэша learn-cli (TTL 24 ч там же). learn-cli…, Сохраняет сессию в формат кэша learn-cli (или обновляет существующую запись)., Клиент Microsoft Learn MCP: постоянное соединение, запрос на функцию. Замена… (+5 more)

### Community 27 - "DiffProcessRunner"
Cohesion: 0.17
Nodes (14): Action, CancellationToken, Process, ProcessStartInfo, Regex, Task, TimeSpan, DiffProcessRunner (+6 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.16
Nodes (9): Классификатор импортированных модулей с детальными описаниями., BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier, MacOSClassifier, Изолированные классификаторы по платформам. Без пустой строки в расширениях., WindowsClassifier (+1 more)

### Community 29 - "Page"
Cohesion: 0.17
Nodes (18): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, BrowseManPagesButton, CheckManPagesButton, CheckUtilsButton (+10 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - "FileItem"
Cohesion: 0.17
Nodes (12): FileItem, ExpectedI64Path, Name, Path, Size, Status, ScanResult, IReadOnlyList (+4 more)

### Community 32 - "generate_analysis_index"
Cohesion: 0.12
Nodes (16): get_category_description(), Словесное описание категории (пусто для неизвестной категории)., _category_description(), _classify_full(), _classify_module(), _classify_with_context(), generate_analysis_index(), _is_internal_module() (+8 more)

### Community 33 - "OutcomeTracker"
Cohesion: 0.26
Nodes (7): ConcurrentDictionary, OutcomeTracker, Failed, Succeeded, Total, Fact, OutcomeTrackerTests

### Community 34 - ".RunAsync"
Cohesion: 0.19
Nodes (9): Action, CancellationToken, Process, ProcessStartInfo, Task, TimeSpan, ProcessResult, Success (+1 more)

### Community 35 - "PairRowViewModel"
Cohesion: 0.14
Nodes (11): PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair, PidText (+3 more)

### Community 36 - "Task"
Cohesion: 0.22
Nodes (5): Action, CancellationToken, CancellationTokenSource, Func, Task

### Community 37 - "describe_section"
Cohesion: 0.16
Nodes (13): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _elf_sections() (+5 more)

### Community 38 - "manpages_bridge.py"
Cohesion: 0.22
Nodes (13): err(), _extract_sections(), import_archive(), main(), on_progress(), _parse_roff_text(), prog(), Самодостаточная синхронизация man-pages для исполнения 2 (IDABatchToolWinUI).… (+5 more)

### Community 39 - "TreemapControl"
Cohesion: 0.23
Nodes (8): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, TreemapSegment

### Community 40 - ".GitGenerateReportAsync"
Cohesion: 0.20
Nodes (4): DependencyObject, ScrollViewer, TextBox, UiLog

### Community 41 - ".StartComparisonAsync"
Cohesion: 0.26
Nodes (5): DirScanResult, Dictionary, IEnumerable, List, DirScanResult

### Community 42 - ".GenerateModuleReportsAsync"
Cohesion: 0.36
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".ExportAsync"
Cohesion: 0.27
Nodes (6): Action, CancellationToken, Reason, Task, DiaphoraService, Ok

### Community 44 - "ThreadCardViewModel"
Cohesion: 0.18
Nodes (6): INotifyPropertyChanged, ThreadCardViewModel, ModuleText, ProgressText, StateText, Title

### Community 45 - "AnalysisStatus"
Cohesion: 0.20
Nodes (7): AnalysisStatus, Error, InProgress, NotAnalyzed, Success, AnalysisStatusExtensions, AppConstants

### Community 46 - "IDABatchToolWinUI.csproj"
Cohesion: 0.22
Nodes (8): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0), xunit.v3 (4.0.2), Microsoft.Data.Sqlite (8.0.10)

### Community 47 - "ProcessRunnerTests"
Cohesion: 0.49
Nodes (4): Fact, ProcessStartInfo, Task, ProcessRunnerTests

### Community 48 - ".RunPass"
Cohesion: 0.22
Nodes (7): Action, CancellationToken, Func, List, Task, DiffPassRunner, Outcomes

### Community 49 - "PlatformInfo"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, PlatformDef, IEnumerable, PlatformDef, PlatformInfo, Keys

### Community 51 - ".ExportPairAsync"
Cohesion: 0.25
Nodes (5): CancellationToken, Task, ExportJsonService, Primary, Secondary

### Community 53 - "InputScannerTests"
Cohesion: 0.39
Nodes (3): Fact, Task, InputScannerTests

### Community 54 - "BinDiffService"
Cohesion: 0.38
Nodes (4): Action, CancellationToken, Task, BinDiffService

### Community 55 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов., Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".SafeFireAndForget"
Cohesion: 0.33
Nodes (4): Action, Exception, Task, TaskExtensions

### Community 57 - "ProgressBar"
Cohesion: 0.50
Nodes (4): ProcessProgress, GitProgress, SfaProcessProgress, ProgressBar

### Community 58 - "PseudocodeTip"
Cohesion: 0.67
Nodes (3): PseudocodeTip, BindiffTip, TeachingTip

## Knowledge Gaps
- **136 isolated node(s):** `IO_COUNTERS`, `IoCounters`, `PlatformDef`, `StageFile`, `Stem` (+131 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 450 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **2 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IDABatchToolWinUI.Services` connect `IDABatchToolWinUI.Services` to `SfaPage`, `AnalysisPage`, `SettingsPage`, `MonitorEntry`, `DirectoriesDiffService`, `AppConfig`, `MainWindow`, `ManPagesSyncWorker`, `JobObject`, `AppLog`, `IdaRunner`, `ProcessPulse`, `FileItem`, `OutcomeTracker`, `.RunAsync`, `.GitGenerateReportAsync`, `.GenerateModuleReportsAsync`, `AnalysisStatus`, `InputScannerTests`, `.SafeFireAndForget`?**
  _High betweenness centrality (0.182) - this node is a cross-community bridge._
- **Why does `DiffPage` connect `DiffPage` to `SfaPage`, `OutcomeTracker`, `Task`, `IDABatchToolWinUI.Services`, `.GitGenerateReportAsync`, `MainWindow`, `.StartComparisonAsync`, `.GenerateModuleReportsAsync`, `Page`, `ThreadCardViewModel`, `AppConfig`, `RoutedEventArgs`, `DiffWorker`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **Why does `DiffWorker` connect `DiffWorker` to `.ReadJson`, `OutcomeTracker`, `IDABatchToolWinUI.Services`, `.StartComparisonAsync`, `.ExportAsync`, `.RunPass`, `AppLog`, `.ExportPairAsync`, `BinDiffService`, `DiffPage`, `DiffProcessRunner`?**
  _High betweenness centrality (0.098) - this node is a cross-community bridge._
- **What connects `IO_COUNTERS`, `IoCounters`, `PlatformDef` to the rest of the system?**
  _136 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `.ReadJson` be split into smaller, more focused modules?**
  _Cohesion score 0.06112616681722373 - nodes in this community are weakly interconnected._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.054773082942097026 - nodes in this community are weakly interconnected._
- **Should `AnalysisPage` be split into smaller, more focused modules?**
  _Cohesion score 0.06881287726358148 - nodes in this community are weakly interconnected._