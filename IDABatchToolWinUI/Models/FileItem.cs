using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI.Models;

/// <summary>Элемент тремапа и списка файлов на страницах анализа.</summary>
public sealed class FileItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public long Size { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.NotAnalyzed;

    /// <summary>Путь к ожидаемой базе .i64 рядом с файлом.</summary>
    public string ExpectedI64Path => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(Path) ?? ".",
        Name + ".i64");
}

/// <summary>Сопоставленная пара файлов для сравнения.</summary>
public sealed record DiffPair(string Primary, string Secondary, string RelKey)
{
    /// <summary>Безопасное имя для файлов результатов (аналог _safe_filename из diff_worker.py).</summary>
    public string Stem => string.Join("_", RelKey
        .Replace("\\", "_").Replace("/", "_").Replace(" ", "_").Replace(".", "_"));
}