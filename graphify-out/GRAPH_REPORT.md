# Graph Report - IDABatchToolWinUI3  (2026-11-02)

## Corpus Check
- 99 files · ~118,533 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 8 file(s) not represented in the graph (top: (none) 3, .props 1, .appxmanifest 1)

## Summary
- 1668 nodes · 3354 edges · 75 communities (65 shown, 6 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 111 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `abbed6c3`
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
- AnalysisWorker
- JobObject
- AppLog
- .StartAnalysisAsync
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
- .Append
- generate_analysis_report
- manpages_build.py
- AnalysisStatus
- IDABatchToolWinUI.Diff
- DiffPage
- .GitGenerateReportAsync
- .ExportAsync
- AnalysisPage
- FileItem
- IDABatchToolWinUI.csproj
- .WriteJson
- OutcomeTracker
- build_sfa_index
- _DocCache
- ConfigService
- IDABatchToolWinUI.Services
- ToolLocator
- DiffJson
- _SfaLog
- .ProcessPostPairAsync
- .Run
- .Save
- VersionConsistencyTests
- .GitStartAsync
- _manpages_get_page
- ThreadCardViewModel
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- IDABatchToolWinUI.Tests.csproj
- InputScannerTests
- ThemeHelper
- .ExtractArchiveAsync
- LibraryMatchingTest
- .ReadManPagesInfo
- get_category_description

## God Nodes (most connected - your core abstractions)
1. `DiffPage` - 62 edges
2. `IDABatchToolWinUI.Services` - 51 edges
3. `Page` - 43 edges
4. `SfaPage` - 42 edges
5. `AnalysisPage` - 41 edges
6. `run_generate()` - 39 edges
7. `DiffWorker` - 36 edges
8. `DirectoriesDiffService` - 32 edges
9. `HtmlGenWorker` - 28 edges
10. `Page` - 24 edges

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

## Communities (75 total, 6 thin omitted)

### Community 0 - ".Enrich"
Cohesion: 0.20
Nodes (11): Action, Dictionary, HashSet, IEnumerable, List, Tag, DiffJsonEnricher, I1 (+3 more)

### Community 1 - "SfaPage"
Cohesion: 0.06
Nodes (37): Msg, Action, DispatcherQueueTimer, Func, List, RoutedEventArgs, Tag, Task (+29 more)

### Community 2 - "IdaRunner"
Cohesion: 0.09
Nodes (20): IReadOnlyList, ArtifactCleanup, Action, CancellationToken, DateTime, Dictionary, IEnumerable, IReadOnlyList (+12 more)

### Community 3 - "SettingsPage"
Cohesion: 0.25
Nodes (4): IEnumerable, RoutedEventArgs, Task, SettingsPage

### Community 4 - "MonitorEntry"
Cohesion: 0.06
Nodes (41): Brush, DateTime, IEnumerable, IReadOnlyList, List, AnalysisMonitor, IsSessionActive, MaxWorkers (+33 more)

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
Cohesion: 0.17
Nodes (11): AppConfig, BindiffExecutable, DefaultInputDir, IdaExecutable, LogLevel, MaxIda, Theme, ConfigStore (+3 more)

### Community 9 - "MainWindow"
Cohesion: 0.06
Nodes (30): AnalysisPage, AppWindow, AppWindowClosingEventArgs, DiffPage, AppTitleBar, NavAnalysis, NavDiff, NavFrame (+22 more)

### Community 10 - "system_modules.py"
Cohesion: 0.05
Nodes (45): _build_category_key_map(), get_module_category(), get_module_category_and_description(), _manpages_lib_dict(), Логика классификации и группировка модулей по категориям. Linux / Android…, Возвращает категорию и описание категории (используется в отчётах). Поиск по…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся… (+37 more)

### Community 11 - "ArtifactValidatorTests"
Cohesion: 0.17
Nodes (4): ArtifactValidator, Fact, ArtifactValidatorTests, Task

### Community 12 - "Page"
Cohesion: 0.06
Nodes (52): BindiffInfoButton, BindiffTip, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, CurrentFileLabel, DiffErrorTextBox (+44 more)

### Community 13 - "report_bridge.py"
Cohesion: 0.05
Nodes (64): is_syscall_section(), Системность функции по секции её man-страницы. Системная функция - только из…, _back_link(), _build_internal_set(), _cancel_requested(), _collect_export_jsons(), emit_error(), emit_progress() (+56 more)

### Community 14 - "AnalysisWorker"
Cohesion: 0.24
Nodes (4): List, CancellationTokenSource, IReadOnlyList, AnalysisWorker

### Community 15 - "JobObject"
Cohesion: 0.11
Nodes (17): Application, Window, App, ConfigStore, IO_COUNTERS, JOBOBJECT_BASIC_LIMIT_INFORMATION, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, LaunchActivatedEventArgs (+9 more)

### Community 16 - "AppLog"
Cohesion: 0.10
Nodes (13): IDisposable, Exception, IEnumerable, IReadOnlyList, List, AppLog, LogDirectories, Action (+5 more)

### Community 17 - ".StartAnalysisAsync"
Cohesion: 0.21
Nodes (4): Action, Func, Task, AppNotifier

### Community 18 - "Page"
Cohesion: 0.08
Nodes (32): BrowseDirButton, CancelButton, CleanupCheck, DeleteJsonCheck, DetailsButton, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox (+24 more)

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
Cohesion: 0.11
Nodes (18): IdenticalPairsService, CancellationToken, CancellationTokenSource, IReadOnlyList, Task, DiffWorker, AddOutputDir, BindiffPath (+10 more)

### Community 23 - "Task"
Cohesion: 0.17
Nodes (5): HeaderCheckBox, Action, Func, RoutedEventArgs, Task

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
Nodes (18): Action, CancellationToken, Task, BinDiffService, Action, CancellationToken, Process, ProcessStartInfo (+10 more)

### Community 28 - "platform_classifier.py"
Cohesion: 0.09
Nodes (16): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, Классификатор импортированных модулей с детальными описаниями., linux_soname_descriptions(), soname и короткая форма → описание библиотеки (анализ ПО). Ключи - glibc- и…, BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier (+8 more)

### Community 29 - "Page"
Cohesion: 0.14
Nodes (21): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, CheckManPagesButton, CheckUtilsButton, IdatPathTextBox (+13 more)

### Community 30 - ".MergeIntoJson"
Cohesion: 0.20
Nodes (12): Address, Dictionary, IEnumerable, List, DiaphoraMatch, DiaphoraParser, DiaphoraParseResult, AlgorithmDistribution (+4 more)

### Community 31 - ".WarnAsync"
Cohesion: 0.09
Nodes (21): Code, ContentDialogResult, LogPath, DispatcherQueue, Func, Task, PythonEnvironmentUi, IReadOnlyList (+13 more)

### Community 32 - "generate_analysis_index"
Cohesion: 0.09
Nodes (33): category_name_ru(), module_library_key(), Таксономия man-pages - единственная таблица соответствия для классификации…, Русское название категории man-pages для интерфейса. Категории man-pages…, Базовая секция man: «2const»/«2type» → «2», «3type» → «3»., Русское название категории-секции man ('' - секция неизвестна)., Порядок тега секции у модуля (системные → библиотечные → прочие)., Названия категорий-секций в порядке левой панели индекса. (+25 more)

### Community 33 - "README.md"
Cohesion: 0.08
Nodes (25): Git-сравнение (исходники), HTML‑отчёты, IDA Batch Tool - WinUI 3 (исполнение 2), Python‑часть (мосты и скрипты), Release‑публикация и переносимая поставка, Анализ СФ (системных функций), Архитектура, Быстрый старт (из исходников) (+17 more)

### Community 34 - ".ReadJson"
Cohesion: 0.30
Nodes (8): Action, CancellationToken, List, Task, AddAnalysisRunner, StageFile, DiffPair, Stem

### Community 35 - "PairRowViewModel"
Cohesion: 0.11
Nodes (16): DirScanResult, PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair (+8 more)

### Community 36 - ".Append"
Cohesion: 0.38
Nodes (4): DependencyObject, ScrollViewer, TextBox, UiLog

### Community 37 - "generate_analysis_report"
Cohesion: 0.09
Nodes (25): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _api_to_release() (+17 more)

### Community 38 - "manpages_build.py"
Cohesion: 0.09
Nodes (23): Path, _collect_pages(), _library_tokens(), main(), render_one(), _names_and_summary(), Сборка встроенной БД man-pages для исполнения 2 (IDABatchToolWinUI). Собирает…, Короткие имена библиотек из секции LIBRARY/БИБЛИОТЕКА. Токены в скобках… (+15 more)

### Community 39 - "AnalysisStatus"
Cohesion: 0.10
Nodes (16): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, AnalysisStatus (+8 more)

### Community 40 - "IDABatchToolWinUI.Diff"
Cohesion: 0.13
Nodes (7): IDABatchToolWinUI.Controls, IDABatchToolWinUI.Models, IDABatchToolWinUI.Diff, BinDiffParser, Action, DiffPassRunner, Outcomes

### Community 41 - "DiffPage"
Cohesion: 0.16
Nodes (7): Page, Cur, DispatcherQueueTimer, Queue, DiffPage, IsBusy, TextBlock

### Community 42 - ".GitGenerateReportAsync"
Cohesion: 0.24
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 43 - ".ExportAsync"
Cohesion: 0.17
Nodes (10): Action, CancellationToken, Reason, Task, DiaphoraService, CancellationToken, Func, List (+2 more)

### Community 44 - "AnalysisPage"
Cohesion: 0.19
Nodes (5): DispatcherQueueTimer, RoutedEventArgs, TaskManagerWindow, AnalysisPage, IsBusy

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
Cohesion: 0.32
Nodes (6): ConcurrentDictionary, OutcomeTracker, Failed, Succeeded, Fact, OutcomeTrackerTests

### Community 49 - "build_sfa_index"
Cohesion: 0.25
Nodes (8): build_sfa_index(), _is_sfa_module(), _normalize_module_key(), Модуль входит в отчёты СФ: категория «Системные библиотеки ОС». Отчёты СФ…, Размер файла в байтах; 0, если файл исчез или недоступен. exists() + stat() в…, Канонический ключ модуля - как в исполнении 1…, Строит index-БД системных функций из JSON-файлов (аналог…, _stat_size_safe()

### Community 51 - "ConfigService"
Cohesion: 0.26
Nodes (4): Dictionary, ConfigService, MiniYaml, YamlMapExtensions

### Community 52 - "IDABatchToolWinUI.Services"
Cohesion: 0.14
Nodes (5): IDABatchToolWinUI.Services, IDABatchToolWinUI, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Tests

### Community 54 - "DiffJson"
Cohesion: 0.18
Nodes (7): Dictionary, HashSet, SqliteConnection, Task, DiffJson, JsonDictExtensions, JsonElement

### Community 55 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов. Базы старой…, Журнал генерации СФ (sfa_debug.log рядом с отчётами) - аналог _log…, _SfaLog

### Community 56 - ".ProcessPostPairAsync"
Cohesion: 0.21
Nodes (9): Action, CancellationToken, Task, DiffPairProcessor, CancellationToken, Task, ExportJsonService, Primary (+1 more)

### Community 57 - ".Run"
Cohesion: 0.20
Nodes (7): Action, Func, Action, DispatcherQueue, Func, Task, UiThread

### Community 62 - ".GitStartAsync"
Cohesion: 0.21
Nodes (3): CancellationToken, CancellationTokenSource, IReadOnlyList

### Community 63 - "_manpages_get_page"
Cohesion: 0.25
Nodes (8): lang_rank(), Порядок секции man для выбора страницы (неизвестные - в конец)., Порядок языка: русская страница предпочитается английской., section_rank(), _manpages_candidates(), _manpages_get_page(), Варианты имени для поиска man-pages: без версии символа (@GLIBC_...) и без…, Документация функции из встроенной БД man-pages. Одна страница на функцию по…

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

### Community 68 - "ThemeHelper"
Cohesion: 0.23
Nodes (7): ElementTheme, List, Window, ThemeHelper, CurrentElementTheme, CurrentTheme, WeakReference

### Community 71 - ".ReadManPagesInfo"
Cohesion: 0.50
Nodes (3): Count, SizeMb, Versions

### Community 72 - "get_category_description"
Cohesion: 0.50
Nodes (4): get_category_description(), Словесное описание категории (пусто для неизвестной категории)., _category_description(), Описание категории из словарей классификатора (пусто для эвристических…

## Knowledge Gaps
- **183 isolated node(s):** `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile`, `MatchedFunctions`, `Unmatched1` (+178 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 554 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FileItem` connect `FileItem` to `SfaPage`, `manpages_build.py`, `AnalysisStatus`, `AnalysisPage`, `.StartAnalysisAsync`?**
  _High betweenness centrality (0.319) - this node is a cross-community bridge._
- **Why does `Path` connect `manpages_build.py` to `FileItem`?**
  _High betweenness centrality (0.315) - this node is a cross-community bridge._
- **Why does `LinuxReportsTest` connect `manpages_build.py` to `system_modules.py`?**
  _High betweenness centrality (0.217) - this node is a cross-community bridge._
- **What connects `ConfigStore`, `IDABatchToolWinUI.Controls`, `StageFile` to the rest of the system?**
  _183 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SfaPage` be split into smaller, more focused modules?**
  _Cohesion score 0.056049213943950786 - nodes in this community are weakly interconnected._
- **Should `IdaRunner` be split into smaller, more focused modules?**
  _Cohesion score 0.09024390243902439 - nodes in this community are weakly interconnected._
- **Should `MonitorEntry` be split into smaller, more focused modules?**
  _Cohesion score 0.05673274094326726 - nodes in this community are weakly interconnected._