using System.Diagnostics;
using System.Text;
using IDABatchToolWinUI.Services;
using IDABatchToolWinUI.Workers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Pages;

/// <summary>
/// Страница «Конфигурация» — аналог SettingsPage из исполнения 1 (Qt).
/// </summary>
public sealed partial class SettingsPage : Page
{
    private AppConfig _cfg;
    private ManPagesSyncWorker? _manWorker;

    public SettingsPage()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BrowseIdaButton.Click += async (_, _) => await UiDialogs.PickFileAsync(IdatPathTextBox, ".exe");
        AutoIdaButton.Click += async (_, _) => await AutoDetectIdaAsync();
        BrowseBindiffButton.Click += async (_, _) => await UiDialogs.PickFileAsync(BindiffPathTextBox, ".exe");
        AutoBindiffButton.Click += async (_, _) => await AutoDetectBindiffAsync();
        SaveSettingsButton.Click += async (_, _) => await SaveSettingsAsync(showMessage: true);
        CheckUtilsButton.Click += async (_, _) => await CheckUtilitiesAsync();

        ThemeLightButton.Click += (_, _) => SwitchTheme("light");
        ThemeDarkButton.Click += (_, _) => SwitchTheme("dark");

        BrowseManPagesButton.Click += async (_, _) => await UiDialogs.PickFolderAsync(ManPagesPathTextBox);
        CheckManPagesButton.Click += async (_, _) => await CheckManPagesAsync();
        SyncManPagesButton.Click += async (_, _) => await SyncManPagesAsync();

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
            _ = CheckManPagesAsync();
    }

    private void SwitchTheme(string theme)
    {
        _cfg.Theme = theme;
        ThemeLightButton.IsChecked = theme == "light";
        ThemeDarkButton.IsChecked = theme == "dark";
        ConfigService.Save(_cfg);
        ThemeHelper.Apply(UiDialogs.MainWindow, theme);
        ConfigChanged?.Invoke(_cfg);
    }

    /// <summary>Событие изменения конфигурации (для MainWindow — переприменение темы).</summary>
    public event Action<AppConfig>? ConfigChanged;

    private async Task SaveSettingsAsync(bool showMessage)
    {
        _cfg.IdaExecutable = string.IsNullOrWhiteSpace(IdatPathTextBox.Text) ? "idat.exe" : IdatPathTextBox.Text.Trim();
        _cfg.BindiffExecutable = string.IsNullOrWhiteSpace(BindiffPathTextBox.Text) ? "bindiff.exe" : BindiffPathTextBox.Text.Trim();
        _cfg.ManPagesDbPath = ManPagesPathTextBox.Text.Trim();
        try
        {
            ConfigService.Save(_cfg);
            ConfigChanged?.Invoke(_cfg);
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
        var candidates = new[]
        {
            "7z", "7za", "7z.exe", "7za.exe",
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe",
        };
        foreach (var exe in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using var proc = Process.Start(psi);
                if (proc == null) continue;
                var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(5000)) { try { proc.Kill(); } catch { } continue; }
                var ver = "";
                foreach (var line in output.Split('\n'))
                {
                    var l = line.ToLowerInvariant();
                    if (l.Contains("version") || l.Contains("версия") || l.Contains("7-zip") || l.Contains("7za"))
                    { ver = line.Trim(); break; }
                }
                if (ver.Length > 0) return ("ok", $"{exe}  ({ver})");
                if (!string.IsNullOrWhiteSpace(output)) return ("ok", exe);
            }
            catch { /* ищем дальше */ }
        }
        return ("error", "Не найден. Установите 7-Zip и перезапустите программу.");
    });

    private static Task<(string, string)> CheckNpxAsync() => Task.Run(() =>
    {
        var candidates = new[] { "npx", "npx.cmd", "npx.exe" };
        foreach (var exe in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                psi.ArgumentList.Add("--version");
                using var proc = Process.Start(psi);
                if (proc == null) continue;
                var output = (proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd()).Trim();
                if (!proc.WaitForExit(10000)) { try { proc.Kill(); } catch { } continue; }
                if (output.Length > 0) return ("ok", $"{exe}  (v{output})");
                if (proc.ExitCode == 0) return ("ok", exe);
            }
            catch { /* ищем дальше */ }
        }
        var winCandidates = new[]
        {
            @"C:\Program Files\nodejs\npx.cmd", @"C:\Program Files\nodejs\npx.exe",
            @"C:\Program Files (x86)\nodejs\npx.cmd", @"C:\Program Files (x86)\nodejs\npx.exe",
            @"C:\ProgramData\chocolatey\bin\npx.exe",
        };
        foreach (var full in winCandidates)
        {
            if (!File.Exists(full)) continue;
            try
            {
                var psi = new ProcessStartInfo { FileName = full, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                psi.ArgumentList.Add("--version");
                using var proc = Process.Start(psi);
                if (proc == null) continue;
                var output = (proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd()).Trim();
                if (proc.WaitForExit(10000) && output.Length > 0) return ("ok", $"{full}  (v{output})");
            }
            catch { }
        }
        return ("error", "Не найден. Установите Node.js и перезапустите программу.");
    });

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
            return Path.Combine(inputDir, "SFAReports", AppConstants.ManpagesDbFileName);
        }
        var p = Path.GetFullPath(raw);
        return Path.GetExtension(p).Equals(".db", StringComparison.OrdinalIgnoreCase)
            ? p : Path.Combine(p, AppConstants.ManpagesDbFileName);
    }

    private async Task CheckManPagesAsync()
    {
        await Task.Run(() => { }); // краткая пауза для корректного обновления строки
        var dbPath = ManPagesDbPath();
        if (!File.Exists(dbPath))
        {
            ManPagesStatusText.Text = $"Статус: база не найдена ({dbPath}). Нажмите «Скачать и импортировать».";
            return;
        }
        try
        {
            var (count, version, sizeMb) = await Task.Run(() => ReadManPagesInfo(dbPath));
            ManPagesStatusText.Text =
                $"Статус: база найдена — {count} функций, man-pages {version}, {sizeMb:F1} МБ";
        }
        catch
        {
            ManPagesStatusText.Text = $"Статус: файл есть, но не читается ({dbPath}).";
        }
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
            ConfigService.Save(_cfg);
        }

        SyncManPagesButton.IsEnabled = false;
        CheckManPagesButton.IsEnabled = false;
        ManPagesStatusText.Text = "Статус: загрузка архива man-pages…";

        _manWorker = new ManPagesSyncWorker(dbPath);
        _manWorker.Progress += (msg, pct) => RunOnUi(() =>
            ManPagesStatusText.Text = $"Статус: {msg} ({pct}%)");
        _manWorker.ErrorOccurred += msg => RunOnUi(() =>
            ManPagesStatusText.Text = $"Статус: ошибка — {msg}");
        _manWorker.Finished += (success, message) => RunOnUi(async () =>
        {
            SyncManPagesButton.IsEnabled = true;
            CheckManPagesButton.IsEnabled = true;
            if (success)
            {
                _cfg.ManPagesDbPath = Path.GetDirectoryName(dbPath) ?? "";
                ConfigService.Save(_cfg);
                await UiDialogs.InfoAsync("Готово", $"Документация man-pages импортирована:\n{message}");
                await CheckManPagesAsync();
            }
            else
            {
                await UiDialogs.WarnAsync("Ошибка", $"Не удалось импортировать man-pages:\n{message}");
            }
            _manWorker = null;
        });
        _manWorker.Start();
    }

    private void RunOnUi(Action a) => DispatcherQueue.TryEnqueue(() => a());
}

/// <summary>Применение темы оформления (платформенной) к окну.</summary>
public static class ThemeHelper
{
    public static void Apply(Window? window, string theme)
    {
        if (window == null) return;
        var root = window.Content as FrameworkElement;
        if (root == null) return;
        root.RequestedTheme = theme == "dark" ? ElementTheme.Dark : ElementTheme.Light;
    }
}