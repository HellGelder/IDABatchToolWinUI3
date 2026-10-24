using System.Diagnostics;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Фазы Diaphora: экспорт БД через idat (с пульсом) и сравнение БД через pythonw.
/// Перенос из DiffWorker при декомпозиции (пачка D) — поведение не менялось.
/// </summary>
public sealed class DiaphoraService
{
    private readonly DiffProcessRunner _proc;
    private readonly string _idatPath;
    private readonly string _diaphoraPath;
    private readonly Action<string>? _onError;

    public DiaphoraService(DiffProcessRunner proc, string idatPath, string diaphoraPath, Action<string>? onError = null)
    {
        _proc = proc;
        _idatPath = idatPath;
        _diaphoraPath = diaphoraPath;
        _onError = onError;
    }

    public async Task<(bool Ok, string? Reason)> ExportAsync(string i64Path, string outSqlite, string relKey, CancellationToken ct)
    {
        var psi = DiffProcessRunner.CreateIdatPsi(_idatPath);
        var env = psi.Environment;
        env["DIAPHORA_AUTO"] = "1";
        env["DIAPHORA_EXPORT_FILE"] = outSqlite;
        // Облегчённый профиль экспорта (перенос из исполнения 1): псевдокод-эвристики
        // работают, микрокод off — Hex-Rays вызывается один раз на функцию вместо двух.
        // Работает только с патчем get_value_for (иначе '0' остаётся строкой и не
        // отключает флаг) и патчем ключа export_microcode в diaphora_ida.py.
        env["DIAPHORA_USE_DECOMPILER"] = "1";
        env["DIAPHORA_EXPORT_MICROCODE"] = "0";
        env["DIAPHORA_FUNCTION_SUMMARIES_ONLY"] = "0";

        // Для крупных файлов — как в исполнении 1: увеличенная виртуальная память,
        // отключение undo, отдельный лог IDA.
        if (DiffProcessRunner.IsLarge(i64Path))
        {
            psi.ArgumentList.Add("-dVPAGESIZE=16384");
            psi.ArgumentList.Add("-dUNDO_MAXSIZE=0");
            var idaLog = Path.Combine(Path.GetDirectoryName(outSqlite) ?? ".", Path.GetFileNameWithoutExtension(outSqlite) + ".ida.log");
            psi.ArgumentList.Add($"-L{idaLog}");
        }

        psi.ArgumentList.Add("-A");
        // Важно: без кавычек внутри аргумента — ProcessStartInfo.ArgumentList сам
        // экранирует путь; кавычки в значении ломали нахождение скрипта IDA.
        psi.ArgumentList.Add($"-S{_diaphoraPath}");
        psi.ArgumentList.Add(i64Path);

        var (rc, stdout, stderr) = await _proc.RunWithPulseAsync(psi, relKey, "экспорт", outSqlite, ct);
        if (rc == 0 && File.Exists(outSqlite)) return (true, null);
        return (false, DiffProcessRunner.DescribeProcFailure(rc, stdout, stderr, $"экспорт {Path.GetFileName(i64Path)}"));
    }

    public async Task<(bool Ok, string? Reason)> DiffAsync(string db1, string db2, string outSqlite, string relKey, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = IDABatchToolWinUI.Services.AppConstants.PythonwPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        // Быстрый профиль дифа (перенос из исполнения 1): медленные и экспериментальные
        // эвристики избыточны, таймаут эвристики снижен, чтобы одна тяжёлая эвристика
        // не растягивала диф на часы. Нужен патч get_value_for в diaphora.py —
        // без него env-строки не приводятся к bool/int и флаги не работают.
        psi.Environment["DIAPHORA_SLOW_HEURISTICS"] = "0";
        psi.Environment["DIAPHORA_EXPERIMENTAL"] = "0";
        psi.Environment["DIAPHORA_UNRELIABLE"] = "0";
        psi.Environment["DIAPHORA_SQL_TIMEOUT_LIMIT"] = "60";

        psi.ArgumentList.Add(_diaphoraPath);
        psi.ArgumentList.Add(db1);
        psi.ArgumentList.Add(db2);
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outSqlite);
        var (rc, stdout, stderr) = await _proc.RunWithPulseAsync(psi, relKey, "диф", outSqlite, ct);
        if (rc == 0 && File.Exists(outSqlite)) return (true, null);
        return (false, DiffProcessRunner.DescribeProcFailure(rc, stdout, stderr, "сравнение БД"));
    }
}
