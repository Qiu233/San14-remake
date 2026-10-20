using System.Collections.Immutable;

namespace San14.Core;

/// <summary>A persistent chronology entry. EntryKind zero is inactive, but
/// all windows, snapshots and old sequence values remain owned and retained.</summary>
public sealed record CampaignChronicleRecord(ushort EventCode, byte Family, ushort CampaignDay,
    ImmutableArray<ushort> MainLabel, byte ColorIndex, uint EntryKind,
    ImmutableArray<ushort> Label0, ImmutableArray<ushort> Label1, ImmutableArray<ushort> Label2,
    ImmutableArray<byte> AreaColors, ImmutableArray<ImmutableArray<ushort>> CityForceShortNames,
    ImmutableArray<ushort> HexIds, ushort Sequence, byte EntryFlags)
{
    /// <summary>The proved primary/important constructor values. These are
    /// fresh values, never a replacement for an unknown restored entry.</summary>
    public static CampaignChronicleRecord Empty(bool important) => new(0, 0, 0,
        ImmutableArray.CreateRange(new ushort[16]), 0, 0,
        ImmutableArray.CreateRange(new ushort[9]), ImmutableArray.CreateRange(new ushort[9]),
        ImmutableArray.CreateRange(new ushort[9]), ImmutableArray.CreateRange(new byte[501]),
        Enumerable.Repeat(ImmutableArray.Create<ushort>(0, 0), 52).ToImmutableArray(),
        ImmutableArray.Create<ushort>(48400, 48400, 48400, 48400), 0, important ? (byte)1 : (byte)0);
}

/// <summary>One campaign's saved chronology and independent recording control.
/// Managed index zero corresponds to native writable slot one; sentinels have
/// no consumed fields here. The separate report ring is not this owner.</summary>
public sealed record CampaignChronicleState(int? RecordingSuppression,
    ImmutableArray<CampaignChronicleRecord> PrimaryEntries,
    ImmutableArray<CampaignChronicleRecord> ImportantEntries)
{
    public const int PrimaryCapacity = 200;
    public const int ImportantCapacity = 30;

    public static CampaignChronicleState Fresh() => new(0,
        Enumerable.Range(0, PrimaryCapacity).Select(_ => CampaignChronicleRecord.Empty(false)).ToImmutableArray(),
        Enumerable.Range(0, ImportantCapacity).Select(_ => CampaignChronicleRecord.Empty(true)).ToImmutableArray());
}

public sealed partial record GameState
{
    public CampaignChronicleState? CampaignChronicle { get; init; }
}

internal static class CampaignChronicleValidation
{
    internal static void Validate(GameState state)
    {
        if (state.CampaignChronicle is not { } chronicle) return;
        ValidateEntries(chronicle.PrimaryEntries, CampaignChronicleState.PrimaryCapacity);
        ValidateEntries(chronicle.ImportantEntries, CampaignChronicleState.ImportantCapacity);
    }

    private static void ValidateEntries(ImmutableArray<CampaignChronicleRecord> entries, int capacity)
    {
        if (entries.IsDefault || entries.Length != capacity)
            throw new InvalidDataException("Campaign chronology entries are incomplete.");
        foreach (var entry in entries)
        {
            if (entry is null || entry.Family > 3 || entry.ColorIndex > 60 ||
                entry.MainLabel.IsDefault || entry.MainLabel.Length != 16 ||
                entry.Label0.IsDefault || entry.Label0.Length != 9 ||
                entry.Label1.IsDefault || entry.Label1.Length != 9 ||
                entry.Label2.IsDefault || entry.Label2.Length != 9 ||
                entry.AreaColors.IsDefault || entry.AreaColors.Length != 501 ||
                entry.CityForceShortNames.IsDefault || entry.CityForceShortNames.Length != 52 ||
                entry.HexIds.IsDefault || entry.HexIds.Length != 4)
                throw new InvalidDataException("Campaign chronology entry fields are invalid.");
            if (entry.AreaColors.Any(color => color > 60) || entry.HexIds.Any(hex => hex > 48400) ||
                entry.CityForceShortNames.Any(name => name.IsDefault || name.Length != 2))
                throw new InvalidDataException("Campaign chronology snapshots or Hex fields are invalid.");
        }
    }
}
