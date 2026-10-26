using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Конфигурация» - аналог SettingsPage из исполнения 1 (Qt).
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly IConfigStore _store;
    private AppConfig _cfg;
    private ManPagesSyncWorker? _manWorker;

    public SettingsPage(IConfigStore store)
    {
        InitializeComponent();
        _store = store;
        _cfg = _store.Current;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BrowseIdaButton.Click += (_, _) => UiDialogs.PickFileAsync(IdatPathTextBox, ".exe").SafeFireAndForget("Settings.BrowseIda");
        AutoIdaButton.Click += (_, _) => RunSafe(AutoDetectIdaAsync());
        BrowseBindiffButton.Click += (_, _) => UiDialogs.PickFileAsync(BindiffPathTextBox, ".exe").SafeFireAndForget("Settings.BrowseBindiff");
        AutoBindiffButton.Click += (_, _) => RunSafe(AutoDetectBindiffAsync());
        SaveSettingsButton.Click += (_, _) => RunSafe(SaveSettingsAsync(showMessage: true));
        CheckUtilsButton.Click += (_, _) => RunSafe(CheckUtilitiesAsync());

        ThemeLightButton.Click += (_, _) => SwitchTheme("light");
        ThemeDarkButton.Click += (_, _) => SwitchTheme("dark");

        BrowseManPagesButton.Click += (_, _) => UiDialogs.PickFolderAsync(ManPagesPathTextBox).SafeFireAndForget("Settings.BrowseManPages");
        CheckManPagesButton.Click += (_, _) => RunSafe(CheckManPagesAsync(showPopup: true));
        SyncManPagesButton.Click += (_, _) => RunSafe(SyncManPagesAsync());

        LoadToUi();
    }

    private void LoadToUi()
    {
        IdatPathTextBox.Text = _cfg.IdaExecutable;
        BindiffPathTextBox.Text = _cfg.BindiffExecutable;
        var isDark = _cfg.Theme == "dark";
        ThemeLightButton.IsChecked = !isDark;
        ThemeDarkButton.IsChecked = isDark;
        ManPagesPathTextBox.Text = _cfg.ManPagesDbPath;

        if (!string.IsNullOrWhiteSpace(ManPagesPathTextBox.Text))
            CheckManPagesAsync().SafeFireAndForget("Settings.CheckManPages");
    }

    private void SwitchTheme(string theme)
    {
        _cfg.Theme = theme;
        ThemeLightButton.IsChecked = theme == "light";
        ThemeDarkButton.IsChecked = theme == "dark";
        // Store.Changed доходит до MainWindow - тема применится без перезапуска.
        _store.Save(_cfg);
        ThemeHelper.Apply(UiDialogs.MainWindow, theme);
    }

    private async Task SaveSettingsAsync(bool showMessage)
    {
        _cfg.IdaExecutable = string.IsNullOrWhiteSpace(IdatPathTextBox.Text) ? "idat.exe" : IdatPathTextBox.Text.Trim();
        _cfg.BindiffExecutable = string.IsNullOrWhiteSpace(BindiffPathTextBox.Text) ? "bindiff.exe" : BindiffPathTextBox.Text.Trim();
        _cfg.ManPagesDbPath = ManPagesPathTextBox.Text.Trim();
        try
        {
            _store.Save(_cfg);
            if (showMessage) await UiDialogs.InfoAsync("Успех", "Настройки сохранены.");
        }
        catch (Exception ex)
        {
            await UiDialogs.WarnAsync("Ошибка", $"Не удалось сохранить конфиг:\n{ex.Message}");
        }
    }

    private async Task AutoDetectIdaAsync()
    {
        var found = ToolLocator.GetIdaExecutable(_cfg);
        if (string.IsNullOrEmpty(found) || !File.Exists(found))
        {
            await UiDialogs.InfoAsync("Не найдено",
                "Не удалось автоматически найти idat.\nПроверьте PATH или укажите путь вручную.");
            return;
        }
        IdatPathTextBox.Text = found;
        await UiDialogs.InfoAsync("Найдено", $"IDAT найден:\n{found}");
    }

    private async Task AutoDetectBindiffAsync()
    {
        var found = ToolLocator.GetBindiffExecutable(_cfg);
        if (string.IsNullOrEmpty(found) || !File.Exists(found))
        {
            await UiDialogs.InfoAsync("Не найдено",
                "Не удалось автоматически найти bindiff.\nПроверьте PATH или поместите файл в корень проекта.");
            return;
        }
        BindiffPathTextBox.Text = found;
        await UiDialogs.InfoAsync("Найдено", $"BinDiff найден:\n{found}");
    }

    // ──────────────────────────────────────────────
    //  Проверка утилит (7z / npx)
    // ──────────────────────────────────────────────

    private async Task CheckUtilitiesAsync()
    {
        var (status7z, msg7z) = await Check7zAsync();
        SevenZIcon.Text = status7z == "ok" ? "✅" : "⚠️";
        SevenZPath.Text = msg7z;

        var (statusNpx, msgNpx) = await CheckNpxAsync();
        NpxIcon.Text = statusNpx == "ok" ? "✅" : "⚠️";
        NpxPath.Text = msgNpx;
    }

    private static Task<(string, string)> Check7zAsync() => Task.Run(() =>
    {
        // Единая цепочка поиска с ArchiveHandler (включая портативный Tools\7-Zip),
        // плюс where.exe - раньше настройка не видела портативную копию 7-Zip.
        var candidates = new List<string>();
        var found = ArchiveHandler.Find7z();
        if (found != null) candidates.Add(found);
        candidates.AddRange(AppConstants.ProgramFilesDirs.Select(pf => Path.Combine(pf, "7-Zip", "7z.exe")));
        candidates.AddRange(ResolveOnPath("7z.exe"));
        candidates.AddRange(ResolveOnPath("7za.exe"));
        foreach (var exe in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(exe)) continue;
            try
            {
                var (code, output) = RunCaptureOutput(exe);
                if (code != 0) continue;
                var ver = "";
                foreach (var line in output.Split('\n'))
                {
                    var l = line.ToLowerInvariant();
                    if (l.Contains("version") || l.Contains("версия") || l.Contains("7-zip") || l.Contains("7za"))
                    { ver = line.Trim(); break; }
                }
                if (ver.Length > 0)
                {
                    var cut = ver.IndexOf("Copyright", StringComparison.OrdinalIgnoreCase);
                    if (cut > 0) ver = ver[..cut].Trim().TrimEnd(':').Trim();
                    return ("ok", $"{exe}  ({ver})");
                }
                return ("ok", exe);
            }
            catch { /* ищем дальше */ }
        }
        return ("error", "Не найден. Установите 7-Zip и перезапустите программу.");
    });

    private static Task<(string, string)> CheckNpxAsync() => Task.Run(() =>
    {
        var nodeCandidates = new List<string>(AppConstants.NodeCandidates);
        nodeCandidates.AddRange(ResolveOnPath("node.exe"));

        var nodeVersion = "";
        var nodeDir = "";
        foreach (var exe in nodeCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(exe)) continue;
            var (code, output) = RunCaptureOutput(exe, "--version");
            if (code != 0 || !IsVersion(output, allowVPrefix: true)) continue;
            nodeVersion = output;
            nodeDir = Path.GetDirectoryName(exe) ?? "";
            break;
        }
        if (nodeVersion.Length == 0)
            return ("error", "Не найден. Установите Node.js и перезапустите программу.");

        // npx ищем рядом с проверенным node и по известным путям - строго по полным путям,
        // чтобы случайный npx.cmd из рабочего каталога не маскировал системный.
        var npxCandidates = new List<string>();
        if (nodeDir.Length > 0) npxCandidates.Add(Path.Combine(nodeDir, "npx.cmd"));
        npxCandidates.AddRange(ResolveOnPath("npx.cmd"));
        npxCandidates.Add(AppConstants.NpxChocolateyCandidate);

        var npxVersion = "";
        var npxPath = "";
        var npxExit = -1;
        foreach (var exe in npxCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(exe)) continue;
            var (code, output) = RunCaptureOutput(exe, "--version");
            npxExit = code;
            if (code == 0 && IsVersion(output, allowVPrefix: true))
            {
                npxVersion = output;
                npxPath = exe;
                break;
            }
        }
        if (npxVersion.Length == 0)
            return ("error", npxExit < 0
                ? $"Node.js {nodeVersion} найден, но npx не найден. Переустановите Node.js."
                : $"Node.js {nodeVersion} найден, но npx не работает (код {npxExit}). Переустановите Node.js.");
        return ("ok", $"{npxPath}  (Node.js {nodeVersion}, npx {npxVersion})");
    });

    /// <summary>Запускает exe с аргументами, возвращает (код выхода, объединённый вывод).</summary>
    private static (int, string) RunCaptureOutput(string exe, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe),
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi);
            if (proc == null) return (-1, "");
            var output = (proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd()).Trim();
            if (!proc.WaitForExit(10000)) { try { proc.Kill(); } catch { } return (-1, ""); }
            return (proc.ExitCode, output);
        }
        catch { return (-1, ""); }
    }

    /// <summary>Ищет утилиту через where.exe; его рабочий каталог фиксирован на System32,
    /// чтобы «мусорный» exe из каталога приложения не маскировал системный.</summary>
    private static IEnumerable<string> ResolveOnPath(string name)
    {
        var found = new List<string>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "where.exe"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Environment.SystemDirectory,
            };
            psi.ArgumentList.Add(name);
            using var proc = Process.Start(psi);
            if (proc == null) return found;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            if (proc.ExitCode == 0)
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (File.Exists(line)) found.Add(line);
        }
        catch { /* where недоступен - остались известные пути */ }
        return found;
    }

    /// <summary>Строгая проверка вывода версии: одна строка вида "11.19.0"/"v26.7.0",
    /// без стектрейсов упавшего процесса.</summary>
    private static bool IsVersion(string output, bool allowVPrefix)
    {
        if (output.Length == 0 || output.Length > 20 || output.Contains('\n')) return false;
        if (allowVPrefix && output.StartsWith('v')) output = output[1..];
        return output.Length > 0 && output.All(c => char.IsDigit(c) || c == '.') && output.Any(char.IsDigit);
    }

    // ──────────────────────────────────────────────
    //  man-pages (Linux)
    // ──────────────────────────────────────────────

    private string ManPagesDbPath()
    {
        var raw = ManPagesPathTextBox.Text.Trim();
        if (string.IsNullOrEmpty(raw)) raw = _cfg.ManPagesDbPath.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            var inputDir = string.IsNullOrWhiteSpace(_cfg.DefaultInputDir) || _cfg.DefaultInputDir == "."
                ? Directory.GetCurrentDirectory() : _cfg.DefaultInputDir;
            return Path.Combine(inputDir, AppConstants.SfaReportsDirName, AppConstants.ManpagesDbFileName);
        }
        var p = Path.GetFullPath(raw);
        return Path.GetExtension(p).Equals(".db", StringComparison.OrdinalIgnoreCase)
            ? p : Path.Combine(p, AppConstants.ManpagesDbFileName);
    }

    private async Task CheckManPagesAsync(bool showPopup = false)
    {
        // Даём строке статуса уйти в очередь отрисовки до синхронной проверки
        await Task.Yield();
        var dbPath = ManPagesDbPath();
        if (!File.Exists(dbPath))
        {
            ManPagesStatusText.Text = $"Статус: база не найдена ({dbPath}). Нажмите «Скачать и импортировать».";
            if (showPopup) await ShowManPagesResultAsync(false, ManPagesStatusText.Text);
            return;
        }
        try
        {
            var (count, version, sizeMb) = await Task.Run(() => ReadManPagesInfo(dbPath));
            ManPagesStatusText.Text =
                $"Статус: база найдена - {count} функций, man-pages {version}, {sizeMb:F1} МБ";
            if (showPopup) await ShowManPagesResultAsync(true, ManPagesStatusText.Text);
        }
        catch
        {
            ManPagesStatusText.Text = $"Статус: файл есть, но не читается ({dbPath}).";
            if (showPopup) await ShowManPagesResultAsync(false, ManPagesStatusText.Text);
        }
    }

    /// <summary>Всплывающее окно результата проверки БД man-pages: пользователь
    /// жмёт «Проверить» и ждёт явного ответа, строка статуса мала и просматривается
    /// не всегда. Автозакрытие - чтобы диалог не приходилось закрывать вручную.</summary>
    private async Task ShowManPagesResultAsync(bool ok, string message)
    {
        var dlg = new ContentDialog
        {
            Title = ok ? "БД man-pages найдена" : "БД man-pages не найдена",
            Content = message,
            CloseButtonText = "ОК",
        };
        if (UiDialogs.XamlRoot != null) dlg.XamlRoot = UiDialogs.XamlRoot;
        try { await dlg.ShowAsync(); } catch { /* окно закрыто пользователем */ }
    }

    private static (long Count, string Version, double SizeMb) ReadManPagesInfo(string dbPath)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        long count = 0;
        var version = "";
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM function_index";
            count = Convert.ToInt64(cmd.ExecuteScalar());
        }
        catch { /* пустая/старая БД */ }
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key='manpages_version'";
            var v = cmd.ExecuteScalar();
            if (v != null) version = Convert.ToString(v) ?? "";
        }
        catch { /* нет meta */ }
        var size = new FileInfo(dbPath).Length / 1024.0 / 1024.0;
        return (count, version, size);
    }

    private async Task SyncManPagesAsync()
    {
        if (_manWorker != null)
        {
            await UiDialogs.InfoAsync("Синхронизация", "Загрузка уже выполняется.");
            return;
        }

        var dbPath = ManPagesDbPath();
        if (string.IsNullOrWhiteSpace(ManPagesPathTextBox.Text))
        {
            ManPagesPathTextBox.Text = Path.GetDirectoryName(dbPath) ?? "";
            _cfg.ManPagesDbPath = ManPagesPathTextBox.Text;
            _store.Save(_cfg);
        }

        SyncManPagesButton.IsEnabled = false;
        CheckManPagesButton.IsEnabled = false;
        ManPagesStatusText.Text = "Статус: загрузка архива man-pages…";

        _manWorker = new ManPagesSyncWorker(dbPath);
        _manWorker.Progress += (msg, pct) => RunOnUi(() =>
            ManPagesStatusText.Text = $"Статус: {msg} ({pct}%)");
        _manWorker.ErrorOccurred += msg => RunOnUi(() =>
            ManPagesStatusText.Text = $"Статус: ошибка - {msg}");
        _manWorker.Finished += (success, message) =>
            RunOnUi(() => OnManPagesFinishedAsync(success, dbPath, message));
        _manWorker.Start();
    }

    private async Task OnManPagesFinishedAsync(bool success, string dbPath, string message)
    {
        SyncManPagesButton.IsEnabled = true;
        CheckManPagesButton.IsEnabled = true;
            if (success)
            {
                _cfg.ManPagesDbPath = Path.GetDirectoryName(dbPath) ?? "";
                _store.Save(_cfg);
            await UiDialogs.InfoAsync("Готово", $"Документация man-pages импортирована:\n{message}");
            await CheckManPagesAsync();
        }
        else
        {
            await UiDialogs.WarnAsync("Ошибка", $"Не удалось импортировать man-pages:\n{message}");
        }
        _manWorker?.Dispose();
        _manWorker = null;
    }

    /// <summary>Запуск задачи страницы: исключение пишется в AppLog, а не роняет
    /// процесс (см. TaskExtensions.SafeFireAndForget).</summary>
    private void RunSafe(Task task, [CallerMemberName] string? context = null)
        => task.SafeFireAndForget(context ?? "Settings");

    private void RunOnUi(Action a) => UiThread.Run(DispatcherQueue, a);

    private void RunOnUi(Func<Task> a) => UiThread.Run(DispatcherQueue, a, "Settings.RunOnUi");
}