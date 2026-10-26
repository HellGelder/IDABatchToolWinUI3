using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Потоки обработки одной пары по фазам: BinDiff (экспорт+диф+разбор),
/// Diaphora (экспорт+диф+слияние), пост-анализ (export.json + enrich +
/// hexdump + правда экспорта). Перенос Process*PairAsync из DiffWorker
/// при декомпозиции (пачка D) - поведение не менялось.
/// </summary>
public sealed class DiffPairProcessor
{
    private readonly string _outputDir;
    private readonly string _diaphoraScriptPath;
    private readonly BinDiffService _binDiff;
    private readonly DiaphoraService _diaphora;
    private readonly ExportJsonService _exportJson;
    private readonly Action<string> _onError;

    public DiffPairProcessor(string outputDir, string diaphoraScriptPath, BinDiffService binDiff,
        DiaphoraService diaphora, ExportJsonService exportJson, Action<string> onError)
    {
        _outputDir = outputDir;
        _diaphoraScriptPath = diaphoraScriptPath;
        _binDiff = binDiff;
        _diaphora = diaphora;
        _exportJson = exportJson;
        _onError = onError;
    }

    public async Task<bool> ProcessBindiffPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(_outputDir, $"{stem}.diff.json");
        var binexportP = Path.Combine(_outputDir, $"{stem}_primary.BinExport");
        var binexportS = Path.Combine(_outputDir, $"{stem}_secondary.BinExport");

        if (!await _binDiff.ExportBinExportAsync(p.Primary, binexportP, p.RelKey, ct)) return false;
        if (!await _binDiff.ExportBinExportAsync(p.Secondary, binexportS, p.RelKey, ct)) return false;

        var diffOutput = Path.Combine(_outputDir, $"{stem}.BinDiff");
        if (!await _binDiff.RunBindiffAsync(binexportP, binexportS, diffOutput, p.RelKey, ct))
        {
            _onError($"BinDiff: {stem}");
            return false;
        }
        BinDiffParser.Parse(diffOutput, p.Primary, p.Secondary, jsonOutput);
        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            _onError($"BinDiff {stem}: {reason}");
            return false;
        }
        return true;
    }

    public async Task<bool> ProcessDiaphoraPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(_outputDir, $"{stem}.diff.json");
        var dbPri = Path.Combine(_outputDir, $"{stem}_primary.diaphora.sqlite");
        var dbSec = Path.Combine(_outputDir, $"{stem}_secondary.diaphora.sqlite");
        var result = Path.Combine(_outputDir, $"{stem}_diaphora_result.sqlite");

        // Модули без функций (BinDiff насчитал 0) фаза Diaphora пропускает:
        // сравнение пустых БД падает в Diaphora (ZeroDivisionError, код 1).
        // Ориентир для такой пары - сравнение hexdump; в сводном индексе
        // показателем остаётся hexdump-схожесть.
        var bindiffJson = ReadJson(jsonOutput);
        if (bindiffJson != null && bindiffJson.TryGetValue("total_functions1", out var totalFuncs)
            && totalFuncs != null
            && Convert.ToInt64(totalFuncs, System.Globalization.CultureInfo.InvariantCulture) == 0)
            return true;

        if (!File.Exists(_diaphoraScriptPath))
        {
            _onError($"Diaphora не найден: {_diaphoraScriptPath}");
            return false;
        }

        var (priOk, priReason) = await _diaphora.ExportAsync(p.Primary, dbPri, p.RelKey, ct);
        if (!priOk)
        {
            _onError($"Diaphora экспорт primary {stem}: {priReason}");
            Cleanup(dbPri, dbSec, result);
            return false;
        }
        var (secOk, secReason) = await _diaphora.ExportAsync(p.Secondary, dbSec, p.RelKey, ct);
        if (!secOk)
        {
            _onError($"Diaphora экспорт secondary {stem}: {secReason}");
            Cleanup(dbPri, dbSec, result);
            return false;
        }

        if (!File.Exists(dbPri) || !File.Exists(dbSec))
        {
            _onError($"Diaphora диф {stem}: базы не созданы");
            Cleanup(dbPri, dbSec, result);
            return false;
        }

        var (diffOk, diffReason) = await _diaphora.DiffAsync(dbPri, dbSec, result, p.RelKey, ct);
        if (diffOk)
        {
            DiaphoraParser.MergeIntoJson(jsonOutput, result);
        }
        else
        {
            _onError($"Diaphora диф {stem}: {diffReason}");
        }

        Cleanup(dbPri, dbSec, result);
        if (!diffOk) return false;

        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            _onError($"Diaphora {stem}: {reason}");
            return false;
        }
        return true;
    }

    public async Task<bool> ProcessPostPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(_outputDir, $"{stem}.diff.json");
        var primJson = Path.Combine(_outputDir, $"{stem}_primary.export.json");
        var secJson = Path.Combine(_outputDir, $"{stem}_secondary.export.json");

        var exported = await _exportJson.ExportPairAsync(p.Primary, primJson, p.Secondary, secJson, p.RelKey, ct);
        if (exported.Primary == null || exported.Secondary == null)
        {
            _onError($"Пост-анализ {stem}: не удалось получить export.json");
            Cleanup(primJson, secJson);
            return false;
        }
        DiffJsonEnricher.Enrich(jsonOutput, exported.Primary, exported.Secondary);

        // Hexdump diff
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
                data["real_primary"] = orig1;
                data["real_secondary"] = orig2;
                // Аддитивно: отчёт показывает «не рассчитана» вместо отрицательного процента.
                if (skipped)
                {
                    data["hexdump_skipped"] = true;
                    data["hexdump_skip_reason"] = skipReason;
                }
                WriteJson(jsonOutput, data);
            }
            catch { /* не критично */ }
        }

        // Экспорт IDA - источник истины: согласуем matched/unmatched и тоталы
        // с реальным перечнем функций (доработка исполнения 1: 190fff3, ee9d761).
        DiffJsonEnricher.ApplyIdaExportTruth(jsonOutput, exported.Primary, exported.Secondary, _onError);

        Cleanup(primJson, secJson);

        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            _onError($"Пост-анализ {stem}: {reason}");
            return false;
        }
        return true;
    }
}
