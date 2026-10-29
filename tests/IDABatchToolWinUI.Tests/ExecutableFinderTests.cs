using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>
/// ExecutableFinder: версионные soname (libcrypto.so.1.1) не должны выпадать
/// из анализа - Path.GetExtension даёт для них «.1.1», которого нет в списке
/// расширений платформы.
/// </summary>
public sealed class ExecutableFinderTests : IDisposable
{
    private readonly string _dir;

    public ExecutableFinderTests()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "findertest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* не критично */ }
    }

    private static byte[] ElfBytes() => new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0 };

    [Theory]
    [InlineData("libcrypto.so.1.1", true)]
    [InlineData("libssl.so.3", true)]
    [InlineData("libcrypto.so", true)]
    [InlineData("libz.so.1.2.11", true)]
    [InlineData("ld-linux-x86-64.so.2", true)]
    [InlineData("libfoo.dylib", true)]
    [InlineData("kernel32.dll", true)]
    [InlineData("readme.txt", false)]
    [InlineData("libcrypto.so.1", true)]
    public void HasKnownExtension_MatchesVersionedSonames(string fileName, bool expected)
    {
        Assert.Equal(expected, ExecutableFinder.HasKnownExtension(fileName, PlatformInfo.AllExtensions()));
    }

    [Fact]
    public void FindExecutables_FindsBaseAndVersionedLibrary()
    {
        File.WriteAllBytes(Path.Combine(_dir, "libcrypto.so"), ElfBytes());
        File.WriteAllBytes(Path.Combine(_dir, "libcrypto.so.1.1"), ElfBytes());
        File.WriteAllBytes(Path.Combine(_dir, "readme.txt"), new byte[] { 1 });

        var found = ExecutableFinder.FindExecutables(_dir, PlatformInfo.AllExtensions());

        var names = found.Select(Path.GetFileName).ToHashSet();
        Assert.Contains("libcrypto.so", names);
        Assert.Contains("libcrypto.so.1.1", names);
        Assert.DoesNotContain("readme.txt", names);
    }

    [Fact]
    public void FindExecutables_RejectsTextFilesWithExecutableNames()
    {
        // Текстовые файлы, маскирующиеся под библиотеки (версионный soname,
        // обычное расширение) - не исполняемые, на анализ не идут.
        File.WriteAllBytes(Path.Combine(_dir, "libcrypto.so.1.1.CRG"),
            "CRG-текстовый формат, не ELF"u8.ToArray());
        File.WriteAllBytes(Path.Combine(_dir, "fake.so"), "не бинарник"u8.ToArray());

        var found = ExecutableFinder.FindExecutables(_dir, PlatformInfo.AllExtensions());

        Assert.Empty(found);
    }

    [Fact]
    public void FindExecutables_AcceptsDexBySignature()
    {
        // .dex в списке расширений Linux/Android: сигнатура "dex\n".
        File.WriteAllBytes(Path.Combine(_dir, "classes.dex"),
            new byte[] { 0x64, 0x65, 0x78, 0x0A, 0x03, 0x35, 0x00 });

        var found = ExecutableFinder.FindExecutables(_dir, PlatformInfo.AllExtensions());

        Assert.Equal(new[] { "classes.dex" }, found.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void FindExecutables_ExtensionlessFile_BySignature()
    {
        File.WriteAllBytes(Path.Combine(_dir, "siesunit_lin"), ElfBytes());

        var found = ExecutableFinder.FindExecutables(_dir, PlatformInfo.AllExtensions());

        Assert.Equal(new[] { "siesunit_lin" }, found.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void DetectPlatform_CountsVersionedSonamesAsLinux()
    {
        var a = Path.Combine(_dir, "libcrypto.so.1.1");
        var b = Path.Combine(_dir, "libc.so.6");
        File.WriteAllBytes(a, ElfBytes());
        File.WriteAllBytes(b, ElfBytes());

        Assert.Equal("Linux / Android", PlatformInfo.DetectPlatform(new[] { a, b }));
    }

    [Fact]
    public void ComputeExecutablesSize_IncludesVersionedLibrary()
    {
        var bytes = ElfBytes();
        File.WriteAllBytes(Path.Combine(_dir, "libcrypto.so"), bytes);
        File.WriteAllBytes(Path.Combine(_dir, "libcrypto.so.1.1"), bytes);

        var size = ExecutableFinder.ComputeExecutablesSize(_dir, PlatformInfo.AllExtensions());

        Assert.Equal(2 * bytes.Length, size);
    }
}
