namespace IDABatchToolWinUI.Services;

/// <summary>
/// Определение исполняемых файлов по сигнатурам (PE/ELF/Mach-O) и рекурсивный
/// поиск — аналог ida_batch_tool/discovery/finder.py.
/// </summary>
public static class ExecutableFinder
{
    public static bool IsMachO(ReadOnlySpan<byte> magic4)
    {
        if (magic4.Length < 4) return false;
        var magic = BitConverter.ToUInt32(magic4[..4]);
        return magic is 0xFEEDFACE or 0xFEEDFACF or 0xCAFEBABE or 0xCEFAEDFE or 0xCFFAEDFE;
    }

    private static uint ReadUInt32LE(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b[..4]);

    public static bool IsExecutable(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[4];
            int n = fs.Read(header);
            if (n < 4) return false;
            if (header[0] == (byte)'M' && header[1] == (byte)'Z') return true;
            if (header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F') return true;
            var magic = BitConverter.ToUInt32(header);
            return magic is 0xFEEDFACE or 0xFEEDFACF or 0xCAFEBABE or 0xCEFAEDFE or 0xCFFAEDFE;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Рекурсивно находит исполняемые файлы. Файлы без расширения проверяются по сигнатуре.
    /// Безопасно обходит недоступные подкаталоги (не падает, а пропускает их).
    /// </summary>
    public static List<string> FindExecutables(string rootDir, string[]? extensions, bool useSignatures = false)
    {
        var extSet = extensions is { Length: > 0 } ? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase) : null;
        var result = new List<string>();
        if (!Directory.Exists(rootDir)) return result;

        foreach (var entry in SafeEnumerateFiles(rootDir))
        {
            var ext = Path.GetExtension(entry).ToLowerInvariant();
            try
            {
                if (extSet != null)
                {
                    if (ext == "")
                    {
                        if (IsExecutable(entry)) result.Add(entry);
                        continue;
                    }
                    if (extSet.Contains(ext))
                    {
                        if (useSignatures && !IsExecutable(entry)) continue;
                        result.Add(entry);
                    }
                }
                else if (useSignatures)
                {
                    if (IsExecutable(entry)) result.Add(entry);
                }
                else
                {
                    result.Add(entry);
                }
            }
            catch { /* файл недоступен */ }
        }
        return result;
    }

    /// <summary>Проверяет, что файл — исполняемый образ, а не объектный ELF (.o).</summary>
    public static bool IsExecutableImage(string filePath)
    {
        if (!IsExecutable(filePath)) return false;
        try
        {
            using var fs = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[18];
            int n = fs.Read(header);
            if (n < 18) return true;
            if (header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
                return true;
            var eType = BitConverter.ToUInt16(header[16..18]);
            return eType != 1; // ET_REL
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Суммарный размер исполняемых модулей в директории (для индекса отчёта).</summary>
    public static long ComputeExecutablesSize(string rootDir, string[]? extensions)
    {
        if (!Directory.Exists(rootDir)) return 0;
        var extSet = extensions is { Length: > 0 } ? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase) : null;
        long total = 0;
        foreach (var entry in SafeEnumerateFiles(rootDir))
        {
            // Папки готовых отчётов исключаем
            var rel = Path.GetRelativePath(rootDir, entry);
            if (rel.Contains("SFAReports") || rel.Contains("Reports") ||
                rel.Contains("AddDiffResults") || rel.Contains("DiffResults")) continue;
            var ext = Path.GetExtension(entry).ToLowerInvariant();
            try
            {
                if (extSet != null && ext != "" && !extSet.Contains(ext)) continue;
                if (!IsExecutableImage(entry)) continue;
                total += new FileInfo(entry).Length;
            }
            catch { /* пропускаем */ }
        }
        return total;
    }

    /// <summary>
    /// Рекурсивный обход файлов, переживающий недоступные подкаталоги
    /// (UnauthorizedAccessException и пр.) — пропускает их, а не падает.
    /// </summary>
    public static IEnumerable<string> SafeEnumerateFiles(string rootDir)
    {
        var pending = new Stack<string>();
        pending.Push(rootDir);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }
            foreach (var f in files) yield return f;

            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch { continue; }
            foreach (var d in subDirs) pending.Push(d);
        }
    }
}