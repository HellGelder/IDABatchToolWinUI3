using System.Runtime.CompilerServices;
using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Анализ СФ» (системных функций) - аналог SfaPage из исполнения 1.
/// </summary>
public sealed partial class SfaPage : Page
{
    private readonly IConfigStore _store;
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

public SfaPage(IConfigStore store)
    {
        InitializeComponent();
        _store = store;
        // max_ida: 0 = дефолт 4; значение ограничено разумным максимумом.
        var configuredThreads = _store.Current.MaxIda > 0 ? _store.Current.MaxIda : 4;
        SfaMaxIdaSlider.Maximum = Math.Max(8, Environment.ProcessorCount);
        SfaMaxIdaSlider.Value = Math.Clamp(configuredThreads, 1, (int)SfaMaxIdaSlider.Maximum);
        // Предупреждение о повышенной нагрузке при потоках по максимуму железа
        SfaMaxIdaSlider.ValueChanged += (_, _) => UpdateThreadsWarning();
        UpdateThreadsWarning();

        // Подписки - один раз в конструкторе, чтобы при повторном показе
        // не дублировались (страница кэшируется в MainWindow).
        SfaBrowseDirButton.Click += (_, _) =>
            UiDialogs.PickFolderAsync(SfaInputDirTextBox).SafeFireAndForget("Sfa.BrowseDir");
        SfaStartButton.Click += StartAnalysis_Click;
        SfaCancelButton.Click += (_, _) => CancelAnalysis();
        SfaGenerateHtmlButton.Click += GenerateHtml_Click;
        SfaInputDirTextBox.TextChanged += (_, _) => RefreshFileList();

        // Скан папки - в фоне с дебаунсом TextChanged (как на вкладке «Общий анализ»).
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(400);
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Tick += (_, _) => RefreshFileListAsync().SafeFireAndForget("Sfa.Refresh");

        Loaded += (_, _) => RefreshFileList();
    }

    public bool IsAnalysisRunning() => _analysisInProgress || _htmlInProgress || _docsInProgress;

    /// <summary>Есть ли активная работа (для закрытия окна).</summary>
    public bool IsBusy => _analysisInProgress || _htmlInProgress || _docsInProgress;

    /// <summary>Отменяет все текущие операции (для закрытия окна).</summary>
    public void CancelAll()
    {
        _analysisCancelled = true;
        _worker?.Cancel();
        _htmlCancelled = true;
        _htmlWorker?.Cancel();
        _docsCancelled = true;
        _docsWorker?.Cancel();
    }

    /// <summary>Инфобар-ворнинг: потоки на максимуме аппаратных возможностей.</summary>
    private void UpdateThreadsWarning()
        => SfaThreadsWarningBar.IsOpen = (int)SfaMaxIdaSlider.Value >= (int)SfaMaxIdaSlider.Maximum;

    /// <summary>Индикатор занятости (как SetProgressRunning на вкладке
    /// «Общий анализ»): пока идёт любая фаза - полоса анимируется, точные
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

        InputScanner.ScanResult scan;
        try
        {
            scan = await InputScanner.ScanAsync(inputDir);
        }
        catch (Exception)
        {
            if (gen != _refreshGeneration) return;
            scan = new InputScanner.ScanResult(Array.Empty<FileItem>(), Array.Empty<string>());
        }
        if (gen != _refreshGeneration) return; // уже запрошен более свежий скан

        _cachedFiles = scan.Files.ToList();

        if (_cachedFiles.Count == 0)
        {
            SfaTreemap.SetData(new List<FileItem>());
            SfaGenerateHtmlButton.IsEnabled = false;
            SfaPlatformInfoBar.IsOpen = false;
            return;
        }

        // Платформа - по фактическим файлам (расширения, при их отсутствии - сигнатуры)
        UpdatePlatformBar(PlatformInfo.DetectPlatform(_cachedFiles.Select(f => f.Path)));

        SfaTreemap.SetData(_cachedFiles);
        UpdateGenerateHtmlButton();
    }

    /// <summary>
    /// Кнопка HTML-отчёта: доступна только когда завершены все фазы анализа
    /// (включая поиск документации) и есть по чему строить отчёт. Без проверки
    /// занятости фоновая пересканирование папки включало кнопку прямо во время
    /// поиска документации: дебаунс RefreshFileList срабатывает уже после того,
    /// как фаза документации стартовала.
    /// </summary>
    private void UpdateGenerateHtmlButton()
        => SfaGenerateHtmlButton.IsEnabled = !IsBusy && HasHtmlSources();

    /// <summary>Есть ли исходные данные для отчёта: готовые базы или JSON-экспорт.</summary>
    private bool HasHtmlSources()
    {
        if (_cachedFiles.Any(f => ArtifactValidator.HasValidArtifacts(f.Path))) return true;
        var inputDir = SfaInputDirTextBox.Text.Trim();
        return !string.IsNullOrEmpty(inputDir) && Directory.Exists(inputDir)
            && ExecutableFinder.SafeEnumerateFiles(inputDir)
                .Any(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase));
    }

    // ──────────────────────────────────────────────
    //  Анализ
    // ──────────────────────────────────────────────

    private void StartAnalysis_Click(object sender, RoutedEventArgs e)
        => RunSafe(StartAnalysisAsync());

    private async Task StartAnalysisAsync()
    {
        if (_analysisInProgress) return;

        var idatPath = ToolLocator.GetIdaExecutable(_store.Current);
        if (!File.Exists(idatPath))
        {
            await UiDialogs.WarnAsync("IDA не найдена", $"{idatPath} не найден.\nПроверьте конфигурацию.");
            return;
        }

        var inputDir = SfaInputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir)) inputDir = _store.Current.DefaultInputDir;
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

        var withIdb = files.Where(f => ArtifactValidator.IsValidIdaDatabase(f.ExpectedI64Path, out _)).ToList();
        var withoutIdb = files.Where(f => !ArtifactValidator.IsValidIdaDatabase(f.ExpectedI64Path, out _)).ToList();
        var exportOnly = false;

        if (withIdb.Count > 0)
        {
            var action = await UiDialogs.AskExistingBasesAsync(
                "Существующие базы",
                $"Готово: {withIdb.Count} из {files.Count}\nТребуют анализа: {withoutIdb.Count}");
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

        // Кэш документации MS Learn: диалог показывается сразу после нажатия
        // кнопки анализа (при наличии БД кэша и только для Windows - Linux
        // априори документируется по man-pages, без вопросов). «Перегенерировать
        // из кэша» запускает перегенерацию HTML без IDA-анализа; «полный
        // анализ» - обычный цикл, документация доищется фазой после анализа.
        if (_detectedPlatform == "Windows")
        {
            var sfaReportsDir = Path.Combine(SfaInputDirTextBox.Text.Trim(), AppConstants.SfaReportsDirName);
            var cacheDbPath = Path.Combine(sfaReportsDir, "mslearn_cache.db");
            var indexDbPath = Path.Combine(sfaReportsDir, "sfa_function_index.db");
            if (File.Exists(cacheDbPath) && File.Exists(indexDbPath))
            {
                var sizeKb = new FileInfo(cacheDbPath).Length / 1024.0;
                var (cacheAction, _) = await AskSfaCacheAsync(sizeKb);
                switch (cacheAction)
                {
                    case "cancel":
                        return;
                    case "reuse":
                        var cachedJsonFiles = ReadJsonPathsFromIndex(indexDbPath);
                        if (cachedJsonFiles.Length == 0)
                        {
                            await UiDialogs.WarnAsync("Ошибка",
                                "Индекс БД устарел или не содержит данных.\nВыполните полный анализ для перестроения индекса.");
                            return;
                        }
                        await DoGenerateHtmlAsync(SfaInputDirTextBox.Text.Trim(), cachedJsonFiles,
                            reuseCache: true, sfaReports: sfaReportsDir);
                        return;
                }
            }
        }

        _analysisInProgress = true;
        _analysisCancelled = false;
        SfaStartButton.IsEnabled = false;
        SfaCancelButton.IsEnabled = true;
        UpdateGenerateHtmlButton();
        SfaProcessStatusText.Text = exportOnly ? "Фаза: экспорт в JSON..." : "Фаза: анализ файлов...";
        SfaProcessProgress.Value = 0;
        SetSfaProgressRunning(true);
        SfaErrorLogTextBox.Text = "";

        _worker = new AnalysisWorker(
            files.Select(f => f.Path).ToList(), idatPath, (int)SfaMaxIdaSlider.Value, null,
            SfaCleanupCheck.IsChecked == true, SfaTempCleanupCheck.IsChecked == true,
            // СФ-отчёты псевдокод не используют - экспорт всегда без него
            false, SfaDeleteJsonCheck.IsChecked == true,
            exportOnly);
        HookWorker(_worker);
        _worker.Start();
    }

    private void HookWorker(AnalysisWorker w)
    {
        w.PhaseChanged += phase => RunOnUi(() =>
        {
            SfaProcessStatusText.Text = phase == "analysis" ? "Фаза: анализ файлов..." : "Фаза: экспорт в JSON...";
            AppendLog($"[Фаза] {SfaProcessStatusText.Text}");
        });
        w.AnalysisProgress += (name, cur, total) => RunOnUi(() =>
        {
            SfaProcessStatusText.Text = $"Анализ: {cur}/{total} - {name}";
            if (total > 0) SfaProcessProgress.Value = 100.0 * cur / total;
        });
        w.PulseUpdated += (_, text) => RunOnUi(() =>
        {
            // Долгий модуль: показываем, что процесс жив и работает, даже если
            // прогресс по файлам давно не менялся.
            SfaProcessStatusText.Text = text;
        });
        w.AnalysisFileStarted += name => RunOnUi(() =>
        {
            SetFileStatusByName(name, AnalysisStatus.InProgress);
            AppendLog($"[Анализ] Начало: {name}");
        });
        w.AnalysisFileCompleted += (name, ok) => RunOnUi(() =>
        {
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error);
            AppendLog($"[Анализ] {name} - {(ok ? "успешно" : "ошибка")}");
        });
        w.ExportProgress += (name, cur, total) => RunOnUi(() =>
        {
            SfaProcessStatusText.Text = $"Экспорт: {cur}/{total} - {name}";
            if (total > 0) SfaProcessProgress.Value = 100.0 * cur / total;
        });
        w.ExportFileCompleted += (name, ok) => RunOnUi(() =>
        {
            SetFileStatusByName(name, ok ? AnalysisStatus.Success : AnalysisStatus.Error);
            if (!ok) AppendError($"Ошибка экспорта для {name}");
            else AppendLog($"[Экспорт] {name} - успешно");
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
        SfaProcessStatusText.Text = $"Завершено. Успешно: {succeeded}/{total}";
        SfaProcessProgress.Value = 100;
        SetSfaProgressRunning(false);

        if (_exportAllAfterAnalysis && succeeded > 0)
        {
            _exportAllAfterAnalysis = false;
            SfaProcessStatusText.Text = "Автоматический экспорт всех баз...";
            var allIdb = _cachedFiles
                .Where(f => ArtifactValidator.IsValidIdaDatabase(f.ExpectedI64Path, out _))
                .Select(f => f.ExpectedI64Path).ToList();
            if (allIdb.Count > 0) { StartExportOnlyAsync(allIdb).SafeFireAndForget("Sfa.ExportOnly"); return; }
            SfaProcessStatusText.Text = "Нет готовых баз для экспорта";
        }

        var inputDir = SfaInputDirTextBox.Text.Trim();
        var anyJson = !string.IsNullOrEmpty(inputDir) && Directory.Exists(inputDir) &&
            ExecutableFinder.SafeEnumerateFiles(inputDir).Any(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase));
        _worker?.Dispose();
        _worker = null;
        RefreshFileList();

        // Поиск документации MS Learn - фаза кнопки «Запустить анализ СФ»
        // (после анализа и экспорта), чтобы генерация отчёта шла по кэшу
        // без вызовов npx. Linux/Android использует man-pages при генерации.
        if (_analysisCancelled)
        {
            _analysisCancelled = false;
            UpdateGenerateHtmlButton();
            return;
        }
        if (anyJson && _detectedPlatform == "Windows")
        {
            // Кнопка остаётся выключенной до конца поиска документации:
            // StartDocsSearchAsync выставит IsBusy и включит её в OnDocsFinished.
            StartDocsSearchAsync().SafeFireAndForget("Sfa.DocsSearch");
            return;
        }
        UpdateGenerateHtmlButton();
    }

    /// <summary>Поиск документации MS Learn по всем export.json - выполняется
    /// сразу после анализа СФ, параллельно, с тихими вызовами npx.</summary>
    private async Task StartDocsSearchAsync()
    {
        var inputDir = SfaInputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir)) return;

        var sfaReports = Path.Combine(inputDir, AppConstants.SfaReportsDirName);
        var jsonFiles = ExecutableFinder.SafeEnumerateFiles(inputDir)
            .Where(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (jsonFiles.Length == 0) return;

        _docsInProgress = true;
        _docsCancelled = false;
        SfaStartButton.IsEnabled = false;
        SfaCancelButton.IsEnabled = true;
        UpdateGenerateHtmlButton();
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
                ? $"Поиск документации: {cur}/{total} - {msg}"
                : $"Поиск документации: {cur}/{total}";
        });
        _docsWorker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        _docsWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            if (msg.Length > 0) AppendLog($"[Документация] {msg}");
        });
        _docsWorker.Finished += result => RunOnUi(() => OnDocsFinished(result));

        await _docsWorker.RunAsync(inputDir, sfaReports, inputDir, null, null, null, jsonFiles);
    }

    private void OnDocsFinished(HtmlGenResult result)
    {
        _docsInProgress = false;
        SfaStartButton.IsEnabled = true;
        SfaCancelButton.IsEnabled = false;
        UpdateGenerateHtmlButton();
        SfaProcessProgress.Value = SfaProcessProgress.Maximum;
        SetSfaProgressRunning(false);
        SfaProcessStatusText.Text = _docsCancelled
            ? "Поиск документации отменён"
            : $"Документация готова: найдено {result.FoundCount} из {result.SearchedCount}"
              + (result.CachedTotal > 0 ? $" (в кэше {result.CachedTotal} функций)" : "");
        _docsWorker?.Dispose();
        _docsWorker = null;
    }

    private Task StartExportOnlyAsync(List<string> idbFiles)
    {
        var idatPath = ToolLocator.GetIdaExecutable(_store.Current);
        var script = Path.Combine(AppConstants.ScriptsDir, "export_data.py");
        if (!File.Exists(script)) { AppendError($"Скрипт экспорта не найден: {script}"); return Task.CompletedTask; }
        // Экспорт - такая же фаза анализа: пока он идёт, страница занята и
        // кнопка HTML-отчёта должна оставаться выключенной.
        _analysisInProgress = true;
        _analysisCancelled = false;
        SfaStartButton.IsEnabled = false;
        SfaCancelButton.IsEnabled = true;
        UpdateGenerateHtmlButton();
        SfaProcessStatusText.Text = "Фаза: экспорт в JSON...";
        SfaProcessProgress.Value = 0;
        var worker = new AnalysisWorker(idbFiles, idatPath, (int)SfaMaxIdaSlider.Value, null,
            false, false, false, false, true);
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
            // Фаза генерации отчётов: мягкая отмена - мост останавливает
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
        AppLog.Error(message);
        AppendLog(message);
    }

    /// <summary>Журнал выполнения: дублирует в «Лог выполнения» любые события
    /// (этапы, файлы, экспорт, документация, ошибки), не только ошибки.</summary>
    private void AppendLog(string message) => UiLog.Append(SfaErrorLogTextBox, message);

    private void RunOnUi(Action a) => UiThread.Run(DispatcherQueue, a);

    private void RunOnUi(Func<Task> a) => UiThread.Run(DispatcherQueue, a, "Sfa.RunOnUi");

    /// <summary>Запуск задачи страницы: исключение попадает в журнал страницы и
    /// AppLog, а не роняет процесс (см. TaskExtensions.SafeFireAndForget).</summary>
    private void RunSafe(Task task, [CallerMemberName] string? context = null)
        => task.SafeFireAndForget(context ?? "Sfa",
            ex => RunOnUi(() => AppendError($"{context}: {ex.Message}")));

    // ──────────────────────────────────────────────
    //  HTML-отчёты СФ (с кэшем MS Learn)
    // ──────────────────────────────────────────────

    private void GenerateHtml_Click(object sender, RoutedEventArgs e)
        => RunSafe(GenerateHtmlAsync());

    private async Task GenerateHtmlAsync()
    {
        if (_htmlInProgress) return;

        var inputDir = SfaInputDirTextBox.Text.Trim();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
        {
            await UiDialogs.WarnAsync("Ошибка", "Папка не найдена.");
            return;
        }

        var sfaReports = Path.Combine(inputDir, AppConstants.SfaReportsDirName);

        // Вопрос про кэш задаётся при запуске анализа, не здесь: к моменту
        // нажатия «Сгенерировать HTML» поиск документации уже выполнен, и
        // генерация идёт по кэшу mslearn_cache.db. Если индекс покрывает все
        // найденные JSON - перегенерация из кэша; есть неучтённые файлы
        // (добавлены после анализа) - полная генерация, индекс перестроится.
        var scannedJsons = ExecutableFinder.SafeEnumerateFiles(inputDir)
            .Where(f => f.EndsWith(".export.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var indexDb = Path.Combine(sfaReports, "sfa_function_index.db");
        var indexPaths = File.Exists(indexDb) ? ReadJsonPathsFromIndex(indexDb) : Array.Empty<string>();
        var indexed = new HashSet<string>(indexPaths.Select(p => Path.GetFullPath(p)), StringComparer.OrdinalIgnoreCase);

        string[] jsonFiles;
        bool reuseCache;
        if (scannedJsons.Length == 0 && indexPaths.Length > 0)
        {
            // JSON удалены (например, «удалять JSON» включён) - только кэш.
            jsonFiles = indexPaths;
            reuseCache = true;
        }
        else if (scannedJsons.Length > 0 && scannedJsons.All(f => indexed.Contains(Path.GetFullPath(f))))
        {
            jsonFiles = scannedJsons;
            reuseCache = true;
        }
        else
        {
            jsonFiles = scannedJsons;
            reuseCache = false;
        }
        if (jsonFiles.Length == 0)
        {
            await UiDialogs.WarnAsync("Ошибка", "Нет JSON-файлов экспорта.");
            return;
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
        UpdateGenerateHtmlButton();
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
                ? $"Генерация HTML: {cur}/{total} - {msg}"
                : $"Генерация HTML: {cur}/{total}";
        });
        _htmlWorker.ErrorOccurred += msg => RunOnUi(() => AppendError(msg));
        _htmlWorker.ProgressUpdated += (cur, total, msg) => RunOnUi(() =>
        {
            if (msg.Length > 0) AppendLog($"[HTML] {msg}");
        });
        // Finished приходит из фонового потока Task.Run: без RunOnUi обработчик
        // трогал UI (кнопки, прогресс, ContentDialog) из не-UI потока -
        // приложение падало аварийно в момент завершения/отмены генерации.
        _htmlWorker.Finished += result => RunOnUi(() => OnHtmlFinishedAsync(result));

        await _htmlWorker.RunAsync(inputDir, sfaReports, inputDir, null, null,
            manpagesDb, jsonFiles);
    }

    private string ResolveManpagesDb(string sfaReports)
    {
        // Путь читаем из store на момент вызова: значение, заданное в настройках
        // после создания страницы (например, сразу после скачивания man-pages),
        // попадает сюда без перезапуска приложения.
        var cfgPath = _store.Current.ManPagesDbPath;
        if (!string.IsNullOrWhiteSpace(cfgPath))
        {
            var p = Path.GetFullPath(cfgPath);
            return Path.GetExtension(p).Equals(".db", StringComparison.OrdinalIgnoreCase)
                ? p : Path.Combine(p, AppConstants.ManpagesDbFileName);
        }
        return Path.Combine(sfaReports, AppConstants.ManpagesDbFileName);
    }

    private async Task OnHtmlFinishedAsync(HtmlGenResult result)
    {
        _htmlInProgress = false;
        SfaStartButton.IsEnabled = true;
        SfaCancelButton.IsEnabled = false;
        UpdateGenerateHtmlButton();
        SfaProcessProgress.Value = SfaProcessProgress.Maximum;
        SetSfaProgressRunning(false);
        SfaProcessStatusText.Text = _htmlCancelled ? "Генерация отменена" : "Готово";
        _htmlWorker?.Dispose();
        _htmlWorker = null;

        if (_htmlCancelled) return;

        if (result.IndexPath != null || result.ReportsDir != null)
            await UiDialogs.InfoAsync("Готово",
                $"Отчётов: {result.GeneratedCount} из {result.TotalFiles}\n" +
                $"Системных функций: {result.TotalSystemFunctions}" +
                $" (без документации: {result.SystemNotFound})\n" +
                $"Всего импортов: {result.TotalImports}\n\n" +
                $"Отчёты: {result.ReportsDir}\nИндекс: {result.IndexPath}");
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


    private static async Task<(string Action, object? Tag)> AskSfaCacheAsync(double sizeKb)
    {
        // Прежний вариант (TCS + ContinueWith поверх ShowAsync) зависал навсегда,
        // если ShowAsync падал; AskButtonsAsync завершает задачу «отменой»/исключением.
        var idx = await UiDialogs.AskButtonsAsync(
            "Найден кэш документации MS Learn",
            $"Обнаружен ранее собранный кэш документации ({sizeKb:F1} КБ).\n\n" +
            "«Перегенерировать HTML из кэша» - отчёты строятся из сохранённой " +
            "документации без повторного анализа IDA; недостающие функции доищутся автоматически.\n\n" +
            "«Выполнить полный анализ» - обычный цикл IDA + экспорт; документация " +
            "будет искаться после анализа.",
            new[] { "Выполнить полный анализ", "Перегенерировать HTML из кэша", "Отмена" });
        return idx switch
        {
            0 => ("full", (object?)null),
            1 => ("reuse", (object?)null),
            _ => ("cancel", (object?)null),
        };
    }
}