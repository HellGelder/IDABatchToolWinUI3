using System.Diagnostics;
using System.IO.Compression;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Обработка архивов APK/IPA/DMG — аналог ida_batch_tool/archive_handler.py.
/// APK/IPA распаковываются через System.IO.Compression, DMG через 7z.
/// </summary>
public static class ArchiveHandler
{
    public static readonly string[] ArchiveExtensions = { ".apk", ".ipa", ".dmg" };
    private static readonly string[] SevenZipNames = { "7z", "7z.exe", "7za", "7za.exe" };

    public static string? Find7z()
    {
        foreach (var name in SevenZipNames)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var cand = Path.Combine(dir.Trim('"'), name);
                    if (File.Exists(cand)) return cand;
                }
                catch { /* ignore */ }
            }
        }
        foreach (var p in new[]
                 {
                     @"C:\Program Files\7-Zip\7z.exe",
                     @"C:\Program Files (x86)\7-Zip\7z.exe",
                 })
            if (File.Exists(p)) return p;
        return null;
    }

    /// <summary>
    /// Извлекает архив в папку рядом (имя = имя архива без расширения).
    /// Возвращает путь к папке или null.
    /// </summary>
    public static string? ExtractArchive(string archivePath, string? outputDir = null)
    {
        var archive = Path.GetFullPath(archivePath);
        if (!File.Exists(archive)) return null;

        var suffix = Path.GetExtension(archive).ToLowerInvariant();
        if (!ArchiveExtensions.Contains(suffix)) return null;

        outputDir ??= Path.Combine(Path.GetDirectoryName(archive) ?? ".", Path.GetFileNameWithoutExtension(archive));
        outputDir = Path.GetFullPath(outputDir);

        // Если папка уже существует и не пуста — считаем архив распакованным
        if (Directory.Exists(outputDir) && Directory.EnumerateFileSystemEntries(outputDir).Any())
            return outputDir;

        Directory.CreateDirectory(outputDir);
        try
        {
            if (suffix is ".apk" or ".ipa")
            {
                ZipFile.ExtractToDirectory(archive, outputDir);
                return outputDir;
            }

            if (suffix == ".dmg")
            {
                var sevenZip = Find7z();
                if (sevenZip == null)
                {
                    TryDeleteDir(outputDir);
                    return null;
                }
                var psi = new ProcessStartInfo
                {
                    FileName = sevenZip,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("x");
                psi.ArgumentList.Add(archive);
                psi.ArgumentList.Add($"-o{outputDir}");
                psi.ArgumentList.Add("-y");
                using var proc = Process.Start(psi);
                if (proc == null) { TryDeleteDir(outputDir); return null; }
                proc.WaitForExit();
                if (proc.ExitCode != 0) { TryDeleteDir(outputDir); return null; }
                return outputDir;
            }
        }
        catch
        {
            TryDeleteDir(outputDir);
            return null;
        }
        return null;
    }

    private static void TryDeleteDir(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
    }
}