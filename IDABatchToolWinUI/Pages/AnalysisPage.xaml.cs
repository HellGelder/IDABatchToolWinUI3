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

    public AnalysisPage()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();
        MaxIdaSlider.Value = _cfg.MaxIda;

        // Подписки — один раз в конструкторе, чтобы при повторном показе страницы
        // (кэш навигации) обработчики не дублировались.
        BrowseDirButton.Click += BrowseDir_Click;
        StartAnalysisButton.Click += StartAnalysis_Click;
        CancelButton.Click += Cancel_Click;
        GenerateHtmlButton.Click += GenerateHtml_Click;
        InputDirTextBox.TextChanged += (_, _) => RefreshFileList();

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

    private string? SelectedPlatformKey()
    {
        if (PlatformWindows.IsChecked == true) return "Windows";
        if (PlatformLinuxAndroid.IsChecked == true) return "Linux / Android";
        if (PlatformMacIos.IsChecked == true) return "macOS / iOS";
        return null;
    }

    private string[] SelectedExtensions() => PlatformInfo.ExtsFor(SelectedPlatformKey());

    private void SetPlatformRadio(string key)
    {
        if (key == "Windows") PlatformWindows.IsChecked = true;
        else if (key == "Linux / Android") PlatformLinuxAndroid.IsChecked = true;
        else if (key == "macOS / iOS") PlatformMacIos.IsChecked = true;
    }

    private async void BrowseDir_Click(object sender, RoutedEventArgs e)
        => await UiDialogs.PickFolderAsync(InputDirTextBox);

    private void RefreshFileList()
    {
        var inputDir = InputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
        {
            _cachedFiles = new List<FileItem>();
            Treemap.SetData(_cachedFiles);
            GenerateHtmlButton.IsEnabled = false;
            return;
        }

        var extensions = SelectedExtensions();
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
                    AppendError($"Не удалось извлечь {Path.GetFileName(archive)}. Убедитесь, что 7z установлен и доступен в PATH.");
                }
            }
        }

        _cachedFiles = files.Distinct().Select(MakeItem).ToList();

        if (_cachedFiles.Count == 0)
        {
            Treemap.SetData(new List<FileItem>());
            GenerateHtmlButton.IsEnabled = false;
            return;
        }

        var detected = PlatformInfo.DetectByFiles(_cachedFiles.Select(f => Path.GetExtension(f.Path).ToLowerInvariant()));
        SetPlatformRadio(detected);

        // Повторный поиск, если расширения изменились после определения платформы
        var newExts = SelectedExtensions();
        if (!newExts.SequenceEqual(extensions))
        {
            var refiles = ExecutableFinder.FindExecutables(inputDir, newExts);
            _cachedFiles = refiles.Distinct().Select(MakeItem).ToList();
        }

        Treemap.SetData(_cachedFiles);
        GenerateHtmlButton.IsEnabled = _cachedFiles.Any(f => File.Exists(f.ExpectedI64Path));
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
        CancelButton.IsEnabled = true;
        GenerateHtmlButton.IsEnabled = false;
        ProcessStatusText.Text = exportOnly ? "Фаза: экспорт в JSON..." : "Фаза: анализ файлов...";
        SetProgressRunning(true);   // running-индикатор
        ErrorLogTextBox.Text = "";

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
            ProcessStatusText.Text = phase == "analysis" ? "Фаза: анализ файлов..." : "Фаза: экспорт в JSON...");
        w.AnalysisProgress += (name, cur, total) => RunOnUi(() =>
        {
            ProcessStatusText.Text = $"Анализ: {cur}/{total} – {name}";
        });
        w.AnalysisFileStarted += name => RunOnUi(() => SetFileStatusByName(name, AnalysisStatus.InProgress));
        w.AnalysisFileCompleted += (name, ok) => RunOnUi(() =>
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error));
        w.ExportProgress += (name, cur, total) => RunOnUi(() =>
        {
            ProcessStatusText.Text = $"Экспорт: {cur}/{total} – {name}";
        });
        w.ExportFileCompleted += (name, ok) => RunOnUi(() =>
        {
            if (!ok) AppendError($"Ошибка экспорта для {name}");
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
        CancelButton.IsEnabled = false;
        ProcessStatusText.Text = $"Завершено. Обработано: {succeeded}/{total}";
        SetProgressRunning(false);  // вернуть в простой
        AppNotifier.Notify("Анализ завершён",
            $"Обработано: {succeeded}/{total}.");

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

    private void AppendError(string message)
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

        _htmlWorker = new HtmlGenWorker("analysis", DeleteJsonCheck.IsChecked == true, false, SelectedPlatformKey() ?? "Windows");
        _htmlWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            ProcessStatusText.Text = msg.Length > 0
                ? $"Генерация HTML: {cur}/{total} {msg}"
                : $"Генерация HTML: {cur}/{total}";
        });
        _htmlWorker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
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