namespace IDABatchToolWinUI.Services;

/// <summary>
/// Определение исполняемых файлов по сигнатурам (PE/ELF/Mach-O) и рекурсивный
/// поиск - аналог ida_batch_tool/discovery/finder.py.
/// </summary>
public static class ExecutableFinder
{
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
            // Dalvik: dex-файлы (".dex" в списке расширений Linux/Android).
            if (header[0] == (byte)'d' && header[1] == (byte)'e' && header[2] == (byte)'x' && header[3] == 0x0A) return true;
            if (header[0] == (byte)'d' && header[1] == (byte)'e' && header[2] == (byte)'y' && header[3] == 0x0A) return true;
            var magic = BitConverter.ToUInt32(header);
            return magic is 0xFEEDFACE or 0xFEEDFACF or 0xCAFEBABE or 0xCEFAEDFE or 0xCFFAEDFE;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Соответствует ли имя файла одному из расширений, включая версионные
    /// формы soname: «libcrypto.so.1.1» - это «.so» с версионным суффиксом.
    /// ``Path.GetExtension`` даёт для него «.1.1», и такие библиотеки молча
    /// выпадали из анализа.
    /// </summary>
    public static bool HasKnownExtension(string fileName, string[] extensions)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        foreach (var ext in extensions)
        {
            if (name.EndsWith(ext, StringComparison.Ordinal)) return true;
            if (name.Contains(ext + ".", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Рекурсивно находит исполняемые файлы: имя должно содержать расширение
    /// платформы (включая версионные soname), а содержимое - сигнатуру PE/ELF/
    /// Mach-O/Dalvik. Текстовый файл с «исполняемым» именем (например,
    /// libcrypto.so.1.1.CRG) в анализ не попадает. Файлы без расширения
    /// проверяются только по сигнатуре. Безопасно обходит недоступные
    /// подкаталоги (не падает, а пропускает их).
    /// </summary>
    public static List<string> FindExecutables(string rootDir, string[]? extensions)
    {
        var extList = extensions is { Length: > 0 } ? extensions : PlatformInfo.AllExtensions();
        var result = new List<string>();
        if (!Directory.Exists(rootDir)) return result;

        foreach (var entry in SafeEnumerateFiles(rootDir))
        {
            try
            {
                var name = Path.GetFileName(entry).ToLowerInvariant();
                if (name.Contains('.') && !HasKnownExtension(name, extList)) continue;
                if (!IsExecutable(entry)) continue;
                result.Add(entry);
            }
            catch { /* файл недоступен */ }
        }
        return result;
    }

    /// <summary>Проверяет, что файл - исполняемый образ, а не объектный ELF (.o).</summary>
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
        var extList = extensions is { Length: > 0 } ? extensions : null;
        long total = 0;
        foreach (var entry in SafeEnumerateFiles(rootDir))
        {
            // Папки готовых отчётов исключаем
            var rel = Path.GetRelativePath(rootDir, entry);
            if (rel.Contains(AppConstants.SfaReportsDirName) || rel.Contains(AppConstants.ReportsDirName) ||
                rel.Contains(AppConstants.AddDiffResultsDirName) || rel.Contains(AppConstants.DiffResultsDirName)) continue;
            try
            {
                var name = Path.GetFileName(entry).ToLowerInvariant();
                if (extList != null && name.Contains('.') && !HasKnownExtension(name, extList)) continue;
                if (!IsExecutableImage(entry)) continue;
                total += new FileInfo(entry).Length;
            }
            catch { /* пропускаем */ }
        }
        return total;
    }

    /// <summary>
    /// Рекурсивный обход файлов, переживающий недоступные подкаталоги
    /// (UnauthorizedAccessException и пр.) - пропускает их, а не падает.
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