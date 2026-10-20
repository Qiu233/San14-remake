using San14.Core;

namespace San14.Data;

/// <summary>Associations follow the sorted accepted pairs, then the complete
/// current Army registry. Presentation requests cannot gate these flag writes.</summary>
internal static class NormalCombatAssociationTail
{
    internal static GameState Apply(GameState state, SourceWorldContext current,
        ArmyMapGroupingCurrentQueries queries, NormalCombatMixedProjection projection,
        NormalCombatPresentationCapture presentation)
    {
        var outgoing = new Dictionary<int, List<NormalCombatParticipant>>();
        var incoming = new Dictionary<int, NormalCombatParticipant>();
        foreach (var pair in projection.OrderedPairs)
        {
            if (pair.Actor.Kind == 27)
            {
                if (!outgoing.TryGetValue(pair.Actor.PhysicalSlot, out var targets))
                    outgoing.Add(pair.Actor.PhysicalSlot, targets = []);
                targets.Add(pair.Target);
            }
            if (pair.Target.Kind == 27) incoming.TryAdd(pair.Target.PhysicalSlot, pair.Actor);
        }
        var arena = state.ReadArmyLifecycle() ?? throw NormalCombatMixedProducer.Missing("association_lifecycle");
        var slots = arena.ReceiverSlots().ToArray();
        var units = new Dictionary<int, UnitState>(state.Units);
        foreach (int physical in queries.ReadRegistry() ?? throw NormalCombatMixedProducer.Missing("association_registry"))
        {
            if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 4, false))
                throw NormalCombatMixedProducer.Missing("association_flags150");
            var receiver = slots[physical];
            var army = receiver.ActiveUnitId is int id ? units.GetValueOrDefault(id) : receiver.PendingImage;
            if (army?.NativeTacticRuntime is not { } runtime)
                throw NormalCombatMixedProducer.Missing("association_Army_runtime");
            var targets = outgoing.GetValueOrDefault(physical);
            var firstAttacker = incoming.GetValueOrDefault(physical);
            byte flags = runtime.Flags26;
            if (targets is null && firstAttacker is null) flags &= 0xFE;
            else
            {
                if (targets is { Count: > 0 } && targets.All(target => target.Kind == 11))
                    _ = OwnedArmyDestructorFlags.SetSourceBit(current, physical, 4, true);
                if ((flags & 1) == 0)
                {
                    presentation.FirstContact(army, targets is null ? [] : targets, firstAttacker);
                    flags |= 1;
                }
            }
            if (flags == runtime.Flags26) continue;
            var updated = army with { NativeTacticRuntime = runtime with { Flags26 = flags } };
            if (receiver.ActiveUnitId is int active) units[active] = updated;
            else slots[physical] = receiver with { PendingImage = updated };
        }
        //First-contact reports have already advanced this candidate's ring.
        //Keep their latest state while publishing this tail's own flag stores.
        var latest = presentation.State;
        var latestArena = latest.ReadArmyLifecycle() ?? throw NormalCombatMixedProducer.Missing("association_latest_lifecycle");
        return (latest with { Units = units }).WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(latestArena.WithReceiverSlots(slots), current));
    }
}
