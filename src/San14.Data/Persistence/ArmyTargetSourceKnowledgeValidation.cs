using San14.Core;

namespace San14.Data;

/// <summary>Admits compatibility knowledge at the save boundary. Ordinary
/// operations use their immutable owner without revalidating the campaign.</summary>
internal static class ArmyTargetSourceKnowledgeValidation
{
    public static void Validate(GameState state)
    {
        var knowledge = state.ResourceState as CampaignSourceKnowledge;
        var arena = state.NativeArmyLifecycle;
        var receivers = arena is null ? null : new ArmyReceiverSourceDirectory(arena);
        foreach (var residue in knowledge?.ArmyTargets ?? [])
        {
            var target = arena?.Targets?.Find(residue.ReceiverId);
            if (target is null || residue.KindCode is not null && target.Kind != ArmyTargetKind.Unresolved ||
                residue.IdentityCode is not null && target.Destination?.Kind != ArmyTargetDestinationKind.Unresolved)
                throw new InvalidDataException("Army target source residue has no matching unresolved channel.");
            if (residue.IdentityCode is ushort code &&
                (target.Kind == ArmyTargetKind.City && code is >= 1 and <= 51 ||
                 target.Kind == ArmyTargetKind.ArmyReceiver && receivers!.TryResolveReceiverId(code, out _)))
                throw new InvalidDataException("Decoded army target identity is stored as unresolved knowledge.");
        }
        foreach (var row in arena?.Targets?.Assignments ?? [])
        {
            var residue = knowledge?.FindArmyTarget(row.ReceiverId);
            if (row.Target.Kind == ArmyTargetKind.Unresolved && residue?.KindCode is null ||
                row.Target.Destination?.Kind == ArmyTargetDestinationKind.Unresolved && residue?.IdentityCode is null)
                throw new InvalidDataException("Saved unresolved army target has lost its source knowledge.");
            if (residue?.KindCode is 5 or 27 or 255)
                throw new InvalidDataException("Decoded army target category is stored as unresolved knowledge.");
        }
    }
}
