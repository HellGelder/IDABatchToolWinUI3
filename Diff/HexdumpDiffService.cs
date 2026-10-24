namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Схожесть hex-дампов двух файлов (аналог _compute_hexdump_diff из diff_worker.py).
/// Самодостаточен: только BCL, линкуется в тесты отдельным файлом.
/// </summary>
public static class HexdumpDiffService
{
    /// <summary>Максимальный размер одного файла для расчёта hexdump-схожести.
    /// Индекс 16-байтовых ключей второго файла занимает ~12 байт на позицию:
    /// для сотен мегабайт это гигабайты ОЗУ, поэтому выше порога — честный пропуск.</summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    private const int MinMatch = 16;

    /// <summary>
    /// Доля байт первого файла (округлённая до 6 знаков), для которых найдена
    /// совпадающая последовательность ≥16 байт во втором.
    /// Детальные строки (equal/inserted/deleted) раньше строились, но ни одним
    /// потребителем не читались — считаем только число совпавших байт.
    /// Индекс второго файла: 64-битный хэш 16-байтового окна в сортированном
    /// массиве (12 байт на позицию вместо ~56 у словаря); коллизии безвредны —
    /// совпадение всегда проверяется сравнением байт.
    /// </summary>
    /// <returns>Similarity — схожесть (при пропуске −1); Skipped/Reason — причина пропуска.</returns>
    public static (double Similarity, bool Skipped, string Reason) ComputeSimilarity(
        string path1, string path2, long maxFileBytes = MaxFileBytes)
    {
        byte[]? d1, d2;
        try { d1 = File.ReadAllBytes(path1); d2 = File.ReadAllBytes(path2); }
        catch (Exception e) { return (-1.0, true, $"не удалось прочитать файлы: {e.Message}"); }
        if (d1.Length == 0 || d2.Length == 0) return (0.0, false, "");
        if (d1.Length > maxFileBytes || d2.Length > maxFileBytes)
            return (-1.0, true, $"размер файла превышает порог {maxFileBytes / (1024 * 1024)} МБ");

        int indexCount = d2.Length - MinMatch + 1;
        if (indexCount <= 0) return (0.0, false, "");

        // Индекс 16-байтовых окон data2. Array.Sort неустойчив, но результат
        // от порядка кандидатов не зависит — берётся максимум длины расширения.
        var keys = new ulong[indexCount];
        var positions = new int[indexCount];
        for (int pos = 0; pos < indexCount; pos++)
        {
            keys[pos] = WindowKey(d2, pos);
            positions[pos] = pos;
        }
        Array.Sort(keys, positions);

        long matchingBytes = 0;
        int p1 = 0;
        while (p1 < d1.Length)
        {
            int maxLen = 0;
            if (d1.Length - p1 >= MinMatch)
            {
                var key = WindowKey(d1, p1);
                int lo = LowerBound(keys, key);
                for (int i = lo; i < indexCount && keys[i] == key; i++)
                {
                    int pc = positions[i];
                    // Отсечение: если байт на позиции текущего максимума не совпал,
                    // расширение не превысит maxLen и кандидата можно пропустить.
                    if (maxLen > 0 &&
                        p1 + maxLen < d1.Length && pc + maxLen < d2.Length &&
                        d1[p1 + maxLen] != d2[pc + maxLen])
                        continue;
                    int len = 0;
                    while (p1 + len < d1.Length && pc + len < d2.Length && d1[p1 + len] == d2[pc + len]) len++;
                    if (len > maxLen) maxLen = len;
                }
            }

            if (maxLen >= MinMatch)
            {
                matchingBytes += maxLen;
                p1 += maxLen;
            }
            else
            {
                p1 += MinMatch;
            }
        }

        return (Math.Round((double)matchingBytes / d1.Length, 6), false, "");
    }

    /// <summary>Хэш 16-байтового окна: XOR двух ulong с поворотом второго,
    /// чтобы сдвиги данных давали другие ключи.</summary>
    private static ulong WindowKey(byte[] d, int pos)
        => BitConverter.ToUInt64(d, pos) ^ ulong.RotateLeft(BitConverter.ToUInt64(d, pos + 8), 1);

    /// <summary>Первый индекс массива с ключом ≥ value (массив отсортирован).</summary>
    private static int LowerBound(ulong[] keys, ulong value)
    {
        int lo = 0, hi = keys.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (keys[mid] < value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
