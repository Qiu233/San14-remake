using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiEvaluationRuntime
{
    private readonly Dictionary<ArmyReceiverId, ArmyReceiverScore> armyReceivers = [];

    internal int ReadArmyEvaluation(GameState state, int unitId)
    {
        var row = ArmyScore(state, unitId);
        return unchecked((int)(row.Value ?? throw new InvalidDataException(
            "original_ai.Army.retained_receiver_evaluation_word8")));
    }

    internal void SetArmyEvaluation(GameState state, int unitId, int value)
    {
        var row = ArmyScore(state, unitId);
        row.Value = unchecked((uint)value);
        row.Dirty = true;
    }

    internal ImmutableArray<OriginalAiArmyEvaluationChange> CaptureArmyReceiverChanges() =>
        armyReceivers.Where(pair => pair.Value.Dirty).Select(pair =>
            new OriginalAiArmyEvaluationChange(pair.Key, pair.Value.Observed,
                pair.Value.Value ?? throw new InvalidDataException("original_ai.Army.assigned_evaluation")))
            .ToImmutableArray();

    private ArmyReceiverScore ArmyScore(GameState state, int unitId)
    {
        var observed = ArmyAiEvaluationSource.Read(state, unitId);
        if (armyReceivers.TryGetValue(observed.ReceiverId, out var row) && row.Observed == observed.Value) return row;
        // Only the actual receiver's observed scalar is compared. Auxiliary
        // record changes do not discard an in-flight score, and a real score
        // writer replacing the observation takes precedence. No World is held.
        row = new(observed.Value);
        armyReceivers[observed.ReceiverId] = row;
        return row;
    }

    private sealed class ArmyReceiverScore(uint? observed)
    {
        internal readonly uint? Observed = observed;
        internal uint? Value = observed;
        internal bool Dirty;
    }
}
