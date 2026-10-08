namespace San14.Core;

/// <summary>Saved current numeric receivers and containers used by native
/// CTroops allocation and pending Army cleanup. Arrays follow complete native
/// physical table order. A null field remains unknown, including older saves.</summary>
public sealed record NativeArmyLifecycleAuxiliariesState(string Revision,
    ushort?[] ArmyTroops58, ushort?[] TroopsLeader2C,
    ushort?[] CautionArmy10, ushort?[] CautionArmy12, ushort?[] CautionTroops18,
    NativeSharedListHandle?[] CautionArmy30Descriptors,
    NativeSharedListHandle? AssociatedCommand18Descriptor)
{
    /// <summary>The current secondary manager140 tuple order. This client
    /// creates no original tuple objects; fresh empty is installed once.</summary>
    public NativeArmyInteractionPair[]? InteractionPairs140 { get; init; }
}
public sealed record NativeArmyLifecycleReceiverReference(int? PhysicalSlot);
public sealed record NativeArmyInteractionPair(NativeArmyLifecycleReceiverReference? FirstArmy,
    NativeArmyLifecycleReceiverReference? SecondArmy, NativeArmyLifecycleReceiverReference? ThirdArmy);

public sealed partial record NativeArmyLifecycleState
{
    public NativeArmyLifecycleAuxiliariesState? Auxiliaries { get; init; }
}

public static class NativeArmyLifecycleAuxiliariesStateValidation
{
    public static void Validate(NativeArmyLifecycleState lifecycle)
    {
        if (lifecycle.Auxiliaries is not { } value) return;
        if (string.IsNullOrWhiteSpace(value.Revision) || lifecycle.Slots.Length != 501 ||
            value.ArmyTroops58 is not { Length: 501 } || value.TroopsLeader2C is not { Length: 501 } ||
            value.CautionArmy10 is not { Length: 1001 } || value.CautionArmy12 is not { Length: 1001 } ||
            value.CautionTroops18 is not { Length: 501 } || value.CautionArmy30Descriptors is not { Length: 501 })
            throw new InvalidDataException("Native Army cleanup auxiliary physical tables are malformed.");
        if (value.InteractionPairs140?.Any(x => x is null || x.FirstArmy?.PhysicalSlot is < 0 or > 500 ||
            x.SecondArmy?.PhysicalSlot is < 0 or > 500 || x.ThirdArmy?.PhysicalSlot is < 0 or > 500) == true)
            throw new InvalidDataException("Native Army interaction tuple identities are malformed.");
        var live = lifecycle.Pool.LiveDescriptors.ToHashSet();
        foreach (var descriptor in value.CautionArmy30Descriptors.Append(value.AssociatedCommand18Descriptor))
            if (descriptor?.Identity is int identity && !live.Contains(identity))
                throw new InvalidDataException("Native Army auxiliary container is not owned by its saved current pool.");
    }
}
