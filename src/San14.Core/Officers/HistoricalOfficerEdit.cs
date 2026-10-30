using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Application-owned overrides for the fields supported by the
/// historical officer editor. Campaign Persons remain independently owned.</summary>
public sealed record HistoricalOfficerEdit(int Id)
{
    public int PortraitId { get; init; }
    public ushort DeathYear { get; init; }
    public ImmutableArray<int> LikePersonIds { get; init; }
    public ImmutableArray<int> DislikePersonIds { get; init; }
    public byte Leadership { get; init; }
    public byte Strength { get; init; }
    public byte Intelligence { get; init; }
    public byte Politics { get; init; }
    public byte Charm { get; init; }
    public ImmutableArray<int> TraitIds { get; init; }
    public ImmutableArray<int> FormationIds { get; init; }
    public ImmutableArray<int> TacticIds { get; init; }
    public byte DoctrineId { get; init; }
    public byte PolicyId { get; init; }
    public byte PolicyBaseCost { get; init; }

    public static bool IsSupportedId(int id) => id is >= 1 and <= 1000 or >= 2001 and <= 2088;

    public void Validate()
    {
        if (!IsSupportedId(Id) || PortraitId is < 0 or > ushort.MaxValue || DeathYear > 350 ||
            Leadership > 100 || Strength > 100 || Intelligence > 100 || Politics > 100 || Charm > 100 ||
            DoctrineId > 10 || PolicyId > 120 || PolicyBaseCost > 10)
            throw new InvalidDataException("Historical officer fields are outside the editor's supported format.");
        Check(LikePersonIds, 8, 6000); Check(DislikePersonIds, 8, 6000);
        Check(TraitIds, 9, ushort.MaxValue); Check(TacticIds, 10, byte.MaxValue);
        if (FormationIds.IsDefault || FormationIds.Length > 21 ||
            FormationIds.Any(id => id is < 0 or > 20) || FormationIds.Distinct().Count() != FormationIds.Length)
            throw new InvalidDataException("Historical officer formation selection is invalid.");
    }

    private static void Check(ImmutableArray<int> values, int count, int maximum)
    {
        if (values.IsDefault || values.Length != count || values.Any(id => id < 0 || id > maximum))
            throw new InvalidDataException("Historical officer slot values are invalid.");
    }
}
