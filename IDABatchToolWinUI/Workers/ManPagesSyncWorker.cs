using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI.Workers;

/// <summary>
/// Синхронизация документации man-pages (Linux) — аналог ManPagesSyncWorker из исполнения 1.
/// Скачивает официальный архив man-pages и импортирует его в SQLite через Python-модули
/// (man_pages_db / man_pages_sync). Процесс идёт в фоне с прогрессом и отменой.
/// </summary>
public sealed class ManPagesSyncWorker : IDisposable
{
    private readonly string _dbPath;
    private Process? _proc;
    private readonly string _bridge;

    public event Action<string, int>? Progress;   // (сообщение, проценты)
    public event Action<string>? ErrorOccurred;
    public event Action<bool, string>? Finished;  // (успех, сообщение)

    public ManPagesSyncWorker(string dbPath)
    {
        _dbPath = dbPath;
        _bridge = Path.Combine(AppConstants.WinUiDir, "_python", "manpages_bridge.py");
    }

    public void Start()
    {
        if (!File.Exists(_bridge))
        {
            ErrorOccurred?.Invoke($"Скрипт синхронизации man-pages не найден: {_bridge}");
            Finished?.Invoke(false, "Скрипт не найден");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = PythonHelper.ResolvePython(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add(_bridge);
        psi.ArgumentList.Add(_dbPath);

        _proc = Process.Start(psi);
        if (_proc == null)
        {
            ErrorOccurred?.Invoke("Не удалось запустить синхронизацию man-pages.");
            Finished?.Invoke(false, "Процесс не запущен");
            return;
        }

        var stdout = ReadStdoutAsync();
        var stderrTask = _proc.StandardError.ReadToEndAsync();
        _proc.WaitForExit();
        stdout.GetAwaiter().GetResult();
    }

    private async Task ReadStdoutAsync()
    {
        string? line;
        while ((line = await _proc!.StandardOutput.ReadLineAsync()) != null)
        {
            if (line.StartsWith("PROGRESS ", StringComparison.Ordinal))
            {
                var rest = line.Substring(9);
                var sp = rest.IndexOf(' ');
                if (sp > 0 && int.TryParse(rest[..sp], out var pct))
                    Progress?.Invoke(rest[(sp + 1)..], pct);
            }
            else if (line.StartsWith("ERROR ", StringComparison.Ordinal))
                ErrorOccurred?.Invoke(line.Substring(6));
            else if (line.StartsWith("DONE ", StringComparison.Ordinal))
                _doneMessage = line.Substring(5);
            else if (line.StartsWith("OK ", StringComparison.Ordinal))
                _doneMessage = line.Substring(3);
        }
        Finished?.Invoke(_proc!.ExitCode == 0, _doneMessage);
    }

    private string _doneMessage = "";

    public void Cancel()
    {
        try { _proc?.Kill(entireProcessTree: true); } catch { /* уже завершился */ }
    }

    public void Dispose() => _proc?.Dispose();
}

public static class ManPagesBridgeScript
{
    // Дублируется в _python/manpages_bridge.py — путь к БД передаётся аргументом.
    public const string ManpagesArchiveUrl = "https://www.kernel.org/pub/linux/docs/man-pages/man-pages.tar.gz";
}