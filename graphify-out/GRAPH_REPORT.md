# Graph Report - IDABatchToolWinUI3  (2026-10-27)

## Corpus Check
- 95 files · ~113,849 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 7 file(s) not represented in the graph (top: (none) 3, .props 1, .appxmanifest 1)

## Summary
- 1553 nodes · 3121 edges · 66 communities (58 shown, 6 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 115 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e95b67bb`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .Enrich
- SfaPage
- IdaRunner
- SettingsPage
- MonitorEntry
- IDABatchToolWinUI.Diff
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
- DiffPage
- Window
- .ComputeSimilarity
- McpDocClient
- DiffProcessRunner
- platform_classifier.py
- Page
- .MergeIntoJson
- FileItem
- generate_analysis_index
- README.md
- .ReadJson
- PairRowViewModel
- .GitStartAsync
- generate_analysis_report
- manpages_bridge.py
- AnalysisStatus
- .Append
- .StartComparisonAsync
- .GitGenerateReportAsync
- .RunPass
- OutcomeTracker
- Task
- IDABatchToolWinUI.csproj
- .WriteJson
- AnalysisWorker
- .ExtractArchiveAsync
- _DocCache
- .HeaderCheckBox_Click
- IDABatchToolWinUI.Services
- InputScannerTests
- .ConvertElement
- _SfaLog
- .ProcessPostPairAsync
- ToolLocator
- VersionConsistencyTests
- .Run
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- IDABatchToolWinUI.Tests.csproj
- ThemeHelper
- AppNotifier.cs

## God Nodes (most connected - your core abstractions)
1. `DiffPage` - 62 edges
2. `IDABatchToolWinUI.Services` - 51 edges
3. `Page` - 43 edges
4. `AnalysisPage` - 41 edges
5. `SfaPage` - 40 edges
6. `DiffWorker` - 36 edges
7. `run_generate()` - 36 edges
8. `DirectoriesDiffService` - 32 edges
9. `SettingsPage` - 27 edges
10. `Page` - 24 edges

## Surprising Connections (you probably didn't know these)
- `DiffPage` --references--> `Total`  [EXTRACTED]
  Pages/DiffPage.xaml.cs → Services/OutcomeTracker.cs
- `App` --inherits--> `Application`  [EXTRACTED]
  App.xaml.cs → App.xaml
- `App` --references--> `IConfigStore`  [EXTRACTED]
  App.xaml.cs → Services/ConfigStore.cs
- `DiffWorker` --references--> `AddAnalysisRunner`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/AddAnalysisRunner.cs
- `DiffWorker` --references--> `BinDiffService`  [EXTRACTED]
  Workers/DiffWorker.cs → Diff/BinDiffService.cs

## Import Cycles
- None detected.

## Communities (66 total, 6 thin omitted)

### Community 0 - ".Enrich"
Cohesion: 0.20
Nodes (11): Action, Dictionary, HashSet, IEnumerable, List, Tag, DiffJsonEnricher, I1 (+3 more)

### Community 1 - "SfaPage"
Cohesion: 0.06
Nodes (32): Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag, Task, SfaPage (+24 more)

### Community 2 - "IdaRunner"
Cohesion: 0.09
Nodes (20): IReadOnlyList, ArtifactCleanup, Action, CancellationToken, DateTime, Dictionary, IEnumerable, IReadOnlyList (+12 more)

### Community 3 - "SettingsPage"
Cohesion: 0.05
Nodes (32): Code, LogPath, Action, Func, IEnumerable, RoutedEventArgs, Task, SettingsPage (+24 more)

### Community 4 - "MonitorEntry"
Cohesion: 0.06
Nodes (41): Brush, DateTime, IEnumerable, IReadOnlyList, List, AnalysisMonitor, IsSessionActive, MaxWorkers (+33 more)

### Community 5 - "IDABatchToolWinUI.Diff"
Cohesion: 0.12
Nodes (6): IDABatchToolWinUI.Controls, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff, Action, DiffPassRunner, Outcomes

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

### Community 11 - "ArtifactValidatorTests"
Cohesion: 0.20
Nodes (3): ArtifactValidator, Fact, ArtifactValidatorTests

### Community 12 - "Page"
Cohesion: 0.06
Nodes (52): BindiffInfoButton, BindiffTip, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, CurrentFileLabel, DiffErrorTextBox (+44 more)

### Community 13 - "report_bridge.py"
Cohesion: 0.06
Nodes (61): _back_link(), _build_internal_set(), build_sfa_index(), _cancel_requested(), _collect_export_jsons(), emit_error(), emit_result(), _extract_dll() (+53 more)

### Community 14 - ".StartAnalysisAsync"
Cohesion: 0.22
Nodes (3): Action, Func, Task

### Community 15 - "JobObject"
Cohesion: 0.11
Nodes (17): Application, Window, App, ConfigStore, IO_COUNTERS, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, LaunchActivatedEventArgs (+9 more)

### Community 16 - "AppLog"
Cohesion: 0.10
Nodes (13): IDisposable, Exception, IEnumerable, IReadOnlyList, List, AppLog, LogDirectories, Action (+5 more)

### Community 17 - "AnalysisPage"
Cohesion: 0.16
Nodes (6): DispatcherQueueTimer, List, RoutedEventArgs, TaskManagerWindow, AnalysisPage, IsBusy

### Community 18 - "Page"
Cohesion: 0.08
Nodes (32): BrowseDirButton, CancelButton, CleanupCheck, DeleteJsonCheck, DetailsButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox (+24 more)

### Community 19 - "DocSearchManager"
Cohesion: 0.08
Nodes (19): _decode_bytes(), DocSearchManager, _find_npx(), _get_mcp_client(), _kill_process_tree(), _normalize_func_name(), _parse_npx_results(), Декодирует байты из subprocess с автоопределением кодировки. На Windows npx… (+11 more)

### Community 20 - ".RunAsync"
Cohesion: 0.07
Nodes (28): Bytes, IoCounters, Mtime, CancellationToken, CancellationTokenSource, DateTime, DllImport, IntPtr (+20 more)

### Community 21 - "Page"
Cohesion: 0.10
Nodes (25): Page, SfaBrowseDirButton, SfaCancelButton, SfaCleanupCheck, SfaDeleteJsonCheck, SfaErrorLogTextBox, SfaGenerateHtmlButton, SfaInputDirTextBox (+17 more)

### Community 22 - "DiffWorker"
Cohesion: 0.11
Nodes (18): IdenticalPairsService, CancellationToken, CancellationTokenSource, IReadOnlyList, Task, DiffWorker, AddOutputDir, BindiffPath (+10 more)

### Community 23 - "DiffPage"
Cohesion: 0.15
Nodes (7): Cur, Page, DispatcherQueueTimer, Queue, DiffPage, IsBusy, TextBlock

### Community 24 - "Window"
Cohesion: 0.24
Nodes (9): RowsList, SessionText, SummaryText, ThreadCards, ThreadsPanel, Window, ItemsControl, StackPanel (+1 more)

### Community 25 - ".ComputeSimilarity"
Cohesion: 0.24
Nodes (6): Reason, HexdumpDiffService, Similarity, Skipped, Fact, HexdumpDiffServiceTests

### Community 26 - "McpDocClient"
Cohesion: 0.14
Nodes (13): _mcp_load_cached_session(), _mcp_save_cached_session(), _mcp_session_cache_path(), McpDocClient, Путь к кэшу сессии learn-cli (%LOCALAPPDATA%\\mslearn\\Cache\\...)., sessionId и имя docsSearch-тула из кэша learn-cli (TTL 24 ч там же). learn-cli…, Сохраняет сессию в формат кэша learn-cli (или обновляет существующую запись)., Клиент Microsoft Learn MCP: постоянное соединение, запрос на функцию. Замена… (+5 more)

### Community 27 - "DiffProcessRunner"
Cohesion: 0.12
Nodes (20): Action, CancellationToken, Reason, Task, DiaphoraService, Action, CancellationToken, Process (+12 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.16
Nodes (9): Классификатор импортированных модулей с детальными описаниями., BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier, MacOSClassifier, Изолированные классификаторы по платформам. Без пустой строки в расширениях., WindowsClassifier (+1 more)

### Community 29 - "Page"
Cohesion: 0.13
Nodes (24): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, BrowseManPagesButton, CheckManPagesButton, CheckUtilsButton (+16 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - "FileItem"
Cohesion: 0.20
Nodes (12): FileItem, ExpectedI64Path, Name, Path, Size, Status, ScanResult, IReadOnlyList (+4 more)

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
Cohesion: 0.08
Nodes (17): INotifyPropertyChanged, PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair (+9 more)

### Community 36 - ".GitStartAsync"
Cohesion: 0.19
Nodes (3): CancellationToken, CancellationTokenSource, IReadOnlyList

### Community 37 - "generate_analysis_report"
Cohesion: 0.10
Nodes (21): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _api_to_release() (+13 more)

### Community 38 - "manpages_bridge.py"
Cohesion: 0.17
Nodes (17): done(), err(), _extract_sections(), import_archive(), main(), on_progress(), _parse_roff_text(), prog() (+9 more)

### Community 39 - "AnalysisStatus"
Cohesion: 0.07
Nodes (22): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, IReadOnlyDictionary (+14 more)

### Community 40 - ".Append"
Cohesion: 0.38
Nodes (4): DependencyObject, ScrollViewer, TextBox, UiLog

### Community 41 - ".StartComparisonAsync"
Cohesion: 0.22
Nodes (5): DirScanResult, Dictionary, IEnumerable, List, DirScanResult

### Community 42 - ".GitGenerateReportAsync"
Cohesion: 0.31
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".RunPass"
Cohesion: 0.33
Nodes (4): CancellationToken, Func, List, Task

### Community 44 - "OutcomeTracker"
Cohesion: 0.26
Nodes (7): ConcurrentDictionary, OutcomeTracker, Failed, Succeeded, Total, Fact, OutcomeTrackerTests

### Community 45 - "Task"
Cohesion: 0.24
Nodes (4): Action, Func, RoutedEventArgs, Task

### Community 46 - "IDABatchToolWinUI.csproj"
Cohesion: 0.29
Nodes (6): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0)

### Community 47 - ".WriteJson"
Cohesion: 0.17
Nodes (10): Dictionary, BinDiffParser, HashSet, SqliteConnection, DiffJson, Dictionary, Fact, List (+2 more)

### Community 48 - "AnalysisWorker"
Cohesion: 0.33
Nodes (4): CancellationTokenSource, IReadOnlyList, Task, AnalysisWorker

### Community 52 - "IDABatchToolWinUI.Services"
Cohesion: 0.15
Nodes (5): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Tests

### Community 53 - "InputScannerTests"
Cohesion: 0.48
Nodes (3): Fact, Task, InputScannerTests

### Community 54 - ".ConvertElement"
Cohesion: 0.29
Nodes (4): Dictionary, Task, JsonDictExtensions, JsonElement

### Community 55 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов., Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".ProcessPostPairAsync"
Cohesion: 0.14
Nodes (13): Action, CancellationToken, Task, BinDiffService, Action, CancellationToken, Task, DiffPairProcessor (+5 more)

### Community 64 - ".Run"
Cohesion: 0.29
Nodes (5): Action, DispatcherQueue, Func, Task, UiThread

### Community 65 - "AGENTS.md — контекст проекта для ИИ-агентов (ZCode)"
Cohesion: 0.33
Nodes (5): AGENTS.md — контекст проекта для ИИ-агентов (ZCode), Грабли (проверено на этом коде), О проекте, Сборка и запуск, Стандарты

### Community 66 - "IDABatchToolWinUI.Tests.csproj"
Cohesion: 0.40
Nodes (4): xunit.v3 (4.0.2), net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk

### Community 68 - "ThemeHelper"
Cohesion: 0.23
Nodes (7): ElementTheme, List, Window, ThemeHelper, CurrentElementTheme, CurrentTheme, WeakReference

## Knowledge Gaps
- **180 isolated node(s):** `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile`, `MatchedFunctions`, `Unmatched1` (+175 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 497 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IDABatchToolWinUI.Services` connect `IDABatchToolWinUI.Services` to `SfaPage`, `IdaRunner`, `SettingsPage`, `MonitorEntry`, `IDABatchToolWinUI.Diff`, `DirectoriesDiffService`, `AppConfig`, `MainWindow`, `ArtifactValidatorTests`, `JobObject`, `AppLog`, `.RunAsync`, `AnalysisStatus`, `.Append`, `.GitGenerateReportAsync`, `OutcomeTracker`, `.ExtractArchiveAsync`, `ToolLocator`, `.Run`, `ThemeHelper`, `AppNotifier.cs`?**
  _High betweenness centrality (0.164) - this node is a cross-community bridge._
- **Why does `DiffPage` connect `DiffPage` to `PairRowViewModel`, `.GitStartAsync`, `AppConfig`, `MainWindow`, `.StartComparisonAsync`, `.GitGenerateReportAsync`, `Page`, `Task`, `OutcomeTracker`, `.HeaderCheckBox_Click`, `IDABatchToolWinUI.Services`, `DiffWorker`?**
  _High betweenness centrality (0.134) - this node is a cross-community bridge._
- **Why does `DiffWorker` connect `DiffWorker` to `.ReadJson`, `IDABatchToolWinUI.Diff`, `.StartComparisonAsync`, `OutcomeTracker`, `AppLog`, `IDABatchToolWinUI.Services`, `DiffPage`, `.ProcessPostPairAsync`, `DiffProcessRunner`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **What connects `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile` to the rest of the system?**
  _180 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.059237319511292116 - nodes in this community are weakly interconnected._
- **Should `IdaRunner` be split into smaller, more focused modules?**
  _Cohesion score 0.08906882591093117 - nodes in this community are weakly interconnected._
- **Should `SettingsPage` be split into smaller, more focused modules?**
  _Cohesion score 0.052393857271906055 - nodes in this community are weakly interconnected._