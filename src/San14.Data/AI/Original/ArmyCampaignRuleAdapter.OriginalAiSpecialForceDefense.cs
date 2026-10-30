using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnSpecialForceRaidsSource
    {
        internal GameState RunSpecialDefenseAndOffense(int force)
        {
            if (force is not (>= 46 and <= 50)) throw SpecialMissing("Force_scope");
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw SpecialMissing("same_current_pool");
            var home = Home(new(force));
            RunSpecialDefense(force, home);
            RunSpecialOffense(force, home);
            return state;
        }

        private void RunSpecialDefense(int force, BaseReference home)
        {
            int available = AvailableAtHome(home);
            if (available < SP("defense.minimum-available-people") || HomeTroops(home) < SP("defense.minimum-home-troops")) return;
            var grouping = new ArmyMapGroupingCurrentQueries(state, current, package._scenario, package._governorInputs,
                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            var targets = Temporary(); var own = Temporary();
            try
            {
                foreach (var army in SpecialArmies())
                    if (SpecialForce(army) == force) Append(own, new RegistryObjectReference(RegistryKind.Army, army.PhysicalSlot));
                foreach (var army in SpecialArmies())
                {
                    if (K(grouping.IsAtHome2C96E0(K(army.PhysicalSlot, "at_home_receiver")), "at_home2C96E0") || SpecialExcluded(army)) continue;
                    int hex = SpecialHex(army);
                    int jitter = SampleSpecial(6, hex, force, 0, 0);
                    if (ArmyRouteSearch.Distance(hex, current.ReadBaseCurrentHexIndex(home)) > unchecked(SP("defense.radius") + jitter)) continue;
                    if (!K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, SpecialForce(army), force), "defense_relation")) continue;
                    if ((SpecialTerrainFlags(hex) & 2) != 0) continue;
                    int physical = K(army.PhysicalSlot, "defense_target_Army");
                    int assigned = 0;
                    foreach (var defender in ArmyValues(own))
                    {
                        var target = current.ReadArmyLifecycleTargetFields(K(defender.PhysicalSlot, "defender_Army"))
                            ?? throw SpecialMissing("defender_target");
                        if (K(target.Kind38, "defender_target_kind") == 27 &&
                            K(target.Identity3A, "defender_target_identity") == physical) assigned = unchecked(assigned + SpecialTroops(defender));
                    }
                    if (unchecked(assigned * SP("defense.existing-coverage-percent")) / 100 < SpecialTroops(army))
                        Append(targets, new RegistryObjectReference(RegistryKind.Army, army.PhysicalSlot));
                }
                SortSpecialDefenseTargets(targets, home);
                var people = Temporary(); var excluded = Temporary();
                try
                {
                    FillHealthyHomePeople(home, people, excluded);
                    if (Count(people) == 0) return;
                    if (Count(people) >= 2) SortCommanders(people);
                    int attempted = 0;
                    foreach (var target in ArmyValues(targets))
                    {
                        int nearby = 0;
                        foreach (var defender in ArmyValues(own))
                            if (ArmyRouteSearch.Distance(SpecialHex(defender), SpecialHex(target)) <= SP("defense.support-radius"))
                                nearby = unchecked(nearby + SpecialTroops(defender));
                        int required = unchecked(SpecialTroops(target) * SP("defense.required-strength-percent")) / 100 - nearby;
                        if (Count(people) == 0) break;
                        int emitted = 0;
                        foreach (int person in Persons(people))
                        {
                            if (available - attempted < SP("defense.remaining-people") || HomeTroops(home) < SP("defense.remaining-home-troops")) return;
                            if ((K(current.ReadArmyReturnPersonFlags196(person), "defense_Person15E") & 1) != 0) continue;
                            int split = Count(people) >= SP("defense.split-people") && required >= SP("defense.split-strength")
                                ? Math.Max(SP("defense.split-count") - emitted, 1) : 1;
                            int troops = Math.Min(unchecked(HomeTroops(home) - SP("defense.home-reserve")), required / split);
                            troops = SpecialTroopAmount(troops, SP("defense.army-maximum"));
                            state = package.CreateSpecialForceDefenseArmyCurrent(state, current, pool, person, home, target, troops, force + 85);
                            // The source counts the actual creation attempt,
                            // including a native allocation/registration failure.
                            required = unchecked(required - troops); attempted++;
                            if (required < 1) break;
                            emitted++;
                        }
                    }
                }
                finally { Release(excluded); Release(people); }
            }
            finally { Release(own); Release(targets); }
        }

        private int AvailableAtHome(BaseReference home)
        {
            int location = K(current.ReadBaseObjectLink(home), "available_Home4E"), count = 0;
            if (location is < 1 or > 61) return 0;
            // Mode4 accepts all statuses in the real Person100 registry. This
            // admission count is separate from the later healthy mode0 list.
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw SpecialMissing("Person100"))
            {
                int person = entry ?? throw SpecialMissing("Person100_payload");
                if (K(current.ReadPersonFirstLocation(person), "available_PersonE2") != location) continue;
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw SpecialMissing("available_Army22A200");
                if (K(current.ReadArmyUnitNativeValidity(army), "available_Army_validity")) continue;
                int kind = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person), "available_PersonE4")).Kind;
                if (kind is 5 or 6) count++;
            }
            return count;
        }
        private IEnumerable<ArmyUnitReference> SpecialArmies()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw SpecialMissing("Army60"))
                yield return entry is { Kind: RegistryKind.Army, PhysicalSlot: int physical } ? new(physical) : throw SpecialMissing("Army60_payload");
        }
        private IEnumerable<ArmyUnitReference> ArmyValues(HexListHandle list)
        {
            foreach (var row in Nodes(list))
                yield return row.Value is { Kind: RegistryKind.Army, PhysicalSlot: int physical } ? new(physical) : throw SpecialMissing("Army_list_payload");
        }
        private bool SpecialExcluded(ArmyUnitReference army) => K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current,
            K(army.PhysicalSlot, "excluded_receiver")), "excluded233560");
        private int SpecialForce(ArmyUnitReference army) => K(current.ReadArmyUnitForceReference(army).PhysicalSlot, "Army_Force");
        private int SpecialHex(ArmyUnitReference army) => K(current.ReadArmyUnitRawHex2A(army), "Army2A");
        private int SpecialTroops(ArmyUnitReference army) => K(current.ReadArmyReturnResourceFields(army)?.Ready16, "Army16");
        private int HomeTroops(BaseReference home) => K(current.ReadBaseResource(home, BaseResourceKind.Troops), "Home3C");
        private byte SpecialTerrainFlags(int raw)
        {
            var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw SpecialMissing("Hex_pointer");
            int code = K(current.ReadHexSurfaceCode(hex), "Hex10");
            int terrain = K((current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw SpecialMissing("Terrain_pointer")).PhysicalSlot, "Terrain_identity");
            return K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrain), "Terrain2C");
        }
        private int SampleSpecial(int maximum, int first, int second, int third, int fourth)
            => TacticEffectSourceSamplerMath.Sample(maximum, 1, first, second, third, fourth,
                package.ReadArmyDestructionSampler(state, current) ?? throw SpecialMissing("sampler"));
        private int SP(string name) => OriginalAiSpecialForceParameters.Read(state, name);
        private int SpecialTroopAmount(int value, int maximum)
        {
            int minimum = SP("army-minimum");
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
        private static InvalidDataException SpecialMissing(string name) => new("original_ai.special." + name);

        private void SortSpecialDefenseTargets(HexListHandle targets, BaseReference home)
        {
            Sort(targets, (a, b) =>
            {
                if (a.Kind != RegistryKind.Army || b.Kind != RegistryKind.Army) throw SpecialMissing("defense_sort_kind");
                var left = new ArmyUnitReference(a.PhysicalSlot); var right = new ArmyUnitReference(b.PhysicalSlot);
                bool Ship(ArmyUnitReference army)
                {
                    int physical = K(army.PhysicalSlot, "sort_receiver");
                    int id = ArmyDestructionQueue.ResolveUnitId(state, physical) ?? throw SpecialMissing("sort_Army_image");
                    int formation = K(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, state.Units[id]), "sort_formation");
                    byte flags = K(TacticRemakeCurrentFormations.ReadParameter(state, formation < 21 ? formation : 0, 0xA6), "sort_FormationA6");
                    return (flags & 2) != 0;
                }
                bool first = Ship(left), second = Ship(right);
                if (first != second) return first;
                int origin = current.ReadBaseCurrentHexIndex(home);
                int x = ArmyRouteSearch.Distance(SpecialHex(left), origin), y = ArmyRouteSearch.Distance(SpecialHex(right), origin);
                if (x != y) return x < y;
                int t = SpecialTroops(left), u = SpecialTroops(right);
                return t != u ? t < u : K(left.PhysicalSlot, "sort_left") < K(right.PhysicalSlot, "sort_right");
            });
        }
    }
}
