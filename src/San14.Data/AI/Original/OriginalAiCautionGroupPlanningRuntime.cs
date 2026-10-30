using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Only the three actual caution-group planning fields borrowed by
/// a policy transaction. Reads synchronize one real row; no World is retained
/// and no cache query clones the World. Final host capture publishes dirtied
/// fields once through the existing typed state-owner command.</summary>
public sealed class OriginalAiCautionGroupPlanningRuntime
{
    private readonly Dictionary<int, Row> rows = [];

    public ArmyCautionGroupPlanningState Read(int group,
        ImmutableDictionary<int, ArmyCautionGroupPlanningState>? actual)
        => Synchronize(group, actual).Value;

    public void SetStrength(int group, ImmutableDictionary<int, ArmyCautionGroupPlanningState>? actual, float value)
    {
        var row = Synchronize(group, actual);
        row.Value = row.Value with { EstimatedStrength = value }; row.Dirty = true;
    }
    public void SetStatus(int group, ImmutableDictionary<int, ArmyCautionGroupPlanningState>? actual, bool value)
    {
        var row = Synchronize(group, actual);
        row.Value = row.Value with { Status = value }; row.Dirty = true;
    }
    public void SetVirtualTarget(int group, ImmutableDictionary<int, ArmyCautionGroupPlanningState>? actual, int location)
    {
        var row = Synchronize(group, actual);
        row.Value = row.Value with { VirtualTargetLocationId = location }; row.Dirty = true;
    }

    public ImmutableDictionary<int, ArmyCautionGroupPlanningState>? Capture(
        ImmutableDictionary<int, ArmyCautionGroupPlanningState>? actual)
    {
        var result = actual;
        foreach (var pair in rows)
        {
            var current = actual?.GetValueOrDefault(pair.Key);
            // A real reset/removal/replacement is authoritative, even when
            // its numeric values happen to equal the previous reset values.
            if (!pair.Value.Dirty || !ReferenceEquals(current, pair.Value.Baseline)) continue;
            result = (result ?? ImmutableDictionary<int, ArmyCautionGroupPlanningState>.Empty)
                .SetItem(pair.Key, pair.Value.Value);
        }
        return result;
    }

    private Row Synchronize(int group, ImmutableDictionary<int, ArmyCautionGroupPlanningState>? actual)
    {
        if ((uint)group >= 501) throw new InvalidDataException("original_ai.caution_planning.group_identity");
        var current = actual?.GetValueOrDefault(group);
        if (!rows.TryGetValue(group, out var row) || !ReferenceEquals(row.Baseline, current))
            rows[group] = row = new(current);
        return row;
    }

    private sealed class Row(ArmyCautionGroupPlanningState? baseline)
    {
        internal readonly ArmyCautionGroupPlanningState? Baseline = baseline;
        internal ArmyCautionGroupPlanningState Value = baseline ?? new(null, null, null);
        internal bool Dirty;
    }
}
