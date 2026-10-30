using San14.Core;

namespace San14.Data;

/// <summary>One genuine Army map indicator state, selected by the view's retained cycle.</summary>
public sealed record MapSummaryIndicatorState(int Index, string SpriteName, int Palette);

/// <summary>The authored node and Army identity share the manager's independent index owner.</summary>
public sealed record MapSummaryIndicator(int Node, int ArmyPhysicalSlot, IReadOnlyList<MapSummaryIndicatorState> States);

/// <summary>One actual-current publication. Omitted invalid/excluded Armies and
/// unresolved rows retain their previous view-owned index; neither means deletion.</summary>
public sealed record MapSummaryIndicatorFrame(GameState World,
    IReadOnlyDictionary<int, IReadOnlyList<MapSummaryIndicatorState>> ArmyStates,
    IReadOnlyDictionary<int, string> UnresolvedArmies);

/// <summary>Pure six-state display and rotation rules. The normal map owner
/// keeps all501 indexes and the source callback counter until map retirement.</summary>
public static class MapSummaryIndicatorRules
{
    private static readonly MapSummaryIndicatorState[] Definitions =
    [
        new(0, "000_cmn_icon_map_konran", 12),
        new(1, "000_cmn_icon_map_asidome", 12),
        new(2, "000_cmn_icon_map_chohatu", 12),
        new(3, "000_cmn_icon_map_hyourou_fuchi", 11),
        new(4, "000_cmn_icon_map_hyourou_fuchi", 12),
        new(5, "000_cmn_icon_map_hyourou_fuchi", 61)
    ];
    private static readonly MapSummaryIndicatorState Empty = new(-1, "000_cmn_icon_map_hyourou_fuchi", 11);

    //A retained index can temporarily point to a state absent from the new
    //mask until the next source refresh. Do not eagerly normalize it on query.
    public static MapSummaryIndicatorState Describe(int index) => (uint)index < 6 ? Definitions[index] : Empty;

    public static int NextIndex(int previous, IReadOnlyList<MapSummaryIndicatorState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        int first = -1;
        foreach (var state in states)
        {
            if (first == -1) first = state.Index;
            if (previous < state.Index) return state.Index;
        }
        return first;
    }

    /// <summary>Fresh counter0 compares before incrementing: source30/60
    /// rotates on callback31/61, including callbacks while the page is hidden.</summary>
    public static int RefreshCallbackCount(int sourceFramesPerSecond) => sourceFramesPerSecond is 30 or 60
        ? sourceFramesPerSecond + 1 : throw new ArgumentOutOfRangeException(nameof(sourceFramesPerSecond));

    internal static IReadOnlyList<MapSummaryIndicatorState> FromMask(int mask)
        => Array.AsReadOnly(Definitions.Where(state => (mask & (1 << state.Index)) != 0).ToArray());
}
