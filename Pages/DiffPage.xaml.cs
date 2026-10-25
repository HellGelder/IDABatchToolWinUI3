using System.Runtime.CompilerServices;
using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Сравнение» (BinDiff + Diaphora) — аналог DiffPage из исполнения 1.
/// </summary>
public sealed partial class DiffPage : Page
{
    private readonly IConfigStore _store;
    private bool _diffInProgress;
    private bool _gitDiffInProgress;
    private CancellationTokenSource? _gitDiffCts;
    private DiffWorker? _worker;
    private string? _outputDir;
    private string? _lastGitDiffPath;
    private string? _lastModuleRunDir;

    // ── Батчинг UI-обновлений диффа ──
    // Колбэки из фоновых потоков только пишут состояние под замком; UI-поток
    // выносит накопленное одним обновлением по таймеру (FlushGitUiUpdates).
    // Прежний вариант — RunOnUi на каждый файл + O(n)-конкатенация строки
    // журнала — топил UI-поток: окно «зависало», карточки не обновлялись.
    private readonly object _gitUiGate = new();
    private readonly Queue<string> _gitPendingLog = new();
    private string?[] _gitPendingModule = Array.Empty<string?>();
    private (int Cur, int Total)[] _gitPendingProgress = Array.Empty<(int, int)>();
    private string? _gitPendingStatus;
    private int _gitDone, _gitTotal;
    private List<ThreadCardViewModel> _threadCards = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _gitUiTimer;
    private readonly Dictionary<string, TextBlock> _stageLabels = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _analyzeTimer;
    private int _analyzeGeneration;

    public DiffPage(IConfigStore store)
    {
        InitializeComponent();
        _store = store;

        // Подсказка BinDiff: TeachingTip — не FrameworkElement, x:Bind к Target
        // ненадёжен — Target назначаем из кода. Клик подключён атрибутом Click
        // в XAML ровно один раз (обработчик тоглит IsOpen).
        BindiffTip.Target = BindiffInfoButton;

        // Подписки — один раз в конструкторе, чтобы при повторном показе
        // (кэш навигации) обработчики не дублировались.
        BrowseLeftButton.Click += (_, _) => UiDialogs.PickFolderAsync(LeftDirTextBox).SafeFireAndForget("Diff.BrowseLeft");
        BrowseRightButton.Click += (_, _) => UiDialogs.PickFolderAsync(RightDirTextBox).SafeFireAndForget("Diff.BrowseRight");
        BrowseOutputButton.Click += (_, _) => UiDialogs.PickFolderAsync(OutputDirTextBox).SafeFireAndForget("Diff.BrowseOutput");
        StartDiffButton.Click += StartComparison_Click;
        CancelDiffButton.Click += (_, _) => CancelComparison();
        GenerateReportButton.Click += GenerateReport_Click;

        LeftDirTextBox.TextChanged += (_, _) => AnalyzeDirectories();
        RightDirTextBox.TextChanged += (_, _) => AnalyzeDirectories();

        // Рекурсивный скан двух директорий — в фоне; TextChanged дебаунсится.
        _analyzeTimer = DispatcherQueue.CreateTimer();
        _analyzeTimer.Interval = TimeSpan.FromMilliseconds(400);
        _analyzeTimer.IsRepeating = false;
        _analyzeTimer.Tick += (_, _) => AnalyzeDirectoriesAsync().SafeFireAndForget("Diff.AnalyzeDirs");

        // Таймер батчинга прогресса диффа (см. комментарий у полей _gitUi*)
        _gitUiTimer = DispatcherQueue.CreateTimer();
        _gitUiTimer.Interval = TimeSpan.FromMilliseconds(150);
        _gitUiTimer.IsRepeating = true;
        _gitUiTimer.Tick += (_, _) => FlushGitUiUpdates();

        Loaded += (_, _) =>
        {
            AnalyzeDirectories();
            RefreshGitReportButton();
        };

        // Подвкладка «Git-сравнение»: пикеры папок, запуск и отмена текстового диффа
        GitBrowseOldButton.Click += (_, _) => UiDialogs.PickFolderAsync(GitOldDirTextBox).SafeFireAndForget("Diff.GitBrowseOld");
        GitBrowseNewButton.Click += (_, _) => UiDialogs.PickFolderAsync(GitNewDirTextBox).SafeFireAndForget("Diff.GitBrowseNew");
        GitBrowseOutButton.Click += (_, _) => UiDialogs.PickFolderAsync(GitOutDirTextBox).SafeFireAndForget("Diff.GitBrowseOut");
        GitStartButton.Click += GitStart_Click;
        GitCancelButton.Click += (_, _) => _gitDiffCts?.Cancel();
        GitReportButton.Click += GitGenerateReport_Click;
        GitExcludesTextBox.Text = DirectoriesDiffService.DefaultExcludes;
        GitFullDiffCheckBox.IsChecked = false;
    }

    public bool IsDiffRunning() => _diffInProgress || _gitDiffInProgress;

    /// <summary>Есть ли активная работа (для закрытия окна).</summary>
    public bool IsBusy => _diffInProgress || _gitDiffInProgress;

    /// <summary>Отменяет все текущие операции (для закрытия окна).</summary>
    public void CancelAll()
    {
        _worker?.Cancel();
        _gitDiffCts?.Cancel();
    }

    /// <summary>«i» у радиокнопки BinDiff: нажатие тоглит TeachingTip.</summary>
    private void BindiffInfoButton_Click(object sender, RoutedEventArgs e)
        => BindiffTip.IsOpen = !BindiffTip.IsOpen;

    /// <summary>Главный чекбокс «выбрать все» в заголовке таблицы.</summary>
    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        SetAllRowsSelected(cb.IsChecked == true);
    }

    /// <summary>Строчный чекбокс: пересчёт выбранных.</summary>
    private void RowCheckBox_Click(object sender, RoutedEventArgs e) => UpdateSelectedCount();

    private void SetAllRowsSelected(bool selected)
    {
        var rows = PairsListView.ItemsSource as List<PairRowViewModel>;
        if (rows == null) return;
        foreach (var r in rows) r.IsSelected = selected;
        UpdateSelectedCount();
    }

    // ──────────────────────────────────────────────
    //  Анализ директорий
    // ──────────────────────────────────────────────

    private void AnalyzeDirectories()
    {
        // Перезапуск дебаунса: применяется последний вариант путей
        _analyzeTimer.Stop();
        _analyzeTimer.Start();
    }

    private async Task AnalyzeDirectoriesAsync()
    {
        var gen = ++_analyzeGeneration;
        var left = LeftDirTextBox.Text.Trim();
        var right = RightDirTextBox.Text.Trim();

        DirScanResult scan;
        try
        {
            scan = await Task.Run(() => ScanPairs(left, right));
        }
        catch (Exception)
        {
            if (gen != _analyzeGeneration) return;
            scan = new DirScanResult(false, new List<PairRowViewModel>(), 0, 0, 0, 0);
        }
        if (gen != _analyzeGeneration) return; // уже запрошен более свежий скан

        if (!scan.DirsExist)
        {
            MapStatusLabel.Text = "Укажите обе директории для анализа.";
            StartDiffButton.IsEnabled = false;
            PairsListView.ItemsSource = null;
            return;
        }

        PairsListView.ItemsSource = scan.Rows;
        ApplyInitialStatuses(scan.Rows);

        if (scan.Rows.Count > 0)
        {
            var selected = scan.Rows.Count(r => r.IsSelected);
            var msg = $"✅ {scan.Rows.Count} пар сопоставлено, выбрано: {selected}";
            if (scan.OnlyLeft > 0) msg += $" (+{scan.OnlyLeft} только слева)";
            if (scan.OnlyRight > 0) msg += $" (+{scan.OnlyRight} только справа)";
            MapStatusLabel.Text = msg;
            StartDiffButton.IsEnabled = selected > 0;
        }
        else
        {
            MapStatusLabel.Text = $"❌ Нет совпадений: {scan.LeftCount} слева, {scan.RightCount} справа.";
            StartDiffButton.IsEnabled = false;
        }
    }

    private sealed record DirScanResult(
        bool DirsExist, List<PairRowViewModel> Rows, int LeftCount, int RightCount, int OnlyLeft, int OnlyRight);

    private static DirScanResult ScanPairs(string left, string right)
    {
        if (!Directory.Exists(left) || !Directory.Exists(right))
            return new DirScanResult(false, new List<PairRowViewModel>(), 0, 0, 0, 0);

        var leftMap = new Dictionary<string, string>();
        foreach (var p in SafeFindI64(left))
            leftMap[Path.GetRelativePath(left, p)] = p;

        var rightMap = new Dictionary<string, string>();
        foreach (var p in SafeFindI64(right))
            rightMap[Path.GetRelativePath(right, p)] = p;

        var leftSet = leftMap.Keys.ToHashSet();
        var rightSet = rightMap.Keys.ToHashSet();
        var common = leftSet.Intersect(rightSet).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var onlyLeft = leftSet.Except(rightSet).Count();
        var onlyRight = rightSet.Except(leftSet).Count();

        var rows = new List<PairRowViewModel>();
        foreach (var rel in common)
        {
            var pair = new DiffPair(leftMap[rel], rightMap[rel], rel);
            long size = File.Exists(pair.Primary) ? new FileInfo(pair.Primary).Length : 0;
            var sizeText = size < 1024 * 1024 ? $"{size / 1024} KB" : $"{size / 1024.0 / 1024.0:F1} MB";
            rows.Add(new PairRowViewModel
            {
                Pair = pair,
                SizeText = sizeText,
            });
        }

        return new DirScanResult(true, rows, leftSet.Count, rightSet.Count, onlyLeft, onlyRight);
    }

    private void UpdateSelectedCount()
    {
        var rows = PairsListView.ItemsSource as List<PairRowViewModel>;
        var selected = rows?.Count(r => r.IsSelected) ?? 0;
        StartDiffButton.IsEnabled = selected > 0 && !_diffInProgress;
        var baseText = MapStatusLabel.Text;
        int idx = baseText.IndexOf(", выбрано:");
        if (idx >= 0) baseText = baseText.Substring(0, idx);
        MapStatusLabel.Text = $"{baseText}, выбрано: {selected}";
        ApplyInitialStatuses(rows ?? new List<PairRowViewModel>());
    }

    /// <summary>
    /// Проставляет статусы колонок движков и «не выбран» в зависимости от выбора и движка.
    /// Статусы колонок (сырые): waiting | analysis | done | error | not_selected | no_analysis.
    /// Во время анализа не затирает живые статусы «анализ/завершён».
    /// </summary>
    private void ApplyInitialStatuses(List<PairRowViewModel> rows)
    {
        var engine = SelectedEngine();
        bool useBd = engine is "bindiff" or "both";
        bool useDp = engine is "diaphora" or "both";
        foreach (var r in rows)
        {
            r.PidText = "—";
            if (!_diffInProgress)
            {
                r.BindiffStatus = useBd ? "waiting" : "no_analysis";
                r.DiaphoraStatus = useDp ? "waiting" : "no_analysis";
            }
            if (!r.IsSelected)
            {
                r.BindiffStatus = "not_selected";
                r.DiaphoraStatus = "not_selected";
            }
        }
    }

    /// <summary>Рекурсивный поиск .i64 без падения на недоступных подкаталогах.</summary>
    private static IEnumerable<string> SafeFindI64(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir, "*.i64"); }
            catch { continue; }
            foreach (var f in files) yield return f;
            string[] sub;
            try { sub = Directory.GetDirectories(dir); }
            catch { continue; }
            foreach (var d in sub) pending.Push(d);
        }
    }

    // ──────────────────────────────────────────────
    //  Сравнение
    // ──────────────────────────────────────────────

    private string SelectedEngine()
    {
        if (EngineDiaphora.IsChecked == true) return "diaphora";
        if (EngineBoth.IsChecked == true) return "both";
        return "bindiff";
    }

    private void StartComparison_Click(object sender, RoutedEventArgs e)
        => RunSafe(StartComparisonAsync());

    private async Task StartComparisonAsync()
    {
        if (_diffInProgress) return;

        var left = LeftDirTextBox.Text.Trim();
        var right = RightDirTextBox.Text.Trim();
        if (!Directory.Exists(left)) { await UiDialogs.WarnAsync("Ошибка", "Укажите корректную левую директорию."); return; }
        if (!Directory.Exists(right)) { await UiDialogs.WarnAsync("Ошибка", "Укажите корректную правую директорию."); return; }

        var rows = (PairsListView.ItemsSource as List<PairRowViewModel>)?.Where(r => r.IsSelected).Select(r => r.Pair).ToList()
                   ?? new List<DiffPair>();
        if (rows.Count == 0) { await UiDialogs.WarnAsync("Ошибка", "Не выбрано ни одной пары для сравнения."); return; }

        var outputDir = OutputDirTextBox.Text.Trim();
        var outputPath = string.IsNullOrEmpty(outputDir) ? Path.Combine(left, AppConstants.DiffResultsDirName) : outputDir;
        Directory.CreateDirectory(outputPath);

        var engine = SelectedEngine();
        string? addOutputPath = null;
        if (engine == "bindiff")
        {
            addOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", AppConstants.AddDiffResultsDirName);
            Directory.CreateDirectory(addOutputPath);
        }

        var idatPath = ToolLocator.GetIdaExecutable(_store.Current);
        if (!File.Exists(idatPath))
        {
            await UiDialogs.WarnAsync("IDA не найдена",
                $"Исполняемый файл '{idatPath}' не найден.\nПроверьте настройки (config.yaml).");
            return;
        }

        var bindiffPath = ToolLocator.GetBindiffExecutable(_store.Current);
        if (engine is "bindiff" or "both" && !File.Exists(bindiffPath))
        {
            await UiDialogs.WarnAsync("Утилита BinDiff не найдена",
                $"Исполняемый файл '{bindiffPath}' не найден.\n" +
                "Поместите bindiff.exe в корень проекта или укажите путь в config.yaml.");
            return;
        }

        // Пары, для которых уже есть результат
        var existing = rows.Where(p => File.Exists(Path.Combine(outputPath, $"{p.Stem}.diff.json"))).ToList();
        var newPairs = rows.Where(p => !File.Exists(Path.Combine(outputPath, $"{p.Stem}.diff.json"))).ToList();

        if (existing.Count > 0)
        {
            var choice = await AskExistingResultsAsync(existing.Count);
            if (choice == "cancel") return;
            if (choice == "yes")
            {
                if (newPairs.Count == 0) { await UiDialogs.InfoAsync("Готово", "Все пары уже обработаны. Сравнение не требуется."); return; }
                rows = newPairs;
            }
            else if (choice == "no")
            {
                foreach (var f in Directory.GetFiles(outputPath, "*.diff.json"))
                    try { File.Delete(f); } catch { }
                // Полная перегенерация: сбрасываем и результаты доанализа
                // (иначе маркер add_analysis_done не даст ему перезапуститься)
                if (addOutputPath != null && Directory.Exists(addOutputPath))
                {
                    foreach (var f in Directory.GetFiles(addOutputPath, "*.diff.json"))
                        try { File.Delete(f); } catch { }
                    foreach (var f in Directory.GetFiles(addOutputPath, "*_diaphora_result.sqlite"))
                        try { File.Delete(f); } catch { }
                }
            }
        }

        if (rows.Count == 0) return;

        _diffInProgress = true;
        _outputDir = outputPath;
        StartDiffButton.IsEnabled = false;
        CancelDiffButton.IsEnabled = true;
        GenerateReportButton.IsEnabled = false;
        DiffProgressRing.IsActive = true;
        DiffProgressRing.Visibility = Visibility.Visible;
        MapStatusLabel.Text = "Прогресс: запуск...";
        DiffErrorTextBox.Text = "";

        // Этапы прогона — под выбранный движок; счётчик шагов не ведём
        ResetStages(engine);
        CurrentFileLabel.Text = "Файл: —";

        // Сброс статусов колонок: выбранные -> waiting для задействованных движков
        var allRows = PairsListView.ItemsSource as List<PairRowViewModel>;
        if (allRows != null) ApplyInitialStatuses(allRows);

        _worker = new DiffWorker(rows, idatPath, bindiffPath, outputPath, engine,
            left, right, addOutputPath,
            maxWorkers: _store.Current.MaxIda > 0 ? _store.Current.MaxIda : 4);
        HookWorker(_worker);
        _worker.Start();
    }

    private void HookWorker(DiffWorker w)
    {
        w.StageChanged += (stage, phase) => RunOnUi(() =>
        {
            OnStageChanged(stage, phase);
            if (phase == "started" || phase == "done")
                AppendLog($"[Этап] {stage} — {(phase == "started" ? "начало" : "завершён")}");
        });
        w.StageFile += (stage, cur, tot, file) => RunOnUi(() =>
        {
            OnStageFile(stage, cur, tot, file);
            if (!string.IsNullOrEmpty(file))
                AppendLog($"[{stage}] {file} ({cur}/{tot})");
        });
        w.PairProcessStarted += (relKey, pid) => RunOnUi(() =>
        {
            var row = (PairsListView.ItemsSource as List<PairRowViewModel>)
                ?.FirstOrDefault(r => r.RelKey == relKey);
            if (row == null) return;
            row.PidText = pid > 0 ? pid.ToString() : "—";
        });
        w.PairStatus += (relKey, engine, status) => RunOnUi(() =>
        {
            var row = (PairsListView.ItemsSource as List<PairRowViewModel>)
                ?.FirstOrDefault(r => r.RelKey == relKey);
            if (row == null) return;
            if (engine == "bindiff") row.BindiffStatus = status;
            else row.DiaphoraStatus = status;
            if (status is "done" or "error")
                AppendLog($"[{engine}] {relKey} — {(status == "done" ? "успешно" : "ошибка")}");
        });
        w.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        w.Finished += (ok, total) => RunOnUi(() => OnDiffFinishedAsync(ok, total));
    }

    // ──────────────────────────────────────────────
    //  Этапы прогона
    // ──────────────────────────────────────────────

    private void ResetStages(string engine)
    {
        StagesPanel.Children.Clear();
        _stageLabels.Clear();
        var stages = new List<string>();
        if (engine is "bindiff" or "both") stages.Add("BinDiff");
        if (engine is "diaphora" or "both") stages.Add("Diaphora");
        stages.Add("Пост-анализ");
        stages.Add("Генерация HTML");
        foreach (var s in stages)
        {
            var tb = new TextBlock { Text = $"○ {s}", FontSize = 12, Opacity = 0.65 };
            _stageLabels[s] = tb;
            StagesPanel.Children.Add(tb);
        }
    }

    private void OnStageChanged(string stage, string phase)
    {
        if (!_stageLabels.TryGetValue(stage, out var tb))
        {
            // Этапы доанализа добавляются динамически
            tb = new TextBlock { Text = stage, FontSize = 12 };
            _stageLabels[stage] = tb;
            StagesPanel.Children.Add(tb);
        }
        switch (phase)
        {
            case "started":
                tb.Text = $"▶ {stage}";
                tb.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                tb.Opacity = 1.0;
                break;
            case "done":
                tb.Text = $"✔ {stage}";
                tb.Foreground = new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
                tb.Opacity = 1.0;
                break;
        }
    }

    private void OnStageFile(string stage, int current, int total, string file)
    {
        CurrentFileLabel.Text = string.IsNullOrEmpty(file)
            ? $"{stage}: {current} / {total}"
            : $"{stage}: {file} ({current} / {total})";
    }

    private async Task OnDiffFinishedAsync(int successCount, int total)
    {
        _diffInProgress = false;
        StartDiffButton.IsEnabled = true;
        CancelDiffButton.IsEnabled = false;
        DiffProgressRing.IsActive = false;
        DiffProgressRing.Visibility = Visibility.Collapsed;
        // Итог — в строке текущего файла: MapStatusLabel перезапишет AnalyzeDirectories()
        var failedCount = Math.Max(0, total - successCount);
        CurrentFileLabel.Text = failedCount > 0
            ? $"Сравнение завершено: успешно {successCount} из {total}, ошибок: {failedCount}"
            : $"Сравнение завершено: успешно {successCount} из {total} пар";
        if (failedCount > 0)
            AppendError($"Сравнение: пар с ошибками — {failedCount} из {total}");

        AnalyzeDirectories();

        // Диспоузим воркер до диалога: диалог может провисеть надолго, а воркер
        // уже не нужен (Dispose отменяет его CTS — прогон завершён).
        _worker?.Dispose();
        _worker = null;

        var reportsDir = Path.Combine(_outputDir ?? ".", AppConstants.ReportsDirName);
        var indexHtml = Path.Combine(reportsDir, "index.html");
        var addReportsDir = Path.Combine(Path.GetDirectoryName(_outputDir) ?? ".", AppConstants.AddDiffResultsDirName, AppConstants.ReportsDirName);
        var addIndexHtml = Path.Combine(addReportsDir, "index.html");

        var hasMain = File.Exists(indexHtml);
        var hasAdd = File.Exists(addIndexHtml);

        if (hasMain || hasAdd)
        {
            GenerateReportButton.IsEnabled = true;
            var lines = new List<string>();
            if (hasMain) lines.Add($"Основные отчёты: {reportsDir}");
            if (hasAdd) lines.Add($"Доанализ (Diaphora): {addReportsDir}");
            var openIndex = await AskOpenReportAsync(lines);
            if (openIndex)
            {
                var target = hasMain ? indexHtml : addIndexHtml;
                Windows.System.Launcher.LaunchUriAsync(new Uri(target))
                    .AsTask().SafeFireAndForget("Diff.LaunchReport");
            }
        }
        else if (_outputDir != null && Directory.Exists(_outputDir))
        {
            var anyJson = Directory.GetFiles(_outputDir, "*.diff.json").Length > 0;
            GenerateReportButton.IsEnabled = anyJson;
        }
    }

    private void CancelComparison()
    {
        _worker?.Cancel();
        MapStatusLabel.Text = "Прогресс: отменён";
        CurrentFileLabel.Text = "Сравнение: отменено";
        CancelDiffButton.IsEnabled = false;
    }

    // ──────────────────────────────────────────────
    //  Отчёт
    // ──────────────────────────────────────────────

    private void GenerateReport_Click(object sender, RoutedEventArgs e)
        => RunSafe(GenerateReportAsync());

    private async Task GenerateReportAsync()
    {
        if (string.IsNullOrEmpty(_outputDir) || !Directory.Exists(_outputDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Выходная папка не существует.");
            return;
        }

        var jsonFiles = Directory.GetFiles(_outputDir, "*.diff.json");
        if (jsonFiles.Length == 0)
        {
            await UiDialogs.WarnAsync("Ошибка", "Нет JSON-файлов с результатами сравнения.");
            return;
        }

        var reportsDir = Path.Combine(_outputDir, AppConstants.ReportsDirName);
        Directory.CreateDirectory(reportsDir);
        DiffProgressRing.IsActive = true;
        DiffProgressRing.Visibility = Visibility.Visible;

        var worker = new HtmlGenWorker("diff", deleteJson: false, reuseCache: false, "Windows");
        worker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            MapStatusLabel.Text = $"Генерация отчёта: {cur}/{total}";
        });
        worker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        worker.Finished += result => RunOnUi(() => OnDiffReportReadyAsync(result));

        // UI-свойства читаем до Task.Run (см. комментарий в GitStart_Click)
        var leftDir = LeftDirTextBox.Text.Trim();
        var rightDir = RightDirTextBox.Text.Trim();
        await worker.RunAsync(leftDir, reportsDir, _outputDir,
            leftDir, rightDir, null, jsonFiles);
    }

    private async Task OnDiffReportReadyAsync(HtmlGenResult result)
    {
        DiffProgressRing.IsActive = false;
        DiffProgressRing.Visibility = Visibility.Collapsed;
        if (result.IndexPath != null)
        {
            await UiDialogs.InfoAsync("Готово", $"Отчёт сгенерирован:\n{result.IndexPath}");
            Windows.System.Launcher.LaunchUriAsync(new Uri(result.IndexPath))
                .AsTask().SafeFireAndForget("Diff.LaunchReport");
        }
    }

    // ──────────────────────────────────────────────
    //  Вспомогательное
    // ──────────────────────────────────────────────

    private void AppendError(string message)
    {
        AppLog.Error(message);
        AppendLog(message);
    }

    /// <summary>Журнал выполнения: дублирует в «Лог выполнения» любые события
    /// (этапы, файлы, статусы пар, ошибки), не только ошибки.</summary>
    private void AppendLog(string message) => UiLog.Append(DiffErrorTextBox, message);

    // ────────────────── Подвкладка «Git-сравнение» ──────────────────

    private void GitStart_Click(object sender, RoutedEventArgs e)
        => RunSafe(GitStartAsync());

    private async Task GitStartAsync()
    {
        if (_gitDiffInProgress) return;

        var oldDir = GitOldDirTextBox.Text.Trim();
        var newDir = GitNewDirTextBox.Text.Trim();
        var outDir = GitOutDirTextBox.Text.Trim();
        if (oldDir.Length == 0 || !Directory.Exists(oldDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папка OLD (старая версия) не найдена.");
            return;
        }
        if (newDir.Length == 0 || !Directory.Exists(newDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папка NEW (новая версия) не найдена.");
            return;
        }
        if (string.Equals(Path.GetFullPath(oldDir), Path.GetFullPath(newDir),
                StringComparison.OrdinalIgnoreCase))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папки OLD и NEW совпадают.");
            return;
        }
        if (outDir.Length == 0 || !Directory.Exists(outDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папка результата не найдена.");
            return;
        }
        // Размер диффа задаёт чекбокс «Полный дифф»: отмечен — все строки файлов,
        // снят — стандартный контекст 3 строки (поля с числом строк больше нет)
        var fullDiff = GitFullDiffCheckBox.IsChecked == true;
        var context = fullDiff ? DirectoriesDiffService.FullContext : DirectoriesDiffService.DefaultContext;

        var patterns = DirectoriesDiffService.ParsePatterns(GitExcludesTextBox.Text);
        var ct = (_gitDiffCts = new CancellationTokenSource()).Token;

        _gitDiffInProgress = true;
        GitStartButton.IsEnabled = false;
        GitCancelButton.IsEnabled = true;
        GitProgress.Visibility = Visibility.Visible;
        GitStatusText.Text = "Выполняется сравнение...";

        try
        {
            // Состояние UI-элементов читаем ТОЛЬКО на UI-потоке — до запуска фоновой
            // задачи: обращение к чекбоксу внутри Task.Run даёт COMException
            // (RPC_E_WRONG_THREAD) с пустым Message — «ОШИБКА: » без причины.
            var skipBinary = GitSourcesOnlyCheckBox.IsChecked == true;
            var modeFlag = GitModulesCheckBox.IsChecked; // null = авто

            var hasSubDirs = await Task.Run(() =>
                DirectoriesDiffService.HasSubDirectories(oldDir) ||
                DirectoriesDiffService.HasSubDirectories(newDir));
            var moduleMode = modeFlag == true || (modeFlag is null && hasSubDirs);

            if (moduleMode)
            {
                await RunModuleComparisonAsync(oldDir, newDir, outDir, context, patterns, skipBinary, ct);
            }
            else
            {
                GitThreadsGroup.Visibility = Visibility.Collapsed;
                StartGitUiTimer(0); // без карточек: батчим только журнал и статус
                var outPath = Path.Combine(outDir, "changes.diff");
                var stats = await Task.Run(() => DirectoriesDiffService.Run(
                    oldDir, newDir, outPath, context, patterns,
                    line => { lock (_gitUiGate) _gitPendingLog.Enqueue(line); },
                    (cur, total, rel) =>
                    {
                        lock (_gitUiGate) _gitPendingStatus = $"Файл {cur}/{total}: {rel}";
                    },
                    ct, skipBinary));

                foreach (var line in DirectoriesDiffService.Summarize(stats))
                    AppendGitLog(line);
                _lastGitDiffPath = stats.OutPath;
                _lastModuleRunDir = null;
                GitReportButton.IsEnabled = true;
                GitStatusText.Text = $"Готово: {stats.OutPath} ({stats.SizeBytes / 1024f:F1} КБ)";
                if (stats.HasErrors)
                    await UiDialogs.WarnAsync("Готово с ошибками",
                        $"Дифф создан, но при обработке {stats.Errors} файл(ов) возникли ошибки. " +
                        "Подробности — в журнале.");
            }
        }
        catch (OperationCanceledException)
        {
            GitStatusText.Text = "Отменено";
            AppendGitLog("Отменено пользователем.");
        }
        catch (Exception ex)
        {
            GitStatusText.Text = "Ошибка";
            // Пустой Message у COMException (RPC_E_WRONG_THREAD и т.п.) — без типа
            // и HResult причина в журнале выглядела как «ОШИБКА: » и пустая строка
            AppendGitLog($"ОШИБКА: {ex.GetType().Name}: {ex.Message} (HResult 0x{ex.HResult:X8})");
            await UiDialogs.WarnAsync("Ошибка", $"{ex.GetType().Name}: {ex.Message}\n\nПодробности — в логе на странице.");
        }
        finally
        {
            StopGitUiTimerAndFlush();
            _gitDiffInProgress = false;
            _gitDiffCts?.Dispose();
            _gitDiffCts = null;
            GitStartButton.IsEnabled = true;
            GitCancelButton.IsEnabled = false;
            GitProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void AppendGitLog(string message) => UiLog.Append(GitLogTextBox, message, autoscroll: true);

    // ────────────────── Батчинг UI-обновлений диффа ──────────────────

    private void StartGitUiTimer(int workers)
    {
        lock (_gitUiGate)
        {
            _gitPendingLog.Clear();
            _gitPendingModule = new string?[workers];
            _gitPendingProgress = new (int, int)[workers];
            _gitDone = 0;
            _gitTotal = 0;
            _gitPendingStatus = null;
        }
        _gitUiTimer.Start();
    }

    /// <summary>Останов таймера и финальный вынос накопленного (вызывается на UI-потоке).</summary>
    private void StopGitUiTimerAndFlush()
    {
        _gitUiTimer.Stop();
        lock (_gitUiGate)
        {
            // Итоговый статус уже выставлен явно — промежуточный не применяем
            _gitDone = 0;
            _gitTotal = 0;
            _gitPendingStatus = null;
        }
        FlushGitUiUpdates();
        while (true)
        {
            var batch = DrainPendingLog();
            if (batch.Length == 0) break;
            AppendGitLogBatch(batch);
        }
    }

    /// <summary>Очередь журнала опустошаем порциями, чтобы один тик не съедал секунды.</summary>
    private string[] DrainPendingLog()
    {
        lock (_gitUiGate)
        {
            int n = Math.Min(64, _gitPendingLog.Count);
            var arr = new string[n];
            for (int i = 0; i < n; i++) arr[i] = _gitPendingLog.Dequeue();
            return arr;
        }
    }

    /// <summary>Единственная точка обновления журнала/карточек/статуса во время прогона:
    /// накопленное с прошлого тика применяется одним обновлением каждого элемента.</summary>
    private void FlushGitUiUpdates()
    {
        var batch = DrainPendingLog();
        if (batch.Length > 0) AppendGitLogBatch(batch);

        var cards = _threadCards;
        for (int t = 0; t < cards.Count && t < _gitPendingModule.Length; t++)
        {
            string? module;
            (int cur, int total) prog;
            lock (_gitUiGate)
            {
                module = _gitPendingModule[t];
                prog = _gitPendingProgress[t];
            }
            if (module == null)
            {
                if (cards[t].StateText != "свободен") cards[t].SetIdle();
                continue;
            }
            if (cards[t].ModuleText != module) cards[t].SetModule(module);
            if (prog.total > 0) cards[t].SetProgress(prog.cur, prog.total);
        }

        lock (_gitUiGate)
        {
            if (_gitPendingStatus != null)
            {
                GitStatusText.Text = _gitPendingStatus;
                _gitPendingStatus = null;
            }
            if (_gitTotal > 0)
            {
                GitModulesProgress.Text = $"Готово {_gitDone}/{_gitTotal}";
                GitStatusText.Text = $"Модулей готово: {_gitDone}/{_gitTotal}";
            }
        }
    }

    /// <summary>Пакетный журнал: одна конкатенация на пакет; хвост ограничен —
    /// полный журнал живёт в changes.diff.log (плоский режим) и журналах модулей.</summary>
    private void AppendGitLogBatch(IReadOnlyList<string> lines)
        => UiLog.Append(GitLogTextBox, string.Join(Environment.NewLine, lines), autoscroll: true);

    /// <summary>Кнопка HTML-отчёта активна, если есть что отчитать:
    /// существующий changes.diff (плоский режим) или манифест модулей.</summary>
    private void RefreshGitReportButton() =>
        GitReportButton.IsEnabled = ResolveGitDiffPath() != null || ResolveModuleRunDir() != null;

    /// <summary>
    /// Путь к последнему changes.diff: результат текущего сеанса либо
    /// changes.diff в указанной папке результата (после перезапуска приложения).
    /// </summary>
    private string? ResolveGitDiffPath()
    {
        if (_lastGitDiffPath != null && File.Exists(_lastGitDiffPath)) return _lastGitDiffPath;
        var outDir = GitOutDirTextBox.Text.Trim();
        if (outDir.Length == 0) return null;
        var candidate = Path.Combine(outDir, "changes.diff");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Папка модульного прогона: текущая сессия либо манифест modules.json
    /// в указанной папке результата (после перезапуска приложения).
    /// </summary>
    private string? ResolveModuleRunDir()
    {
        if (_lastModuleRunDir != null &&
            File.Exists(Path.Combine(_lastModuleRunDir, GitDiffReportService.ManifestFileName)))
            return _lastModuleRunDir;
        var outDir = GitOutDirTextBox.Text.Trim();
        if (outDir.Length > 0 &&
            File.Exists(Path.Combine(outDir, GitDiffReportService.ManifestFileName)))
            return outDir;
        return null;
    }

    private async Task RunModuleComparisonAsync(string oldDir, string newDir, string outDir,
        int context, IReadOnlyList<string> patterns, bool skipBinary, CancellationToken ct)
    {
        var modules = await Task.Run(() => DirectoriesDiffService.DiscoverModules(oldDir, newDir));
        if (modules.Count == 0)
        {
            await UiDialogs.WarnAsync("Модули не найдены",
                "В OLD/NEW нет подпапок-модулей.\nДля плоского сравнения снимите галку «Модульное сравнение».");
            return;
        }

        // Карточки потоков: по одной на воркер; обновления — через батч-таймер
        var workers = Math.Min(4, modules.Count);
        _threadCards = Enumerable.Range(1, workers).Select(i => new ThreadCardViewModel(i)).ToList();
        GitThreadsItems.ItemsSource = _threadCards;
        GitThreadsGroup.Visibility = Visibility.Visible;
        StartGitUiTimer(workers);

        var result = await DirectoriesDiffService.RunModulesAsync(
            modules, oldDir, newDir, outDir, context, patterns, skipBinary,
            line => { lock (_gitUiGate) _gitPendingLog.Enqueue(line); },
            (t, mod, cur, total) =>
            {
                lock (_gitUiGate)
                {
                    _gitPendingModule[t] = mod;
                    _gitPendingProgress[t] = (cur, total);
                }
            },
            (t, mod) =>
            {
                lock (_gitUiGate)
                {
                    _gitPendingModule[t] = mod;
                    if (mod == null) _gitPendingProgress[t] = (0, 0);
                }
            },
            (done, totalModules) =>
            {
                lock (_gitUiGate)
                {
                    _gitDone = done;
                    _gitTotal = totalModules;
                }
            },
            ct);

        _lastGitDiffPath = null;
        _lastModuleRunDir = outDir;
        GitReportButton.IsEnabled = true;
        GitModulesProgress.Text = $"Готово {result.TotalModules}/{result.TotalModules}";

        var changedCount = result.Outcomes.Count(o => o.Kind != "unchanged");
        GitStatusText.Text = $"Готово: модулей {result.TotalModules}, с изменениями {changedCount} " +
                             $"(потоков {result.Workers}) — отчёт: «Сгенерировать HTML-отчёт»";

        var entries = result.Outcomes.Select(o => new ModuleManifestEntry(
            o.Module, o.Kind,
            o.Stats?.Modified ?? 0, o.Stats?.Added ?? 0, o.Stats?.Deleted ?? 0,
            o.Stats?.Binary ?? 0, o.Error)).ToList();
        await Task.Run(() => GitDiffReportService.WriteManifest(outDir, entries));
        AppendGitLog($"Манифест модулей: {Path.Combine(outDir, GitDiffReportService.ManifestFileName)}");

        var errors = result.Outcomes.Where(o => o.Error != null)
            .Select(o => $"{o.Module}: {o.Error}").Take(10).ToList();
        if (errors.Count > 0)
            await UiDialogs.WarnAsync("Готово с ошибками",
                $"При сравнении модулей возникли ошибки:\n" + string.Join("\n", errors));
    }

    private async Task GenerateModuleReportsAsync(string moduleDir)
    {
        var entries = await Task.Run(() => GitDiffReportService.ReadManifest(moduleDir));
        if (entries == null || entries.Count == 0)
        {
            await UiDialogs.WarnAsync("Манифест недоступен",
                "modules.json не найден или пуст — повторите модульное сравнение.");
            return;
        }

        // UI-свойства — до фоновых задач (см. комментарий в GitStart_Click)
        var oldDir = GitOldDirTextBox.Text.Trim();
        var newDir = GitNewDirTextBox.Text.Trim();
        var withDiff = entries.Where(e => e.HasDiff).ToList();

        GitReportButton.IsEnabled = false;
        GitProgress.Visibility = Visibility.Visible;
        try
        {
            int i = 0;
            foreach (var entry in withDiff)
            {
                i++;
                GitStatusText.Text = $"Отчёт {i}/{withDiff.Count}: {entry.Module}";
                var diffPath = Path.Combine(moduleDir, entry.Module, "changes.diff");
                // обратная ссылка — относительная: отчёт переносим вместе с папкой
                await Task.Run(() => GitDiffReportService.Generate(diffPath, oldDir, newDir,
                    backToIndexUrl: "../" + GitDiffReportService.IndexFileName));
            }

            var indexPath = await Task.Run(() =>
                GitDiffReportService.GenerateIndex(moduleDir, oldDir, newDir, entries));
            GitStatusText.Text = $"Индекс: {indexPath}";
            AppendGitLog($"HTML-индекс сгенерирован: {indexPath} (частных отчётов: {withDiff.Count})");

            var choice = await UiDialogs.AskButtonsAsync("HTML-отчёты готовы", indexPath,
                new[] { "Открыть индекс", "Открыть папку", "ОК" }, defaultIndex: 0);
            if (choice == 0)
            {
                Windows.System.Launcher.LaunchUriAsync(new Uri(indexPath))
                    .AsTask().SafeFireAndForget("Diff.LaunchIndex");
            }
            else if (choice == 1 && Directory.Exists(moduleDir))
            {
                Windows.System.Launcher.LaunchFolderPathAsync(moduleDir)
                    .AsTask().SafeFireAndForget("Diff.LaunchFolder");
            }
        }
        catch (Exception ex)
        {
            GitStatusText.Text = "Ошибка генерации отчётов";
            AppendGitLog($"ОШИБКА отчёта: {ex.GetType().Name}: {ex.Message} (HResult 0x{ex.HResult:X8})");
            await UiDialogs.WarnAsync("Ошибка", $"Не удалось сгенерировать HTML-отчёты:\n{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            GitProgress.Visibility = Visibility.Collapsed;
            GitReportButton.IsEnabled = true;
        }
    }

    private void GitGenerateReport_Click(object sender, RoutedEventArgs e)
        => RunSafe(GitGenerateReportAsync());

    private async Task GitGenerateReportAsync()
    {
        if (_gitDiffInProgress) return;

        // Модульный режим: индекс + частные отчёты по манифесту
        var moduleDir = ResolveModuleRunDir();
        if (moduleDir != null)
        {
            await GenerateModuleReportsAsync(moduleDir);
            return;
        }

        var diffPath = ResolveGitDiffPath();
        if (diffPath == null)
        {
            await UiDialogs.WarnAsync("Нет диффа",
                "changes.diff не найден.\nСначала сгенерируйте дифф кнопкой «Сгенерировать дифф».");
            return;
        }

        GitReportButton.IsEnabled = false;
        GitProgress.Visibility = Visibility.Visible;
        GitStatusText.Text = "Генерация HTML-отчёта...";

        try
        {
            var oldDir = GitOldDirTextBox.Text.Trim();
            var newDir = GitNewDirTextBox.Text.Trim();
            var reportPath = await Task.Run(() => GitDiffReportService.Generate(diffPath, oldDir, newDir));
            GitStatusText.Text = $"Отчёт: {reportPath}";
            AppendGitLog("HTML-отчёт сгенерирован: " + reportPath);

            // По образцу AnalysisPage: диалог «Открыть…» после готовности отчёта
            var choice = await UiDialogs.AskButtonsAsync("HTML-отчёт готов", reportPath,
                new[] { "Открыть отчёт", "Открыть папку", "ОК" }, defaultIndex: 0);
            if (choice == 0)
            {
                Windows.System.Launcher.LaunchUriAsync(new Uri(reportPath))
                    .AsTask().SafeFireAndForget("Diff.LaunchReport");
            }
            else if (choice == 1 && Directory.Exists(Path.GetDirectoryName(reportPath)))
            {
                Windows.System.Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(reportPath)!)
                    .AsTask().SafeFireAndForget("Diff.LaunchFolder");
            }
        }
        catch (Exception ex)
        {
            GitStatusText.Text = "Ошибка генерации отчёта";
            AppendGitLog($"ОШИБКА отчёта: {ex.GetType().Name}: {ex.Message} (HResult 0x{ex.HResult:X8})");
            await UiDialogs.WarnAsync("Ошибка", $"Не удалось сгенерировать HTML-отчёт:\n{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            GitProgress.Visibility = Visibility.Collapsed;
            GitReportButton.IsEnabled = true;
        }
    }

    private void RunOnUi(Action a) => DispatcherQueue.TryEnqueue(() => a());

    private void RunOnUi(Func<Task> a) => DispatcherQueue.TryEnqueue(
        () => a().SafeFireAndForget("Diff.RunOnUi"));

    /// <summary>Запуск задачи страницы: исключение попадает в журнал страницы и
    /// AppLog, а не роняет процесс (см. TaskExtensions.SafeFireAndForget).</summary>
    private void RunSafe(Task task, [CallerMemberName] string? context = null)
        => task.SafeFireAndForget(context ?? "Diff",
            ex => RunOnUi(() => AppendError($"{context}: {ex.Message}")));

    private static async Task<string> AskExistingResultsAsync(int count)
    {
        // Прежний вариант (TCS + ContinueWith поверх ShowAsync) зависал навсегда,
        // если ShowAsync падал; AskButtonsAsync завершает задачу «отменой»/исключением.
        var idx = await UiDialogs.AskButtonsAsync(
            "Обнаружены существующие результаты",
            $"В выходной папке уже найдены результаты сравнения для {count} пар.",
            new[] { "Досравнять только новые", "Полное сравнение заново", "Отмена" });
        return idx switch
        {
            0 => "yes",
            1 => "no",
            _ => "cancel",
        };
    }

    private static async Task<bool> AskOpenReportAsync(IReadOnlyList<string> lines)
    {
        var idx = await UiDialogs.AskButtonsAsync(
            "Сравнение завершено",
            "Все этапы завершены.\n" + string.Join("\n", lines),
            new[] { "Открыть основной сводный отчёт", "Нет" });
        return idx == 0;
    }
}