using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ReportNormalTurnBaseDefendersCurrent(GameState state, SourceWorldContext current)
    {
        var outer = current.CreateNearbyTemporary();
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        try
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ClosingMissing("defenders_player");
            foreach (var target in ClosingBases(current))
            {
                if (!ClosingNeed(current.ReadForceNativeValidity(player), "defenders_player_validity")) continue;
                int owner = ClosingNeed(current.ReadBaseForceSlot(target), "defenders_Base_Force");
                var force = current.ReadForceTableReference(owner <= 51 ? owner : 0) ?? throw ClosingMissing("defenders_Base_Force_pointer");
                if (force != player) continue;
                current.ClearTemporaryHexList(outer);
                var home = current.ReadArmyReturnBasePerson88Descriptor(target) ?? throw ClosingMissing("defenders_Base88");
                var node = current.ReadRegistryHead(home) ?? throw ClosingMissing("defenders_Base88_head");
                while (node.Identity.HasValue)
                {
                    var person = current.ReadRegistryNodePayload(node) ?? throw ClosingMissing("defenders_Base88_payload");
                    if (person.Kind != RegistryKind.Person) throw ClosingMissing("defenders_Base88_kind");
                    current.AppendConstructionPersonTemporary(outer, ClosingNeed(person.PhysicalSlot, "defenders_Base88_person"));
                    node = current.ReadRegistryNodeNext(node) ?? throw ClosingMissing("defenders_Base88_next");
                }
                if (ClosingNeed(current.ReadTemporaryHexListCount(outer), "defenders_home_count") == 0) continue;
                var candidates = current.CreateNearbyTemporary();
                try
                {
                    ushort location = ClosingNeed(current.ReadBaseObjectLink(target), "defenders_Base4E");
                    foreach (int person in ClosingTemporaryPeople(current, outer))
                    {
                        if (!ClosingNeed(current.ReadPersonNativeValidity(person), "defender_validity") ||
                            ClosingNeed(current.ReadPersonStatus(person), "defender_status") is not (>= 1 and <= 4)) continue;
                        if (current.ReadPersonForce(person) != player) continue;
                        int at = ClosingNeed(current.ReadPersonSecondLocation(person), "defender_E4");
                        int currentBase = at < 62 ? at : 0;
                        if (currentBase == location) current.AppendConstructionPersonTemporary(candidates, person);
                    }
                    var nodes = new List<(HexListNodeReference Node, int Person)>();
                    var candidate = current.ReadHexListHead(candidates) ?? throw ClosingMissing("defender_sort_head");
                    while (candidate.Identity.HasValue)
                    {
                        var payload = current.ReadRegistryNodePayload(new(candidate.Identity)) ?? throw ClosingMissing("defender_sort_payload");
                        nodes.Add((candidate, ClosingNeed(payload.PhysicalSlot, "defender_sort_person")));
                        candidate = current.ReadHexListNodeNext(candidate) ?? throw ClosingMissing("defender_sort_next");
                    }
                    if (nodes.Count == 0) continue;
                    //427450 is the source's stable insertion traversal. Equal
                    //keys retain their source-node order; identity is last.
                    for (int at = 1; at < nodes.Count; at++)
                    {
                        var person = nodes[at]; int cursor = at;
                        while (cursor > 0 && Before(person.Person, nodes[cursor - 1].Person)) { nodes[cursor] = nodes[cursor - 1]; cursor--; }
                        nodes[cursor] = person;
                    }
                    current.ReorderCurrentList(candidates, nodes.Select(value => value.Node).ToArray());
                    var order = nodes.Select(value => value.Person).ToArray();
                    int first = order[0];
                    var text = FormatNormalTurnReport(working, current, order.Length == 1 ? 0x53A1 : 0x53A2,
                        new(Person0: 5012, Person1: ClosingNeed(current.ReadPersonIdentity(first), "defender_first_ID"),
                            Base0: location, Integers: order.Length == 1 ? null : [order.Length]));
                    working = text.State;
                    int rawDistrict = ClosingNeed(current.ReadPersonDistrictSlot(first), "defender_first_DistrictE0");
                    var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                        ?? throw ClosingMissing("defender_first_District_pointer");
                    int day = FacilityOperation.Playable(working).ReadCampaignDay(working);
                    working = WriteHistory(working, current, new(2,
                        ClosingNeed(district.PhysicalSlot, "defender_history_District"), current.ReadBaseCurrentHexIndex(target), day,
                        text.Text, CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1), effects);
                }
                finally { current.ClearNearbyTemporary(candidates, true); }
            }
            return ClosingAppendEffects(working, effects);
        }
        finally { current.ClearNearbyTemporary(outer, true); }

        bool Before(int left, int right)
        {
            int l = ClosingNeed(current.ReadPersonStatus(left), "defender_sort_status");
            int r = ClosingNeed(current.ReadPersonStatus(right), "defender_sort_status");
            if ((l == 1) != (r == 1)) return l == 1;
            bool la = Advisor(left), ra = Advisor(right);
            if (la != ra) return la;
            if ((l == 2) != (r == 2)) return l == 2;
            var lf = _governorInputs.ReadCurrentComparatorFields(working, left) ?? throw ClosingMissing("defender_sort_Office_left");
            var rf = _governorInputs.ReadCurrentComparatorFields(working, right) ?? throw ClosingMissing("defender_sort_Office_right");
            if (ClosingNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(working, ClosingNeed(lf.PersonOfficeE7, "defender_sort_OfficeE7")), "defender_sort_Post_validity") &&
                ClosingNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(working, ClosingNeed(rf.PersonOfficeE7, "defender_sort_OfficeE7")), "defender_sort_Post_validity"))
            {
                int lp = ClosingNeed(lf.OfficeOrder37, "defender_sort_Office37");
                int rp = ClosingNeed(rf.OfficeOrder37, "defender_sort_Office37");
                if (lp != rp) return lp < rp;
            }
            int ls = Sum(left), rs = Sum(right);
            if (ls != rs) return ls > rs;
            return ClosingNeed(current.ReadPersonIdentity(left), "defender_sort_ID") < ClosingNeed(current.ReadPersonIdentity(right), "defender_sort_ID");
        }
        bool Advisor(int person)
        {
            var force = current.ReadPersonForce(person) ?? throw ClosingMissing("defender_sort_Force");
            if (!ClosingNeed(current.ReadForceNativeValidity(force), "defender_sort_Force_validity")) return false;
            int ordinal = ClosingNeed(current.ReadForcePhysicalSlot(force), "defender_sort_Force_ordinal");
            int raw = ClosingNeed(current.ReadAssignmentForceAdvisor52(ordinal), "defender_sort_advisor52");
            var advisor = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw ClosingMissing("defender_sort_advisor_pointer");
            int physical = ClosingNeed(advisor.PhysicalSlot, "defender_sort_advisor_physical");
            return ClosingNeed(current.ReadPersonNativeValidity(physical), "defender_sort_advisor_validity") &&
                current.ReadPersonIdentity(physical) == current.ReadPersonIdentity(person);
        }
        int Sum(int person)
        {
            int id = ClosingNeed(current.ReadPersonIdentity(person), "defender_sort_ability_ID");
            var value = abilities.Resolve(working, id) ?? throw ClosingMissing("defender_sort_current_abilities");
            return unchecked(value.Leadership + value.Strength + value.Intelligence + value.Politics + value.Charm);
        }
    }

    private GameState RefreshNormalTurnFoodWarningsCurrent(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ClosingMissing("food_warning_player");
        if (!ClosingNeed(current.ReadForceNativeValidity(player), "food_warning_player_validity")) return state;
        int ordinal = ClosingNeed(current.ReadForcePhysicalSlot(player), "food_warning_player_ordinal");
        var warnings = ImmutableArray.CreateBuilder<int>();
        string textAtSource = string.Empty;
        var working = state;
        if (ordinal is >= 1 and <= 51)
        foreach (var target in ClosingBases(current))
        {
            if (ClosingNeed(current.ReadBaseForceSlot(target), "food_warning_Base_Force") != ordinal) continue;
            int location = ClosingNeed(current.ReadBaseObjectLink(target), "food_warning_Base4E");
            if (location >= 52) continue;
            //22A940 uses the base's actual linked City receiver.
            var city = current.ReadCityTableReference(location <= 51 ? location : 0) ?? throw ClosingMissing("food_warning_City_pointer");
            if (!ClosingNeed(current.ReadBaseNativeValidity(city), "food_warning_City_validity")) continue;
            var expense = CityCaptureFoodWarningSource.TryReadFoodExpense(working, current, city);
            int cost = expense.Value ?? throw ClosingMissing(expense.UnresolvedOwner ?? "food_warning_expense");
            int food = ClosingNeed(current.ReadBaseResource(city, BaseResourceKind.Supplies), "food_warning_City_food");
            bool warning = false;
            if (unchecked(food - unchecked(cost * 3)) < 1)
            {
                var quote = CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, city,
                    abilities: CreateArmyDestructionAbilities(current), sceneries: _landmarkDefinitions);
                int income = quote.Value ?? throw ClosingMissing(quote.UnresolvedOwner ?? "food_warning_income");
                warning = unchecked(food + income - unchecked(cost * 3)) < 1;
            }
            if (warning)
            {
                var text = FormatNormalTurnReport(working, current, 0x5EA7, new());
                working = text.State; textAtSource = text.Text;
                warnings.Add(location);
            }
            int cityId = ClosingNeed(city.PhysicalSlot, "food_warning_City_physical");
            var owner = working.NativeBaseCaptureCurrent ?? throw ClosingMissing("food_warning_C9_owner");
            var fields = owner.Cities.GetValueOrDefault(cityId) ?? throw ClosingMissing("food_warning_C9_City");
            byte before = ClosingNeed(fields.C9, "food_warning_C9");
            byte after = warning ? (byte)(before | 2) : (byte)(before & 0xFD);
            current.SetCaptureCityByteC9(cityId, after);
            working = working with { NativeBaseCaptureCurrent = owner with { Cities = new(owner.Cities) { [cityId] = fields with { C9 = after } } } };
        }
        //The map consumer clears the prior warning layer, then applies the
        //actual source base list. Pending is retained until that real refresh.
        return ClosingAppendEffects(working, [new(null, null) { NormalTurnFoodWarningPresentation =
            new(warnings.ToImmutable(), textAtSource) }]);
    }

    private static IEnumerable<int> ClosingTemporaryPeople(SourceWorldContext current, HexListHandle handle)
    {
        var node = current.ReadRegistryHead(new(handle.Identity)) ?? throw ClosingMissing("temporary_Person_head");
        while (node.Identity.HasValue)
        {
            var person = current.ReadRegistryNodePayload(node) ?? throw ClosingMissing("temporary_Person_payload");
            if (person.Kind != RegistryKind.Person) throw ClosingMissing("temporary_Person_kind");
            yield return ClosingNeed(person.PhysicalSlot, "temporary_Person_pointer");
            node = current.ReadRegistryNodeNext(node) ?? throw ClosingMissing("temporary_Person_next");
        }
    }
    private static GameState ClosingAppendEffects(GameState state, List<CampaignEffectRequest> effects) => effects.Count == 0 ? state : state with
    {
        CampaignEffectRequests = (state.CampaignEffectRequests ?? throw ClosingMissing("effect_owner"))
            .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw ClosingMissing("effect_revision"), effects)
    };
}
