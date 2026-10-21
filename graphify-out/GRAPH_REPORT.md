# Graph Report - IDABatchToolWinUI  (2026-10-21)

## Corpus Check
- 55 files · ~100,849 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 6 file(s) not represented in the graph (top: (none) 3, .appxmanifest 1, .manifest 1)

## Summary
- 1141 nodes · 2217 edges · 58 communities (51 shown, 5 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 45 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `54e95685`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- SfaPage
- SettingsPage
- AnalysisPage
- DirectoriesDiffService
- report_bridge.py
- export_data.py
- AnalysisStatus
- AppConfig
- Task
- system_modules.py
- DiffWorker
- DiffPage
- Dictionary
- Page
- McpDocClient
- IdaRunner
- generate_analysis_report
- HtmlGenResult
- README.md
- Page
- IDABatchToolWinUI.Services
- .GenerateModuleReportsAsync
- DiaphoraParseResult
- generate_analysis_index
- MonitorEntry
- Page
- MainWindow
- PairRowViewModel
- Page
- describe_section
- manpages_bridge.py
- PythonEnvironment
- Window
- platform_classifier.py
- ThreadCardViewModel
- FileItem
- Window
- .RunCoreInner
- TaskThreadCard
- generate_sfa_report
- _DocCache
- PlatformInfo
- App
- TaskManagerWindow
- IDABatchToolWinUI.csproj
- .SimpleDiff
- .GitGenerateReport_Click
- .RunModuleComparisonAsync
- _SfaLog
- .SafeEnumerateFiles
- .HookWorker
- inline_vendor
- AGENTS.md — контекст проекта для ИИ-агентов (ZCode)
- AnalysisWorker
- AppNotifier.cs
- JsonDictExtensions

## God Nodes (most connected - your core abstractions)
1. `DiffWorker` - 88 edges
2. `DiffPage` - 54 edges
3. `Page` - 43 edges
4. `AnalysisPage` - 37 edges
5. `SfaPage` - 36 edges
6. `run_generate()` - 35 edges
7. `DirectoriesDiffService` - 32 edges
8. `Page` - 24 edges
9. `IDABatchToolWinUI.Services` - 24 edges
10. `SettingsPage` - 23 edges

## Surprising Connections (you probably didn't know these)
- `Window` --references--> `MainWindow`  [EXTRACTED]
  MainWindow.xaml → MainWindow.xaml.cs
- `MainWindow` --references--> `AnalysisPage`  [EXTRACTED]
  MainWindow.xaml.cs → Pages/AnalysisPage.xaml.cs
- `MainWindow` --references--> `DiffPage`  [EXTRACTED]
  MainWindow.xaml.cs → Pages/DiffPage.xaml.cs
- `MainWindow` --references--> `SfaPage`  [EXTRACTED]
  MainWindow.xaml.cs → Pages/SfaPage.xaml.cs
- `MainWindow` --references--> `AppConfig`  [EXTRACTED]
  MainWindow.xaml.cs → Services/ConfigService.cs

## Import Cycles
- None detected.

## Communities (58 total, 5 thin omitted)

### Community 0 - "SfaPage"
Cohesion: 0.17
Nodes (7): Action, DispatcherQueueTimer, List, RoutedEventArgs, Tag, Task, SfaPage

### Community 1 - "SettingsPage"
Cohesion: 0.08
Nodes (18): ContentDialog, Count, Action, IEnumerable, RoutedEventArgs, Task, SettingsPage, Task (+10 more)

### Community 2 - "AnalysisPage"
Cohesion: 0.11
Nodes (10): DetailsButton, Action, DispatcherQueueTimer, List, RoutedEventArgs, Task, TaskManagerWindow, AnalysisPage (+2 more)

### Community 3 - "DirectoriesDiffService"
Cohesion: 0.08
Nodes (25): A, B, chunk, ConcurrentDictionary, Kind, excluded, FileStream, isBinary (+17 more)

### Community 4 - "report_bridge.py"
Cohesion: 0.08
Nodes (41): done(), _build_internal_set(), build_sfa_index(), _collect_export_jsons(), emit_error(), emit_progress(), emit_result(), _find_npx() (+33 more)

### Community 5 - "export_data.py"
Cohesion: 0.07
Nodes (43): Any, _compute_file_hashes(), _decompile_function(), _elf_flag_names(), _elf_meta_template(), export_to_json(), callback(), _extract_framework_name() (+35 more)

### Community 6 - "AnalysisStatus"
Cohesion: 0.10
Nodes (16): Canvas, Color, IEnumerable, List, TreemapControl, TreemapSegment, Grid, AnalysisStatus (+8 more)

### Community 7 - "AppConfig"
Cohesion: 0.09
Nodes (14): Dictionary, AppConfig, BindiffExecutable, DefaultInputDir, IdaExecutable, LogLevel, ManPagesDbPath, MaxIda (+6 more)

### Community 8 - "Task"
Cohesion: 0.21
Nodes (9): Ok, Primary, ProcResult, Reason, Secondary, CancellationToken, ProcessStartInfo, Task (+1 more)

### Community 9 - "system_modules.py"
Cohesion: 0.08
Nodes (32): Словари для Android-модулей. Правила поддержки словарей: 1. Каждый модуль…, _build_category_key_map(), get_module_category(), get_module_category_and_description(), Логика классификации и группировка модулей по категориям., Возвращает категорию и описание категории (используется в отчётах). Поиск по…, Возвращает короткое название категории модуля (или пустую строку). Быстрая…, Строит и кэширует отображение «ключ модуля → категория». Приоритет задаётся… (+24 more)

### Community 10 - "DiffWorker"
Cohesion: 0.08
Nodes (21): ExportProgress, SqliteConnection, TimeSpan, CancellationTokenSource, IReadOnlyList, Process, Regex, DiffWorker (+13 more)

### Community 11 - "DiffPage"
Cohesion: 0.15
Nodes (12): Cur, DirScanResult, Page, Dictionary, DispatcherQueueTimer, IEnumerable, List, Queue (+4 more)

### Community 12 - "Dictionary"
Cohesion: 0.16
Nodes (8): DiaphoraParseResult, HashSet, JsonElement, Rows, Similarity, Dictionary, List, DiaphoraMatch

### Community 13 - "Page"
Cohesion: 0.06
Nodes (52): BindiffInfoButton, BindiffTip, BrowseLeftButton, BrowseOutputButton, BrowseRightButton, CancelDiffButton, CurrentFileLabel, DiffErrorTextBox (+44 more)

### Community 14 - "McpDocClient"
Cohesion: 0.06
Nodes (30): _decode_bytes(), DocSearchManager, _get_mcp_client(), _kill_process_tree(), _mcp_load_cached_session(), _mcp_save_cached_session(), _mcp_session_cache_path(), McpDocClient (+22 more)

### Community 15 - "IdaRunner"
Cohesion: 0.15
Nodes (15): Action, CancellationToken, Dictionary, IReadOnlyList, ProcessStartInfo, Task, FileResult, IdaRunner (+7 more)

### Community 16 - "generate_analysis_report"
Cohesion: 0.17
Nodes (13): _api_to_release(), _back_link(), _file_info_entries(), generate_analysis_report(), generate_diff_report(), normalize_display_name(), Рендерит шаблон templates/<name> движком Jinja2 с авто-escape., Относительная ссылка на index.html из вложенного отчёта (как compute_back_link… (+5 more)

### Community 17 - "HtmlGenResult"
Cohesion: 0.09
Nodes (24): StreamWriter, IReadOnlyList, Process, Task, HtmlGenResult, CachedTotal, FoundCount, GeneratedCount (+16 more)

### Community 18 - "README.md"
Cohesion: 0.08
Nodes (25): Git-сравнение (исходники), HTML‑отчёты, IDA Batch Tool — WinUI 3 (исполнение 2), Python‑часть (мосты и скрипты), Release‑публикация и переносимая поставка, Анализ СФ (системных функций), Архитектура, Быстрый старт (из исходников) (+17 more)

### Community 19 - "Page"
Cohesion: 0.13
Nodes (24): AutoBindiffButton, AutoIdaButton, BindiffPathTextBox, BrowseBindiffButton, BrowseIdaButton, BrowseManPagesButton, CheckManPagesButton, CheckUtilsButton (+16 more)

### Community 20 - "IDABatchToolWinUI.Services"
Cohesion: 0.28
Nodes (5): IDABatchToolWinUI.Services, IDABatchToolWinUI.Workers, IDABatchToolWinUI.Pages, IDABatchToolWinUI.Controls, IDABatchToolWinUI.Models

### Community 21 - ".GenerateModuleReportsAsync"
Cohesion: 0.36
Nodes (4): IReadOnlyList, GitDiffReportService, ModuleManifestEntry, HasDiff

### Community 22 - "DiaphoraParseResult"
Cohesion: 0.25
Nodes (8): Address, DiaphoraMatch, Name, DiaphoraParseResult, AlgorithmDistribution, MatchedFunctions, Unmatched1, Unmatched2

### Community 23 - "generate_analysis_index"
Cohesion: 0.12
Nodes (16): get_category_description(), Словесное описание категории (пусто для неизвестной категории)., _category_description(), _classify_full(), _classify_module(), _classify_with_context(), generate_analysis_index(), _is_internal_module() (+8 more)

### Community 24 - "MonitorEntry"
Cohesion: 0.08
Nodes (25): DateTime, IReadOnlyList, List, AnalysisMonitor, IsSessionActive, MaxWorkers, MonitorEntry, CpuTime (+17 more)

### Community 25 - "Page"
Cohesion: 0.08
Nodes (31): BrowseDirButton, CancelButton, CleanupCheck, DeleteJsonCheck, ErrorLogTextBox, GenerateHtmlButton, InputDirTextBox, MaxIdaSlider (+23 more)

### Community 26 - "MainWindow"
Cohesion: 0.18
Nodes (9): Dictionary, Task, MainWindow, NavigationView, NavigationViewSelectionChangedEventArgs, Window, ThemeHelper, Type (+1 more)

### Community 27 - "PairRowViewModel"
Cohesion: 0.14
Nodes (11): PairRowViewModel, BindiffStatus, BindiffStatusDisplay, DiaphoraStatus, DiaphoraStatusDisplay, IsSelected, Pair, PidText (+3 more)

### Community 28 - "Page"
Cohesion: 0.10
Nodes (25): Page, SfaBrowseDirButton, SfaCancelButton, SfaCleanupCheck, SfaDeleteJsonCheck, SfaErrorLogTextBox, SfaGenerateHtmlButton, SfaInputDirTextBox (+17 more)

### Community 29 - "describe_section"
Cohesion: 0.16
Nodes (13): describe_section(), describe_segment(), _normalize(), Справочник назначений секций и сегментов ELF на русском языке. Используется в…, Приводит имя секции/сегмента к виду, пригодному для поиска в словаре., Возвращает словесное назначение секции ELF. Порядок поиска: точное имя →…, Возвращает словесное назначение сегмента (программного заголовка) ELF. Args:…, _elf_sections() (+5 more)

### Community 30 - "manpages_bridge.py"
Cohesion: 0.22
Nodes (13): err(), _extract_sections(), import_archive(), main(), on_progress(), _parse_roff_text(), prog(), Самодостаточная синхронизация man-pages для исполнения 2 (IDABatchToolWinUI).… (+5 more)

### Community 31 - "PythonEnvironment"
Cohesion: 0.16
Nodes (10): Code, DispatcherQueue, LogPath, Func, IReadOnlyList, Task, PythonEnvironment, PythonEnvResult (+2 more)

### Community 32 - "Window"
Cohesion: 0.19
Nodes (12): AppTitleBar, NavAnalysis, NavDiff, NavFrame, NavSettings, NavSfa, NavView, Window (+4 more)

### Community 33 - "platform_classifier.py"
Cohesion: 0.16
Nodes (9): Классификатор импортированных модулей с детальными описаниями., BasePlatformClassifier, classify_module(), CompositeClassifier, LinuxClassifier, MacOSClassifier, Изолированные классификаторы по платформам. Без пустой строки в расширениях., WindowsClassifier (+1 more)

### Community 34 - "ThreadCardViewModel"
Cohesion: 0.18
Nodes (6): INotifyPropertyChanged, ThreadCardViewModel, ModuleText, ProgressText, StateText, Title

### Community 35 - "FileItem"
Cohesion: 0.14
Nodes (8): Errors, FileItem, ExpectedI64Path, Path, Size, Status, Files, ArchiveHandler

### Community 36 - "Window"
Cohesion: 0.24
Nodes (9): RowsList, SessionText, SummaryText, ThreadCards, ThreadsPanel, Window, ItemsControl, StackPanel (+1 more)

### Community 37 - ".RunCoreInner"
Cohesion: 0.22
Nodes (3): DiffPair, Stem, Func

### Community 38 - "TaskThreadCard"
Cohesion: 0.22
Nodes (9): Brush, TaskThreadCard, BusyVisibility, CardBorder, FileName, FreeVisibility, IsBusy, Title (+1 more)

### Community 39 - "generate_sfa_report"
Cohesion: 0.17
Nodes (12): _cancel_requested(), _extract_dll(), generate_sfa_report(), _manpages_candidates(), _manpages_get_page(), Отмена запрошена (через stdin или напрямую менеджеру)., Варианты имени для поиска man-pages: без версии символа (@GLIBC_...) и без…, Документация функции из БД man-pages в формате search_results. (+4 more)

### Community 41 - "PlatformInfo"
Cohesion: 0.25
Nodes (6): IReadOnlyDictionary, PlatformDef, IEnumerable, PlatformDef, PlatformInfo, Keys

### Community 42 - "App"
Cohesion: 0.22
Nodes (5): Application, Window, App, IDABatchToolWinUI, LaunchActivatedEventArgs

### Community 43 - "TaskManagerWindow"
Cohesion: 0.36
Nodes (4): DispatcherQueueTimer, IReadOnlyList, TaskManagerWindow, TaskRow

### Community 44 - "IDABatchToolWinUI.csproj"
Cohesion: 0.29
Nodes (6): net9.0-windows10.0.26100.0, Microsoft.Data.Sqlite (8.0.10), Microsoft.Windows.SDK.BuildTools (10.0.28000.2705), Microsoft.Windows.SDK.BuildTools.WinApp (0.6.1), Microsoft.WindowsAppSDK (2.4.0), Microsoft.NET.Sdk

### Community 45 - ".SimpleDiff"
Cohesion: 0.29
Nodes (6): I1, I2, J1, J2, IEnumerable, Tag

### Community 47 - ".RunModuleComparisonAsync"
Cohesion: 0.19
Nodes (4): CancellationToken, CancellationTokenSource, IReadOnlyList, Task

### Community 48 - "_SfaLog"
Cohesion: 0.33
Nodes (4): _manpages_open(), Открывает БД man-pages: первый читаемый файл из списка кандидатов., Журнал генерации СФ (sfa_debug.log рядом с отчётами) — аналог _log…, _SfaLog

### Community 49 - ".SafeEnumerateFiles"
Cohesion: 0.36
Nodes (3): IEnumerable, List, ExecutableFinder

### Community 51 - "inline_vendor"
Cohesion: 0.29
Nodes (7): format_percent(), inline_vendor(), _jinja_env(), _marked_js(), Встроенный marked.min.js (без внешних файлов)., Содержимое вендорного asset из vendor/ для инлайн-встраивания. Встраивание идёт…, Доля [0..1] в проценты с адаптивной точностью. Обычно 1 знак после запятой;…

### Community 52 - "AGENTS.md — контекст проекта для ИИ-агентов (ZCode)"
Cohesion: 0.33
Nodes (5): AGENTS.md — контекст проекта для ИИ-агентов (ZCode), Грабли (проверено на этом коде), О проекте, Сборка и запуск, Стандарты

### Community 53 - "AnalysisWorker"
Cohesion: 0.40
Nodes (4): IDisposable, CancellationTokenSource, IReadOnlyList, AnalysisWorker

## Knowledge Gaps
- **156 isolated node(s):** `IDABatchToolWinUI.Controls`, `net9.0-windows10.0.26100.0`, `Microsoft.Data.Sqlite (8.0.10)`, `Microsoft.Windows.SDK.BuildTools (10.0.28000.2705)`, `Microsoft.WindowsAppSDK (2.4.0)` (+151 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 400 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IDABatchToolWinUI.Services` connect `IDABatchToolWinUI.Services` to `SettingsPage`, `FileItem`, `DirectoriesDiffService`, `AnalysisStatus`, `AppConfig`, `App`, `TaskManagerWindow`, `IdaRunner`, `.SafeEnumerateFiles`, `.GenerateModuleReportsAsync`, `AppNotifier.cs`, `MonitorEntry`, `PythonEnvironment`?**
  _High betweenness centrality (0.294) - this node is a cross-community bridge._
- **Why does `MonitorEntry` connect `MonitorEntry` to `AnalysisPage`, `TaskManagerWindow`?**
  _High betweenness centrality (0.294) - this node is a cross-community bridge._
- **Why does `DiffPage` connect `DiffPage` to `SettingsPage`, `ThreadCardViewModel`, `AppConfig`, `DiffWorker`, `Page`, `.GitGenerateReport_Click`, `.RunModuleComparisonAsync`, `.HookWorker`, `IDABatchToolWinUI.Services`, `.GenerateModuleReportsAsync`, `MainWindow`?**
  _High betweenness centrality (0.225) - this node is a cross-community bridge._
- **What connects `IDABatchToolWinUI.Controls`, `net9.0-windows10.0.26100.0`, `Microsoft.Data.Sqlite (8.0.10)` to the rest of the system?**
  _156 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SettingsPage` be split into smaller, more focused modules?**
  _Cohesion score 0.083710407239819 - nodes in this community are weakly interconnected._
- **Should `AnalysisPage` be split into smaller, more focused modules?**
  _Cohesion score 0.11411411411411411 - nodes in this community are weakly interconnected._
- **Should `DirectoriesDiffService` be split into smaller, more focused modules?**
  _Cohesion score 0.08490566037735849 - nodes in this community are weakly interconnected._