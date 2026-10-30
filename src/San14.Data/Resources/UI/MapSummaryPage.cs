namespace San14.Data;

/// <summary>A semantic receiver selected by the actual ordinary map-summary manager.</summary>
public sealed record MapSummaryReceiver(int Slot, int ReceiverId)
{
    public int? SourceZoomBand { get; init; }
}

public enum MapSummaryHoverKind { Army, Foothold, Force, District, Commander, Area }

/// <summary>A real hovered domain object, not a selected summary slot.
/// Army uses the live Unit ID; Foothold uses the registered location ID;
/// Force and District use their normal domain IDs; Commander uses the actual
/// Officer ID and resolves only that Person's current commander Army. The retained current
/// resolves all source receivers and related summaries.</summary>
public sealed record MapSummaryHoverTarget(MapSummaryHoverKind Kind, int SourceId);

/// <summary>Absent slots are known empty; unresolved slots retain their prior presentation.</summary>
public sealed record MapSummarySelection(IReadOnlyList<MapSummaryReceiver> Receivers,
    IReadOnlyDictionary<int, string> UnresolvedSlots)
{
    public MapSummaryTargetIdentity? TargetIdentity { get; init; }
    public bool? HoverTargetValid { get; init; }
}

public sealed record MapSummaryTargetIdentity(int? ArmySlot, int? GroupArmySlot,
    int? AreaSlot, int? ObjectSlot, int? BaseObjectId, int? ForceSlot,
    int? ScenerySlot, int? StateSlot);

/// <summary>One original compact summary layout and its current presentation.
/// Layout node identities belong only to this resource publication boundary.</summary>
public sealed record MapSummaryPage(int Slot, int ReceiverId, string LayoutName,
    bool? ReceiverValid, IReadOnlyDictionary<int, string> Text)
{
    public IReadOnlyDictionary<int, int> TextPresets { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, bool> Visibility { get; init; } = new Dictionary<int, bool>();
    public IReadOnlyDictionary<int, MapCornerColors> Colors { get; init; } = new Dictionary<int, MapCornerColors>();
    public IReadOnlyList<MapSummaryImage> Images { get; init; } = Array.Empty<MapSummaryImage>();
    public IReadOnlyList<MapSummaryIndicator> Indicators { get; init; } = Array.Empty<MapSummaryIndicator>();
    public IReadOnlyList<MapSummaryGauge> Gauges { get; init; } = Array.Empty<MapSummaryGauge>();
    public IReadOnlyList<MapSummaryBar> Bars { get; init; } = Array.Empty<MapSummaryBar>();
    public IReadOnlyList<MapSummaryTraitRow> Traits { get; init; } = Array.Empty<MapSummaryTraitRow>();
    public IReadOnlyList<MapSummaryRadar> Radars { get; init; } = Array.Empty<MapSummaryRadar>();
    public IReadOnlyList<MapSummaryForceFlag> Flags { get; init; } = Array.Empty<MapSummaryForceFlag>();
    public IReadOnlyList<MapSummaryPortrait> Portraits { get; init; } = Array.Empty<MapSummaryPortrait>();
    public IReadOnlyList<MapSummaryChild> Children { get; init; } = Array.Empty<MapSummaryChild>();
    public IReadOnlyDictionary<int, string> UnresolvedFields { get; init; } = new Dictionary<int, string>();
    public string? UnresolvedOwner { get; init; }
}

public sealed record MapSummaryImage(int Node, string SpriteName, string? TexturePack = null);
public sealed record MapSummaryGauge(int Node, int ReferenceNode, int Current, int Maximum)
{
    public string? AnimationName { get; init; }
}
public sealed record MapSummaryBar(int Node, int Current, int Maximum, int? Palette = null)
{
    public int? InitialCurrent { get; init; }
    public int? InitialMaximum { get; init; }
}
public sealed record MapSummaryTraitRow(int SourceIndex, string Text, byte Style, bool Active);
public sealed record MapSummaryRadar(int Node, IReadOnlyList<int> Values);
public sealed record MapSummaryForceFlag(int Node, DetailForceAppearance Appearance)
{ public bool Neighbor { get; init; } }
public sealed record MapSummaryPortrait(int Node, OfficerPortraitRoute? Portrait,
    DeploymentNarrationPortraitRoute? StandingPortrait = null)
{
    public string? LayoutName { get; init; }
    public int? SourceNode { get; init; }
}

/// <summary>A real nested layout. Null MountNode uses the child's authored root at the parent origin.</summary>
public sealed record MapSummaryChild(string Key, int? MountNode, MapSummaryPage Page);

/// <summary>Actual summary-manager inputs owned by the map host, independent of map-information overlays.</summary>
public sealed record MapSummaryViewOptions
{
    public int Mode { get; init; }
    public bool GeographySelected { get; init; }
    public int? GeographicSelection { get; init; }
    public MapSummaryReceiver? OverrideReceiver { get; init; }
    public MapSummaryHoverTarget? HoverTarget { get; init; }
    public Func<int, bool?>? ObjectPresentationAvailable { get; init; }
}
