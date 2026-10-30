using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Actual object construction and full226BB0 reset. Neither restore
/// nor a planning query fills unknown legacy rows from these producers.</summary>
internal static class ArmyCautionGroupPlanningSource
{
    internal static ArmyCautionsState Fresh() => new()
    {
        GroupPlanning = Enumerable.Range(0, 501).ToImmutableDictionary(id => id, _ => new ArmyCautionGroupPlanningState(0f, false, 0))
    };

    internal static ArmyCautionsState Reset(ArmyCautionsState state, int group)
    {
        if ((uint)group >= 501) throw new InvalidDataException("caution_group_planning.reset_identity");
        // A real reset replaces the receiver state even when every value was
        // already zero. SetItem may retain an equal record, allowing a borrowed
        // dirty cache to mistake the old receiver observation for the new one.
        var planning = state.GroupPlanning ?? ImmutableDictionary<int, ArmyCautionGroupPlanningState>.Empty;
        return state with { GroupPlanning = planning.Remove(group).Add(group, new(0f, false, 0)) };
    }
}
