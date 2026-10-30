using San14.Core;

namespace San14.Data;

/// <summary>One actual Area-label node18 result. Null visibility preserves
/// that label; palette is a normal sprite palette, not an event kind.</summary>
public sealed record AreaOccupationWarning(int AreaId, bool? Visible, int? Palette,
    string? UnresolvedOwner = null);

/// <summary>A reached reset hides every prior warning and restores palette12
/// before the listed source writes. Null reset preserves the prior display.</summary>
public sealed record AreaOccupationWarningPublication(bool? Reset,
    IReadOnlyList<AreaOccupationWarning> Entries, string? UnresolvedOwner = null);

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record RetainedAreaOccupationWarnings(GameState Owner,
        AreaOccupationWarningPublication Publication);

    private RetainedAreaOccupationWarnings? _areaOccupationWarnings;

    // Called only after the real Strategy/Playback producer has completely
    // resolved and committed. This retains presentation, not another world copy.
    private void PublishAreaOccupationWarnings(GameState state,
        IReadOnlyDictionary<int, int> visiblePalettes)
    {
        var rows = visiblePalettes.Select(row =>
            new AreaOccupationWarning(row.Key, true, row.Value)).ToArray();
        _areaOccupationWarnings = new(state, new(true, Array.AsReadOnly(rows)));
    }

    /// <summary>Read the reached normal producer once when the accepted world
    /// changes. This never recomputes encirclement, captures a world, or infers
    /// visibility from Area73. Use the composition's normal adapter instance.</summary>
    public AreaOccupationWarningPublication QueryAreaOccupationWarnings(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_areaOccupationWarnings is { } retained)
        {
            var owner = retained.Owner;
            // Normal completion may wrap the same accepted domain roots in
            // presentation metadata. Keep that publication without hashing or
            // rerunning its producer. A changed input owner remains unknown.
            if (ReferenceEquals(owner, state) ||
                ReferenceEquals(owner.Areas, state.Areas) &&
                ReferenceEquals(owner.Territory, state.Territory) &&
                ReferenceEquals(owner.Forces, state.Forces) &&
                ReferenceEquals(owner.NativeArmySimulation, state.NativeArmySimulation) &&
                ReferenceEquals(owner.NativeForceUnregisterCurrent, state.NativeForceUnregisterCurrent) &&
                ReferenceEquals(owner.ResourceState, state.ResourceState) &&
                ReferenceEquals(owner.ForceProcessingOrder, state.ForceProcessingOrder))
                return retained.Publication;
        }
        return new(null, Array.Empty<AreaOccupationWarning>(),
            "area-warning.normal_producer_publication");
    }
}
