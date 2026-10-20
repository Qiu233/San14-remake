namespace San14.Data;

/// <summary>Responsibility's area-name predicate. Its offline fixed kana tables
/// and suffix classes are independent of the person's dedicated name key.</summary>
internal static class DeploymentResponsibilityNameOrder
{
    internal static bool Before(string left, string right)
    {
        left = Terminated(left); right = Terminated(right);
        // The native temporary pair has 1024 UTF-16 units including both terminators.
        // Beyond that scope it supplies null to its CRT call; do not invent an order.
        if (left.Length + right.Length + 2 > 1024)
            throw new InvalidDataException("deployment.person-order.responsibility.name_pair_capacity");
        if (Compare(left, right) == 0) return false;
        string a = Normalize(left), b = Normalize(right);
        int order = Compare(a, b);
        if (order != 0) return order < 0;
        for (int index = Math.Min(left.Length, right.Length) - 1; index >= 0; index--)
        {
            int difference = Class(left[index]) - Class(right[index]);
            if (difference != 0) return difference < 0;
        }
        return false;
    }
    internal static string Normalize(string text)
    {
        text = Terminated(text);
        var result = new char[text.Length];
        for (int index = 0; index < result.Length; index++)
        {
            char value = text[index]; bool found = false;
            foreach (var pair in Kana)
                if (pair.From == value) { result[index] = pair.To; found = true; break; }
            if (found) continue;
            if (value == '\u30FC' && index > 0)
                foreach (var pair in LongVowel)
                    if (pair.From == result[index - 1]) { value = pair.To; break; }
            result[index] = value;
        }
        return new(result);
    }
    private static string Terminated(string text)
    {
        int end = text.IndexOf('\0'); return end < 0 ? text : text[..end];
    }
    private static int Compare(string a, string b)
    {
        for (int index = 0; index < 1024; index++)
        {
            char x = index < a.Length ? a[index] : '\0', y = index < b.Length ? b[index] : '\0';
            if (x != y) return x.CompareTo(y);
            if (x == '\0') return 0;
        }
        return 0;
    }
    private static int Class(char value) => value switch
    {
        '\u30A1' or '\u30A3' or '\u30A5' or '\u30A7' or '\u30A9' or '\u30C3' or '\u30E3' or '\u30E5' or '\u30E7' or '\u30EE' => 4,
        '\u30AC' or '\u30AE' or '\u30B0' or '\u30B2' or '\u30B4' or '\u30B6' or '\u30B8' or '\u30BA' or '\u30BC' or '\u30BE' or '\u30C0' or '\u30C2' or '\u30C5' or '\u30C7' or '\u30C9' or '\u30D0' or '\u30D3' or '\u30D6' or '\u30D9' or '\u30DC' => 1,
        '\u30D1' or '\u30D4' or '\u30D7' or '\u30DA' or '\u30DD' => 2,
        '\u30FC' => 3,
        _ => 0
    };
    private static readonly (char From, char To)[] Kana = [('\u30A1','\u30A2'),('\u30A3','\u30A4'),('\u30A5','\u30A6'),('\u30A7','\u30A8'),('\u30A9','\u30AA'),('\u30AC','\u30AB'),('\u30AE','\u30AD'),('\u30B0','\u30AF'),('\u30B2','\u30B1'),('\u30B4','\u30B3'),('\u30B6','\u30B5'),('\u30B8','\u30B7'),('\u30BA','\u30B9'),('\u30BC','\u30BB'),('\u30BE','\u30BD'),('\u30C0','\u30BF'),('\u30C2','\u30C1'),('\u30C5','\u30C4'),('\u30C7','\u30C6'),('\u30C9','\u30C8'),('\u30D0','\u30CF'),('\u30D3','\u30D2'),('\u30D6','\u30D5'),('\u30D9','\u30D8'),('\u30DC','\u30DB'),('\u30D1','\u30CF'),('\u30D4','\u30D2'),('\u30D7','\u30D5'),('\u30DA','\u30D8'),('\u30DD','\u30DB'),('\u30E3','\u30E4'),('\u30E5','\u30E6'),('\u30E7','\u30E8'),('\u30C3','\u30C4'),('\u30EE','\u30EF')];
    private static readonly (char From, char To)[] LongVowel = [('\u30A2','\u30A2'),('\u30AB','\u30A2'),('\u30B5','\u30A2'),('\u30BF','\u30A2'),('\u30CA','\u30A2'),('\u30CF','\u30A2'),('\u30CA','\u30A2'),('\u30E4','\u30A2'),('\u30E9','\u30A2'),('\u30EF','\u30A2'),('\u30A4','\u30A4'),('\u30AD','\u30A4'),('\u30B7','\u30A4'),('\u30C1','\u30A4'),('\u30CB','\u30A4'),('\u30D2','\u30A4'),('\u30DF','\u30A4'),('\u30EA','\u30A4'),('\u30F0','\u30A4'),('\u30A6','\u30A6'),('\u30AF','\u30A6'),('\u30B9','\u30A6'),('\u30C4','\u30A6'),('\u30CC','\u30A6'),('\u30D5','\u30A6'),('\u30E6','\u30A6'),('\u30E0','\u30A6'),('\u30EB','\u30A6'),('\u30A8','\u30EB'),('\u30B1','\u30EB'),('\u30BB','\u30EB'),('\u30C6','\u30EB'),('\u30CD','\u30EB'),('\u30D8','\u30EB'),('\u30E1','\u30EB'),('\u30EC','\u30EB'),('\u30F1','\u30EB'),('\u30AA','\u30AA'),('\u30B3','\u30AA'),('\u30BD','\u30AA'),('\u30C8','\u30AA'),('\u30CE','\u30AA'),('\u30DB','\u30AA'),('\u30E2','\u30AA'),('\u30E8','\u30AA'),('\u30ED','\u30AA'),('\u30F2','\u30AA'),('\u30F3','\u30F3')];
}
