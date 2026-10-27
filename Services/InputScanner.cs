using IDABatchToolWinUI.Models;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Сканирование входной папки для страниц анализа: поиск исполняемых файлов по
/// всем платформам, распаковка архивов рядом и сборка FileItem с первичной
/// оценкой готовности. Общий для AnalysisPage и SfaPage (раньше - две копии,
/// разошедшиеся обработкой ошибок). Ошибки распаковки архивов возвращаются
/// списком: страница «Общий анализ» показывает их, «Анализ СФ» - исторически нет.
/// </summary>
public static class InputScanner
{
    public sealed record ScanResult(IReadOnlyList<FileItem> Files, IReadOnlyList<string> ArchiveErrors);

    public static async Task<ScanResult> ScanAsync(string inputDir)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
            return new ScanResult(Array.Empty<FileItem>(), errors);

        var files = await ScanCoreAsync(inputDir, errors);
        return new ScanResult(files, errors);
    }

    private static async Task<List<FileItem>> ScanCoreAsync(string inputDir, List<string> errors)
    {
        // Поиск сразу по всем расширениям всех платформ: платформа определяется
        // по фактическому содержимому папки, а не по заранее выбранной настройке.
        // Раньше поиск шёл по расширениям текущей платформы, поэтому папка,
        // например, только с .so-файлами давала пустой список и детект не выполнялся.
        var extensions = PlatformInfo.AllExtensions();
        // Рекурсивный обход папки и проверки артефактов - в пуле потоков: на
        // больших и сетевых каталогах синхронный обход подвешивал UI
        // (пересканирование запускается на каждый ввод пути).
        var found = await Task.Run(() => ExecutableFinder.FindExecutables(inputDir, extensions));

        // Архивы: распаковываем рядом и ищем внутри
        foreach (var ext in ArchiveHandler.ArchiveExtensions)
        {
            string[] archives;
            try { archives = Directory.GetFiles(inputDir, "*" + ext); }
            catch { continue; /* каталог недоступен для чтения */ }

            foreach (var archive in archives)
            {
                var extracted = await ArchiveHandler.ExtractArchiveAsync(archive);
                if (extracted != null && Directory.Exists(extracted))
                {
                    var inner = await Task.Run(() => ExecutableFinder.FindExecutables(extracted, extensions));
                    found.AddRange(inner);
                }
                else if (ext == ".dmg")
                {
                    errors.Add($"Не удалось извлечь {Path.GetFileName(archive)}. Убедитесь, что 7z установлен и доступен в PATH.");
                }
            }
        }

        // Проверка артефактов по каждому файлу (FileInfo + валидация базы) - тоже в фоне.
        return await Task.Run(() => found.Distinct().Select(MakeItem).ToList());
    }

    private static FileItem MakeItem(string path)
    {
        long size = 0;
        try { size = new FileInfo(path).Length; }
        catch { /* файл исчез или недоступен - размер 0 */ }

        return new FileItem
        {
            Name = Path.GetFileName(path),
            Path = Path.GetFullPath(path),
            Size = size,
            // Готовность - по валидной базе и валидному export.json, а не по факту
            // существования .i64: иначе остатки прошлого запуска выглядят результатом.
            Status = ArtifactValidator.HasValidArtifacts(path)
                ? AnalysisStatus.Success
                : AnalysisStatus.NotAnalyzed,
        };
    }
}
