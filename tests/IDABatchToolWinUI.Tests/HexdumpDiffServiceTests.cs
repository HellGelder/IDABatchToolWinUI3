using System.Security.Cryptography;
using IDABatchToolWinUI.Diff;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>Тесты схожести hex-дампов: поведение на синтетических данных,
/// отсечение по порогу размера, устойчивость к коллизиям хэша окна.</summary>
public sealed class HexdumpDiffServiceTests : IDisposable
{
    // Каталог тестовых файлов - рядом с тестовой сборкой: %TEMP% в этом
    // окружении может быть закрыт для процесса тестов.
    private readonly string _dir = Path.Combine(
        AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));

    public HexdumpDiffServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* не критично */ }
    }

    private string WriteFile(string name, byte[] data)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] RandomBytes(int size, int seed)
    {
        var data = new byte[size];
        var rng = new Random(seed);
        rng.NextBytes(data);
        return data;
    }

    [Fact]
    public void IdenticalFiles_FullSimilarity()
    {
        var data = RandomBytes(4096, 1);
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("a.bin", data), WriteFile("b.bin", data));
        Assert.False(skipped);
        Assert.Equal(1.0, sim);
    }

    [Fact]
    public void DisjointRandomFiles_NoSimilarity()
    {
        // Разные случайные данные: случайное совпадение 16 байт практически невозможно.
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("a.bin", RandomBytes(4096, 2)), WriteFile("b.bin", RandomBytes(4096, 3)));
        Assert.False(skipped);
        Assert.Equal(0.0, sim);
    }

    [Fact]
    public void ShiftedSuffix_FullSimilarity()
    {
        // Второй файл = случайный префикс + весь первый файл: каждое окно
        // первого находит продолжение во втором - схожесть полная.
        var d1 = RandomBytes(2048, 4);
        var d2 = new byte[RandomBytes(512, 5).Length + d1.Length];
        RandomBytes(512, 5).CopyTo(d2, 0);
        d1.CopyTo(d2, 512);
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("a.bin", d1), WriteFile("b.bin", d2));
        Assert.False(skipped);
        Assert.Equal(1.0, sim);
    }

    [Fact]
    public void HalfOverlap_HalfSimilarity()
    {
        var shared = RandomBytes(2048, 6);
        var d1 = new byte[4096];
        var d2 = new byte[4096];
        shared.CopyTo(d1, 0);
        shared.CopyTo(d2, 0);
        RandomBytes(2048, 7).CopyTo(d1, 2048);
        RandomBytes(2048, 8).CopyTo(d2, 2048);
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("a.bin", d1), WriteFile("b.bin", d2));
        Assert.False(skipped);
        Assert.Equal(0.5, sim);
    }

    [Fact]
    public void OverCapFile_SkippedWithHonestReason()
    {
        var (sim, skipped, reason) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("big.bin", RandomBytes(1024, 9)),
            WriteFile("small.bin", RandomBytes(16, 10)),
            maxFileBytes: 512);
        Assert.True(skipped);
        Assert.Equal(-1.0, sim);
        Assert.Contains("порог", reason);
    }

    [Fact]
    public void MissingFile_Skipped()
    {
        var (sim, skipped, reason) = HexdumpDiffService.ComputeSimilarity(
            Path.Combine(_dir, "no-such-file.bin"),
            WriteFile("b.bin", RandomBytes(64, 11)));
        Assert.True(skipped);
        Assert.Equal(-1.0, sim);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void EmptyFile_ZeroSimilarity_NotSkipped()
    {
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("empty.bin", Array.Empty<byte>()),
            WriteFile("b.bin", RandomBytes(64, 12)));
        Assert.False(skipped);
        Assert.Equal(0.0, sim);
    }

    [Fact]
    public void TinyFiles_UnderMinMatch_ZeroSimilarity()
    {
        // Меньше 16 байт - совпадающих окон быть не может.
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("a.bin", RandomBytes(10, 13)), WriteFile("b.bin", RandomBytes(10, 14)));
        Assert.False(skipped);
        Assert.Equal(0.0, sim);
    }

    [Fact]
    public void RepeatedPattern_CountsOncePerConsumedByte()
    {
        // Полностью совпадающие файлы из повторяющегося шаблона - схожесть полная,
        // при этом алгоритм не должен зациклиться на одинаковом ключе окна.
        var data = new byte[4096];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 7 == 0 ? 0xAA : i % 251);
        var (sim, skipped, _) = HexdumpDiffService.ComputeSimilarity(
            WriteFile("a.bin", data), WriteFile("b.bin", data));
        Assert.False(skipped);
        Assert.Equal(1.0, sim);
    }
}
