using System.Collections.Immutable;

namespace San14.Core;

/// <summary>A stored history record. Fixed UTF16 windows preserve terminators,
/// full windows and the untouched tail of an overwritten record.</summary>
public sealed record CampaignHistoryRecord(byte Category, byte DistrictId, ushort HexId,
    ushort CampaignDay, ushort PersonId, byte Flags, byte ColorIndex,
    ImmutableArray<ushort> Text, ImmutableArray<ushort> SupplementalText,
    ImmutableArray<ushort> ForceShortName)
{
    public static CampaignHistoryRecord Empty => new(255, 0, 48400, 0, 0, 0, 0,
        ImmutableArray.CreateRange(new ushort[55]), ImmutableArray.CreateRange(new ushort[85]),
        ImmutableArray.CreateRange(new ushort[2]));
}

/// <summary>One campaign's persistent ordered history. Null is unknown legacy
/// knowledge. Only the genuine fresh producer creates the constructor state.</summary>
public sealed record CampaignHistoryState(ushort Cursor,
    ImmutableArray<CampaignHistoryRecord> Records, string? DefaultSupplementalText)
{
    public const int Capacity = 2001;
    public static CampaignHistoryState Fresh() => new(0,
        Enumerable.Range(0, Capacity).Select(_ => CampaignHistoryRecord.Empty).ToImmutableArray(), "");
}

/// <summary>Immutable source-time receipt for a completed record write. The
/// ring is the mutable world's authority; this value is post-write UI work,
/// never an instruction to write the history a second time or a played ACK.</summary>
public sealed record CampaignHistoryNotification(ushort Slot, CampaignHistoryRecord Record);

public sealed partial record GameState
{
    public CampaignHistoryState? CampaignHistory { get; init; }
}

internal static class CampaignHistoryValidation
{
    internal static void Validate(GameState state)
    {
        if (state.CampaignHistory is { } history)
        {
            if (history.Records.IsDefault || history.Records.Length != CampaignHistoryState.Capacity)
                throw new InvalidDataException("Campaign history records are incomplete.");
            foreach (var record in history.Records) ValidateRecord(record, false);
        }
    }

    internal static void ValidateNotification(CampaignHistoryNotification value)
    {
        if (value.Slot >= CampaignHistoryState.Capacity)
            throw new InvalidDataException("Campaign history notification slot is invalid.");
        ValidateRecord(value.Record, true);
    }

    private static void ValidateRecord(CampaignHistoryRecord value, bool written)
    {
        if (value is null || value.Category > 6 && value.Category != 255 || value.DistrictId > 51 ||
            value.HexId > 48400 || value.PersonId > 6000 ||
            value.Text.IsDefault || value.Text.Length != 55 ||
            value.SupplementalText.IsDefault || value.SupplementalText.Length != 85 ||
            value.ForceShortName.IsDefault || value.ForceShortName.Length != 2 ||
            written && (value.Category > 6 || value.HexId >= 48400 || value.PersonId == 0 || (value.Flags & 2) == 0))
            throw new InvalidDataException("Campaign history record fields are invalid.");
    }
}
