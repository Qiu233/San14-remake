using San14.Core;

namespace San14.Data;

/// <summary>Only format adapters and legacy algorithm inputs use these codes.</summary>
public sealed record ArmyOverrideTargetSourceFields(byte? Kind3C, ushort? Identity3E);
public readonly record struct ArmyOverrideSourceOwner(ArmyReceiverId? ReceiverId, int? UnitId);
public sealed record ArmyOverrideTargetSourceResidue(ArmyOverrideSourceOwner Owner, byte? KindCode, ushort? IdentityCode);

public static class ArmyOverrideTargetSourceAdapter
{
    public static ArmyOverrideSourceOwner Owner(GameState state, UnitState unit) =>
        state.ArmyLifecycle?.Receivers.FindUnit(unit.Id) is { } receiver
            ? new(receiver.ReceiverId, unit.Id) : new(null, unit.Id);

    public static GameState Write(GameState state, UnitState unit, ArmyOverrideTargetSourceFields fields)
    {
        var target = Decode(fields, state.ReadArmyLifecycle() is { } arena ? new(arena) : null);
        return state with
        {
            Units = new(state.Units) { [unit.Id] = unit with { OverrideTarget = target } },
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithArmyOverride(Residue(Owner(state, unit), fields, target))
        };
    }
    public static ArmyOverrideTargetSourceFields ReadReceiver(GameState state, ArmyReceiverId receiver)
    {
        var slot = state.ArmyLifecycle?.Receivers.Find(receiver);
        return slot?.OverrideTarget is { } target ? Encode(target,
            (state.ResourceState as CampaignSourceKnowledge)?.FindArmyOverride(new(receiver, null)),
            new ArmyReceiverSourceDirectory(state.ReadArmyLifecycle()!)) : new(null, null);
    }
    internal static GameState InheritReceiverKnowledge(GameState state, UnitState created)
    {
        var owner = Owner(state, created);
        if (owner.ReceiverId is not { } receiver) return state;
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        var residue = knowledge.FindArmyOverride(new(receiver, null));
        return state with { ResourceState = knowledge.WithArmyOverride(new(owner, residue?.KindCode, residue?.IdentityCode)) };
    }
    internal static GameState ReconcileOwner(GameState state, UnitState unit)
    {
        if (state.ResourceState is not CampaignSourceKnowledge knowledge) return state;
        var owner = Owner(state, unit);
        if (knowledge.FindArmyOverride(owner) is not { } residue) return state;
        return state with { ResourceState = knowledge.WithArmyOverride(residue with
        {
            KindCode = unit.OverrideTarget?.Kind == ArmyOverrideTargetKind.Unresolved ? residue.KindCode : null,
            IdentityCode = unit.OverrideTarget?.Destination?.Kind == ArmyOverrideDestinationKind.Unresolved ? residue.IdentityCode : null
        }) };
    }

    public static ArmyOverrideTargetState Decode(ArmyOverrideTargetSourceFields fields,
        ArmyReceiverSourceDirectory? receivers = null)
    {
        ArmyOverrideTargetKind? kind = fields.Kind3C switch
        {
            null => null, 255 => ArmyOverrideTargetKind.None, 5 => ArmyOverrideTargetKind.City,
            6 => ArmyOverrideTargetKind.Gate, 9 => ArmyOverrideTargetKind.Hex,
            11 => ArmyOverrideTargetKind.Object, 27 => ArmyOverrideTargetKind.ArmyReceiver,
            _ => ArmyOverrideTargetKind.Unresolved
        };
        ArmyOverrideDestination? destination = fields.Identity3E is not ushort value ? null : kind switch
        {
            ArmyOverrideTargetKind.None when value == 0 => new(ArmyOverrideDestinationKind.None),
            ArmyOverrideTargetKind.City when value is >= 1 and <= 51 => new(ArmyOverrideDestinationKind.City, value),
            ArmyOverrideTargetKind.Gate when value is >= 1 and <= 10 => new(ArmyOverrideDestinationKind.Gate, value + 51),
            ArmyOverrideTargetKind.Hex => new(ArmyOverrideDestinationKind.Hex, value),
            ArmyOverrideTargetKind.ArmyReceiver when receivers?.TryResolveReceiverId(value, out _) == true =>
                ReceiverDestination(receivers, value),
            _ => new(ArmyOverrideDestinationKind.Unresolved)
        };
        return new(kind, destination);
    }
    private static ArmyOverrideDestination ReceiverDestination(ArmyReceiverSourceDirectory directory, int ordinal)
    {
        directory.TryResolveReceiverId(ordinal, out var receiver);
        return new(ArmyOverrideDestinationKind.ArmyReceiver, ReceiverId: receiver);
    }
    public static ArmyOverrideTargetSourceResidue Residue(ArmyOverrideSourceOwner owner,
        ArmyOverrideTargetSourceFields fields, ArmyOverrideTargetState target) => new(owner,
            target.Kind == ArmyOverrideTargetKind.Unresolved ? fields.Kind3C : null,
            target.Destination?.Kind == ArmyOverrideDestinationKind.Unresolved ? fields.Identity3E : null);

    public static ArmyOverrideTargetSourceFields Read(GameState state, UnitState unit) =>
        unit.OverrideTarget is { } target ? Encode(target,
            (state.ResourceState as CampaignSourceKnowledge)?.FindArmyOverride(Owner(state, unit)),
            state.ReadArmyLifecycle() is { } arena ? new(arena) : null) : new(null, null);

    public static ArmyOverrideTargetSourceFields Encode(ArmyOverrideTargetState target,
        ArmyOverrideTargetSourceResidue? residue = null, ArmyReceiverSourceDirectory? receivers = null)
    {
        byte? kind = target.Kind switch
        {
            null => null, ArmyOverrideTargetKind.None => 255, ArmyOverrideTargetKind.City => 5,
            ArmyOverrideTargetKind.Gate => 6, ArmyOverrideTargetKind.Hex => 9,
            ArmyOverrideTargetKind.Object => 11, ArmyOverrideTargetKind.ArmyReceiver => 27,
            ArmyOverrideTargetKind.Unresolved => residue?.KindCode,
            _ => throw new InvalidDataException("Unknown override target category.")
        };
        ushort? identity = target.Destination switch
        {
            null => null,
            { Kind: ArmyOverrideDestinationKind.None } => 0,
            { Kind: ArmyOverrideDestinationKind.City, EntityId: >= 1 and <= 51 } d => (ushort)d.EntityId.Value,
            { Kind: ArmyOverrideDestinationKind.Gate, EntityId: >= 52 and <= 61 } d => (ushort)(d.EntityId.Value - 51),
            { Kind: ArmyOverrideDestinationKind.Hex, EntityId: >= 0 and <= ushort.MaxValue } d => (ushort)d.EntityId.Value,
            { Kind: ArmyOverrideDestinationKind.ArmyReceiver, ReceiverId: ArmyReceiverId id }
                when receivers?.TryResolveSourceOrdinal(id, out _) == true => ReceiverOrdinal(receivers, id),
            { Kind: ArmyOverrideDestinationKind.Unresolved } => residue?.IdentityCode,
            _ => null
        };
        return new(kind, identity);
    }
    private static ushort? ReceiverOrdinal(ArmyReceiverSourceDirectory directory, ArmyReceiverId id) =>
        directory.TryResolveSourceOrdinal(id, out int ordinal) && ordinal <= ushort.MaxValue ? (ushort)ordinal : null;
}
