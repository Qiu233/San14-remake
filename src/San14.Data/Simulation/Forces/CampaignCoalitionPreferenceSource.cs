using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>28B290/24DA60 produces World1630 at4290D0(NormalTurn56),
    /// or44C180's explicit World2214 branch. Consumers retain the saved order.</summary>
    internal GameState PrepareCoalitionPreferenceCurrent(GameState state, SourceWorldContext current)
    {
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
            if (scores.ContainsKey(force)) continue;
            int count = 0, people = 0, gold = 0, supplies = 0;
            foreach (var reference in cities)
            {
                int id = reference?.PhysicalSlot ?? throw OccupationMissing("force_order_city_identity");
                if (OccupationKnown(current.ReadBaseForceSlot(new(BaseKind.City, id)), "force_order_city_force") != force) continue;
                count++;
                gold = unchecked(gold + OccupationKnown(current.ReadBaseResource(new(BaseKind.City, id), BaseResourceKind.Gold), "force_order_city_gold"));
                supplies = unchecked(supplies + OccupationKnown(current.ReadBaseResource(new(BaseKind.City, id), BaseResourceKind.Supplies), "force_order_city_supplies"));
            }
            if (force is >= 1 and <= 51) foreach (int? entry in persons)
            {
                int physical = entry ?? throw OccupationMissing("force_order_person_identity");
                //230570(Force,0,0): ForcePersonIterator tests membership
                //before serving status, with neither mode1 exclusion.
                if (OccupationKnown(current.ReadPersonOldForceSlot(physical), "force_order_person_force") != force) continue;
                if (OccupationKnown(current.ReadPersonStatus(physical), "force_order_person_status") is >= 1 and <= 4) people++;
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
        return state with { ForceProcessingOrder = new(order) };
    }
}
