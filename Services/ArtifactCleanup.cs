namespace IDABatchToolWinUI.Services;

/// <summary>
/// Безопасная очистка артефактов: удаляются только файлы, производные от
/// конкретных проанализированных файлов, а не все <c>*.log</c>/<c>*.asm</c>
/// рекурсивно по входной папке (можно было снести пользовательские файлы).
/// </summary>
public static class ArtifactCleanup
{
    /// <summary>Пути, которые инструмент вправе удалять для данного исходника.</summary>
    public static IReadOnlyList<string> GetKnownArtifacts(string sourcePath, bool includeLogs, bool includeIdaTemps)
    {
        var dir = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        var basePath = ArtifactValidator.IsBasePath(sourcePath)
            ? sourcePath
            : sourcePath + ".i64";
        var stem = Path.GetFileNameWithoutExtension(basePath);

        var paths = new List<string>();
        if (includeLogs)
        {
            paths.Add(Path.Combine(dir, stem + ".log"));
            paths.Add(Path.Combine(dir, stem + ".asm"));
        }

        if (includeIdaTemps)
        {
            foreach (var ext in new[] { ".id0", ".id1", ".nam", ".til" })
                paths.Add(Path.ChangeExtension(basePath, ext));
        }
        return paths;
    }

    /// <summary>Удаляет известные артефакты; возвращает число удалённых файлов.</summary>
    public static int DeleteKnownArtifacts(string sourcePath, bool includeLogs, bool includeIdaTemps)
    {
        var deleted = 0;
        foreach (var path in GetKnownArtifacts(sourcePath, includeLogs, includeIdaTemps))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }
            catch { /* файл занят - не трогаем */ }
        }
        return deleted;
    }
}
