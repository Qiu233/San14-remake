using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnSpecialForceRaidsSource
    {
        private void RunSpecialOffense(int force, BaseReference home)
        {
            if (Setting(0x3A4, 0x100) != 0) return;
            int difficulty = Setting(0x134, 0x28);
            if (difficulty == 0) return;
            string tier = difficulty switch { 2 => "hard", 3 => "very-hard", > 3 => "super", _ => "normal" };
            string gateTier = difficulty switch { 2 => "hard", 3 => "very-hard", 4 => "super", _ => "normal" };
            int elapsed = unchecked((state.CampaignEventWorld ?? throw SpecialMissing("World454_owner")).ElapsedPeriodOffset +
                (int)(state.NativeMapSeason ?? throw SpecialMissing("World454_periods")).AdvancedPeriods);
            if (elapsed < SP("offense.minimum-periods-" + gateTier)) return;
            int available = AvailableAtHome(home);
            if (available < SP("offense.minimum-available-people") || HomeTroops(home) < SP("offense.minimum-home-troops")) return;
            var date = state.NativeMapSeason?.ThirdDate ?? throw SpecialMissing("World34_date");
            if (SP("offense.probability-" + gateTier) <= SampleSpecial(100, force, date.Year, date.Month, date.Day)) return;
            int attacking = HomeArmies(home).Count(army => SpecialAction(army) is 4 or 5);
            if (SP("offense.maximum-existing-attack-armies") < attacking) return;
            var targets = Temporary(); var neighbors = Temporary();
            try
            {
                FillNeighbors(home, neighbors);
                foreach (var target in Bases(neighbors))
                {
                    if (!K(current.ReadBaseNativeValidity(target), "raid_target_validity")) continue;
                    if (SpecialFriendship(force, K(current.ReadBaseForceSlot(target), "raid_target_Force")) >= SP("relation-threshold")) continue;
                    int[] path = ArmyRouteSearch.SpecialForceRaidPath(state, current,
                        current.ReadBaseCurrentHexIndex(home), current.ReadBaseCurrentHexIndex(target));
                    // An empty path is not separately rejected by this source:
                    // only a returned native-valid water Hex excludes a target.
                    bool water = false;
                    foreach (int hex in path)
                    {
                        var at = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw SpecialMissing("raid_path_Hex");
                        if (at.PhysicalSlot is not null && (SpecialTerrainFlags(hex) & 2) != 0) { water = true; break; }
                    }
                    if (!water) Append(targets, target);
                }
                if (Count(targets) == 0) return;
                if (Count(targets) > 1) SortSpecialRaidTargets(targets, home, force);
                var people = Temporary(); var excluded = Temporary();
                try
                {
                    FillHealthyHomePeople(home, people, excluded);
                    if (Count(people) == 0) return;
                    if (Count(people) > 1) SortCommanders(people);
                    int selected = 0, attempted = 0;
                    foreach (var target in Bases(targets))
                    {
                        selected++;
                        if (selected > SP("offense.targets-" + tier)) break;
                        int nearby = 0;
                        foreach (var army in HomeArmies(target))
                            if (ArmyRouteSearch.Distance(SpecialHex(army), current.ReadBaseCurrentHexIndex(target)) <= SP("offense.target-support-radius"))
                                nearby = unchecked(nearby + SpecialTroops(army));
                        int boost = 0, targetForce = K(current.ReadBaseForceSlot(target), "raid_target_Force_again");
                        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw SpecialMissing("raid_player");
                        if (targetForce == player.PhysicalSlot &&
                            SampleSpecial(100, targetForce, date.Year, date.Month, date.Day) < SP("offense.player-boost-probability"))
                            boost = SP("offense.player-boost-" + tier);
                        int required = unchecked(unchecked(SP("offense.required-percent-" + tier) + boost) *
                            unchecked(HomeTroops(target) + nearby)) / 100;
                        int emitted = 0;
                        foreach (int person in Persons(people))
                        {
                            if (available - attempted < SP("offense.remaining-people") || HomeTroops(home) < SP("offense.remaining-home-troops")) return;
                            if ((K(current.ReadArmyReturnPersonFlags196(person), "raid_Person15E") & 1) != 0) continue;
                            int split = difficulty > 1 && Count(people) >= SP("offense.split-people") && required >= SP("offense.split-strength")
                                ? Math.Max(SP("offense.split-count") - emitted, 1) : 1;
                            int troops = Math.Min(unchecked(HomeTroops(home) - SP("offense.home-reserve")), required / split);
                            troops = SpecialTroopAmount(troops, SP("offense.army-maximum"));
                            state = package.CreateSpecialForceRaidArmyCurrent(state, current, pool, person, home, target, troops, 14, 11, force + 85);
                            required = unchecked(required - troops); attempted++;
                            if (required < 1) break;
                            emitted++;
                        }
                    }
                }
                finally { Release(excluded); Release(people); }
            }
            finally { Release(neighbors); Release(targets); }
        }

        private IEnumerable<ArmyUnitReference> HomeArmies(BaseReference home)
        {
            foreach (var entity in package.BaseInfoArmies(state, current, home, null))
                yield return new(WorldIndexContext.FindArmyPhysical(state, entity.Id) ?? throw SpecialMissing("home_Army_receiver"));
        }
        private int SpecialAction(ArmyUnitReference army)
        {
            int id = ArmyDestructionQueue.ResolveUnitId(state, K(army.PhysicalSlot, "action_receiver"))
                ?? throw SpecialMissing("action_Army_image");
            return K(ArmyConfigurationSourceAdapter.ReadAction(state, state.Units[id]), "Army37_action");
        }
        private int SpecialFriendship(int force, int other)
        {
            if (other is < 0 or >= 52) return 0;
            var values = state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath?.Relations13;
            return values is { Length: 52 } ? values[other] : throw SpecialMissing("Force13_relationships");
        }
        private void SortSpecialRaidTargets(HexListHandle targets, BaseReference home, int force)
        {
            Sort(targets, (left, right) =>
            {
                var a = Base(left); var b = Base(right);
                int x = SpecialFriendship(force, K(current.ReadBaseForceSlot(a), "sort_left_Force"));
                int y = SpecialFriendship(force, K(current.ReadBaseForceSlot(b), "sort_right_Force"));
                if (x != y) return x < y;
                int t = HomeTroops(a), u = HomeTroops(b);
                if (t != u) return t < u;
                int origin = current.ReadBaseCurrentHexIndex(home);
                x = ArmyRouteSearch.Distance(origin, current.ReadBaseCurrentHexIndex(a));
                y = ArmyRouteSearch.Distance(origin, current.ReadBaseCurrentHexIndex(b));
                return x != y ? x < y : K(current.ReadBaseObjectLink(a), "sort_left_Base4E") < K(current.ReadBaseObjectLink(b), "sort_right_Base4E");
            });
        }
    }
}
