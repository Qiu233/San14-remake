namespace San14.Core;

/// <summary>Owned CCmdAction manager IDs and current numeric receivers.
/// Unknown old saves retain a null owner. The primary associated18 descriptor
/// stays in NativeArmyLifecycle.Auxiliaries; these two value pools are separate.</summary>
public sealed record NativePersonCommandManagerState(string Revision, ushort? FreeHeadEA8,
    ushort?[] FreeNext30, int NextReceiverIdentity, Dictionary<int, int> IdToReceiver,
    Dictionary<int, NativePersonCommandState> Commands, NativeSharedListHandle Secondary28Descriptor,
    NativeSharedListPoolState MembersPool, NativeSharedListPoolState TargetsPool,
    Dictionary<int, NativeCommandUnionValue> TargetNodeValues);

/// <summary>Source-sized numeric byte window. Pointer and unwritten padding
/// bytes remain null; actual ctor/writers set only the reached numeric bytes.</summary>
public sealed record NativePersonCommandState(string Kind, byte?[] RawFields,
    NativeSharedListHandle Members20, NativeSharedListHandle Targets38);
public sealed record NativeCommandUnionValue(int FirstKind8, int? FirstPhysical0,
    int SecondKindC, int? SecondPhysical10);

public sealed partial record GameState
{
    public NativePersonCommandManagerState? NativePersonReturnCommands { get; init; }
}

public static class NativePersonCommandManagerStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativePersonReturnCommands is not { } owner) return;
        if (string.IsNullOrWhiteSpace(owner.Revision) || owner.FreeNext30 is not { Length: 1852 } ||
            owner.NextReceiverIdentity < 0 || owner.IdToReceiver is null || owner.Commands is null ||
            owner.Secondary28Descriptor is null || owner.MembersPool is null || owner.TargetsPool is null ||
            owner.TargetNodeValues is null || state.NativeArmyLifecycle?.Auxiliaries?.AssociatedCommand18Descriptor is null)
            throw new InvalidDataException("Current person-command manager owner is incomplete.");
        NativeArmyLifecycleStateValidation.ValidatePool(owner.MembersPool);
        NativeArmyLifecycleStateValidation.ValidatePool(owner.TargetsPool);
        if (owner.MembersPool.DescriptorCapacity != 0x400 || owner.MembersPool.NodeCapacity != 0x8000 ||
            owner.TargetsPool.DescriptorCapacity != 0x400 || owner.TargetsPool.NodeCapacity != 0x1400 ||
            owner.Secondary28Descriptor.Identity is int descriptor && !state.NativeArmyLifecycle.Pool.LiveDescriptors.Contains(descriptor) ||
            owner.Commands.Any(x => x.Key < 0 || x.Key >= owner.NextReceiverIdentity || x.Value is null ||
                x.Value.Kind != "return" || x.Value.RawFields is not { Length: 128 } ||
                x.Value.Members20 is null || x.Value.Targets38 is null) ||
            owner.IdToReceiver.Any(x => x.Key is < 0 or >= 1852 || !owner.Commands.ContainsKey(x.Value)))
            throw new InvalidDataException("Current person-command manager contains an invalid source allocation.");
        foreach (var command in owner.Commands.Values)
        {
            if (command.Members20.Identity is int member && !owner.MembersPool.LiveDescriptors.Contains(member) ||
                command.Targets38.Identity is int target && !owner.TargetsPool.LiveDescriptors.Contains(target))
                throw new InvalidDataException("Current command value descriptor does not belong to its own pool.");
        }
        //The source Int32 pool is also used by Force ranking temporaries.
        //Released nodes keep their last scalar payload; each live command
        //member chain still consumes only its own MemberId32 shape.
        if (owner.MembersPool.Nodes.Values.Any(x => x.Payload is { } p && (p.Kind is not ("MemberId32" or "Int32Value") || p.Identity is < 0 or > ushort.MaxValue)) ||
            owner.TargetsPool.Nodes.Any(x => x.Value.Payload is { } p && (p.Kind != "Union24" || p.Identity != x.Key || !owner.TargetNodeValues.ContainsKey(x.Key))) ||
            owner.TargetNodeValues.Any(x => !owner.TargetsPool.Nodes.ContainsKey(x.Key) || x.Value is null))
            throw new InvalidDataException("Current command value payloads have another source shape.");
    }
}
