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
    private readonly AppConfig _cfg;
    private bool _diffInProgress;
    private bool _gitDiffInProgress;
    private CancellationTokenSource? _gitDiffCts;
    private DiffWorker? _worker;
    private string? _outputDir;
    private readonly Dictionary<string, TextBlock> _stageLabels = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _analyzeTimer;
    private int _analyzeGeneration;

public DiffPage()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();

        // Подсказка BinDiff: TeachingTip — не FrameworkElement, x:Bind к Target
        // ненадёжен — Target назначаем из кода. Клик подключён атрибутом Click
        // в XAML ровно один раз (обработчик тоглит IsOpen).
        BindiffTip.Target = BindiffInfoButton;

        // Подписки — один раз в конструкторе, чтобы при повторном показе
        // (кэш навигации) обработчики не дублировались.
        BrowseLeftButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(LeftDirTextBox);
        BrowseRightButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(RightDirTextBox);
        BrowseOutputButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(OutputDirTextBox);
        StartDiffButton.Click += StartComparison_Click;
        CancelDiffButton.Click += (_, _) => CancelComparison();
        GenerateReportButton.Click += GenerateReport_Click;

        LeftDirTextBox.TextChanged += (_, _) => AnalyzeDirectories();
        RightDirTextBox.TextChanged += (_, _) => AnalyzeDirectories();

        // Рекурсивный скан двух директорий — в фоне; TextChanged дебаунсится.
        _analyzeTimer = DispatcherQueue.CreateTimer();
        _analyzeTimer.Interval = TimeSpan.FromMilliseconds(400);
        _analyzeTimer.IsRepeating = false;
        _analyzeTimer.Tick += (_, _) => _ = AnalyzeDirectoriesAsync();

        Loaded += (_, _) => AnalyzeDirectories();

        // Подвкладка «Git-сравнение»: пикеры папок, запуск и отмена текстового диффа
        GitBrowseOldButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(GitOldDirTextBox);
        GitBrowseNewButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(GitNewDirTextBox);
        GitBrowseOutButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(GitOutDirTextBox);
        GitStartButton.Click += GitStart_Click;
        GitCancelButton.Click += (_, _) => _gitDiffCts?.Cancel();
        GitExcludesTextBox.Text = DirectoriesDiffService.DefaultExcludes;
        GitContextTextBox.Text = "3";
    }

    public bool IsDiffRunning() => _diffInProgress || _gitDiffInProgress;

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

    private async void StartComparison_Click(object sender, RoutedEventArgs e)
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
        var outputPath = string.IsNullOrEmpty(outputDir) ? Path.Combine(left, "DiffResults") : outputDir;
        Directory.CreateDirectory(outputPath);

        var engine = SelectedEngine();
        string? addOutputPath = null;
        if (engine == "bindiff")
        {
            addOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", "AddDiffResults");
            Directory.CreateDirectory(addOutputPath);
        }

        var idatPath = ToolLocator.GetIdaExecutable(_cfg);
        if (!File.Exists(idatPath))
        {
            await UiDialogs.WarnAsync("IDA не найдена",
                $"Исполняемый файл '{idatPath}' не найден.\nПроверьте настройки (config.yaml).");
            return;
        }

        var bindiffPath = ToolLocator.GetBindiffExecutable(_cfg);
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
            left, right, addOutputPath);
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
        w.Finished += (ok, total) => RunOnUi(() => OnDiffFinished(ok, total));
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

    private async void OnDiffFinished(int successCount, int total)
    {
        _diffInProgress = false;
        StartDiffButton.IsEnabled = true;
        CancelDiffButton.IsEnabled = false;
        DiffProgressRing.IsActive = false;
        DiffProgressRing.Visibility = Visibility.Collapsed;
        // Итог — в строке текущего файла: MapStatusLabel перезапишет AnalyzeDirectories()
        CurrentFileLabel.Text = $"Сравнение завершено: успешно {successCount} из {total} пар";

        AnalyzeDirectories();

        var reportsDir = Path.Combine(_outputDir ?? ".", "Reports");
        var indexHtml = Path.Combine(reportsDir, "index.html");
        var addReportsDir = Path.Combine(Path.GetDirectoryName(_outputDir) ?? ".", "AddDiffResults", "Reports");
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
                try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(target)); } catch { }
            }
        }
        else if (_outputDir != null && Directory.Exists(_outputDir))
        {
            var anyJson = Directory.GetFiles(_outputDir, "*.diff.json").Length > 0;
            GenerateReportButton.IsEnabled = anyJson;
        }

        _worker = null;
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

    private async void GenerateReport_Click(object sender, RoutedEventArgs e)
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

        var reportsDir = Path.Combine(_outputDir, "Reports");
        Directory.CreateDirectory(reportsDir);
        DiffProgressRing.IsActive = true;
        DiffProgressRing.Visibility = Visibility.Visible;

        var worker = new HtmlGenWorker("diff", deleteJson: false, reuseCache: false, "Windows");
        worker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            MapStatusLabel.Text = $"Генерация отчёта: {cur}/{total}";
        });
        worker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        worker.Finished += result => RunOnUi(async () =>
        {
            DiffProgressRing.IsActive = false;
            DiffProgressRing.Visibility = Visibility.Collapsed;
            if (result.IndexPath != null)
            {
                await UiDialogs.InfoAsync("Готово", $"Отчёт сгенерирован:\n{result.IndexPath}");
                try { _ = Windows.System.Launcher.LaunchUriAsync(new Uri(result.IndexPath)); } catch { }
            }
        });

        await Task.Run(() => worker.Run(LeftDirTextBox.Text.Trim(), reportsDir, _outputDir,
            LeftDirTextBox.Text.Trim(), RightDirTextBox.Text.Trim(), null, jsonFiles));
    }

    // ──────────────────────────────────────────────
    //  Вспомогательное
    // ──────────────────────────────────────────────

    private void AppendError(string message) => AppendLog(message);

    /// <summary>Журнал выполнения: дублирует в «Лог выполнения» любые события
    /// (этапы, файлы, статусы пар, ошибки), не только ошибки.</summary>
    private void AppendLog(string message)
    {
        if (string.IsNullOrEmpty(DiffErrorTextBox.Text)) DiffErrorTextBox.Text = message;
        else DiffErrorTextBox.Text += Environment.NewLine + message;
    }

    // ────────────────── Подвкладка «Git-сравнение» ──────────────────

    private async void GitStart_Click(object sender, RoutedEventArgs e)
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
        if (!int.TryParse(GitContextTextBox.Text.Trim(), out var context) || context < 0)
        {
            await UiDialogs.WarnAsync("Ошибка", "«Строк контекста» — укажите неотрицательное число.");
            return;
        }

        var outPath = Path.Combine(outDir, "changes.diff");
        var patterns = DirectoriesDiffService.ParsePatterns(GitExcludesTextBox.Text);
        var ct = (_gitDiffCts = new CancellationTokenSource()).Token;

        _gitDiffInProgress = true;
        GitStartButton.IsEnabled = false;
        GitCancelButton.IsEnabled = true;
        GitProgress.Visibility = Visibility.Visible;
        GitStatusText.Text = "Выполняется сравнение...";

        try
        {
            var stats = await Task.Run(() => DirectoriesDiffService.Run(
                oldDir, newDir, outPath, context, patterns,
                line => RunOnUi(() => AppendGitLog(line)),
                (cur, total, rel) => RunOnUi(() => GitStatusText.Text = $"Файл {cur}/{total}: {rel}"),
                ct));

            foreach (var line in DirectoriesDiffService.Summarize(stats))
                AppendGitLog(line);
            GitStatusText.Text = $"Готово: {stats.OutPath} ({stats.SizeBytes / 1024f:F1} КБ)";
            if (stats.HasErrors)
                await UiDialogs.WarnAsync("Готово с ошибками",
                    $"Дифф создан, но при обработке {stats.Errors} файл(ов) возникли ошибки. " +
                    "Подробности — в журнале.");
        }
        catch (OperationCanceledException)
        {
            GitStatusText.Text = "Отменено";
            AppendGitLog("Отменено пользователем.");
        }
        catch (Exception ex)
        {
            GitStatusText.Text = "Ошибка";
            AppendGitLog("ОШИБКА: " + ex.Message);
            await UiDialogs.WarnAsync("Ошибка", ex.Message + "\n\nПодробности — в логе на странице.");
        }
        finally
        {
            _gitDiffInProgress = false;
            _gitDiffCts?.Dispose();
            _gitDiffCts = null;
            GitStartButton.IsEnabled = true;
            GitCancelButton.IsEnabled = false;
            GitProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void AppendGitLog(string message)
    {
        if (string.IsNullOrEmpty(GitLogTextBox.Text)) GitLogTextBox.Text = message;
        else GitLogTextBox.Text += Environment.NewLine + message;
        GitLogTextBox.SelectionStart = GitLogTextBox.Text.Length;
    }

    private void RunOnUi(Action a) => DispatcherQueue.TryEnqueue(() => a());

    private static Task<string> AskExistingResultsAsync(int count)
    {
        var tcs = new TaskCompletionSource<string>();
        var dlg = new ContentDialog
        {
            Title = "Обнаружены существующие результаты",
            Content = $"В выходной папке уже найдены результаты сравнения для {count} пар.",
            PrimaryButtonText = "Досравнять только новые",
            SecondaryButtonText = "Полное сравнение заново",
            CloseButtonText = "Отмена",
        };
        if (UiDialogs.XamlRoot != null) dlg.XamlRoot = UiDialogs.XamlRoot;
        _ = dlg.ShowAsync().AsTask().ContinueWith(t =>
        {
            tcs.TrySetResult(t.Result switch
            {
                ContentDialogResult.Primary => "yes",
                ContentDialogResult.Secondary => "no",
                _ => "cancel",
            });
        }, TaskScheduler.FromCurrentSynchronizationContext());
        return tcs.Task;
    }

    private static Task<bool> AskOpenReportAsync(IReadOnlyList<string> lines)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dlg = new ContentDialog
        {
            Title = "Сравнение завершено",
            Content = "Все этапы завершены.\n" + string.Join("\n", lines),
            PrimaryButtonText = "Открыть основной сводный отчёт",
            CloseButtonText = "Нет",
        };
        if (UiDialogs.XamlRoot != null) dlg.XamlRoot = UiDialogs.XamlRoot;
        _ = dlg.ShowAsync().AsTask().ContinueWith(t =>
            tcs.TrySetResult(t.Result == ContentDialogResult.Primary),
            TaskScheduler.FromCurrentSynchronizationContext());
        return tcs.Task;
    }
}