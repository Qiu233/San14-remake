using San14.Core;

namespace San14.Data;

/// <summary>Publishes the operation's actual army writes and retirements. It never
/// imports an unchanged army from a narrower source snapshot.</summary>
internal static class ArmyReturnImagePublication
{
    internal static GameState Publish(GameState state, SourceWorldContext context,
        IEnumerable<int> retired)
    {
        var changes = context.ArmyImageChanges;
        var lifecycle = state.ReadArmyLifecycle()
            ?? throw new InvalidDataException("Army image publication lacks its saved lifecycle.");
        var retiring = retired.ToHashSet();
        if (changes.Writes.Count == 0 && retiring.Count == 0) return state;
        Dictionary<int, UnitState>? units = null;
        Dictionary<int, OfficerState>? people = null;
        var receivers = new List<ArmyReceiverState>();
        ArmyReceiverSourceSlot[]? sourceSlots = null;
        var resources = state.ResourceState;
        foreach (int physical in changes.Writes.Keys.Concat(retiring).Distinct())
        {
            var write = changes.Writes.GetValueOrDefault(physical);
            var owner = write?.Owner ?? changes.Owner(physical);
            var source = lifecycle.Slots[physical];
            var receiver = lifecycle.Receivers.Find(owner.ReceiverId);
            if (source.ReceiverId != owner.ReceiverId || receiver is null ||
                (receiver.ActiveUnitId ?? receiver.PendingImage?.Id) != owner.UnitId)
                throw new InvalidDataException("Army operation write no longer identifies its captured receiver image.");
            var image = receiver.ActiveUnitId is int id ? (units ?? state.Units).GetValueOrDefault(id) : receiver.PendingImage;
            if (write?.TraitSource is { } traits)
            {
                sourceSlots ??= lifecycle.Slots.ToArray();
                sourceSlots[physical] = source with { AuxiliaryAC = traits.PersonsAC, AuxiliaryF4 = traits.AuxiliaryF4 };
                if (image is not null)
                {
                    image = image with { TraitRoster = write.Traits };
                    resources = ArmyTraitSourceAdapter.Write(resources, image.Id, traits.Traits64);
                }
            }
            if (image is not null && write is not null)
            {
                if (write.WritesWounded) image = image with { WoundedTroops = write.Wounded };
                if (write.WritesMorale) image = image with { MoraleState = image.MoraleState with { CurrentValue = write.Morale } };
            }
            if (retiring.Contains(physical))
            {
                if (image is null) throw new InvalidDataException("Army retirement has no captured image.");
                if (receiver.ActiveUnitId is int active)
                {
                    (units ??= new(state.Units)).Remove(active);
                    receiver = receiver.Retire(image);
                }
                else receiver = receiver with { PendingImage = image };
                var officer = (people ?? state.Officers)[image.OfficerId];
                if (officer.Deployed) (people ??= new(state.Officers))[image.OfficerId] = officer with { Deployed = false };
                receivers.Add(receiver);
            }
            else if (image is not null)
            {
                if (receiver.ActiveUnitId is int active) (units ??= new(state.Units))[active] = image;
                else receivers.Add(receiver with { PendingImage = image });
            }
        }
        var updated = lifecycle with
        {
            Receivers = receivers.Count == 0 ? lifecycle.Receivers : lifecycle.Receivers.WithEntries(receivers),
            Source = sourceSlots is null ? lifecycle.Source : lifecycle.Source with { Slots = sourceSlots }
        };
        var result = (state with { Units = units ?? state.Units, Officers = people ?? state.Officers, ResourceState = resources }).WithArmyLifecycle(updated);
        //Only a successful publication consumes these writes. Later callbacks can
        //supply new domain knowledge without an old source observation replaying it.
        changes.Clear();
        return result;
    }
}
