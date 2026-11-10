using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace IDABatchToolWinUI.Services;

/// <summary>Итог проверки актуальности версии приложения.</summary>
public sealed record VersionCheckResult(
    bool Succeeded,
    string CurrentVersion,
    string LatestVersion,
    bool IsOutdated,
    string? Error);

/// <summary>
/// Проверка актуальной версии по журналу version.txt в git-репозитории.
///
/// Релизы не публикуются - актуальный номер берётся из шапки version.txt
/// (строка «АКТУАЛЬНАЯ ВЕРСИЯ: x.y.z.w»), файл читается по HTTPS из
/// raw.githubusercontent.com. Проверка необязательна: при ошибке сети
/// приложение работает как обычно, страница «Конфигурация» показывает
/// нейтральный инфобар.
/// </summary>
public static class VersionCheckService
{
    public const string VersionFileUrl =
        "https://raw.githubusercontent.com/HellGelder/IDABatchToolWinUI3/master/version.txt";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly Regex LatestLine = new(
        @"^\s*АКТУАЛЬНАЯ\s+ВЕРСИЯ\s*:\s*(\d+(?:\.\d+){1,3})\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AnyVersion = new(
        @"\d+(?:\.\d+){1,3}", RegexOptions.CultureInvariant);

    /// <summary>Версия запущенной сборки (Directory.Build.props → AssemblyInformationalVersion).</summary>
    public static string CurrentVersion { get; } = ResolveCurrentVersion();

    /// <summary>Разбор номера актуальной версии из текста version.txt; null - строки нет.</summary>
    public static string? ParseLatestVersion(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var match = LatestLine.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Актуальная версия новее установленной (сравнение по числовым полям).</summary>
    public static bool IsOutdated(string current, string latest)
    {
        var installed = ParseVersion(current);
        var actual = ParseVersion(latest);
        return installed != null && actual != null && actual > installed;
    }

    private static Version? ParseVersion(string? value)
    {
        var match = AnyVersion.Match(value ?? "");
        if (!match.Success || !Version.TryParse(match.Value, out var parsed))
            return null;
        // Приводим к четырём полям: 1.0.0 и 1.0.0.0 - одна версия.
        return new Version(parsed.Major, parsed.Minor,
            Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
    }

    private static string ResolveCurrentVersion()
    {
        var assembly = typeof(VersionCheckService).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var match = AnyVersion.Match(informational ?? "");
        if (match.Success) return match.Value;
        return assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";
    }

    /// <summary>Один запрос к version.txt; ошибки возвращаются в результате, не бросаются.</summary>
    public static async Task<VersionCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var current = CurrentVersion;
        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"IDABatchToolWinUI/{current}");
            var text = await http.GetStringAsync(VersionFileUrl, ct).ConfigureAwait(false);
            var latest = ParseLatestVersion(text);
            if (latest == null)
                return new(false, current, "", false,
                    "в version.txt нет строки «АКТУАЛЬНАЯ ВЕРСИЯ»");
            return new(true, current, latest, IsOutdated(current, latest), null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(false, current, "", false, ex.Message);
        }
    }
}

/// <summary>
/// Состояние проверки версии на всё приложение: один запрос при старте,
/// общий результат для «Конфигурации» и значка на вкладке.
/// </summary>
public static class VersionCheckState
{
    private static readonly object Gate = new();
    private static VersionCheckResult? _result;
    private static bool _started;

    /// <summary>Состояние изменилось (проверка завершилась или значок прочитан).</summary>
    public static event Action? Changed;

    public static VersionCheckResult? Result
    {
        get { lock (Gate) return _result; }
    }

    /// <summary>Пользователь уже видел сообщение о версии - значок гаснет.</summary>
    public static bool NoticeRead
    {
        get { lock (Gate) return _noticeRead; }
        private set { lock (Gate) _noticeRead = value; }
    }

    private static bool _noticeRead;

    /// <summary>Запустить фоновую проверку (повторные вызовы игнорируются).</summary>
    public static void Start()
    {
        lock (Gate)
        {
            if (_started) return;
            _started = true;
        }

        AppLog.Info("Проверка актуальной версии: старт");
        _ = Task.Run(async () =>
        {
            var result = await VersionCheckService.CheckAsync().ConfigureAwait(false);
            lock (Gate) _result = result;
            if (result.Succeeded)
                AppLog.Info($"Проверка версии: установлена {result.CurrentVersion}, " +
                            $"актуальная {result.LatestVersion}" +
                            (result.IsOutdated ? " (доступно обновление)" : ""));
            else
                AppLog.Info($"Проверка версии не удалась: {result.Error}");
            RaiseChanged();
        });
    }

    /// <summary>Отметить сообщение прочитанным (без повторного события, если уже прочитано).</summary>
    public static void MarkRead()
    {
        bool changed;
        lock (Gate)
        {
            changed = !_noticeRead;
            _noticeRead = true;
        }
        if (changed) RaiseChanged();
    }

    /// <summary>Только для тестов: сбросить состояние.</summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _result = null;
            _started = false;
            _noticeRead = false;
        }
    }

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(); }
        catch (Exception ex) { AppLog.Error("Обработчик состояния версии", ex); }
    }
}
