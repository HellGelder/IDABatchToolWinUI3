using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>
/// ConfigStore: слияние при сохранении (комментарии/неизвестные ключи не теряются),
/// событие Changed, слияние с дефолтами при загрузке, нормализация max_ida.
/// </summary>
public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ConfigStoreTests()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "cfgtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.yaml");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* не критично */ }
    }

    [Fact]
    public void Save_PreservesCommentsAndUnknownKeys()
    {
        File.WriteAllText(_path,
            "# мой комментарий\n" +
            "future_key: 42\n" +
            "theme: light\n" +
            "max_ida: 4\n");

        var store = new ConfigStore(_path);
        store.Current.MaxIda = 8;
        store.Current.Theme = "dark";
        store.Save(store.Current);

        var text = File.ReadAllText(_path);
        Assert.Contains("# мой комментарий", text);
        Assert.Contains("future_key: 42", text);
        Assert.Contains("theme: dark", text);
        Assert.Contains("max_ida: 8", text);
    }

    [Fact]
    public void Save_UpdatesNestedExecutablePaths()
    {
        File.WriteAllText(_path,
            "ida:\n" +
            "  executable: C:\\old\\idat.exe\n" +
            "bindiff:\n" +
            "  executable: C:\\old\\bindiff.exe\n");

        var store = new ConfigStore(_path);
        store.Current.IdaExecutable = "C:\\new dir\\idat.exe";
        store.Current.BindiffExecutable = "bindiff.exe";
        store.Save(store.Current);

        var text = File.ReadAllText(_path);
        Assert.Contains("\"C:\\new dir\\idat.exe\"", text);
        Assert.Contains("executable: bindiff.exe", text);
    }

    [Fact]
    public void Changed_FiresOnSave()
    {
        var store = new ConfigStore(_path);
        AppConfig? received = null;
        store.Changed += c => received = c;

        store.Save(new AppConfig { Theme = "dark" });

        Assert.NotNull(received);
        Assert.Equal("dark", received!.Theme);
    }

    [Fact]
    public void Load_MergesFileWithDefaults()
    {
        File.WriteAllText(_path, "theme: dark\n");

        var store = new ConfigStore(_path);

        Assert.Equal("dark", store.Current.Theme);
        Assert.Equal(0, store.Current.MaxIda); // дефолт: 0 = «по умолчанию 4»
        Assert.Equal("INFO", store.Current.LogLevel);
        Assert.Equal(".", store.Current.DefaultInputDir);
    }

    [Fact]
    public void Save_ClampsMaxIda()
    {
        var store = new ConfigStore(_path);
        var cfg = new AppConfig { MaxIda = 99 };
        store.Save(cfg);

        Assert.Equal(32, cfg.MaxIda);
        Assert.Contains("max_ida: 32", File.ReadAllText(_path));
    }

    [Fact]
    public void Save_WithoutExistingFile_CreatesAllKeys()
    {
        var store = new ConfigStore(_path);
        store.Save(new AppConfig());

        var text = File.ReadAllText(_path);
        Assert.Contains("ida:", text);
        Assert.Contains("bindiff:", text);
        Assert.Contains("max_ida: 0", text);
        Assert.Contains("default_inputdir:", text);
        Assert.Contains("log_level: INFO", text);
        Assert.Contains("theme: light", text);
        Assert.Contains("manpages_db_path:", text);
        // Временный файл записи не остаётся
        Assert.False(File.Exists(_path + ".tmp"));
    }
}
