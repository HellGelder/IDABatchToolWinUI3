using System.Diagnostics;
using System.Text;
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
    private bool _finished;
    private string _doneMessage = "";
    private string _lastError = "";

    public event Action<string, int>? Progress;   // (сообщение, проценты)
    public event Action<string>? ErrorOccurred;
    public event Action<bool, string>? Finished;  // (успех, сообщение)

    public ManPagesSyncWorker(string dbPath)
    {
        _dbPath = dbPath;
        _bridge = Path.Combine(AppConstants.WinUiDir, "_python", "manpages_bridge.py");
    }

    /// <summary>
    /// Запуск синхронизации: управление возвращается сразу, события приходят асинхронно.
    /// Раньше Start() был синхронным и вызывался с UI-потока: WaitForExit замораживал окно
    /// на всю загрузку, а ожидание stdout-задачи на заблокированном UI-потоке давало дедлок.
    /// </summary>
    public void Start() => _ = RunSafeAsync();

    private Task RunSafeAsync()
    {
        try
        {
            return RunAsync();
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Синхронизация man-pages: {e.Message}");
            Finish(false, e.Message);
            return Task.CompletedTask;
        }
    }

    private async Task RunAsync()
    {
        if (!File.Exists(_bridge))
        {
            ErrorOccurred?.Invoke($"Скрипт синхронизации man-pages не найден: {_bridge}");
            Finish(false, "Скрипт не найден");
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
        // Протокол моста — UTF-8 (иначе pythonw пишет в cp1251 и русские
        // сообщения прогресса в GUI нечитаемы).
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.ArgumentList.Add(_bridge);
        psi.ArgumentList.Add(_dbPath);

        Process proc;
        try
        {
            proc = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start вернул null");
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Не удалось запустить синхронизацию man-pages: {e.Message}");
            Finish(false, "Процесс не запущен");
            return;
        }
        _proc = proc;

        // stderr читается параллельно: без читателя заполненный буфер канала
        // подвешивал бы сам мост (Python блокируется на записи в stderr).
        var stderrTask = proc.StandardError.ReadToEndAsync();

        await ReadStdoutAsync(proc);

        try { await proc.WaitForExitAsync(); } catch { /* процесс убит при отмене */ }

        // Успех — только при явном DONE/OK от моста: при ошибке мост печатает
        // ERROR и завершается с кодом 0, поэтому одного кода выхода мало.
        var ok = proc.ExitCode == 0 && !string.IsNullOrEmpty(_doneMessage);
        if (ok)
        {
            Finish(true, _doneMessage);
            return;
        }

        string stderr = "";
        try { stderr = (await stderrTask).Trim(); } catch { /* канал закрыт */ }
        var msg = _lastError.Length > 0 ? _lastError
            : stderr.Length > 0 ? stderr
            : $"Процесс завершился с кодом {proc.ExitCode}";
        Finish(false, msg);
    }

    private async Task ReadStdoutAsync(Process proc)
    {
        try
        {
            string? line;
            while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
            {
                if (line.StartsWith("PROGRESS ", StringComparison.Ordinal))
                {
                    var rest = line.Substring(9);
                    var sp = rest.IndexOf(' ');
                    if (sp > 0 && int.TryParse(rest[..sp], out var pct))
                        Progress?.Invoke(rest[(sp + 1)..], pct);
                }
                else if (line.StartsWith("ERROR ", StringComparison.Ordinal))
                {
                    _lastError = line.Substring(6);
                    ErrorOccurred?.Invoke(_lastError);
                }
                else if (line.StartsWith("DONE ", StringComparison.Ordinal))
                    _doneMessage = line.Substring(5);
                else if (line.StartsWith("OK ", StringComparison.Ordinal))
                    _doneMessage = line.Substring(3);
            }
        }
        catch { /* процесс завершён/убит — канал закрыт */ }
    }

    private void Finish(bool ok, string message)
    {
        if (_finished) return;
        _finished = true;
        Finished?.Invoke(ok, message);
    }

    public void Cancel()
    {
        try { _proc?.Kill(entireProcessTree: true); } catch { /* уже завершился */ }
    }

    public void Dispose() => _proc?.Dispose();
}