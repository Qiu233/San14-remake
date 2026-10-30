namespace San14.Data;

/// <summary>The ordinary summary manager's source admission matrix.
/// This is shared by receiver selection and the retained UI visibility loop.</summary>
public static class MapSummaryAdmissionRules
{
    public static int TargetMask(int mode, int sourceBand, bool geographySelected)
    {
        if (mode is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(mode));
        if (sourceBand is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(sourceBand));
        return mode switch
        {
            0 when sourceBand == 6 => geographySelected ? 1 << 12 : (1 << 5) | (1 << 6),
            0 => (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4) |
                (1 << 8) | (1 << 10) | (1 << 11) | (1 << 14) | (1 << 15),
            1 => 1 << 2,
            2 => (1 << 2) | (1 << 3),
            3 => (1 << 1) | (1 << 2) | (1 << 3) | (1 << 15),
            4 => 1 << 6,
            5 => 1 << 7,
            6 => 1 << 9,
            7 => 1 << 12,
            8 => 1 << 15,
            _ => 0
        };
    }

    public static int VisibleMask(int mode, int sourceBand, bool geographySelected)
    {
        int mask = TargetMask(mode, sourceBand, geographySelected);
        return sourceBand == 6 && geographySelected ? mask | (1 << 12) | (1 << 13) : mask;
    }
}
