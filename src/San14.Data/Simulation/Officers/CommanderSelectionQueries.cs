using San14.Core;

namespace San14.Data;

public sealed record CommanderSelectionPerson(int OfficerId, string Name,
    OfficerAbilities Abilities, ushort? PortraitAppearanceIndex);

/// <summary>Saved order, including vacant entries and the unchanged auxiliary
/// channel. Slots outside the active prefix remain saved but are not selected.</summary>
public sealed record CommanderSelectionSlot(int Ordinal, int? OfficerId, int? SelectedAtPeriod);

/// <summary>One actual visible card, after the source card ordering. An unknown
/// location is distinct from a known absence of a valid map destination.</summary>
public sealed record CommanderSelectionCard(int OfficerId, string Name,
    OfficerAbilities Abilities, ushort? PortraitAppearanceIndex, int? SelectedAtPeriod,
    int? HexId, bool LocationKnown, string? LocationUnresolvedOwner);

public sealed record CommanderSelectionView(int ForceId, int? Capacity,
    IReadOnlyList<CommanderSelectionPerson> Candidates,
    IReadOnlyList<CommanderSelectionSlot> Slots,
    IReadOnlyList<CommanderSelectionCard> Cards, string? UnresolvedOwner)
{
    public bool Available => UnresolvedOwner is null;
    public OfficerInformationPageSet? PageSource { get; init; }
    public string? GuideText { get; init; }
}

public sealed record CommanderCardLocation(int? HexId, string? UnresolvedOwner)
{
    public bool Available => UnresolvedOwner is null;
}
