using IDABatchToolWinUI.Models;
using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Доанализ Diaphora (engine=bindiff): пары с similarity &lt; 99% прогоняются
/// через Diaphora повторно в отдельной папке AddDiffResults, итог помечается
/// маркером add_analysis_done (с нейтральной заметкой при отсутствии новых пар).
/// Перенос из DiffWorker при декомпозиции (пачка D) — поведение не менялось.
/// </summary>
public sealed class AddAnalysisRunner
{
    private readonly string _outputDir;
    private readonly string? _addOutputDir;
    private readonly string _leftDir;
    private readonly string _rightDir;
    private readonly string _diaphoraPath;
    private readonly DiaphoraService _diaphora;
    private readonly ExportJsonService _exportJson;
    private readonly DiffPassRunner _pass;
    private readonly Action<string, string> _stageChanged;   // (stage, "started"|"done")
    private readonly Action<string> _onError;

    public AddAnalysisRunner(string outputDir, string? addOutputDir, string leftDir, string rightDir,
        string diaphoraPath, DiaphoraService diaphora, ExportJsonService exportJson, DiffPassRunner pass,
        Action<string, string> stageChanged, Action<string> onError)
    {
        _outputDir = outputDir;
        _addOutputDir = addOutputDir;
        _leftDir = leftDir;
        _rightDir = rightDir;
        _diaphoraPath = diaphoraPath;
        _diaphora = diaphora;
        _exportJson = exportJson;
        _pass = pass;
        _stageChanged = stageChanged;
        _onError = onError;
    }

    /// <summary>Доанализ уже успешно завершён: в add-JSON стоит маркер add_analysis_done.
    /// Факт существования файла маркером не является — там может лежать
    /// необработанная копия исходного diff.json.</summary>
    public bool IsAddAnalysisDone(string stem)
    {
        if (string.IsNullOrEmpty(_addOutputDir)) return false;
        var path = Path.Combine(_addOutputDir, $"{stem}.diff.json");
        if (!File.Exists(path)) return false;
        var d = ReadJson(path);
        return d != null && d.TryGetValue("add_analysis_done", out var v) && v is true;
    }

    public int CountLowSimilarityPairs(List<DiffPair> pairs)
    {
        int low = 0;
        foreach (var p in pairs)
        {
            var d = Path.Combine(_outputDir, $"{p.Stem}.diff.json");
            if (!File.Exists(d)) continue;
            if (IsAddAnalysisDone(p.Stem)) continue;
            if (IdenticalPairsService.IsIdenticalOrFullHexdump(d)) continue;
            try
            {
                var data = ReadJson(d);
                var sim = data != null && data.TryGetValue("similarity", out var v) && v != null
                    ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) : 0.0;
                if (sim < 0.99) low++;
            }
            catch { /* пропускаем */ }
        }
        return low;
    }

    public async Task RunAsync(List<DiffPair> pairs, CancellationToken ct)
    {
        if (!File.Exists(_diaphoraPath))
        {
            _onError("Diaphora не найден — доанализ для пар <99% пропущен");
            return;
        }

        var low = pairs.Where(p =>
        {
            var stem = p.Stem;
            var d = Path.Combine(_outputDir, $"{stem}.diff.json");
            if (!File.Exists(d)) return false;
            if (IsAddAnalysisDone(stem)) return false;
            if (IdenticalPairsService.IsIdenticalOrFullHexdump(d)) return false;
            try
            {
                var data = ReadJson(d);
                var sim = data != null && data.TryGetValue("similarity", out var v) && v != null
                    ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) : 0.0;
                return sim < 0.99;
            }
            catch { return false; }
        }).ToList();

        if (low.Count == 0) return;
        _onError($"Доанализ Diaphora: {low.Count} пар с similarity < 99%");

        Directory.CreateDirectory(_addOutputDir!);
        foreach (var p in low)
        {
            var src = Path.Combine(_outputDir, $"{p.Stem}.diff.json");
            var dst = Path.Combine(_addOutputDir!, $"{p.Stem}.diff.json");
            if (File.Exists(src)) File.Copy(src, dst, overwrite: true);
        }

        _stageChanged("Доанализ (Diaphora)", "started");
        await _pass.RunPass("Доанализ (Diaphora)", low, low.Count, "diaphora", "Доанализ (Diaphora)",
            p => ProcessAddDiaphoraPairAsync(p, ct), ct, parallelLarge: true, trackOutcome: false);
        _stageChanged("Доанализ (Diaphora)", "done");
        if (ct.IsCancellationRequested) return;

        _stageChanged("Доанализ (пост-анализ)", "started");
        await _pass.RunPass("Доанализ (пост-анализ)", low, low.Count, "diaphora", "Доанализ (пост-анализ)",
            p => ProcessAddPostPairAsync(p, ct), ct, parallelLarge: true, trackOutcome: false);
        _stageChanged("Доанализ (пост-анализ)", "done");
        if (ct.IsCancellationRequested) return;

        _stageChanged("Генерация HTML (доанализ)", "started");
        await GenerateAddReportsAsync(low, ct);
        _stageChanged("Генерация HTML (доанализ)", "done");
    }

    private async Task<bool> ProcessAddDiaphoraPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(_addOutputDir!, $"{stem}.diff.json");
        var dbPri = Path.Combine(_addOutputDir!, $"{stem}_primary.diaphora.sqlite");
        var dbSec = Path.Combine(_addOutputDir!, $"{stem}_secondary.diaphora.sqlite");
        var result = Path.Combine(_addOutputDir!, $"{stem}_diaphora_result.sqlite");

        // Причина провала — в отчёт доанализа (add_analysis_note): модуль не удалось
        // проанализировать (экспорт/сравнение не состоялись — например, БД не росла).
        string? note = null;
        var (priOk, priReason) = await _diaphora.ExportAsync(p.Primary, dbPri, p.RelKey, ct);
        if (!priOk)
        {
            Cleanup(dbPri, dbSec, result);
            MarkAddAnalysisDone(jsonOutput, false,
                $"не удался экспорт основной БД ({Path.GetFileName(p.Primary)}): {priReason}");
            return false;
        }
        var (secOk, secReason) = await _diaphora.ExportAsync(p.Secondary, dbSec, p.RelKey, ct);
        if (!secOk)
        {
            Cleanup(dbPri, dbSec, result);
            MarkAddAnalysisDone(jsonOutput, false,
                $"не удался экспорт сравниваемой БД ({Path.GetFileName(p.Secondary)}): {secReason}");
            return false;
        }

        var merged = false;
        if (File.Exists(dbPri) && File.Exists(dbSec))
        {
            var (diffOk, diffReason) = await _diaphora.DiffAsync(dbPri, dbSec, result, p.RelKey, ct);
            if (diffOk)
            {
                DiaphoraParser.MergeIntoJson(jsonOutput, result);
                merged = true;
                // Diaphora отработала штатно, но ни одной новой пары не добавила —
                // так и пишем в отчёт (нейтрально, без «ошибки»).
                var newPairs = 0;
                if (ReadJson(jsonOutput) is { } mergedData &&
                    mergedData.TryGetValue("matched_diaphora_only", out var md) && md is List<object?> ml)
                    newPairs = ml.Count;
                if (newPairs == 0) note = "Доанализ Diaphora дополнительных пар не дал — все совпадения уже найдены BinDiff.";
            }
            else note = diffReason;
        }
        if (!merged && note == null)
            note = "сравнение БД не дало результата (файл результата не создан)";
        // Маркер успешного доанализа — по нему пара исключается из следующих прогонов
        MarkAddAnalysisDone(jsonOutput, merged, note);
        Cleanup(dbPri, dbSec, result);
        return merged;
    }

    /// <summary>Ставит (или снимает) маркер add_analysis_done в add-JSON пары, note —
    /// причина провала либо нейтральный итог (рендерится баннером в HTML).</summary>
    private static void MarkAddAnalysisDone(string jsonPath, bool done, string? failureNote = null)
    {
        try
        {
            if (!File.Exists(jsonPath)) return;
            var data = ReadJson(jsonPath);
            if (data == null) return;
            data["add_analysis_done"] = done;
            if (failureNote == null) data.Remove("add_analysis_note");
            else data["add_analysis_note"] = failureNote;
            WriteJson(jsonPath, data);
        }
        catch { /* не критично */ }
    }

    private async Task<bool> ProcessAddPostPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(_addOutputDir!, $"{stem}.diff.json");
        var primJson = Path.Combine(_addOutputDir!, $"{stem}_primary.export.json");
        var secJson = Path.Combine(_addOutputDir!, $"{stem}_secondary.export.json");

        var exported = await _exportJson.ExportPairAsync(p.Primary, primJson, p.Secondary, secJson, p.RelKey, ct);
        if (exported.Primary == null || exported.Secondary == null)
        {
            _onError($"Доанализ {stem}: не удалось получить export.json");
            Cleanup(primJson, secJson);
            return false;
        }
        DiffJsonEnricher.Enrich(jsonOutput, exported.Primary, exported.Secondary);

        var orig1 = IdenticalPairsService.FindOriginalBinary(p.Primary);
        var orig2 = IdenticalPairsService.FindOriginalBinary(p.Secondary);
        if (orig1 != null && orig2 != null)
        {
            try
            {
                var (sim, skipped, skipReason) = HexdumpDiffService.ComputeSimilarity(orig1, orig2);
                var data = ReadJson(jsonOutput) ?? new Dictionary<string, object?>();
                data["global_hex_diff"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["name1"] = Path.GetFileName(orig1), ["path1"] = orig1,
                        ["name2"] = Path.GetFileName(orig2), ["path2"] = orig2,
                        ["hexdump_similarity"] = skipped ? -1.0 : sim,
                    }
                };
                data["hexdump_similarity"] = skipped ? -1.0 : sim;
                if (skipped)
                {
                    data["hexdump_skipped"] = true;
                    data["hexdump_skip_reason"] = skipReason;
                }
                WriteJson(jsonOutput, data);
            }
            catch { /* не критично */ }
        }

        // Экспорт IDA — источник истины (доработка исполнения 1: 190fff3, ee9d761).
        DiffJsonEnricher.ApplyIdaExportTruth(jsonOutput, exported.Primary, exported.Secondary, _onError);

        Cleanup(primJson, secJson);

        if (!IDABatchToolWinUI.Services.ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            _onError($"Доанализ {stem}: {reason}");
            return false;
        }
        return true;
    }

    private async Task GenerateAddReportsAsync(List<DiffPair> pairs, CancellationToken ct)
    {
        try
        {
            var reportsDir = Path.Combine(_addOutputDir!, "Reports");
            Directory.CreateDirectory(reportsDir);
            var jsonFiles = Directory.GetFiles(_addOutputDir!, "*.diff.json").ToList();
            if (jsonFiles.Count == 0) return;

            using var worker = new Workers.HtmlGenWorker("diff", deleteJson: false, reuseCache: false, platform: "Windows");
            worker.ProgressUpdated += (cur, tot, msg) => _stageFile?.Invoke("Генерация HTML (доанализ)", cur, tot, msg);
            worker.ErrorOccurred += e => _onError(e);
            using var cancelRegistration = ct.Register(worker.Cancel);

            await Task.Run(() => worker.Run(
                inputDir: _leftDir, reportsDir: reportsDir, jsonDir: _addOutputDir!,
                leftDir: _leftDir, rightDir: _rightDir, manpagesDb: null,
                jsonPaths: jsonFiles), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            _onError($"Ошибка генерации отчётов (доанализ): {e.Message}");
        }
    }

    private Action<string, int, int, string>? _stageFile; // (stage, current, total, fileName)

    public Action<string, int, int, string>? StageFile
    {
        get => _stageFile;
        set => _stageFile = value;
    }
}
