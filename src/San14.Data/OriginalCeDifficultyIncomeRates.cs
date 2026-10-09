using San14.Core;

namespace San14.Data;

/// <summary>Verified CE income-stage percentages. The monthly base calculation,
/// caps and final stock delta are separate rules.</summary>
public sealed class OriginalCeDifficultyIncomeRates : IPlayableMonthlyIncomeRates
{
    private readonly HashSet<int> _humanForceIds;
    private readonly int _humanGoldPercent;
    private readonly int _humanSuppliesPercent;
    private readonly int _computerGoldPercent;
    private readonly int _computerSuppliesPercent;

    public string RuleVersion { get; }
    public string DifficultyId { get; }
    public string Locale { get; }

    private OriginalCeDifficultyIncomeRates(string difficultyId, string locale,
        string sourceRuleVersion, IReadOnlySet<int> humanForceIds)
    {
        // The original has distinct gold and supplies branches. Their current
        // tables happen to agree, but keep their values independent here.
        (_humanGoldPercent, _computerGoldPercent) = difficultyId switch
        {
            "beginner" => (200, 100),
            "intermediate" => (150, 100),
            "advanced" => (100, 100),
            "super" => (100, 150),
            "top" => (100, 200),
            _ => throw new InvalidDataException($"CE income difficulty {difficultyId} is unsupported.")
        };
        (_humanSuppliesPercent, _computerSuppliesPercent) = difficultyId switch
        {
            "beginner" => (200, 100),
            "intermediate" => (150, 100),
            "advanced" => (100, 100),
            "super" => (100, 150),
            "top" => (100, 200),
            _ => throw new InvalidDataException($"CE income difficulty {difficultyId} is unsupported.")
        };
        if (humanForceIds.Any(id => id < 1))
            throw new ArgumentOutOfRangeException(nameof(humanForceIds));
        _humanForceIds = humanForceIds.ToHashSet();
        (DifficultyId, Locale, RuleVersion) =
            (difficultyId, locale, sourceRuleVersion);
    }

    public static OriginalCeDifficultyIncomeRates LoadCurrent(string sourceDirectory,
        string locale, string difficultyId, IReadOnlySet<int> humanForceIds)
    {
        ArgumentNullException.ThrowIfNull(humanForceIds);
        string version = SimulationRuleIdentity.ForLocale(locale);
        return new(difficultyId, locale, version, humanForceIds);
    }

    public (int GoldPercent, int SuppliesPercent) ForForce(int forceId)
    {
        return _humanForceIds.Contains(forceId)
            ? (_humanGoldPercent, _humanSuppliesPercent)
            : (_computerGoldPercent, _computerSuppliesPercent);
    }
}
