using San14.Core;

namespace San14.Data;

public enum CommanderHudCardState
{
    ReadyArmy,
    WaitingArmy,
    ReadyWithoutArmy,
    WaitingWithoutArmy,
    Empty,
    Locked
}

public sealed record CommanderHudTacticSlot(int Slot, int? TacticId, string? Name, bool? Enabled,
    string? UnresolvedInput);

/// <summary>One of the five ordered source card positions. A null scalar is
/// unknown; empty/locked positions and hidden source components are explicit.</summary>
public sealed record CommanderHudCard(int Slot, int? OfficerId, string Name, CommanderHudCardState? State)
{
    public int? SelectedAtPeriod { get; init; }
    public int? UnitId { get; init; }
    public ushort? PortraitAppearanceIndex { get; init; }
    public int? HexId { get; init; }
    public int? Troops { get; init; }
    public int? SupplyPalette { get; init; }
    public int? Morale { get; init; }
    public int? MoraleMaximum { get; init; }
    public byte? MapMarker { get; init; }
    public ArmyMapIconSelection? FormationIcon { get; init; }
    public IReadOnlyList<int?> GaugeFrames { get; init; } = [];
    public bool? AutomaticEnabled { get; init; }
    public bool CanRemove { get; init; }
    public IReadOnlyList<CommanderHudTacticSlot> Tactics { get; init; } = [];
    public int? WaitingDays { get; init; }
    public string? StatusText { get; init; }
    public int? UnlockRankId { get; init; }
    public string? UnlockText { get; init; }
    public IReadOnlyDictionary<string, string> UnresolvedFields { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>Captured once on the main thread or while a genuine period worker
/// is stopped. This publication neither advances nor retains a mutable capture.</summary>
public sealed record CommanderHudPublication(GameState World, int ForceId, int? Capacity,
    IReadOnlyList<CommanderHudCard> Cards, string? UnresolvedInput);
