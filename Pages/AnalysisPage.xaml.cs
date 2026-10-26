using System.Runtime.CompilerServices;
using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Общий анализ» - аналог AnalysisPage из исполнения 1 (Qt).
/// </summary>
public sealed partial class AnalysisPage : Page
{
    private readonly IConfigStore _store;
    private bool _analysisInProgress;
    private bool _htmlInProgress;
    private AnalysisWorker? _worker;
    private HtmlGenWorker? _htmlWorker;
    private List<FileItem> _cachedFiles = new();
    private bool _exportAllAfterAnalysis;
    private TaskManagerWindow? _taskManagerWindow;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _refreshTimer;
    private int _refreshGeneration;

    public AnalysisPage(IConfigStore store)
    {
        InitializeComponent();
        _store = store;
        // max_ida: 0 = дефолт 4; значение ограничено разумным максимумом.
        var configuredThreads = _store.Current.MaxIda > 0 ? _store.Current.MaxIda : 4;
        MaxIdaSlider.Maximum = Math.Max(8, Environment.ProcessorCount);
        MaxIdaSlider.Value = Math.Clamp(configuredThreads, 1, (int)MaxIdaSlider.Maximum);
        // Предупреждение о повышенной нагрузке при потоках по максимуму железа
        MaxIdaSlider.ValueChanged += (_, _) => UpdateThreadsWarning();
        UpdateThreadsWarning();

        // Подписки - один раз в конструкторе, чтобы при повторном показе страницы
        // (кэш навигации) обработчики не дублировались.
        BrowseDirButton.Click += BrowseDir_Click;
        StartAnalysisButton.Click += StartAnalysis_Click;
        DetailsButton.Click += DetailsButton_Click;
        // TeachingTip - не FrameworkElement: x:Bind к Target ненадёжен, задаём
        // из кода. Клик подключён атрибутом Click в XAML ровно один раз:
        // обработчик тоглит IsOpen, двойная подписка гасила бы тип.
        PseudocodeTip.Target = PseudocodeInfoButton;
        CancelButton.Click += Cancel_Click;
        GenerateHtmlButton.Click += GenerateHtml_Click;
        InputDirTextBox.TextChanged += (_, _) => RefreshFileList();

        // Скан папки (включая распаковку архивов) идёт в фоне; TextChanged
        // дебаунсится, чтобы не пересканировать диск на каждый символ пути.
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(400);
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Tick += (_, _) => RefreshFileListAsync().SafeFireAndForget("Analysis.Refresh");

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshFileList();
    }

    /// <summary>Привязка тени блока к общему слою-приёмнику (паттерн из проекта).</summary>
    private void ShadowRect_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Shadow != null && fe.Shadow is Microsoft.UI.Xaml.Media.ThemeShadow ts)
            ts.Receivers.Add(ShadowCastGrid);
    }

    /// <summary>Переключение running-индикатора: виден только во время работы.</summary>
    private void SetProgressRunning(bool running)
    {
        ProcessProgress.IsIndeterminate = running;
        ProcessProgress.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool IsAnalysisRunning() => _analysisInProgress || _htmlInProgress;

    /// <summary>Есть ли активная работа (для закрытия окна).</summary>
    public bool IsBusy => _analysisInProgress || _htmlInProgress;

    /// <summary>Отменяет все текущие операции (для закрытия окна).</summary>
    public void CancelAll()
    {
        _worker?.Cancel();
        _htmlWorker?.Cancel();
    }

    // ──────────────────────────────────────────────
    //  Список файлов
    // ──────────────────────────────────────────────

    // Платформа определяется автоматически по содержимому папки (без выбора пользователем)
    private string _detectedPlatform = "Windows";

    private void UpdatePlatformBar(string key)
    {
        _detectedPlatform = key;
        PlatformInfoBar.Message = $"Платформа: {key} (расширения: {PlatformInfo.ExtsDisplay(key)})";
        PlatformInfoBar.IsOpen = true;
    }

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
        => UiDialogs.PickFolderAsync(InputDirTextBox).SafeFireAndForget("Analysis.BrowseDir");

    private void RefreshFileList()
    {
        // Перезапуск дебаунса: применяется последний вариант пути
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private async Task RefreshFileListAsync()
    {
        var gen = ++_refreshGeneration;
        var inputDir = InputDirTextBox.Text.Trim();

        InputScanner.ScanResult scan;
        try
        {
            scan = await InputScanner.ScanAsync(inputDir);
        }
        catch (Exception e)
        {
            if (gen != _refreshGeneration) return;
            scan = new InputScanner.ScanResult(
                Array.Empty<FileItem>(), new[] { $"Ошибка чтения папки: {e.Message}" });
        }
        if (gen != _refreshGeneration) return; // уже запрошен более свежий скан

        foreach (var err in scan.ArchiveErrors) AppendError(err);
        _cachedFiles = scan.Files.ToList();

        if (_cachedFiles.Count == 0)
        {
            Treemap.SetData(new List<FileItem>());
            GenerateHtmlButton.IsEnabled = false;
            PlatformInfoBar.IsOpen = false;
            return;
        }

        // Платформа - по фактическим файлам (расширения, при их отсутствии - сигнатуры)
        UpdatePlatformBar(PlatformInfo.DetectPlatform(_cachedFiles.Select(f => f.Path)));

        Treemap.SetData(_cachedFiles);
        GenerateHtmlButton.IsEnabled = _cachedFiles.Any(f => ArtifactValidator.HasValidArtifacts(f.Path));
    }

    // ──────────────────────────────────────────────
    //  Запуск анализа
    // ──────────────────────────────────────────────

    private void StartAnalysis_Click(object sender, RoutedEventArgs e)
        => RunSafe(StartAnalysisAsync());

    private async Task StartAnalysisAsync()
    {
        if (_analysisInProgress) return;

        var idatPath = ToolLocator.GetIdaExecutable(_store.Current);
        if (!File.Exists(idatPath))
        {
            await UiDialogs.WarnAsync("Утилита IDA не найдена",
                $"Исполняемый файл '{idatPath}' не найден.\nПроверьте путь в разделе «Конфигурация».");
            return;
        }

        var inputDir = InputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir)) inputDir = _store.Current.DefaultInputDir;

        if (!Directory.Exists(inputDir))
        {
            await UiDialogs.WarnAsync("Ошибка", $"Указанная директория не существует: {inputDir}");
            return;
        }

        var files = _cachedFiles;
        if (files.Count == 0)
        {
            await UiDialogs.InfoAsync("Информация", "Не найдено подходящих файлов.");
            return;
        }

        if (files.Any(f => Path.GetExtension(f.Path).Equals(".dmg", StringComparison.OrdinalIgnoreCase))
            && ArchiveHandler.Find7z() == null)
        {
            await UiDialogs.WarnAsync("Требуется 7z",
                "Для обработки .dmg файлов необходим 7-Zip.\nУбедитесь, что '7z' доступен в системном PATH.");
            return;
        }

        var withIdb = files.Where(f => ArtifactValidator.IsValidIdaDatabase(f.ExpectedI64Path, out _)).ToList();
        var withoutIdb = files.Where(f => !ArtifactValidator.IsValidIdaDatabase(f.ExpectedI64Path, out _)).ToList();

        var exportOnly = false;
        if (withIdb.Count > 0)
        {
            var action = await UiDialogs.AskExistingBasesAsync(
                "Обнаружены существующие базы данных",
                $"Готово баз: {withIdb.Count} из {files.Count}\nТребуют анализа: {withoutIdb.Count}");
            switch (action)
            {
                case "cancel": return;
                case "continue":
                    if (withoutIdb.Count == 0)
                    {
                        await UiDialogs.InfoAsync("Информация", "Все файлы уже проанализированы.");
                        files = withIdb;
                        exportOnly = true;
                    }
                    else
                    {
                        files = withoutIdb;
                        _exportAllAfterAnalysis = true;
                    }
                    break;
                case "overwrite": exportOnly = false; break;
                case "export": files = withIdb; exportOnly = true; break;
            }
        }

        if (files.Count == 0)
        {
            await UiDialogs.InfoAsync("Информация", "Нет файлов для обработки.");
            return;
        }

        _analysisInProgress = true;
        StartAnalysisButton.IsEnabled = false;
        DetailsButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        GenerateHtmlButton.IsEnabled = false;
        ProcessStatusText.Text = exportOnly ? "Фаза: экспорт в JSON..." : "Фаза: анализ файлов...";
        SetProgressRunning(true);   // running-индикатор
        ErrorLogTextBox.Text = "";

        // Очередь файлов в мини-диспетчере задач
        AnalysisMonitor.BeginSession(files.Select(f => f.Path), (int)MaxIdaSlider.Value);

        _worker = new AnalysisWorker(
            files.Select(f => f.Path).ToList(),
            idatPath,
            (int)MaxIdaSlider.Value,
            null,
            CleanupCheck.IsChecked == true,
            TempCleanupCheck.IsChecked == true,
            PseudocodeCheck.IsChecked == true,
            DeleteJsonCheck.IsChecked == true,
            exportOnly);
        HookWorker(_worker);
        _worker.Start();
    }

    private void HookWorker(AnalysisWorker w)
    {
        w.PhaseChanged += phase => RunOnUi(() =>
        {
            ProcessStatusText.Text = phase == "analysis" ? "Фаза: анализ файлов..." : "Фаза: экспорт в JSON...";
            AppendLog($"[Фаза] {ProcessStatusText.Text}");
        });
        w.ProcessStarted += (name, pid, tid) => RunOnUi(() =>
        {
            AnalysisMonitor.SetProcessInfo(name, pid, tid);
            AppendLog($"Запущен процесс: {name} (PID {pid})");
        });
        w.PulseUpdated += (name, text) => RunOnUi(() =>
        {
            // Пульс: жив/работает/затих - в строку статуса и в диспетчер задач.
            AnalysisMonitor.SetPulse(name, text);
            ProcessStatusText.Text = text;
        });
        w.AnalysisProgress += (name, cur, total) => RunOnUi(() =>
        {
            ProcessStatusText.Text = $"Анализ: {cur}/{total} - {name}";
        });
        w.AnalysisFileStarted += name => RunOnUi(() =>
        {
            SetFileStatusByName(name, AnalysisStatus.InProgress);
            AnalysisMonitor.MarkRunning(name, "analysis");
            AppendLog($"[Анализ] Начало: {name}");
        });
        w.AnalysisFileCompleted += (name, ok) => RunOnUi(() =>
        {
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error);
            AnalysisMonitor.MarkCompleted(name, ok);
            AppendLog($"[Анализ] {name} - {(ok ? "успешно" : "ошибка")}");
        });
        w.ExportFileStarted += name => RunOnUi(() =>
        {
            AnalysisMonitor.MarkRunning(name, "export");
            AppendLog($"[Экспорт] Начало: {name}");
        });
        w.ExportProgress += (name, cur, total) => RunOnUi(() =>
        {
            ProcessStatusText.Text = $"Экспорт: {cur}/{total} - {name}";
        });
        w.ExportFileCompleted += (name, ok) => RunOnUi(() =>
        {
            // Финальный статус файла - по результату экспорта, а не по созданию базы.
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error);
            if (!ok) AppendError($"Ошибка экспорта для {name}");
            else AppendLog($"[Экспорт] {name} - успешно");
            AnalysisMonitor.MarkCompleted(name, ok);
        });
        w.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        w.Finished += (ok, total) => RunOnUi(() => OnAnalysisFinished(ok, total));
    }

    private void SetFileStatusByName(string fileName, AnalysisStatus status)
    {
        var match = _cachedFiles.FirstOrDefault(f => f.Name == fileName);
        if (match != null) Treemap.UpdateStatus(match.Path, status);
    }

    private void OnAnalysisFinished(int succeeded, int total)
    {
        _analysisInProgress = false;
        StartAnalysisButton.IsEnabled = true;
        DetailsButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ProcessStatusText.Text = $"Завершено. Успешно: {succeeded}/{total}";
        SetProgressRunning(false);  // вернуть в простой
        AppNotifier.Notify("Анализ завершён",
            $"Успешно: {succeeded}/{total}.");
        AnalysisMonitor.EndSession();

        if (_exportAllAfterAnalysis && succeeded > 0)
        {
            _exportAllAfterAnalysis = false;
            ProcessStatusText.Text = "Автоматический экспорт всех баз...";
            var allIdb = _cachedFiles
                .Where(f => ArtifactValidator.IsValidIdaDatabase(f.ExpectedI64Path, out _))
                .Select(f => f.ExpectedI64Path).ToList();
            if (allIdb.Count > 0)
            {
                _worker?.Dispose();
                _worker = null;
                StartExportOnlyAsync(allIdb).SafeFireAndForget("Analysis.ExportOnly");
                return;
            }
            ProcessStatusText.Text = "Нет готовых баз для экспорта";
        }

        var inputDir = InputDirTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(inputDir) && Directory.Exists(inputDir))
        {
            var anyJson = ExecutableFinder.SafeEnumerateFiles(inputDir).Any(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase));
            GenerateHtmlButton.IsEnabled = anyJson;
        }
        _worker?.Dispose();
        _worker = null;
        RefreshFileList();
    }

    private Task StartExportOnlyAsync(List<string> idbFiles)
    {
        var idatPath = ToolLocator.GetIdaExecutable(_store.Current);
        var script = Path.Combine(AppConstants.ScriptsDir, "export_data.py");
        if (!File.Exists(script))
        {
            AppendError($"Скрипт экспорта не найден: {script}");
            return Task.CompletedTask;
        }
        ProcessStatusText.Text = "Фаза: экспорт в JSON...";
        ProcessProgress.Value = 0;
        DetailsButton.IsEnabled = true;
        // Новая сессия в мини-диспетчере: имена баз без суффикса .i64
        AnalysisMonitor.BeginSession(idbFiles, (int)MaxIdaSlider.Value);
        var worker = new AnalysisWorker(idbFiles, idatPath, (int)MaxIdaSlider.Value, null,
            false, false, PseudocodeCheck.IsChecked == true, false, true);
        HookWorker(worker);
        _worker = worker;
        worker.Start();
        return Task.CompletedTask;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _worker?.Cancel();
        _htmlWorker?.Cancel();
        ProcessStatusText.Text = "Отмена...";
        CancelButton.IsEnabled = false;
    }

    /// <summary>Открыть окно «мини-диспетчер задач» (или вывести существующее на передний план).</summary>
    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_taskManagerWindow != null)
        {
            _taskManagerWindow.Activate();
            return;
        }
        _taskManagerWindow = new TaskManagerWindow();
        _taskManagerWindow.Closed += (_, _) => _taskManagerWindow = null;
        _taskManagerWindow.Activate();
    }

    private void AppendError(string message)
    {
        AppLog.Error(message);
        AppendLog(message);
    }

    /// <summary>Журнал выполнения: дублирует в «Лог выполнения» любые события
    /// (этапы, файлы, экспорт, ошибки), не только ошибки.</summary>
    private void AppendLog(string message) => UiLog.Append(ErrorLogTextBox, message);

    private void RunOnUi(Action a) => UiThread.Run(DispatcherQueue, a);

    private void RunOnUi(Func<Task> a) => UiThread.Run(DispatcherQueue, a, "Analysis.RunOnUi");

    /// <summary>Запуск задачи страницы: исключение попадает в журнал страницы и
    /// AppLog, а не роняет процесс (см. TaskExtensions.SafeFireAndForget).</summary>
    private void RunSafe(Task task, [CallerMemberName] string? context = null)
        => task.SafeFireAndForget(context ?? "Analysis",
            ex => RunOnUi(() => AppendError($"{context}: {ex.Message}")));

    // ──────────────────────────────────────────────
    //  HTML-отчёты
    // ──────────────────────────────────────────────

    /// <summary>Инфобар-ворнинг: потоки на максимуме аппаратных возможностей.</summary>
    private void UpdateThreadsWarning()
        => ThreadsWarningBar.IsOpen = (int)MaxIdaSlider.Value >= (int)MaxIdaSlider.Maximum;

    private void PseudocodeInfoButton_Click(object sender, RoutedEventArgs e)
        => PseudocodeTip.IsOpen = !PseudocodeTip.IsOpen;

    private void GenerateHtml_Click(object sender, RoutedEventArgs e)
        => RunSafe(GenerateHtmlAsync());

    private async Task GenerateHtmlAsync()
    {
        if (_htmlInProgress) return;

        var inputDir = InputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папка не найдена.");
            return;
        }

        var jsonFiles = ExecutableFinder.SafeEnumerateFiles(inputDir)
            .Where(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (jsonFiles.Length == 0)
        {
            await UiDialogs.WarnAsync("Ошибка", "Нет JSON-файлов экспорта. Сначала выполните анализ.");
            return;
        }

        var reportsDir = Path.Combine(inputDir, AppConstants.IdaReportsDirName);
        Directory.CreateDirectory(reportsDir);

        _htmlInProgress = true;
        StartAnalysisButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        GenerateHtmlButton.IsEnabled = false;
        SetProgressRunning(true);   // running-индикатор
        ProcessStatusText.Text = "Генерация HTML-отчётов...";
        ErrorLogTextBox.Text = "";

        _htmlWorker = new HtmlGenWorker("analysis", DeleteJsonCheck.IsChecked == true, false, _detectedPlatform);
        _htmlWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            ProcessStatusText.Text = msg.Length > 0
                ? $"Генерация HTML: {cur}/{total} {msg}"
                : $"Генерация HTML: {cur}/{total}";
        });
        _htmlWorker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        _htmlWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            if (msg.Length > 0) AppendLog($"[HTML] {msg}");
        });
        _htmlWorker.Finished += result => RunOnUi(() => OnHtmlFinishedAsync(result));

        await _htmlWorker.RunAsync(inputDir, reportsDir, inputDir, null, null, null, jsonFiles);
    }

    private async Task OnHtmlFinishedAsync(HtmlGenResult result)
    {
        _htmlInProgress = false;
        StartAnalysisButton.IsEnabled = true;
        CancelButton.IsEnabled = false;
        GenerateHtmlButton.IsEnabled = true;
        SetProgressRunning(false);  // вернуть в простой
        ProcessStatusText.Text = "Готово";
        _htmlWorker?.Dispose();
        _htmlWorker = null;

        CleanupAfterReport();

        var reportsDir = result.ReportsDir;
        if (string.IsNullOrEmpty(reportsDir)) reportsDir = Path.Combine(InputDirTextBox.Text.Trim(), AppConstants.IdaReportsDirName);

        // Диалог: предложить открыть папку с отчётами
        var open = await UiDialogs.AskButtonsAsync("HTML-отчёты готовы",
            reportsDir,
            new[] { "Открыть папку", "ОК" },
            defaultIndex: 0);
        if (open == 0 && Directory.Exists(reportsDir))
        {
            Windows.System.Launcher.LaunchFolderPathAsync(reportsDir)
                .AsTask().SafeFireAndForget("Analysis.LaunchReports");
        }
    }

    /// <summary>
    /// Удаляет временные файлы (.asm, .log, .id0, .id1, .nam, .til)
    /// в зависимости от флагов - аналог clean_directory из исполнения 1.
    /// </summary>
    private void CleanupAfterReport()
    {
        var inputDir = InputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir)) return;

        var includeLogs = CleanupCheck.IsChecked == true;
        var includeTemps = TempCleanupCheck.IsChecked == true;
        if (!includeLogs && !includeTemps) return;

        // Удаляем только артефакты конкретных обработанных файлов, а не все
        // *.log/*.asm рекурсивно: в папке могут лежать пользовательские файлы.
        foreach (var item in _cachedFiles)
            ArtifactCleanup.DeleteKnownArtifacts(item.Path, includeLogs, includeTemps);
    }

    // ──────────────────────────────────────────────
    //  Диалог существующих баз
    // ──────────────────────────────────────────────

}