using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Проход фазы по списку пар: статусы колонок, параллельная обработка малых пар
/// через семафор, последовательная — крупных, счётчик прогресса, учёт провалов.
/// Перенос RunPass из DiffWorker при декомпозиции (пачка D) — поведение не менялось.
/// </summary>
public sealed class DiffPassRunner
{
    private readonly string _engine;
    private readonly int _maxWorkers;
    private readonly Action<string, string, string> _pairStatus;   // (relKey, engine, status)
    private readonly Action<string, int, int, string> _stageFile;  // (stage, current, total, fileName)
    private readonly Action<string> _errorOccurred;

    /// <summary>Учёт результатов создаётся на каждый прогон — выставляется перед запуском.</summary>
    public OutcomeTracker? Outcomes { get; set; }

    public DiffPassRunner(string engine, int maxWorkers,
        Action<string, string, string> pairStatus,
        Action<string, int, int, string> stageFile,
        Action<string> errorOccurred)
    {
        _engine = engine;
        _maxWorkers = maxWorkers;
        _pairStatus = pairStatus;
        _stageFile = stageFile;
        _errorOccurred = errorOccurred;
    }

    public async Task RunPass(
        string stage, List<DiffPair> pairs, int total, string? engine,
        string statusText,
        Func<DiffPair, Task<bool>> process, CancellationToken ct,
        bool parallelLarge = false, bool trackOutcome = true)
    {
        if (pairs.Count == 0) return;

        // Начало фазы: статус этапа для задействованных колонок
        // (не «waiting» — колонки других движков уже содержат их результат).
        foreach (var p in pairs)
            if (engine == null)
            {
                if (_engine is "bindiff" or "both") _pairStatus(p.RelKey, "bindiff", statusText);
                if (_engine is "diaphora" or "both") _pairStatus(p.RelKey, "diaphora", statusText);
            }
            else _pairStatus(p.RelKey, engine, statusText);

        _stageFile(stage, 0, total, "");

        // parallelLarge=true — крупные файлы тоже идут через общий семафор
        // (доанализ Diaphora должен быть многопоточным; в исполнении 1 крупные
        // файлы всегда последовательны — для основных фаз сохраняем это поведение).
        var small = parallelLarge ? pairs : pairs.Where(p => !DiffProcessRunner.IsLarge(p.Primary)).ToList();
        var large = parallelLarge ? new List<DiffPair>() : pairs.Where(p => DiffProcessRunner.IsLarge(p.Primary)).ToList();
        var completed = 0;

        async Task RunOne(DiffPair p)
        {
            // Ставим «анализ» для задействованного движка (engine == null -> оба)
            if (engine == null)
            {
                if (_engine is "bindiff" or "both") _pairStatus(p.RelKey, "bindiff", "analysis");
                if (_engine is "diaphora" or "both") _pairStatus(p.RelKey, "diaphora", "analysis");
            }
            else _pairStatus(p.RelKey, engine, "analysis");

            bool ok;
            try { ok = await process(p); }
            catch (Exception e)
            {
                _errorOccurred($"Ошибка {p.RelKey} в фазе {stage}: {e.Message}");
                ok = false;
            }
            var status = ok ? "done" : "error";
            if (!ok && trackOutcome) Outcomes?.Fail(p.Stem);
            if (engine == null)
            {
                if (_engine is "bindiff" or "both") _pairStatus(p.RelKey, "bindiff", status);
                if (_engine is "diaphora" or "both") _pairStatus(p.RelKey, "diaphora", status);
            }
            else _pairStatus(p.RelKey, engine, status);

            var done = Interlocked.Increment(ref completed);
            _stageFile(stage, done, total, Path.GetFileName(p.Primary));
        }

        using var sem = new SemaphoreSlim(_maxWorkers);
        var tasks = small.Select(async p =>
        {
            if (ct.IsCancellationRequested) return;
            await sem.WaitAsync(ct);
            try { await RunOne(p); } finally { sem.Release(); }
        }).ToList();

        await Task.WhenAll(tasks);

        // Крупные файлы — последовательно
        foreach (var p in large)
        {
            if (ct.IsCancellationRequested) break;
            await RunOne(p);
        }
    }
}
