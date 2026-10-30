namespace San14.Data;

/// <summary>The four original bank9..11 external archive ranges. Entries are
/// grouped by bank, rather than interleaved by portrait.</summary>
public sealed record DlcPortraitDescriptor(int Selector, int First, int Count, int ResourceKind,
    string RelativePath)
{
    private static readonly DlcPortraitDescriptor[] Ranges =
    [
        new(0, 6000, 100, 3, "0005/000.bin"),
        new(1, 6100, 15, 3, "0005/001.bin"),
        new(2, 6115, 100, 3, "0005/002.bin"),
        new(3, 6215, 30, 2, "DLC/0005/003.bin")
    ];
    public static IReadOnlyList<DlcPortraitDescriptor> All { get; } = Array.AsReadOnly(Ranges);
    public string ResourceUri { get; } = GameResourceId.File(RelativePath).ToString();

    public bool Contains(int appearance) => appearance >= First && appearance < First + Count;
    public int EntryIndex(int appearance, int bank)
    {
        if (!Contains(appearance) || bank is < 9 or > 11)
            throw new ArgumentOutOfRangeException(nameof(appearance));
        return checked((bank - 9) * Count + appearance - First);
    }
    public string SpriteName(int appearance, int bank, int part = 0)
    {
        _ = EntryIndex(appearance, bank);
        if (bank == 9 ? part is < 1 or > 3 : part != 0) throw new ArgumentOutOfRangeException(nameof(part));
        return $"portrait_{appearance}_{bank}_{part}";
    }

    public static DlcPortraitDescriptor? ForAppearance(int appearance)
    {
        foreach (var range in Ranges) if (range.Contains(appearance)) return range;
        return null;
    }
    internal static DlcPortraitDescriptor? ForResource(string resourceName)
    {
        foreach (var range in Ranges) if (range.ResourceUri == resourceName) return range;
        return null;
    }
}
