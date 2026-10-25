using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2A3390's pure2A4180 target selection. Both maxima retain
    /// managerC0 order and the source's special-Force comparison. No App
    /// random state, presentation or temporary-list owner changes here.</summary>
    internal ForceReference SelectNormalTurnCoalitionTargetCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        _ = pool;
        var forces = (current.ReadRegistryReferences(RegistryKind.Force)
            ?? throw CoalitionTargetMissing("Force_registryC0"))
            .Select(entry => entry is { Kind: RegistryKind.Force, PhysicalSlot: int id }
                ? new ForceReference(id)
                : throw CoalitionTargetMissing("Force_registry_payload")).ToArray();
        ForceReference None() => current.ReadForceTableReference(0)
            ?? throw CoalitionTargetMissing("Force0_pointer");
        if (forces.Length == 0) return None();

        var cityCounts = new Dictionary<int, int>();
        var troopCounts = new Dictionary<int, int>();
        int Cities(ForceReference force)
        {
            int id = CoalitionTargetNeed(force.PhysicalSlot, "City_count_Force");
            if (!cityCounts.TryGetValue(id, out int value))
            {
                value = CoalitionTargetNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "City_count22AB60");
                cityCounts.Add(id, value);
            }
            return value;
        }
        int Troops(ForceReference force)
        {
            int id = CoalitionTargetNeed(force.PhysicalSlot, "troops_Force");
            if (!troopCounts.TryGetValue(id, out int value))
            {
                value = ReadNormalTurnCoalitionTotalTroopsCurrent(state, current, force);
                troopCounts.Add(id, value);
            }
            return value;
        }
        ForceReference mostCities = forces[0], mostTroops = forces[0];
        foreach (var force in forces.Skip(1))
        {
            //2484A0/248590 replace a special incumbent even for another
            //special candidate. Ordinary ties retain the earlier pointer.
            if (Special(mostCities) || !Special(force) && Cities(mostCities) < Cities(force)) mostCities = force;
            if (Special(mostTroops) || !Special(force) && Troops(mostTroops) < Troops(force)) mostTroops = force;
        }
        if (mostCities != mostTroops) return None();
        int total = Troops(mostCities);
        int selector = CoalitionTargetNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        int difficulty = CoalitionTargetNeed(BattleRemakeCurrentSettings.ReadInt32(state,
            selector == 0 ? 0x134 : 0x28), "difficulty3EB240");
        //TC typed/data.tsv141A4ABC4/D0/DC/E8 records these four
        //immutable thresholds. Runtime never reads an original executable.
        int threshold = difficulty switch { 0 => 500000, 1 => 425000, 2 => 350000, _ => 275000 };
        if (total < threshold) return None();

        int? strongestOpponent = null;
        foreach (var force in forces)
        {
            if (force == mostCities || !CoalitionTargetNeed(current.ReadForceNativeValidity(force), "opponent_validity") || Special(force)) continue;
            int value = Troops(force);
            if (strongestOpponent is null || strongestOpponent.Value < value) strongestOpponent = value;
        }
        //24A150/2532B0 sort opponents by this same pure signed total. Only
        //the first total is consumed; reducing its maximum preserves ties.
        return strongestOpponent is int second && unchecked(second * 2) <= total
            && CoalitionTargetNeed(current.ReadForceNativeValidity(mostCities), "target_validity") ? mostCities : None();

        static bool Special(ForceReference force) => CoalitionTargetNeed(force.PhysicalSlot, "comparison_Force") is >= 46 and <= 50;
    }

    /// <summary>2AD6F0(Force,1,1): all matching Base80 ready+wounded,
    /// then matching nonexcluded Army60 ready+wounded. Neither iterator
    /// adds a Base/Army native validity check to its own source filter.</summary>
    internal int ReadNormalTurnCoalitionTotalTroopsCurrent(GameState state,
        SourceWorldContext current, ForceReference force)
    {
        _ = state;
        if (!CoalitionTargetNeed(current.ReadForceNativeValidity(force), "troops_Force_validity")) return 0;
        int id = CoalitionTargetNeed(force.PhysicalSlot, "troops_Force_identity");
        int total = 0;
        if (id is < 1 or > 51) return total;
        foreach (var entry in current.ReadRegisteredBases() ?? throw CoalitionTargetMissing("troops_Base_registry80"))
        {
            var target = entry ?? throw CoalitionTargetMissing("troops_Base_pointer");
            if (CoalitionTargetNeed(current.ReadBaseForceSlot(target), "troops_Base_Force") != id) continue;
            total = unchecked(total
                + CoalitionTargetNeed(current.ReadBaseResource(target, BaseResourceKind.Troops), "troops_Base3C")
                + CoalitionTargetNeed(current.ReadArmyReturnBaseWounded40(target), "troops_Base40"));
        }
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw CoalitionTargetMissing("troops_Army_registry60"))
        {
            var registered = entry ?? throw CoalitionTargetMissing("troops_Army_pointer");
            if (registered is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw CoalitionTargetMissing("troops_Army_identity");
            var army = new ArmyUnitReference(physical);
            if (current.ReadArmyUnitRawForceSlot(army) != id
                || CoalitionTargetNeed(current.ReadOwnedArmyExclusion233560(registered), "troops_Army_excluded233560")) continue;
            var resources = current.ReadArmyReturnResourceFields(army) ?? throw CoalitionTargetMissing("troops_Army_resources");
            total = unchecked(total + CoalitionTargetNeed(resources.Ready16, "troops_Army16")
                + CoalitionTargetNeed(resources.Wounded18, "troops_Army18"));
        }
        return total;
    }

    private static T CoalitionTargetNeed<T>(T? value, string field) where T : struct
        => value ?? throw CoalitionTargetMissing(field);
    private static InvalidDataException CoalitionTargetMissing(string field)
        => new("normal.turn.coalition.target." + field);
}
