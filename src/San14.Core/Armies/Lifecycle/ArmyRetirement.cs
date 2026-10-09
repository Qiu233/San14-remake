namespace San14.Core;

/// <summary>Detaches a removed unit into its reusable campaign receiver.
/// Deferred cleanup remains a separate operation and does not free it here.</summary>
public static class ArmyRetirement
{
    public const string RuleId = "san14:provisional-native-army-retirement-v1";

    public static GameState PreservePendingImage(GameState postRemoval, UnitState retired)
    {
        ArgumentNullException.ThrowIfNull(postRemoval);
        ArgumentNullException.ThrowIfNull(retired);
        if (postRemoval.ArmyLifecycle is not { } lifecycle || lifecycle.Receivers.FindUnit(retired.Id) is null)
            return postRemoval;
        if (postRemoval.Units.ContainsKey(retired.Id))
            throw new InvalidDataException("Army retirement still has an active world projection.");
        //Keep the receiver detached from transient live Army arrays. Historical
        //ready troops are the settled quantity, including zero after combat;
        //independent native fields remain exactly as their owners last wrote.
        //The removed projection cannot execute a queued playable tactic, even
        //when retirement occurs during strategy (for example immediate disband).
        //Do not change native waiting, abnormal counters or other source fields.
        UnitState terminal = retired with { TacticRuntime = retired.TacticRuntime is { } runtime
            ? runtime with { Pending = null } : null };
        return postRemoval with { ArmyLifecycle = lifecycle with
            { Receivers = lifecycle.Receivers.Retire(retired.Id, terminal) } };
    }
}
