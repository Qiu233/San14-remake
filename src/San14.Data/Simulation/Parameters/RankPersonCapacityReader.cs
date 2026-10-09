namespace San14.Data.Formats;

public sealed record RankPersonCapacityCatalog(IReadOnlyList<int> Capacities, string ProfileId);

/// <summary>Owned CE person capacities indexed by thirteen ruler-rank identities.
/// No executable version or table hash is consulted.</summary>
public static class RankPersonCapacityReader
{
    public const string ImplementationVersion = "san14:ce-rank-person-capacities-v1";
    public const string ResourceUri = "san14://rules/ce/rank-person-capacities/v1";
    public const int CommonCapacity = 40;
    private static readonly RankPersonCapacityCatalog Catalog = new(
        Array.AsReadOnly(new[]
        {
            CommonCapacity, CommonCapacity, CommonCapacity,
            30, 24, 20, 12, 10, 5,
            CommonCapacity, CommonCapacity, CommonCapacity, CommonCapacity
        }), ImplementationVersion);

    public static RankPersonCapacityCatalog ReadDefaults(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new NotSupportedException("CE rank person capacities support SC and TC only.");
        return Catalog;
    }
}
