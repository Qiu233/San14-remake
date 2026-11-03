namespace San14.Core;

/// <summary>Writes the real emergence chronology, including its map snapshot.
/// The simulation retains ownership of the source queue and its completion.</summary>
public interface IForceEmergenceChronicleRules
{
    CampaignChronicleState RecordForceEmergenceChronicle(GameState state, ForceEmergenceSceneRequest request);
}

public sealed partial class GameSimulation
{
    /// <summary>Compatibility consumer for an older saved pending emergence
    /// record. Writing and removing this exact source head publish atomically.</summary>
    public bool TryCompleteForceEmergenceChronicle(GameState expectedOwner, long batchSequence,
        int requestIndex, IForceEmergenceChronicleRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!TryReadSourcePresentationHead(expectedOwner, CampaignEffectSource.MonthlyMaintenance,
                batchSequence, requestIndex, out var owner, out var pending, out var effects,
                out int batch, out var request) || request.ForceEmergenceScene is not { } emergence)
            return false;
        var chronicle = rules.RecordForceEmergenceChronicle(owner, emergence)
            ?? throw new InvalidDataException("force-emergence.chronicle.saved_owner");
        var recorded = owner with { CampaignChronicle = chronicle };
        CompleteSourcePresentationHead(recorded, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
