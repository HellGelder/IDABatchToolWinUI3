using System.Diagnostics;
using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Фазы BinDiff для одной пары: экспорт BinExport через idat, запуск bindiff.exe.
/// Перенос из DiffWorker при декомпозиции (пачка D) — поведение не менялось.
/// </summary>
public sealed class BinDiffService
{
    private readonly DiffProcessRunner _proc;
    private readonly string _idatPath;
    private readonly string _bindiffPath;
    private readonly Action<string>? _onError;

    public BinDiffService(DiffProcessRunner proc, string idatPath, string bindiffPath, Action<string>? onError = null)
    {
        _proc = proc;
        _idatPath = idatPath;
        _bindiffPath = bindiffPath;
        _onError = onError;
    }

    /// <summary>Экспорт BinExport из базы IDA (idat + BinExportAutoAction).</summary>
    public async Task<bool> ExportBinExportAsync(string i64Path, string outputFile, string relKey, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (File.Exists(outputFile)) File.Delete(outputFile);

        var psi = DiffProcessRunner.CreateIdatPsi(_idatPath);
        psi.ArgumentList.Add("-OBinExportAutoAction:BinExportBinary");
        psi.ArgumentList.Add($"-OBinExportModule:{outputFile}");
        psi.ArgumentList.Add(i64Path);
        var (rc, _, _) = await _proc.RunAsync(psi, relKey, ct);
        return rc == 0 && File.Exists(outputFile);
    }

    /// <summary>Запуск bindiff.exe для пары BinExport-файлов; результат перемещается в output.</summary>
    public async Task<bool> RunBindiffAsync(string primary, string secondary, string output, string relKey, CancellationToken ct)
    {
        var tmpDir = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "_tmp");
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
        Directory.CreateDirectory(tmpDir);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _bindiffPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--primary"); psi.ArgumentList.Add(primary);
            psi.ArgumentList.Add("--secondary"); psi.ArgumentList.Add(secondary);
            psi.ArgumentList.Add("--output_dir"); psi.ArgumentList.Add(tmpDir);
            var (rc, _, _) = await _proc.RunAsync(psi, relKey, ct);
            if (rc != 0) return false;

            var diffFiles = Directory.GetFiles(tmpDir, "*.BinDiff");
            if (diffFiles.Length == 0) return false;
            if (File.Exists(output)) File.Delete(output);
            File.Move(diffFiles[0], output);
            return true;
        }
        catch (Exception e)
        {
            _onError?.Invoke($"BinDiff: {e.Message}");
            return false;
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
