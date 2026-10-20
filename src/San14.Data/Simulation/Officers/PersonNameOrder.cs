namespace San14.Data;

/// <summary>The person-list name key's ordinal comparison. This deliberately
/// differs from responsibility-area name normalization.</summary>
internal static class PersonNameOrder
{
    internal static int Compare(string left, string right)
    {
        for (int index = 0; index < 1024; index++)
        {
            char a = index < left.Length ? left[index] : '\0';
            char b = index < right.Length ? right[index] : '\0';
            if (a != b) return a.CompareTo(b);
            if (a == '\0') break;
        }
        return 0;
    }
}
