using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Анализ СФ» (системных функций) — аналог SfaPage из исполнения 1.
/// </summary>
public sealed partial class SfaPage : Page
{
    private readonly AppConfig _cfg;
    private bool _analysisInProgress;
    private bool _analysisCancelled;
    private bool _htmlInProgress;
    private bool _htmlCancelled;
    private bool _docsInProgress;
    private bool _docsCancelled;
    private AnalysisWorker? _worker;
    private HtmlGenWorker? _htmlWorker;
    private HtmlGenWorker? _docsWorker;
    private List<FileItem> _cachedFiles = new();
    private bool _exportAllAfterAnalysis;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _refreshTimer;
    private int _refreshGeneration;

public SfaPage()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();
        SfaMaxIdaSlider.Value = _cfg.MaxIda;

        // Подписки — один раз в конструкторе, чтобы при повторном показе
        // не дублировались (страница кэшируется в MainWindow).
        SfaBrowseDirButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(SfaInputDirTextBox);
        SfaStartButton.Click += StartAnalysis_Click;
        SfaCancelButton.Click += (_, _) => CancelAnalysis();
        SfaGenerateHtmlButton.Click += GenerateHtml_Click;
        SfaInputDirTextBox.TextChanged += (_, _) => RefreshFileList();

        // Скан папки — в фоне с дебаунсом TextChanged (как на вкладке «Общий анализ»).
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(400);
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Tick += (_, _) => _ = RefreshFileListAsync();

        Loaded += (_, _) => RefreshFileList();
    }

    public bool IsAnalysisRunning() => _analysisInProgress || _htmlInProgress || _docsInProgress;

    /// <summary>Индикатор занятости (как SetProgressRunning на вкладке
    /// «Общий анализ»): пока идёт любая фаза — полоса анимируется, точные
    /// счётчики видны в строке статуса над ней. Без анимации полоса стоит
    /// на месте, пока IDA/npx обрабатывает очередной файл, и выглядит «замороженной».</summary>
    private void SetSfaProgressRunning(bool running)
    {
        SfaProcessProgress.IsIndeterminate = running;
    }

    // ──────────────────────────────────────────────
    //  Список файлов
    // ──────────────────────────────────────────────

    // Платформа определяется автоматически по содержимому папки (без выбора пользователем)
    private string _detectedPlatform = "Windows";

    private void UpdatePlatformBar(string key)
    {
        _detectedPlatform = key;
        SfaPlatformInfoBar.Message = $"Платформа: {key} (расширения: {PlatformInfo.ExtsDisplay(key)})";
        SfaPlatformInfoBar.IsOpen = true;
    }

    private void RefreshFileList()
    {
        // Перезапуск дебаунса: применяется последний вариант пути
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private async Task RefreshFileListAsync()
    {
        var gen = ++_refreshGeneration;
        var inputDir = SfaInputDirTextBox.Text.Trim();

        List<FileItem> files;
        try
        {
            files = await Task.Run(() => ScanInputFiles(inputDir));
        }
        catch (Exception)
        {
            if (gen != _refreshGeneration) return;
            files = new List<FileItem>();
        }
        if (gen != _refreshGeneration) return; // уже запрошен более свежий скан

        _cachedFiles = files;

        if (_cachedFiles.Count == 0)
        {
            SfaTreemap.SetData(new List<FileItem>());
            SfaGenerateHtmlButton.IsEnabled = false;
            SfaPlatformInfoBar.IsOpen = false;
            return;
        }

        // Платформа — по фактическим файлам (расширения, при их отсутствии — сигнатуры)
        UpdatePlatformBar(PlatformInfo.DetectPlatform(_cachedFiles.Select(f => f.Path)));

        SfaTreemap.SetData(_cachedFiles);
        SfaGenerateHtmlButton.IsEnabled = _cachedFiles.Any(f => File.Exists(f.ExpectedI64Path));
    }

    /// <summary>Поиск исполняемых файлов и распаковка архивов — вне UI-потока.</summary>
    private static List<FileItem> ScanInputFiles(string inputDir)
    {
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
            return new List<FileItem>();

        // Поиск сразу по всем расширениям всех платформ: платформа определяется
        // по фактическому содержимому папки. Раньше поиск шёл по расширениям
        // текущей платформы, поэтому папка только с .so-файлами давала пустой
        // список и детект не выполнялся.
        var extensions = PlatformInfo.AllExtensions();
        var files = ExecutableFinder.FindExecutables(inputDir, extensions);

        foreach (var ext in ArchiveHandler.ArchiveExtensions)
        {
            string[] archives;
            try { archives = Directory.GetFiles(inputDir, "*" + ext); }
            catch { continue; /* каталог недоступен для чтения */ }

            foreach (var archive in archives)
            {
                var extracted = ArchiveHandler.ExtractArchive(archive);
                if (extracted != null && Directory.Exists(extracted))
                    files.AddRange(ExecutableFinder.FindExecutables(extracted, extensions));
            }
        }

        return files.Distinct().Select(MakeItem).ToList();
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
    //  Анализ
    // ──────────────────────────────────────────────

    private async void StartAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (_analysisInProgress) return;

        var idatPath = ToolLocator.GetIdaExecutable(_cfg);
        if (!File.Exists(idatPath))
        {
            await UiDialogs.WarnAsync("IDA не найдена", $"{idatPath} не найден.\nПроверьте конфигурацию.");
            return;
        }

        var inputDir = SfaInputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir)) inputDir = _cfg.DefaultInputDir;
        if (!Directory.Exists(inputDir))
        {
            await UiDialogs.WarnAsync("Ошибка", $"Директория не существует: {inputDir}");
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
            await UiDialogs.WarnAsync("Требуется 7z", "Для DMG необходим 7-Zip.");
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
                    else { files = withoutIdb; _exportAllAfterAnalysis = true; }
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
        _analysisCancelled = false;
        SfaStartButton.IsEnabled = false;
        SfaCancelButton.IsEnabled = true;
        SfaGenerateHtmlButton.IsEnabled = false;
        SfaProcessStatusText.Text = exportOnly ? "Фаза: экспорт в JSON..." : "Фаза: анализ файлов...";
        SfaProcessProgress.Value = 0;
        SetSfaProgressRunning(true);
        SfaErrorLogTextBox.Text = "";

        _worker = new AnalysisWorker(
            files.Select(f => f.Path).ToList(), idatPath, (int)SfaMaxIdaSlider.Value, null,
            SfaCleanupCheck.IsChecked == true, SfaTempCleanupCheck.IsChecked == true,
            SfaPseudocodeCheck.IsChecked == true, SfaDeleteJsonCheck.IsChecked == true,
            exportOnly);
        HookWorker(_worker);
        _worker.Start();
    }

    private void HookWorker(AnalysisWorker w)
    {
        w.PhaseChanged += phase => RunOnUi(() =>
            SfaProcessStatusText.Text = phase == "analysis" ? "Фаза: анализ файлов..." : "Фаза: экспорт в JSON...");
        w.AnalysisProgress += (name, cur, total) => RunOnUi(() =>
        {
            SfaProcessStatusText.Text = $"Анализ: {cur}/{total} – {name}";
            if (total > 0) SfaProcessProgress.Value = 100.0 * cur / total;
        });
        w.AnalysisFileStarted += name => RunOnUi(() => SetFileStatusByName(name, AnalysisStatus.InProgress));
        w.AnalysisFileCompleted += (name, ok) => RunOnUi(() =>
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error));
        w.ExportProgress += (name, cur, total) => RunOnUi(() =>
        {
            SfaProcessStatusText.Text = $"Экспорт: {cur}/{total} – {name}";
            if (total > 0) SfaProcessProgress.Value = 100.0 * cur / total;
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
        if (match != null) SfaTreemap.UpdateStatus(match.Path, status);
    }

    private void OnAnalysisFinished(int succeeded, int total)
    {
        _analysisInProgress = false;
        SfaStartButton.IsEnabled = true;
        SfaCancelButton.IsEnabled = false;
        SfaProcessStatusText.Text = $"Завершено. Обработано: {succeeded}/{total}";
        SfaProcessProgress.Value = 100;
        SetSfaProgressRunning(false);

        if (_exportAllAfterAnalysis && succeeded > 0)
        {
            _exportAllAfterAnalysis = false;
            SfaProcessStatusText.Text = "Автоматический экспорт всех баз...";
            var allIdb = _cachedFiles.Where(f => File.Exists(f.ExpectedI64Path)).Select(f => f.ExpectedI64Path).ToList();
            if (allIdb.Count > 0) { _ = StartExportOnlyAsync(allIdb); return; }
            SfaProcessStatusText.Text = "Нет готовых баз для экспорта";
        }

        var inputDir = SfaInputDirTextBox.Text.Trim();
        var anyJson = !string.IsNullOrEmpty(inputDir) && Directory.Exists(inputDir) &&
            ExecutableFinder.SafeEnumerateFiles(inputDir).Any(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase));
        SfaGenerateHtmlButton.IsEnabled = anyJson;
        _worker = null;
        RefreshFileList();

        // Поиск документации MS Learn — фаза кнопки «Запустить анализ СФ»
        // (после анализа и экспорта), чтобы генерация отчёта шла по кэшу
        // без вызовов npx. Linux/Android использует man-pages при генерации.
        if (_analysisCancelled)
        {
            _analysisCancelled = false;
            return;
        }
        if (anyJson && _detectedPlatform == "Windows")
            _ = StartDocsSearchAsync();
    }

    /// <summary>Поиск документации MS Learn по всем export.json — выполняется
    /// сразу после анализа СФ, параллельно, с тихими вызовами npx.</summary>
    private async Task StartDocsSearchAsync()
    {
        var inputDir = SfaInputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir)) return;

        var sfaReports = Path.Combine(inputDir, "SFAReports");
        var jsonFiles = ExecutableFinder.SafeEnumerateFiles(inputDir)
            .Where(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (jsonFiles.Length == 0) return;

        _docsInProgress = true;
        _docsCancelled = false;
        SfaStartButton.IsEnabled = false;
        SfaCancelButton.IsEnabled = true;
        SfaGenerateHtmlButton.IsEnabled = false;
        SfaProcessProgress.Value = 0;
        SfaProcessProgress.Maximum = 1;
        SetSfaProgressRunning(true);
        SfaProcessStatusText.Text = "Поиск документации MS Learn...";
        SfaErrorLogTextBox.Text = "";

        _docsWorker = new HtmlGenWorker("sfa-docs", deleteJson: false, reuseCache: false,
            _detectedPlatform);
        _docsWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            if (total > 0) { SfaProcessProgress.Maximum = total; SfaProcessProgress.Value = cur; }
            SfaProcessStatusText.Text = msg.Length > 0
                ? $"Поиск документации: {cur}/{total} — {msg}"
                : $"Поиск документации: {cur}/{total}";
        });
        _docsWorker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        _docsWorker.Finished += result => RunOnUi(() => OnDocsFinished(result));

        await Task.Run(() => _docsWorker.Run(inputDir, sfaReports, inputDir, null, null, null, jsonFiles));
    }

    private void OnDocsFinished(HtmlGenResult result)
    {
        _docsInProgress = false;
        SfaStartButton.IsEnabled = true;
        SfaCancelButton.IsEnabled = false;
        SfaGenerateHtmlButton.IsEnabled = true;
        SfaProcessProgress.Value = SfaProcessProgress.Maximum;
        SetSfaProgressRunning(false);
        SfaProcessStatusText.Text = _docsCancelled
            ? "Поиск документации отменён"
            : $"Документация готова: найдено {result.FoundCount} из {result.SearchedCount}"
              + (result.CachedTotal > 0 ? $" (в кэше {result.CachedTotal} функций)" : "");
        _docsWorker = null;
    }

    private Task StartExportOnlyAsync(List<string> idbFiles)
    {
        var idatPath = ToolLocator.GetIdaExecutable(_cfg);
        var script = Path.Combine(AppConstants.ScriptsDir, "export_data.py");
        if (!File.Exists(script)) { AppendError($"Скрипт экспорта не найден: {script}"); return Task.CompletedTask; }
        SfaProcessStatusText.Text = "Фаза: экспорт в JSON...";
        SfaProcessProgress.Value = 0;
        var worker = new AnalysisWorker(idbFiles, idatPath, (int)SfaMaxIdaSlider.Value, null,
            false, false, SfaPseudocodeCheck.IsChecked == true, false, true);
        HookWorker(worker);
        _worker = worker;
        worker.Start();
        return Task.CompletedTask;
    }

    private void CancelAnalysis()
    {
        if (_docsInProgress)
        {
            // Фаза поиска документации: мост убивает процессы npx и завершается.
            _docsCancelled = true;
            _docsWorker?.Cancel();
            SfaProcessStatusText.Text = "Отмена поиска документации...";
        }
        else if (_htmlInProgress)
        {
            // Фаза генерации отчётов: мягкая отмена — мост останавливает
            // поиск документации и завершает все процессы npx.
            _htmlCancelled = true;
            _htmlWorker?.Cancel();
            SfaProcessStatusText.Text = "Отмена генерации отчёта...";
        }
        else
        {
            _analysisCancelled = true;
            _worker?.Cancel();
            SfaProcessStatusText.Text = "Отмена...";
        }
        SfaCancelButton.IsEnabled = false;
    }

    private void AppendError(string message)
    {
        if (string.IsNullOrEmpty(SfaErrorLogTextBox.Text)) SfaErrorLogTextBox.Text = message;
        else SfaErrorLogTextBox.Text += Environment.NewLine + message;
    }

    private void RunOnUi(Action a) => DispatcherQueue.TryEnqueue(() => a());

    // ──────────────────────────────────────────────
    //  HTML-отчёты СФ (с кэшем MS Learn)
    // ──────────────────────────────────────────────

    private async void GenerateHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_htmlInProgress) return;

        var inputDir = SfaInputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папка не найдена.");
            return;
        }

        var sfaReports = Path.Combine(inputDir, "SFAReports");
        var cacheDb = Path.Combine(sfaReports, "mslearn_cache.db");
        var indexDb = Path.Combine(sfaReports, "sfa_function_index.db");
        var reuseCache = false;

        if (Directory.Exists(sfaReports) && File.Exists(cacheDb) && File.Exists(indexDb))
        {
            var sizeKb = new FileInfo(cacheDb).Length / 1024.0;
            var (action, _) = await AskSfaCacheAsync(sizeKb);
            if (action == "cancel") return;
            reuseCache = action == "reuse";
        }

        string[] jsonFiles;
        if (reuseCache)
        {
            jsonFiles = ReadJsonPathsFromIndex(indexDb);
            if (jsonFiles.Length == 0)
            {
                await UiDialogs.WarnAsync("Ошибка",
                    "Индекс БД устарел или не содержит данных.\nВыполните полный анализ для перестроения индекса.");
                return;
            }
        }
        else
        {
            jsonFiles = ExecutableFinder.SafeEnumerateFiles(inputDir)
            .Where(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (jsonFiles.Length == 0)
            {
                await UiDialogs.WarnAsync("Ошибка", "Нет JSON-файлов экспорта.");
                return;
            }
        }

        await DoGenerateHtmlAsync(inputDir, jsonFiles, reuseCache, sfaReports);
    }

    private async Task DoGenerateHtmlAsync(string inputDir, string[] jsonFiles, bool reuseCache, string sfaReports)
    {
        if (!reuseCache) Directory.CreateDirectory(sfaReports);

        _htmlInProgress = true;
        _htmlCancelled = false;
        SfaStartButton.IsEnabled = false;
        SfaCancelButton.IsEnabled = true;   // отмена доступна и во время генерации
        SfaGenerateHtmlButton.IsEnabled = false;
        SfaProcessProgress.Value = 0;
        SfaProcessProgress.Maximum = Math.Max(jsonFiles.Length, 1);
        SetSfaProgressRunning(true);
        SfaProcessStatusText.Text = reuseCache
            ? $"Перегенерация HTML-отчётов СФ из кэша (с добором недостающей документации)…\n{sfaReports}"
            : $"Генерация HTML-отчётов СФ…\nРезультаты: {sfaReports}";
        SfaErrorLogTextBox.Text = "";

        var manpagesDb = ResolveManpagesDb(sfaReports);

        _htmlWorker = new HtmlGenWorker("sfa", SfaDeleteJsonCheck.IsChecked == true,
            reuseCache, _detectedPlatform);
        _htmlWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            if (total > 0) { SfaProcessProgress.Maximum = total; SfaProcessProgress.Value = cur; }
            SfaProcessStatusText.Text = msg.Length > 0
                ? $"Генерация HTML: {cur}/{total} — {msg}"
                : $"Генерация HTML: {cur}/{total}";
        });
        _htmlWorker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        // Finished приходит из фонового потока Task.Run: без RunOnUi обработчик
        // трогал UI (кнопки, прогресс, ContentDialog) из не-UI потока —
        // приложение падало аварийно в момент завершения/отмены генерации.
        _htmlWorker.Finished += result => RunOnUi(() => OnHtmlFinished(result));

        await Task.Run(() => _htmlWorker.Run(inputDir, sfaReports, inputDir, null, null,
            manpagesDb, jsonFiles));
    }

    private string ResolveManpagesDb(string sfaReports)
    {
        // Страница кэшируется, её _cfg — снимок из конструктора: путь, заданный
        // в настройках после создания страницы (например, сразу после скачивания
        // man-pages), сюда бы не попал. Читаем свежий конфиг с диска.
        var cfgPath = ConfigService.Load().ManPagesDbPath;
        if (!string.IsNullOrWhiteSpace(cfgPath))
        {
            var p = Path.GetFullPath(cfgPath);
            return Path.GetExtension(p).Equals(".db", StringComparison.OrdinalIgnoreCase)
                ? p : Path.Combine(p, AppConstants.ManpagesDbFileName);
        }
        return Path.Combine(sfaReports, AppConstants.ManpagesDbFileName);
    }

    private async void OnHtmlFinished(HtmlGenResult result)
    {
        _htmlInProgress = false;
        SfaStartButton.IsEnabled = true;
        SfaCancelButton.IsEnabled = false;
        SfaGenerateHtmlButton.IsEnabled = true;
        SfaProcessProgress.Value = SfaProcessProgress.Maximum;
        SetSfaProgressRunning(false);
        SfaProcessStatusText.Text = _htmlCancelled ? "Генерация отменена" : "Готово";
        _htmlWorker = null;

        if (_htmlCancelled) return;

        if (result.IndexPath != null || result.ReportsDir != null)
            await UiDialogs.InfoAsync("Готово",
                $"Отчёты СФ сохранены в {result.ReportsDir}\nИндекс: {result.IndexPath}");
    }

    /// <summary>Читает список json_path из index-БД для перегенерации из кэша.</summary>
    private static string[] ReadJsonPathsFromIndex(string indexDb)
    {
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={indexDb}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT json_path FROM file_imports WHERE json_path IS NOT NULL";
            using var r = cmd.ExecuteReader();
            var list = new List<string>();
            while (r.Read())
            {
                var p = r.GetString(0);
                if (!string.IsNullOrEmpty(p)) list.Add(p);
            }
            return list.ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    // ──────────────────────────────────────────────
    //  Диалоги
    // ──────────────────────────────────────────────

    private static async Task<string> AskExistingBasesAsync(int ready, int total, int needAnalysis)
    {
        var idx = await UiDialogs.AskButtonsAsync(
            "Существующие базы",
            $"Готово: {ready} из {total}\nТребуют анализа: {needAnalysis}",
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

    private static Task<(string Action, object? Tag)> AskSfaCacheAsync(double sizeKb)
    {
        var tcs = new TaskCompletionSource<(string, object?)>();
        var dlg = new ContentDialog
        {
            Title = "Существующий кэш MS Learn",
            Content = $"Найдена папка SFAReports с ранее сформированным кэшем документации ({sizeKb:F1} КБ).\n\n" +
                      "Перегенерация из кэша использует сохранённую документацию; " +
                      "если в кэше её не хватает (например, поиск прерывался), " +
                      "недостающие функции будут доискаться автоматически.",
            PrimaryButtonText = "Выполнить полный анализ",
            SecondaryButtonText = "Перегенерировать HTML из кэша",
            CloseButtonText = "Отмена",
        };
        if (UiDialogs.XamlRoot != null) dlg.XamlRoot = UiDialogs.XamlRoot;
        _ = dlg.ShowAsync().AsTask().ContinueWith(t =>
        {
            tcs.TrySetResult(t.Result switch
            {
                ContentDialogResult.Primary => ("full", null),
                ContentDialogResult.Secondary => ("reuse", null),
                _ => ("cancel", null),
            });
        }, TaskScheduler.FromCurrentSynchronizationContext());
        return tcs.Task;
    }
}