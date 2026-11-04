namespace San14.Core;

/// <summary>Performs the saved independence prefix chronology operation.
/// A known source suppression may leave an absent chronology unchanged.</summary>
public interface IForceIndependenceChronicleRules
{
    CampaignChronicleState? RecordForceIndependenceChronicle(GameState state, ForceIndependenceSceneRequest request);
}

public sealed partial class GameSimulation
{
    /// <summary>Compatibility for a pending prefix record. The token proves
    /// neither successful Force creation nor a historical snapshot/slot.</summary>
    public bool TryCompleteForceIndependenceChronicle(GameState expectedOwner,
        CampaignEffectSource expectedSource, long batchSequence, int requestIndex,
        IForceIndependenceChronicleRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (expectedSource is not (CampaignEffectSource.StrategicCommands or CampaignEffectSource.MonthlyMaintenance) ||
            !TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.ForceIndependenceScene is not { } independence)
            return false;
        var chronicle = rules.RecordForceIndependenceChronicle(owner, independence);
        var recorded = owner with { CampaignChronicle = chronicle };
        CompleteSourcePresentationHead(recorded, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
