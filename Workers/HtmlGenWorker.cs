using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI.Workers;

/// <summary>
/// Результат генерации HTML-отчётов (аналог HtmlGenerationResult / SfaHtmlGenerationResult).
/// </summary>
public sealed class HtmlGenResult
{
    public int GeneratedCount { get; set; }
    public string? ReportsDir { get; set; }
    public string? InputDir { get; set; }
    public string? IndexPath { get; set; }
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public int TotalSystemModules { get; set; }
    public int TotalSystemFunctions { get; set; }
    public int TotalSystemNotfound { get; set; }
    public int TotalImports { get; set; }
    public string Platform { get; set; } = "Windows";
    // Статистика предварительного поиска документации (kind = sfa-docs)
    public int SearchedCount { get; set; }
    public int FoundCount { get; set; }
    public int CachedTotal { get; set; }
}

/// <summary>
/// Запускает Python-bridge (_python/report_bridge.py) в фоновом режиме для генерации
/// HTML-отчётов (Общий анализ / СФ / Сравнение). Читает PROGRESS/ERROR/RESULT строки
/// и даёт прогресс на UI-поток. Отмена — завершение процесса.
/// </summary>
public sealed class HtmlGenWorker : IDisposable
{
    public event Action<int, int, string>? ProgressUpdated;   // (current, total, message)
    public event Action<string>? ErrorOccurred;
    public event Action<HtmlGenResult>? Finished;

    private Process? _proc;
    private StreamWriter? _stdin;

    public string Kind { get; }          // analysis | sfa | diff
    public bool DeleteJson { get; }
    public bool ReuseCache { get; }
    public string Platform { get; }

    public HtmlGenWorker(string kind, bool deleteJson, bool reuseCache, string platform)
    {
        Kind = kind;
        DeleteJson = deleteJson;
        ReuseCache = reuseCache;
        Platform = platform;
    }

    public void Run(string inputDir, string reportsDir, string jsonDir,
                    string? leftDir, string? rightDir, string? manpagesDb,
                    IReadOnlyList<string>? jsonPaths = null)
    {
        // Страница ждёт Run через await Task.Run внутри async void: необработанное
        // исключение здесь уронило бы приложение — гасим в событие.
        try
        {
            RunCore(inputDir, reportsDir, jsonDir, leftDir, rightDir, manpagesDb, jsonPaths);
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Ошибка процесса генерации: {e.Message}");
            Finished?.Invoke(new HtmlGenResult { GeneratedCount = 0 });
        }
    }

    private void RunCore(string inputDir, string reportsDir, string jsonDir,
                         string? leftDir, string? rightDir, string? manpagesDb,
                         IReadOnlyList<string>? jsonPaths)
    {
        var bridge = Path.Combine(AppConstants.WinUiDir, "_python", "report_bridge.py");
        if (!File.Exists(bridge))
        {
            ErrorOccurred?.Invoke($"Скрипт генерации отчётов не найден: {bridge}");
            Finished?.Invoke(new HtmlGenResult { GeneratedCount = 0 });
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = PythonHelper.ResolvePython(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // Протокол моста — UTF-8: без этого pythonw пишет в системной кодировке
        // (cp1251), и русские сообщения в GUI превращаются в нечитаемые символы.
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.ArgumentList.Add(bridge);
        psi.ArgumentList.Add("generate");
        psi.ArgumentList.Add("--kind"); psi.ArgumentList.Add(Kind);
        psi.ArgumentList.Add("--input-dir"); psi.ArgumentList.Add(inputDir);
        psi.ArgumentList.Add("--reports-dir"); psi.ArgumentList.Add(reportsDir);
        psi.ArgumentList.Add("--json-dir"); psi.ArgumentList.Add(jsonDir);
        if (!string.IsNullOrEmpty(leftDir)) { psi.ArgumentList.Add("--left-dir"); psi.ArgumentList.Add(leftDir); }
        if (!string.IsNullOrEmpty(rightDir)) { psi.ArgumentList.Add("--right-dir"); psi.ArgumentList.Add(rightDir); }
        if (!string.IsNullOrEmpty(manpagesDb)) { psi.ArgumentList.Add("--manpages-db"); psi.ArgumentList.Add(manpagesDb); }
        psi.ArgumentList.Add("--platform"); psi.ArgumentList.Add(Platform);
        if (DeleteJson) psi.ArgumentList.Add("--delete-json");
        if (ReuseCache) psi.ArgumentList.Add("--reuse-cache");
        // Список JSON передаётся файлом-ответкой: строка "a;b;c" в командной
        // строке упирается в лимит CreateProcess ~32 КБ (Win32 ошибка 206,
        // «имя файла или его расширение имеет слишком большую длину») уже на
        // нескольких сотнях файлов с длинными путями.
        string? pathsFile = null;
        if (jsonPaths is { Count: > 0 })
        {
            try
            {
                pathsFile = Path.Combine(Path.GetTempPath(),
                    $"idabatchtool_json_{Guid.NewGuid():N}.txt");
                File.WriteAllLines(pathsFile, jsonPaths,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                psi.ArgumentList.Add("--json-paths-file");
                psi.ArgumentList.Add(pathsFile);
            }
            catch
            {
                // %TEMP% недоступен — прежний способ через командную строку
                // (ограничен по длине, но лучше, чем сорванная генерация).
                pathsFile = null;
                psi.ArgumentList.Add("--json-paths");
                psi.ArgumentList.Add(string.Join(";", jsonPaths));
            }
        }

        try
        {
            _proc = Process.Start(psi);
            if (_proc == null)
            {
                ErrorOccurred?.Invoke("Не удалось запустить процесс генерации отчётов.");
                Finished?.Invoke(new HtmlGenResult { GeneratedCount = 0 });
                return;
            }
            _stdin = _proc.StandardInput;

            var result = new HtmlGenResult { Platform = Platform, InputDir = inputDir, ReportsDir = reportsDir };
            var stdoutTask = ReadStdoutAsync(_proc, result);
            var stderrTask = _proc.StandardError.ReadToEndAsync();
            _proc.WaitForExit();
            Task.WhenAll(stdoutTask, stderrTask).GetAwaiter().GetResult();

            if (_proc.ExitCode != 0 && result.GeneratedCount == 0)
            {
                string err = "";
                try { err = stderrTask.Result.Trim(); } catch { /* канал закрыт */ }
                if (!string.IsNullOrEmpty(err)) ErrorOccurred?.Invoke(err);
            }
            Finished?.Invoke(result);
        }
        finally
        {
            if (pathsFile != null)
            {
                try { File.Delete(pathsFile); } catch { /* занят или уже удалён */ }
            }
        }
    }

    private async Task ReadStdoutAsync(Process proc, HtmlGenResult result)
    {
        string? line;
        while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
        {
            if (line.StartsWith("PROGRESS ", StringComparison.Ordinal))
            {
                var parts = line.Substring(9).Split(' ', 3);
                if (parts.Length >= 2 && int.TryParse(parts[0], out var cur) &&
                    int.TryParse(parts[1], out var total))
                {
                    var msg = parts.Length >= 3 ? parts[2] : "";
                    result.TotalFiles = total;
                    ProgressUpdated?.Invoke(cur, total, msg);
                }
            }
            else if (line.StartsWith("ERROR ", StringComparison.Ordinal))
                ErrorOccurred?.Invoke(line.Substring(6));
            else if (line.StartsWith("RESULT ", StringComparison.Ordinal))
                ParseResult(line.Substring(7), result);
        }
    }

    private void ParseResult(string json, HtmlGenResult r)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("generated_count", out var gc) && gc.ValueKind == JsonValueKind.Number)
                r.GeneratedCount = gc.GetInt32();
            if (root.TryGetProperty("index_path", out var ip)) r.IndexPath = ip.GetString();
            if (root.TryGetProperty("total_files", out var tf)) r.TotalFiles = tf.GetInt32();
            if (root.TryGetProperty("total_size_bytes", out var ts)) r.TotalSizeBytes = ts.GetInt64();
            if (root.TryGetProperty("total_system_modules", out var m)) r.TotalSystemModules = m.GetInt32();
            if (root.TryGetProperty("total_system_functions", out var fn)) r.TotalSystemFunctions = fn.GetInt32();
            if (root.TryGetProperty("total_system_notfound", out var nf)) r.TotalSystemNotfound = nf.GetInt32();
            if (root.TryGetProperty("total_imports", out var ti)) r.TotalImports = ti.GetInt32();
            if (root.TryGetProperty("platform", out var pf)) r.Platform = pf.GetString() ?? r.Platform;
            if (root.TryGetProperty("reports_dir", out var rd)) r.ReportsDir = rd.GetString();
            if (root.TryGetProperty("searched", out var sd) && sd.ValueKind == JsonValueKind.Number)
                r.SearchedCount = sd.GetInt32();
            if (root.TryGetProperty("found", out var fd) && fd.ValueKind == JsonValueKind.Number)
                r.FoundCount = fd.GetInt32();
            if (root.TryGetProperty("cached_total", out var cd) && cd.ValueKind == JsonValueKind.Number)
                r.CachedTotal = cd.GetInt32();
        }
        catch { /* неудачный JSON — игнорируем */ }
    }

    public void Cancel()
    {
        // Мягкая отмена: мост следит за stdin, при строке CANCEL убивает все
        // процессы поиска документации (npx/node, дерево taskkill /T) и
        // завершает генерацию сам.
        try { _stdin?.WriteLine("CANCEL"); _stdin?.Flush(); }
        catch { /* процесс уже завершился */ }

        // Страховка: если мост не завершился за 5 секунд — убиваем всё дерево.
        var proc = _proc;
        if (proc == null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* уже завершён */ }
            }
            catch { /* уже завершён */ }
        });
    }

    public void Dispose()
    {
        try { _stdin?.Dispose(); } catch { /* уже закрыт */ }
        _proc?.Dispose();
    }
}