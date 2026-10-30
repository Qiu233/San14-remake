using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>One-time import of previously saved active logical-Army scores
/// into their actually linked retained receiver. Unlinked historical values
/// are not guessed onto a reused slot and never establish a zero value.</summary>
internal static class OriginalAiArmyEvaluationMigration
{
    internal static GameState Apply(GameState state, int? resourceVersion)
    {
        if (resourceVersion is null or >= 13 || state.OriginalAiPolicyExecution is not { } policy) return state;
        var changes = new List<OriginalAiArmyEvaluationChange>();
        var migrated = new HashSet<OriginalAiBlackboardEvaluation>();
        foreach (var entry in policy.Evaluations)
        {
            if (entry.Receiver is not OriginalAiEntityValue { EntityKind: WorldEntityKind.Army } army ||
                !state.Units.ContainsKey(army.Id)) continue;
            var actual = ArmyAiEvaluationSource.Read(state, army.Id);
            if (actual.Value is null) changes.Add(new(actual.ReceiverId, null, unchecked((uint)entry.Value)));
            migrated.Add(entry);
        }
        if (migrated.Count == 0) return state;
        var changed = ArmyAiEvaluationSource.Apply(state, changes);
        return changed with { OriginalAiPolicyExecution = policy with
        { Evaluations = policy.Evaluations.Where(entry => !migrated.Contains(entry)).ToImmutableArray() } };
    }
}
