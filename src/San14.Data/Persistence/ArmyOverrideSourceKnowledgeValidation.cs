using San14.Core;

namespace San14.Data;

internal static class ArmyOverrideSourceKnowledgeValidation
{
    /// <summary>Core writers may replace an override without interpreting its
    /// resource residue. Saving removes only those superseded channels; a null
    /// channel remains unknown and is never filled from an older encoding.</summary>
    public static GameState PruneReplacedChannels(GameState state)
    {
        if (state.ResourceState is not CampaignSourceKnowledge knowledge || knowledge.ArmyOverrides.Count == 0) return state;
        var rows = new List<ArmyOverrideTargetSourceResidue>();
        foreach (var row in knowledge.ArmyOverrides)
        {
            if (row.Owner.UnitId is < 0 || row.Owner.ReceiverId is { Value: <= 0 } ||
                row.Owner.UnitId is null && row.Owner.ReceiverId is null ||
                row.Owner.ReceiverId is { } receiver && state.ArmyLifecycle?.Receivers.Find(receiver) is null)
                throw new InvalidDataException("Override source knowledge has an invalid owner.");
            // Standalone units have no retained receiver image. Their normal
            // retreat/destruction removes the owner itself, so its resource
            // knowledge leaves the serialized campaign too. Loading remains
            // strict: only a live owner can appear in a saved extension.
            if (row.Owner.UnitId is int id && !state.Units.ContainsKey(id) &&
                state.ArmyLifecycle?.Receivers.Entries.Any(slot => slot.PendingImage?.Id == id) != true) continue;
            var target = Find(state, row.Owner);
            if (target is null) throw new InvalidDataException("Override source knowledge has no current owner.");
            if (row.KindCode is 5 or 6 or 9 or 11 or 27 or 255)
                throw new InvalidDataException("Decoded override category is stored as source residue.");
            var retained = row with
            {
                KindCode = target.Kind == ArmyOverrideTargetKind.Unresolved ? row.KindCode : null,
                IdentityCode = target.Destination?.Kind == ArmyOverrideDestinationKind.Unresolved ? row.IdentityCode : null
            };
            if (retained.KindCode is not null || retained.IdentityCode is not null) rows.Add(retained);
        }
        return state with { ResourceState = knowledge.ReplaceArmyOverrides(rows) };
    }
    public static void Validate(GameState state)
    {
        var knowledge = state.ResourceState as CampaignSourceKnowledge;
        var directory = knowledge?.ArmyLifecycleSource is { } source ? new ArmyReceiverSourceDirectory(source) : null;
        foreach (var row in knowledge?.ArmyOverrides ?? [])
        {
            var target = Find(state, row.Owner);
            if (target is null || row.KindCode is not null && target.Kind != ArmyOverrideTargetKind.Unresolved ||
                row.IdentityCode is not null && target.Destination?.Kind != ArmyOverrideDestinationKind.Unresolved ||
                row.KindCode is null && row.IdentityCode is null)
                throw new InvalidDataException("Override source knowledge has no matching unresolved channel.");
            var fields = ArmyOverrideTargetSourceAdapter.Encode(target, row, directory);
            if (ArmyOverrideTargetSourceAdapter.Decode(fields, directory) != target)
                throw new InvalidDataException("Decoded override identity is stored as source residue.");
        }
        foreach (var unit in state.Units.Values) Check(unit.OverrideTarget, ArmyOverrideTargetSourceAdapter.Owner(state, unit));
        foreach (var slot in state.ArmyLifecycle?.Receivers.Entries ?? [])
        {
            Check(slot.OverrideTarget, new(slot.ReceiverId, null));
            if (slot.PendingImage is { } pending) Check(pending.OverrideTarget, ArmyOverrideTargetSourceAdapter.Owner(state, pending));
        }
        void Check(ArmyOverrideTargetState? target, ArmyOverrideSourceOwner owner)
        {
            var row = knowledge?.FindArmyOverride(owner);
            if (target?.Kind == ArmyOverrideTargetKind.Unresolved && row?.KindCode is null ||
                target?.Destination?.Kind == ArmyOverrideDestinationKind.Unresolved && row?.IdentityCode is null)
                throw new InvalidDataException("Saved unresolved override has lost its source knowledge.");
        }
    }
    private static ArmyOverrideTargetState? Find(GameState state, ArmyOverrideSourceOwner owner)
    {
        if (owner.UnitId is int id)
        {
            var unit = state.Units.GetValueOrDefault(id) ?? state.ArmyLifecycle?.Receivers.Entries
                .Select(slot => slot.PendingImage).SingleOrDefault(image => image?.Id == id);
            return unit is not null && ArmyOverrideTargetSourceAdapter.Owner(state, unit) == owner ? unit.OverrideTarget : null;
        }
        return owner.ReceiverId is { } receiver ? state.ArmyLifecycle?.Receivers.Entries
            .SingleOrDefault(slot => slot.ReceiverId == receiver)?.OverrideTarget : null;
    }
}
