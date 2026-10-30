using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    //4243C0 classifies the eligible people. It does not generate an offer or
    //remove the chosen identity from the caller's eligible sequence.
    internal int? GeneratePriority(IList<int> eligible, IList<int> dutyPeople)
    {
        dutyPeople.Clear();
        var areas = new List<int>();
        var training = new List<int>();
        var recruiting = new List<int>();
        var selectedAreas = new Dictionary<int, int>();
        var homes = new Dictionary<int, BaseReference>();
        foreach (int identity in eligible)
        {
            int person = PersonPhysical(identity);
            if (CityAssignedAreas(person).Any())
            {
                int area = CityAssignedArea(person);
                selectedAreas[identity] = area;
                if (K(Current.ReadAssignmentArea6D(area), "priority_area_bonus") < 3 ||
                    K(CityArea(area).Development?.Commerce, "priority_area_commerce") <= 1000 - Rule("priority-development-room", 10) ||
                    K(CityArea(area).Security, "priority_area_security") <= 100 - Rule("priority-security-room", 2))
                { dutyPeople.Add(identity); areas.Add(identity); }
                continue;
            }
            var home = CityBase(K(Current.ReadPersonFirstLocation(person), "priority_person_home"));
            homes[identity] = home;
            if (K(Current.ReadBaseObjectLink(home), "priority_home_link") >= 52) continue;
            int duty = CityDuty(person);
            if (duty == 3)
            {
                bool needed = CityMorale(home) <= CityMoraleCap(home) - Rule("priority-training-room", 3);
                if (!needed)
                    foreach (var target in CityBases())
                    {
                        int link = K(Current.ReadBaseObjectLink(target), "priority_training_link");
                        if (link is < 52 or >= 62 || !K(Current.ReadBaseNativeValidity(target), "priority_training_validity") ||
                            K(Current.ReadBaseDistrictSlot(target), "priority_training_district") != PrimaryDistrict ||
                            !CitySameBase(CityAssociatedCity(target), home)) continue;
                        if (CityMorale(target) <= CityMoraleCap(target) - Rule("priority-training-room", 3)) { needed = true; break; }
                    }
                if (needed) { dutyPeople.Add(identity); training.Add(identity); }
            }
            else if (duty == 2 && CityResource(home, BaseResourceKind.Troops) <=
                BasePostLoad.ReadTroopCapacity(home, Current) - Rule("priority-conscription-room", 100))
            {
                int city = K(home.PhysicalSlot, "priority_recruit_city");
                if ((Current.ReadRegisteredAreaReferences() ?? throw Missing("priority_area_registry"))
                    .Select(reference => reference ?? throw Missing("priority_area_pointer")).Any(reference =>
                    {
                        int area = K(reference.PhysicalSlot, "priority_area_identity");
                        return K(Current.ReadAreaNativeValidity(reference), "priority_recruit_area_validity") &&
                            K(Current.ReadAreaCitySlot(reference), "priority_recruit_area_city") == city &&
                            unchecked((int)K(CityArea(area).NativeNormalAftermath?.Quantity52, "priority_recruit_population") -
                                Rule("priority-recruit-reserve", 5000) - Rule("priority-conscription-room", 100)) >= 0;
                    }))
                { dutyPeople.Add(identity); recruiting.Add(identity); }
            }
        }
        var traits = new Dictionary<int, int>();
        var areaThreats = new Dictionary<int, bool>();
        if (recruiting.Count != 0) return Best(recruiting, 2);
        if (training.Count != 0) return Best(training, 3);
        return areas.Count != 0 ? Best(areas, 1) : null;

        int Best(List<int> candidates, int kind)
        {
            int best = candidates[0];
            foreach (int person in candidates.Skip(1)) if (Before(person, best, kind)) best = person;
            return best;
        }
        bool Before(int first, int second, int kind)
        {
            int a = Trait(first), b = Trait(second);
            if (a != b) return a > b;
            bool firstThreat = kind == 1 ? AreaThreat(selectedAreas[first]) : BaseThreat(homes[first]);
            bool secondThreat = kind == 1 ? AreaThreat(selectedAreas[second]) : BaseThreat(homes[second]);
            if (firstThreat != secondThreat) return firstThreat;
            if (kind == 1)
            {
                int one = selectedAreas[first], two = selectedAreas[second];
                a = K(CityArea(one).Security, "priority_sort_security"); b = K(CityArea(two).Security, "priority_sort_security");
                return a != b ? a < b : one < two;
            }
            var left = homes[first]; var right = homes[second];
            a = kind == 2 ? CityResource(left, BaseResourceKind.Troops) : CityMorale(left);
            b = kind == 2 ? CityResource(right, BaseResourceKind.Troops) : CityMorale(right);
            return a != b ? a < b : K(Current.ReadBaseObjectLink(left), "priority_sort_home") < K(Current.ReadBaseObjectLink(right), "priority_sort_home");
        }
        int Trait(int identity)
        {
            if (traits.TryGetValue(identity, out int result)) return result;
            int physical = PersonPhysical(identity), maximum = -1;
            //42B600 uses Int32.MinValue as the absent result. A matching
            //zero-valued trait must therefore sort ahead of absence.
            for (int index = 0; index < 9; index++)
            {
                int key = K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(Current, physical, index), "priority_trait_key");
                if (key == 0) continue;
                var personality = Current.ReadPersonalityTableReference(key < 356 ? key : 0) ?? throw Missing("priority_trait_pointer");
                int value = -1;
                for (int pair = 0; pair < 2; pair++)
                {
                    int effect = K(Current.ReadPersonalityByte(personality, pair == 0 ? 0xB6 : 0xBA), "priority_trait_effect");
                    if (effect != 50) continue;
                    value = Math.Clamp((int)unchecked((short)K(Current.ReadPersonalityWord(personality, pair == 0 ? 0xB8 : 0xBC), "priority_trait_value")), -100, 2000);
                    break;
                }
                if (value > maximum) maximum = value;
            }
            return traits[identity] = maximum < 0 ? int.MinValue : maximum;
        }
        bool BaseThreat(BaseReference target)
        {
            int id = K(target.PhysicalSlot, "priority_threat_base");
            return K(target.Kind == BaseKind.City ? id == 0 ? State.UnassignedCityHasNearbyEnemyArmy : State.Cities.GetValueOrDefault(id)?.HasNearbyEnemyArmy
                : id == 0 ? State.UnassignedGateHasNearbyEnemyArmy : State.Gates.GetValueOrDefault(id + 51)?.HasNearbyEnemyArmy, "priority_base_nearby_enemy");
        }
        bool AreaThreat(int area)
        {
            if (areaThreats.TryGetValue(area, out bool result)) return result;
            int cursor = AreaHexIterator.FindNextIndex(0, area, Current);
            while (cursor != 48400)
            {
                var hex = Current.ReadHexTableReference(cursor) ?? throw Missing("priority_area_hex");
                int raw = unchecked((short)K(Current.ReadHexRaw1C(hex), "priority_area_occupant"));
                if (raw is >= 1 and <= 500)
                {
                    var army = ProposalArmy(raw);
                    if (K(Current.ReadArmyUnitNativeValidity(army), "priority_area_army_validity") &&
                        ((ProposalArmyImage(raw).NativeTacticRuntime ?? throw Missing("priority_area_army_runtime")).Flags26 & 1) != 0)
                        return areaThreats[area] = true;
                }
                cursor = AreaHexIterator.FindNextIndex(cursor + 1, area, Current);
            }
            return areaThreats[area] = false;
        }
    }
}
