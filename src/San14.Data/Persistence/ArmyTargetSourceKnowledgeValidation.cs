using San14.Core;

namespace San14.Data;

/// <summary>Admits compatibility knowledge at the save boundary. Ordinary
/// operations use their immutable owner without revalidating the campaign.</summary>
internal static class ArmyTargetSourceKnowledgeValidation
{
    public static void Validate(GameState state)
    {
        var knowledge = state.ResourceState as CampaignSourceKnowledge;
        var arena = state.ArmyLifecycle;
        var receivers = knowledge?.ArmyLifecycleSource is { } source ? new ArmyReceiverSourceDirectory(source) : null;
        var mapObjects = state.MapObjects is not null && knowledge?.MapObjectSource is { } objectSource
            ? new MapObjectSourceDirectory(objectSource) : null;
        foreach (var residue in knowledge?.ArmyTargets ?? [])
        {
            var target = arena?.Targets?.Find(residue.ReceiverId);
            if (target is null || residue.KindCode is not null && target.Kind != ArmyTargetKind.Unresolved ||
                residue.IdentityCode is not null && target.Destination?.Kind != ArmyTargetDestinationKind.Unresolved)
                throw new InvalidDataException("Army target source residue has no matching unresolved channel.");
            if (residue.MapObjectLookupAlias is ushort alias)
            {
                if (residue.KindCode is not null || residue.IdentityCode is not null ||
                    target.Kind != ArmyTargetKind.MapObjectReceiver ||
                    target.Destination is not { Kind: ArmyTargetDestinationKind.MapObjectReceiver,
                        MapObjectReceiverId: MapObjectReceiverId mapped } || mapObjects is null || mapObjects.Lookup(alias) != mapped)
                    throw new InvalidDataException("Army target map lookup alias has no matching known destination.");
                int physical = mapObjects.PhysicalOf(mapped);
                if (alias == physical && mapObjects.Lookup(physical) == mapped)
                    throw new InvalidDataException("Canonical army target lookup is stored as redundant alias knowledge.");
            }
            if (residue.IdentityCode is ushort code &&
                (target.Kind == ArmyTargetKind.City && code is >= 1 and <= 51 ||
                 target.Kind == ArmyTargetKind.ArmyReceiver && receivers?.TryResolveReceiverId(code, out _) == true ||
                 target.Kind == ArmyTargetKind.Hex && code < 48400 ||
                 target.Kind == ArmyTargetKind.MapObjectReceiver && mapObjects?.Lookup(code) is not null))
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
            if (residue?.KindCode == 11 && mapObjects is not null)
                throw new InvalidDataException("Known map object target category is stored as unresolved knowledge.");
            if (row.Target.Kind == ArmyTargetKind.MapObjectReceiver ||
                row.Target.Destination?.Kind == ArmyTargetDestinationKind.MapObjectReceiver)
            {
                if (mapObjects is null)
                    throw new InvalidDataException("Saved map object target has no current source directory.");
                if (row.Target.Destination?.MapObjectReceiverId is { } receiver)
                {
                    int physical = mapObjects.PhysicalOf(receiver);
                    if (residue?.MapObjectLookupAlias is null && mapObjects.Lookup(physical) != receiver)
                        throw new InvalidDataException("Saved map object target has neither a canonical lookup nor its source alias.");
                }
            }
        }
    }
}
