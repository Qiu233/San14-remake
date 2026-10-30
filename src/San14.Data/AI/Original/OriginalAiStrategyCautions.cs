using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>The strategy prelude resets registered army-caution relations.
/// This is not army retirement, caution-group cancellation or object disposal.</summary>
public static class OriginalAiStrategyCautions
{
    public static ArmyCautionsState ResetRegistered(ArmyCautionsState cautions,
        IReadOnlyList<WorldEntityReference> registered)
    {
        if (registered.Count == 0) return cautions;
        var relations = new Dictionary<int, ArmyCautionRelation>(cautions.Relations);
        foreach (var entry in registered)
        {
            if (entry is not { Kind: WorldEntityKind.ArmyCaution, Id: int id } || id < 0)
                throw new InvalidDataException("original_ai.strategy_cautions.registry_receiver");
            // Both stored target words are unconditionally zeroed. The normal
            // owned representation of this value is an omitted Empty row;
            // an unknown previous target does not prevent this reached writer.
            relations.Remove(id);
        }
        return cautions with { Relations = relations };
    }
}
