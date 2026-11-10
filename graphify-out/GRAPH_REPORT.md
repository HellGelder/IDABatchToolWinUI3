# Graph Report - IDABatchToolWinUI3  (2026-11-10)

## Corpus Check
- 102 files · ~121,112 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 8 file(s) not represented in the graph (top: (none) 3, .props 1, .appxmanifest 1)

## Summary
- 1727 nodes · 3445 edges · 73 communities (64 shown, 5 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 111 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `841d5adc`
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
- .RunAsync
- Page
- DiffWorker
- .RunSafe
- Window
- .ComputeSimilarity
- McpDocClient
- DiffProcessRunner
- platform_classifier.py
- Page
- .MergeIntoJson
- .WarnAsync
- manpages_taxonomy.py
- README.md
- .ReadJson
- PairRowViewModel
- .GitGenerateReportAsync
- generate_analysis_report
- manpages_build.py
- AnalysisStatus
- IDABatchToolWinUI.Diff
- DiffPage
- .GenerateModuleReportsAsync
- .RunPass
- RoutedEventArgs
- FileItem
- IDABatchToolWinUI.csproj
- .WriteJson
- OutcomeTracker
- AnalysisIndexLayoutTest
- _DocCache
- TaskThreadCard
- IDABatchToolWinUI.Services
- AppConstants.cs
- DiffJson
- _SfaLog
- .ProcessPostPairAsync
- AnalysisWorker
- .ExportPairAsync
- VersionCheckService
- Task
- AppNotifier.cs
- ThreadCardViewModel
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- IDABatchToolWinUI.Tests.csproj
- InputScannerTests
- .ExtractArchiveAsync
- LibraryMatchingTest
- generate_analysis_index

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

## Communities (73 total, 5 thin omitted)

### Community 0 - ".Enrich"
Cohesion: 0.20
Nodes (11): Action, Dictionary, HashSet, IEnumerable, List, Tag, DiffJsonEnricher, I1 (+3 more)

### Community 1 - "SfaPage"
Cohesion: 0.06
Nodes (38): Msg, Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag, Task (+30 more)

### Community 2 - "IdaRunner"
Cohesion: 0.09
Nodes (20): IReadOnlyList, ArtifactCleanup, Action, CancellationToken, DateTime, Dictionary, IEnumerable, IReadOnlyList (+12 more)

### Community 3 - "SettingsPage"
Cohesion: 0.07
Nodes (21): ElementTheme, Action, Func, IEnumerable, RoutedEventArgs, Task, SettingsPage, Count (+13 more)

### Community 4 - "MonitorEntry"
Cohesion: 0.07
Nodes (31): DateTime, IEnumerable, IReadOnlyList, List, AnalysisMonitor, IsSessionActive, MaxWorkers, MonitorEntry (+23 more)

### Community 5 - ".FindExecutables"
Cohesion: 0.12
Nodes (12): IReadOnlyDictionary, PlatformDef, IEnumerable, PlatformDef, PlatformInfo, Keys, List, ExecutableFinder (+4 more)

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
Cohesion: 0.05
Nodes (31): AnalysisPage, AppWindow, AppWindowClosingEventArgs, DiffPage, AppTitleBar, NavAnalysis, NavDiff, NavFrame (+23 more)

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
Nodes (20): Action, CancellationToken, Reason, Task, DiaphoraService, Action, CancellationToken, Process (+12 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.10
Nodes (14): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, Классификатор импортированных модулей с детальными описаниями., linux_soname_descriptions(), soname и короткая форма → описание библиотеки (анализ ПО). Ключи - glibc- и…, BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier (+6 more)

### Community 29 - "Page"
Cohesion: 0.13
Nodes (23): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, CheckManPagesButton, CheckUtilsButton, IdatPathTextBox (+15 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - ".WarnAsync"
Cohesion: 0.20
Nodes (10): ContentDialogResult, ContentDialog, ContentDialog, IReadOnlyList, Task, TextBox, Window, UiDialogs (+2 more)

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

### Community 36 - ".GitGenerateReportAsync"
Cohesion: 0.21
Nodes (4): DependencyObject, ScrollViewer, TextBox, UiLog

### Community 37 - "generate_analysis_report"
Cohesion: 0.08
Nodes (27): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _api_to_release() (+19 more)

### Community 38 - "manpages_build.py"
Cohesion: 0.09
Nodes (23): Path, _collect_pages(), _library_tokens(), main(), render_one(), _names_and_summary(), Сборка встроенной БД man-pages для исполнения 2 (IDABatchToolWinUI). Собирает…, Короткие имена библиотек из секции LIBRARY/БИБЛИОТЕКА. Токены в скобках… (+15 more)

### Community 39 - "AnalysisStatus"
Cohesion: 0.16
Nodes (13): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, AnalysisStatus (+5 more)

### Community 40 - "IDABatchToolWinUI.Diff"
Cohesion: 0.13
Nodes (6): IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Controls, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff, BinDiffParser

### Community 41 - "DiffPage"
Cohesion: 0.11
Nodes (13): DirScanResult, Page, Cur, Dictionary, DispatcherQueueTimer, IEnumerable, IReadOnlyList, List (+5 more)

### Community 42 - ".GenerateModuleReportsAsync"
Cohesion: 0.36
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".RunPass"
Cohesion: 0.22
Nodes (7): Action, CancellationToken, Func, List, Task, DiffPassRunner, Outcomes

### Community 44 - "RoutedEventArgs"
Cohesion: 0.20
Nodes (5): PlatformInfoBar, ThreadsWarningBar, RoutedEventArgs, TaskManagerWindow, InfoBar

### Community 45 - "FileItem"
Cohesion: 0.18
Nodes (11): FileItem, ExpectedI64Path, Name, Size, Status, ScanResult, IReadOnlyList, List (+3 more)

### Community 46 - "IDABatchToolWinUI.csproj"
Cohesion: 0.29
Nodes (6): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk, Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0)

### Community 47 - ".WriteJson"
Cohesion: 0.28
Nodes (6): Dictionary, Dictionary, Fact, List, SqliteConnection, DiffParserTests

### Community 48 - "OutcomeTracker"
Cohesion: 0.29
Nodes (6): ConcurrentDictionary, OutcomeTracker, Failed, Succeeded, Fact, OutcomeTrackerTests

### Community 49 - "AnalysisIndexLayoutTest"
Cohesion: 0.16
Nodes (4): AnalysisIndexLayoutTest, ModuleStatusTagsTest, Тесты компоновки сводного индекса анализа ПО (решения владельца 2026-11-10).…, Правило колонки «Тег»: системность и принадлежность папке.

### Community 51 - "TaskThreadCard"
Cohesion: 0.22
Nodes (9): Brush, TaskThreadCard, BusyVisibility, CardBorder, FileName, FreeVisibility, IsBusy, Title (+1 more)

### Community 52 - "IDABatchToolWinUI.Services"
Cohesion: 0.18
Nodes (4): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Tests, TaskRow

### Community 53 - "AppConstants.cs"
Cohesion: 0.28
Nodes (3): AnalysisStatusExtensions, AppConstants, PythonHelper

### Community 54 - "DiffJson"
Cohesion: 0.18
Nodes (7): Dictionary, HashSet, SqliteConnection, Task, DiffJson, JsonDictExtensions, JsonElement

### Community 55 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов. Базы старой…, Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".ProcessPostPairAsync"
Cohesion: 0.22
Nodes (8): Action, CancellationToken, Task, BinDiffService, Action, CancellationToken, Task, DiffPairProcessor

### Community 57 - "AnalysisWorker"
Cohesion: 0.33
Nodes (4): CancellationTokenSource, IReadOnlyList, Task, AnalysisWorker

### Community 58 - ".ExportPairAsync"
Cohesion: 0.29
Nodes (5): CancellationToken, Task, ExportJsonService, Primary, Secondary

### Community 61 - "VersionCheckService"
Cohesion: 0.05
Nodes (30): AssemblyInformationalVersionAttribute, Code, LogPath, DispatcherQueue, Func, Task, PythonEnvironmentUi, IReadOnlyList (+22 more)

### Community 62 - "Task"
Cohesion: 0.21
Nodes (5): Action, CancellationToken, CancellationTokenSource, Func, Task

### Community 64 - "ThreadCardViewModel"
Cohesion: 0.18
Nodes (6): INotifyPropertyChanged, ThreadCardViewModel, ModuleText, ProgressText, StateText, Title

### Community 65 - "AGENTS.md — контекст проекта для ИИ-агентов (ZCode)"
Cohesion: 0.29
Nodes (6): AGENTS.md — контекст проекта для ИИ-агентов (ZCode), Грабли (проверено на этом коде), Зоны MOTW / SmartScreen (правило сборки поставки), О проекте, Сборка и запуск, Стандарты

### Community 66 - "IDABatchToolWinUI.Tests.csproj"
Cohesion: 0.40
Nodes (4): xunit.v3 (4.0.2), net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.NET.Sdk

### Community 67 - "InputScannerTests"
Cohesion: 0.48
Nodes (3): Fact, Task, InputScannerTests

### Community 72 - "generate_analysis_index"
Cohesion: 0.09
Nodes (26): get_category_description(), Словесное описание категории (пусто для неизвестной категории)., module_library_key(), Короткое имя библиотеки man-pages для модуля ('' - определить нельзя)., _category_description(), _classify_module(), _collect_own_system_libs(), generate_analysis_index() (+18 more)

## Knowledge Gaps
- **188 isolated node(s):** `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile`, `MatchedFunctions`, `Unmatched1` (+183 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 579 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FileItem` connect `FileItem` to `AnalysisPage`, `SfaPage`, `manpages_build.py`, `AnalysisStatus`?**
  _High betweenness centrality (0.330) - this node is a cross-community bridge._
- **Why does `Path` connect `manpages_build.py` to `FileItem`?**
  _High betweenness centrality (0.325) - this node is a cross-community bridge._
- **Why does `LinuxReportsTest` connect `manpages_build.py` to `system_modules.py`?**
  _High betweenness centrality (0.225) - this node is a cross-community bridge._
- **What connects `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile` to the rest of the system?**
  _188 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.05639097744360902 - nodes in this community are weakly interconnected._
- **Should `IdaRunner` be split into smaller, more focused modules?**
  _Cohesion score 0.09024390243902439 - nodes in this community are weakly interconnected._
- **Should `SettingsPage` be split into smaller, more focused modules?**
  _Cohesion score 0.07013574660633484 - nodes in this community are weakly interconnected._