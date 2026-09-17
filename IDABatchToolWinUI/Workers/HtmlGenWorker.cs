using System.Diagnostics;
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
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
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
        if (jsonPaths is { Count: > 0 })
        {
            psi.ArgumentList.Add("--json-paths");
            psi.ArgumentList.Add(string.Join(";", jsonPaths));
        }

        _proc = Process.Start(psi);
        if (_proc == null)
        {
            ErrorOccurred?.Invoke("Не удалось запустить процесс генерации отчётов.");
            Finished?.Invoke(new HtmlGenResult { GeneratedCount = 0 });
            return;
        }

        var result = new HtmlGenResult { Platform = Platform, InputDir = inputDir, ReportsDir = reportsDir };
        var stdoutTask = ReadStdoutAsync(_proc, result);
        var stderrTask = _proc.StandardError.ReadToEndAsync();
        _proc.WaitForExit();
        Task.WhenAll(stdoutTask, stderrTask).GetAwaiter().GetResult();

        if (_proc.ExitCode != 0 && result.GeneratedCount == 0)
        {
            var err = stderrTask.Result.Trim();
            if (!string.IsNullOrEmpty(err)) ErrorOccurred?.Invoke(err);
        }
        Finished?.Invoke(result);
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
        }
        catch { /* неудачный JSON — игнорируем */ }
    }

    public void Cancel()
    {
        try { _proc?.Kill(entireProcessTree: true); } catch { /* процесс мог завершиться */ }
    }

    public void Dispose() => _proc?.Dispose();
}