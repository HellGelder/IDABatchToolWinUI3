using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Экспорт export.json из баз IDA через export_data.py (idat) для пары сторон.
/// Перенос из DiffWorker при декомпозиции (пачка D) — поведение не менялось.
/// </summary>
public sealed class ExportJsonService
{
    private readonly DiffProcessRunner _proc;
    private readonly string _idatPath;
    private readonly string _exportScript;

    public ExportJsonService(DiffProcessRunner proc, string idatPath, string exportScript)
    {
        _proc = proc;
        _idatPath = idatPath;
        _exportScript = exportScript;
    }

    public async Task<(string? Primary, string? Secondary)> ExportPairAsync(
        string primaryI64, string primaryOut, string secondaryI64, string secondaryOut, string relKey, CancellationToken ct)
    {
        var result = (Primary: (string?)null, Secondary: (string?)null);
        foreach (var (i64, outPath, isPrimary) in new[]
                 {
                     (primaryI64, primaryOut, true),
                     (secondaryI64, secondaryOut, false),
                 })
        {
            if (ct.IsCancellationRequested) break;

            // Старый export.json не должен пережить неудачный запуск и попасть в отчёт.
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { /* файл занят */ }

            var psi = DiffProcessRunner.CreateIdatPsi(_idatPath);
            psi.ArgumentList.Add("-A");
            psi.ArgumentList.Add($"-S\"{_exportScript}\" pseudocode=1");
            psi.ArgumentList.Add(i64);
            var (rc, _, _) = await _proc.RunAsync(psi, relKey, ct);
            if (rc != 0) continue;

            var src = i64 + ".export.json";
            if (File.Exists(src) && src != outPath)
            {
                if (File.Exists(outPath)) File.Delete(outPath);
                File.Move(src, outPath);
            }
            if (IDABatchToolWinUI.Services.ArtifactValidator.IsValidExportJson(outPath, out _))
            {
                if (isPrimary) result.Primary = outPath;
                else result.Secondary = outPath;
            }
            else if (File.Exists(outPath))
            {
                try { File.Delete(outPath); } catch { /* файл занят */ }
            }
        }
        return result;
    }
}
