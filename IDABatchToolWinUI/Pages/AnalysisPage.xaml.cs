using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Общий анализ» — аналог AnalysisPage из исполнения 1 (Qt).
/// </summary>
public sealed partial class AnalysisPage : Page
{
    private readonly AppConfig _cfg;
    private bool _analysisInProgress;
    private bool _htmlInProgress;
    private AnalysisWorker? _worker;
    private HtmlGenWorker? _htmlWorker;
    private List<FileItem> _cachedFiles = new();
    private bool _exportAllAfterAnalysis;
    private TaskManagerWindow? _taskManagerWindow;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _refreshTimer;
    private int _refreshGeneration;

    public AnalysisPage()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();
        MaxIdaSlider.Value = _cfg.MaxIda;

        // Подписки — один раз в конструкторе, чтобы при повторном показе страницы
        // (кэш навигации) обработчики не дублировались.
        BrowseDirButton.Click += BrowseDir_Click;
        StartAnalysisButton.Click += StartAnalysis_Click;
        DetailsButton.Click += DetailsButton_Click;
        CancelButton.Click += Cancel_Click;
        GenerateHtmlButton.Click += GenerateHtml_Click;
        InputDirTextBox.TextChanged += (_, _) => RefreshFileList();

        // Скан папки (включая распаковку архивов) идёт в фоне; TextChanged
        // дебаунсится, чтобы не пересканировать диск на каждый символ пути.
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(400);
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Tick += (_, _) => _ = RefreshFileListAsync();

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

    private async void BrowseDir_Click(object sender, RoutedEventArgs e)
        => await UiDialogs.PickFolderAsync(InputDirTextBox);

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

        List<FileItem> files;
        List<string> errors;
        try
        {
            (files, errors) = await Task.Run(() => ScanInputFiles(inputDir));
        }
        catch (Exception e)
        {
            if (gen != _refreshGeneration) return;
            files = new List<FileItem>();
            errors = new List<string> { $"Ошибка чтения папки: {e.Message}" };
        }
        if (gen != _refreshGeneration) return; // уже запрошен более свежий скан

        foreach (var err in errors) AppendError(err);
        _cachedFiles = files;

        if (_cachedFiles.Count == 0)
        {
            Treemap.SetData(new List<FileItem>());
            GenerateHtmlButton.IsEnabled = false;
            PlatformInfoBar.IsOpen = false;
            return;
        }

        // Платформа — по фактическим файлам (расширения, при их отсутствии — сигнатуры)
        UpdatePlatformBar(PlatformInfo.DetectPlatform(_cachedFiles.Select(f => f.Path)));

        Treemap.SetData(_cachedFiles);
        GenerateHtmlButton.IsEnabled = _cachedFiles.Any(f => File.Exists(f.ExpectedI64Path));
    }

    /// <summary>Поиск исполняемых файлов и распаковка архивов — вне UI-потока.</summary>
    private static (List<FileItem> Files, List<string> Errors) ScanInputFiles(string inputDir)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
            return (new List<FileItem>(), errors);

        // Поиск сразу по всем расширениям всех платформ: платформа определяется
        // по фактическому содержимому папки, а не по заранее выбранной настройке.
        // Раньше поиск шёл по расширениям текущей платформы, поэтому папка,
        // например, только с .so-файлами давала пустой список и детект не выполнялся.
        var extensions = PlatformInfo.AllExtensions();
        var files = ExecutableFinder.FindExecutables(inputDir, extensions);

        // Архивы: распаковываем рядом и ищем внутри
        foreach (var ext in ArchiveHandler.ArchiveExtensions)
        {
            string[] archives;
            try { archives = Directory.GetFiles(inputDir, "*" + ext); }
            catch { continue; /* каталог недоступен для чтения */ }

            foreach (var archive in archives)
            {
                var extracted = ArchiveHandler.ExtractArchive(archive);
                if (extracted != null && Directory.Exists(extracted))
                {
                    var archFiles = ExecutableFinder.FindExecutables(extracted, extensions);
                    files.AddRange(archFiles);
                }
                else if (ext == ".dmg")
                {
                    errors.Add($"Не удалось извлечь {Path.GetFileName(archive)}. Убедитесь, что 7z установлен и доступен в PATH.");
                }
            }
        }

        return (files.Distinct().Select(MakeItem).ToList(), errors);
    }

    private static FileItem MakeItem(string path)
    {
        long size = 0;
        try { size = new FileInfo(path).Length; }
        catch { /* файл исчез или недоступен — размер 0 */ }

        return new FileItem
        {
            Name = Path.GetFileName(path),
            Path = Path.GetFullPath(path),
            Size = size,
            Status = File.Exists(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".i64"))
                ? AnalysisStatus.Success
                : AnalysisStatus.NotAnalyzed,
        };
    }

    // ──────────────────────────────────────────────
    //  Запуск анализа
    // ──────────────────────────────────────────────

    private async void StartAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (_analysisInProgress) return;

        var idatPath = ToolLocator.GetIdaExecutable(_cfg);
        if (!File.Exists(idatPath))
        {
            await UiDialogs.WarnAsync("Утилита IDA не найдена",
                $"Исполняемый файл '{idatPath}' не найден.\nПроверьте путь в разделе «Конфигурация».");
            return;
        }

        var inputDir = InputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir)) inputDir = _cfg.DefaultInputDir;

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

        var withIdb = files.Where(f => File.Exists(f.ExpectedI64Path)).ToList();
        var withoutIdb = files.Where(f => !File.Exists(f.ExpectedI64Path)).ToList();

        var exportOnly = false;
        if (withIdb.Count > 0)
        {
            var action = await AskExistingBasesAsync(withIdb.Count, files.Count, withoutIdb.Count);
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
        AnalysisMonitor.BeginSession(files.Select(f => f.Name), (int)MaxIdaSlider.Value);

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
            AnalysisMonitor.SetProcessInfo(AnalysisMonitor.NormalizeName(name), pid, tid);
            AppendLog($"Запущен процесс: {name} (PID {pid})");
        });
        w.AnalysisProgress += (name, cur, total) => RunOnUi(() =>
        {
            ProcessStatusText.Text = $"Анализ: {cur}/{total} – {name}";
        });
        w.AnalysisFileStarted += name => RunOnUi(() =>
        {
            SetFileStatusByName(name, AnalysisStatus.InProgress);
            AnalysisMonitor.MarkRunning(AnalysisMonitor.NormalizeName(name), "analysis");
            AppendLog($"[Анализ] Начало: {name}");
        });
        w.AnalysisFileCompleted += (name, ok) => RunOnUi(() =>
        {
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error);
            AnalysisMonitor.MarkCompleted(AnalysisMonitor.NormalizeName(name), ok);
            AppendLog($"[Анализ] {name} — {(ok ? "успешно" : "ошибка")}");
        });
        w.ExportFileStarted += name => RunOnUi(() =>
        {
            AnalysisMonitor.MarkRunning(AnalysisMonitor.NormalizeName(name), "export");
            AppendLog($"[Экспорт] Начало: {name}");
        });
        w.ExportProgress += (name, cur, total) => RunOnUi(() =>
        {
            ProcessStatusText.Text = $"Экспорт: {cur}/{total} – {name}";
        });
        w.ExportFileCompleted += (name, ok) => RunOnUi(() =>
        {
            if (!ok) AppendError($"Ошибка экспорта для {name}");
            else AppendLog($"[Экспорт] {name} — успешно");
            AnalysisMonitor.MarkCompleted(AnalysisMonitor.NormalizeName(name), ok);
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
        ProcessStatusText.Text = $"Завершено. Обработано: {succeeded}/{total}";
        SetProgressRunning(false);  // вернуть в простой
        AppNotifier.Notify("Анализ завершён",
            $"Обработано: {succeeded}/{total}.");
        AnalysisMonitor.EndSession();

        if (_exportAllAfterAnalysis && succeeded > 0)
        {
            _exportAllAfterAnalysis = false;
            ProcessStatusText.Text = "Автоматический экспорт всех баз...";
            var allIdb = _cachedFiles.Where(f => File.Exists(f.ExpectedI64Path)).Select(f => f.ExpectedI64Path).ToList();
            if (allIdb.Count > 0)
            {
                _ = StartExportOnlyAsync(allIdb);
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
        _worker = null;
        RefreshFileList();
    }

    private Task StartExportOnlyAsync(List<string> idbFiles)
    {
        var idatPath = ToolLocator.GetIdaExecutable(_cfg);
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
        AnalysisMonitor.BeginSession(idbFiles.Select(p => AnalysisMonitor.NormalizeName(p)), (int)MaxIdaSlider.Value);
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

    private void AppendError(string message) => AppendLog(message);

    /// <summary>Журнал выполнения: дублирует в «Лог выполнения» любые события
    /// (этапы, файлы, экспорт, ошибки), не только ошибки.</summary>
    private void AppendLog(string message)
    {
        if (string.IsNullOrEmpty(ErrorLogTextBox.Text)) ErrorLogTextBox.Text = message;
        else ErrorLogTextBox.Text += Environment.NewLine + message;
    }

    private void RunOnUi(Action a) => DispatcherQueue.TryEnqueue(() => a());

    // ──────────────────────────────────────────────
    //  HTML-отчёты
    // ──────────────────────────────────────────────

    private async void GenerateHtml_Click(object sender, RoutedEventArgs e)
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

        var reportsDir = Path.Combine(inputDir, "IDAReports");
        Directory.CreateDirectory(reportsDir);

        _htmlInProgress = true;
        StartAnalysisButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
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
        _htmlWorker.Finished += result => RunOnUi(() => OnHtmlFinished(result));

        await Task.Run(() => _htmlWorker.Run(inputDir, reportsDir, inputDir, null, null, null, jsonFiles));
    }

    private async void OnHtmlFinished(HtmlGenResult result)
    {
        _htmlInProgress = false;
        StartAnalysisButton.IsEnabled = true;
        CancelButton.IsEnabled = false;
        GenerateHtmlButton.IsEnabled = true;
        SetProgressRunning(false);  // вернуть в простой
        ProcessStatusText.Text = "Готово";
        _htmlWorker = null;

        CleanupAfterReport();

        var reportsDir = result.ReportsDir;
        if (string.IsNullOrEmpty(reportsDir)) reportsDir = Path.Combine(InputDirTextBox.Text.Trim(), "IDAReports");

        // Диалог: предложить открыть папку с отчётами
        var open = await UiDialogs.AskButtonsAsync("HTML-отчёты готовы",
            reportsDir,
            new[] { "Открыть папку", "ОК" },
            defaultIndex: 0);
        if (open == 0 && Directory.Exists(reportsDir))
        {
            _ = Windows.System.Launcher.LaunchFolderPathAsync(reportsDir);
        }
    }

    /// <summary>
    /// Удаляет временные файлы (.asm, .log, .id0, .id1, .nam, .til)
    /// в зависимости от флагов — аналог clean_directory из исполнения 1.
    /// </summary>
    private void CleanupAfterReport()
    {
        var inputDir = InputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir)) return;

        var patterns = new List<string>();
        if (CleanupCheck.IsChecked == true) patterns.AddRange(new[] { "*.asm", "*.log" });
        if (TempCleanupCheck.IsChecked == true) patterns.AddRange(new[] { "*.id0", "*.id1", "*.nam", "*.til" });
        if (patterns.Count == 0) return;

        foreach (var pattern in patterns)
        {
            try
            {
                foreach (var f in ExecutableFinder.SafeEnumerateFiles(inputDir).Where(x => x.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)))
                {
                    try { File.Delete(f); } catch { /* файл занят */ }
                }
            }
            catch { /* каталог недоступен */ }
        }
    }

    // ──────────────────────────────────────────────
    //  Диалог существующих баз
    // ──────────────────────────────────────────────

    private static async Task<string> AskExistingBasesAsync(int ready, int total, int needAnalysis)
    {
        var idx = await UiDialogs.AskButtonsAsync(
            "Обнаружены существующие базы данных",
            $"Готово баз: {ready} из {total}\nТребуют анализа: {needAnalysis}",
            new[] { "Доанализировать новые", "Перезаписать всё", "Экспортировать существующие", "Отмена" },
            defaultIndex: 0);
        return idx switch
        {
            0 => "continue",
            1 => "overwrite",
            2 => "export",
            _ => "cancel",
        };
    }
}