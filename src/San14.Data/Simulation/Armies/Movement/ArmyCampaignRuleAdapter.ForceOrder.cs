using San14.Core;

namespace San14.Data;

public sealed record ForceProcessingOrderResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Campaign4290D0 prepares28B290's stable force order. This is
    /// called at that boundary, not recomputed by later consumers.</summary>
    public ForceProcessingOrderResult TryPrepareForceProcessingOrder(GameState state)
    {
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var registered = current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw OccupationMissing("force_order_registered_forces");
            var cities = current.ReadRegistryReferences(RegistryKind.City)
                ?? throw OccupationMissing("force_order_registered_cities");
            var persons = current.ReadRegisteredPersonPhysicalSlots()
                ?? throw OccupationMissing("force_order_registered_persons");
            var order = registered.Select(x => x?.PhysicalSlot ?? throw OccupationMissing("force_order_force_identity")).ToArray();
            var scores = new Dictionary<int, (int Cities, int Persons, int Gold, int Supplies)>();
            foreach (int force in order)
            {
                if (force is < 1 or > 51) throw OccupationMissing("force_order_force_range");
                int count = 0, people = 0, gold = 0, supplies = 0;
                foreach (var reference in cities)
                {
                    int id = reference?.PhysicalSlot ?? throw OccupationMissing("force_order_city_identity");
                    if (!state.Cities.TryGetValue(id, out var city)) throw OccupationMissing("force_order_city_owner");
                    if ((city.ForceId ?? 0) != force) continue;
                    count++;
                    gold = unchecked(gold + OccupationKnown(current.ReadBaseResource(new(BaseKind.City, id), BaseResourceKind.Gold), "force_order_city_gold"));
                    supplies = unchecked(supplies + OccupationKnown(current.ReadBaseResource(new(BaseKind.City, id), BaseResourceKind.Supplies), "force_order_city_supplies"));
                }
                foreach (int? entry in persons)
                {
                    int physical = entry ?? throw OccupationMissing("force_order_person_identity");
                    //These are pure conjunctive predicates. A known status
                    //outside1..4 excludes the person even when their unrelated
                    //membership is absent from the playable projection.
                    if (OccupationKnown(current.ReadPersonStatus(physical), "force_order_person_status") is not (>= 1 and <= 4)) continue;
                    if (OccupationKnown(current.ReadPersonOldForceSlot(physical), "force_order_person_force") == force) people++;
                }
                scores.Add(force, (count, people, gold, supplies));
            }
            //254CA0's map priorities are all initialized to0 by28B290.
            //24DA60 is stable insertion sort. The final identity comparison
            //makes ties deterministic without using original pointer addresses.
            int Compare(int first, int second)
            {
                bool aBand = first is >= 46 and <= 50, bBand = second is >= 46 and <= 50;
                if (aBand != bBand) return aBand ? 1 : -1;
                if (aBand) return first.CompareTo(second);
                var a = scores[first]; var b = scores[second];
                int c = b.Cities.CompareTo(a.Cities); if (c != 0) return c;
                c = b.Persons.CompareTo(a.Persons); if (c != 0) return c;
                c = b.Gold.CompareTo(a.Gold); if (c != 0) return c;
                c = b.Supplies.CompareTo(a.Supplies); return c != 0 ? c : first.CompareTo(second);
            }
            Array.Sort(order, Compare);
            return new(state with { ForceProcessingOrder = new(order) }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }
}
