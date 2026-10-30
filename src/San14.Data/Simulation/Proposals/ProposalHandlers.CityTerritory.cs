using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    private bool CityGenerateTerritory(ProposalKind kind, int proposerId, int cost, out ProposalArguments arguments)
    {
        arguments = default;
        int person = PersonPhysical(proposerId), district = K(Current.ReadPersonDistrictSlot(person), "territory_person_district");
        if (kind == ProposalKind.SecurityDown)
        {
            if (PersonAbility(proposerId, 2) <= Rule("security-down-minimum-intelligence", 50)) return false;
            int homeHex = Current.ReadBaseCurrentHexIndex(CityBase(K(Current.ReadPersonFirstLocation(person), "security_down_home")));
            int rawArea = K(Current.ReadHexAreaSlot(new(homeHex)), "security_down_home_area");
            int homeCity = K(Current.ReadAreaCitySlot(new(rawArea < 501 ? rawArea : 0)), "security_down_home_city");
            int[] neighbors = CityNeighbors(homeCity);
            var candidates = CitySecurityDownTargets(person).Where(area => K(Current.ReadAreaNativeValidity(new(area)), "security_down_area_validity") &&
                CityAreaForce(area) != 0 && CityHostile(proposerId, CityAreaForce(area)) &&
                !CityDuplicate(kind, area) && neighbors.Contains(K(Current.ReadAreaCitySlot(new(area)), "security_down_candidate_city")) &&
                K(CityArea(area).Security, "security_down_candidate_security") >= Rule("security-down-offer-minimum", 60))
                .OrderByDescending(area => K(CityArea(area).Security, "security_down_sort_security"))
                .ThenByDescending(CityAreaIsCenter).ThenBy(area => area).ToList();
            if (candidates.Count == 0) return false;
            arguments = new(Math.Max(0, cost), 0, candidates[0], 0, 0);
            return true;
        }
        if (kind is not (ProposalKind.Exploit or ProposalKind.Requisition)) throw Missing("territory_handler_kind");
        bool gold = kind == ProposalKind.Exploit;
        int disposition = K(Current.ReadAllegianceDisposition(person), "levy_person_disposition");
        if (disposition >= Rule(gold ? "exploit-disposition-limit" : "requisition-disposition-limit", 0)) return false;
        int home = K(CityPersonCity(person).PhysicalSlot, "levy_home_city");
        var resource = gold ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
        var targets = new List<BaseReference>();
        foreach (var entry in Current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("levy_city_registry"))
        {
            int id = K(entry?.PhysicalSlot, "levy_city_pointer");
            var city = CityReference(id);
            if (!K(Current.ReadBaseNativeValidity(city), "levy_city_validity") || K(Current.ReadBaseDistrictSlot(city), "levy_city_district") != district ||
                CityDuplicate(kind, id) || id != home && !CityNeighbors(id).Contains(home)) continue;
            if (CityResource(city, resource) >= unchecked(CityCapacity(city, resource) * Rule(gold ? "exploit-stock-percent" : "requisition-stock-percent", 50)) / 100) continue;
            if (!IsProposalCityResourceNeeded(city, gold)) continue;
            if (CityAreas().Any(area => CityLevyArea(area, city))) targets.Add(city);
        }
        targets = targets.OrderBy(city => K(city.PhysicalSlot, "levy_sort_city") != home)
            .ThenBy(city => CityResource(city, resource)).ThenBy(city => K(city.PhysicalSlot, "levy_sort_city")).ToList();
        if (targets.Count == 0) return false;
        arguments = new(gold ? Math.Max(0, cost) : 0, 0, K(targets[0].PhysicalSlot, "levy_target"), 0, 0);
        return true;
    }

    private void CityApplyTerritory(ProposalEntry entry)
    {
        if (entry.Kind == ProposalKind.SecurityDown) { CityApplySecurityDown(entry); return; }
        if (entry.Kind is not (ProposalKind.Exploit or ProposalKind.Requisition)) throw Missing("territory_apply_kind");
        bool gold = entry.Kind == ProposalKind.Exploit;
        var city = CityReference(entry.Arguments.Parameter2);
        var resource = gold ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
        int count = PersonAbility(entry.ProposerPersonId, 1) / Rule(gold ? "exploit-area-divisor" : "requisition-area-divisor", 35) + Rule(gold ? "exploit-area-base" : "requisition-area-base", 2);
        int completed = 0, total = 0;
        while (completed < count)
        {
            bool changed = false;
            //The original repeatedly visits the physical Area table; it does
            //not sort this application list by income or population.
            for (int area = 0; area < 501 && completed < count; area++)
            {
                if (!K(Current.ReadAreaNativeValidity(new(area)), "levy_apply_area_validity") || !CityLevyArea(area, city)) continue;
                int amount = CityLevyQuote(area, entry.ProposerPersonId, gold);
                amount = Math.Min(amount, CityCapacity(city, resource) - CityResource(city, resource));
                if (amount <= 0) continue;
                CityWriteResource(city, resource, Math.Clamp(unchecked(CityResource(city, resource) + amount), 0, CityCapacity(city, resource)));
                int charm = PersonAbility(entry.ProposerPersonId, 4);
                int sample = Next((Rule(gold ? "exploit-charm-offset" : "requisition-charm-offset", 120) - charm) /
                    Rule(gold ? "exploit-charm-divisor" : "requisition-charm-divisor", 18));
                CityChangeSecurity(area, -unchecked(sample + Rule(gold ? "exploit-security-loss" : "requisition-security-loss", 30)));
                total = unchecked(total + amount); completed++; changed = true;
            }
            if (!changed) break;
        }
        ushort location = K(Current.ReadBaseObjectLink(city), "levy_report_city");
        EnterScene(10);
        Speech(entry, gold ? 0x4F4B : 0x4F54,
            new(Person0: entry.ProposerPersonId, Person1: CityReportRuler(), Base0: location, Integers: [total]));
        ExitScene();
        History(entry, gold ? 0x4F53 : 0x4F5C,
            new(Base0: location, Integers: [total, CityResource(city, resource), 0]), Current.ReadBaseCurrentHexIndex(city));
        if (!gold) State = Package.ApplyProposalCityFoodWarning(State, Current, city);
        Reward(entry.ProposerPersonId, entry.Kind, 1,
            total / Rule(gold ? "exploit-reward-divisor" : "requisition-reward-divisor", 1000) + Rule(gold ? "exploit-reward-base" : "requisition-reward-base", 20), true);
    }

    private bool CityLevyArea(int area, BaseReference city) => K(Current.ReadAreaCitySlot(new(area)), "levy_area_city") == city.PhysicalSlot &&
        CityAreaForce(area) == K(Current.ReadBaseForceSlot(city), "levy_area_force") &&
        K(CityArea(area).Security, "levy_area_security") > Rule("levy-minimum-security", 30);

    private int CityLevyQuote(int area, int proposerId, bool gold)
    {
        ushort old = K(Current.ReadAssignmentArea36(area), "levy_area_person");
        Current.SetAssignmentArea36(area, checked((ushort)proposerId));
        try
        {
            if (gold) return CityQuote(Preview.Gold(area, CityTasks));
            //The proposal passes the present month's byte to2BCD20, while
            //the page's Food query passes next month. Shift only the local
            //quote input; no campaign date is written.
            var season = State.NativeMapSeason ?? throw Missing("levy_food_date");
            int month = season.ThirdDate.Month - 1;
            if (month < 1) month += 12;
            var quoteState = State with { NativeMapSeason = season with { ThirdDate = season.ThirdDate with { Month = (sbyte)month } } };
            var preview = new AreaAdministrationPreviewSource(quoteState, Current, Scenario, Language, Panels, true);
            return CityQuote(preview.Food(area, CityTasks));
        }
        finally { Current.SetAssignmentArea36(area, old); }
    }

    private bool CityAreaIsCenter(int area)
    {
        int city = K(Current.ReadAreaCitySlot(new(area)), "center_city");
        var receiver = CityReference(city);
        if (!K(Current.ReadBaseNativeValidity(receiver), "center_city_validity")) return false;
        return Current.ReadBaseCurrentHexIndex(receiver) == K(Current.ReadAreaHexIndex(new(area)), "center_area_hex");
    }
    private bool CityHostile(int actorId, int otherId)
    {
        int actor = K((Current.ReadPersonForce(PersonPhysical(actorId)) ?? throw Missing("hostile_actor_force")).PhysicalSlot, "hostile_actor_identity");
        if (actor == otherId) return false;
        var native = State.NativeArmySimulation ?? throw Missing("hostile_native_owner");
        var one = native.Forces.GetValueOrDefault(actor) ?? throw Missing("hostile_actor_owner");
        var two = native.Forces.GetValueOrDefault(otherId) ?? throw Missing("hostile_other_owner");
        int selected = K(native.World.SelectedForce1656, "hostile_selected_force");
        byte a = K(one.Flags12, "hostile_actor_flags"), b = K(two.Flags12, "hostile_other_flags");
        if (selected == actor && (b & 2) != 0 || selected == otherId && (a & 2) != 0) return true;
        byte[] treaties = one.RelationsEA is { Length: 52 } values ? values : throw Missing("hostile_relations");
        if ((treaties[otherId] & 127) != 0 || (a & 2) != 0 && (b & 2) != 0) return false;
        return K(one.Flags194, "hostile_actor_parent") != otherId && K(two.Flags194, "hostile_other_parent") != actor;
    }

    private void CityApplySecurityDown(ProposalEntry entry)
    {
        int area = entry.Arguments.Parameter2, person = PersonPhysical(entry.ProposerPersonId);
        int result = Package.ResolveStrategicCommandTrialCurrent(State, Current, 14, person, area, null, 0, 0);
        //40C170 deliberately promotes the failed ordinary trial to an ordinary
        //success for this proposal's separate reduced-effect execution.
        if (result == 0) result = 1;
        int intelligence = PersonAbility(entry.ProposerPersonId, 2);
        int focus = Next(3) + 1;
        int beforeSecurity = K(CityArea(area).Security, "security_down_report_before_security");
        int beforeDevelopment = CityProgress(area, focus, false);
        var loss = CityIncitementLoss(person, intelligence, result);
        int scale = Rule("security-down-effect-scale", 400), divisor = Rule("security-down-effect-divisor", 300);
        int development = unchecked(loss.Development * scale) / divisor;
        int security = unchecked(loss.Security * scale) / divisor;
        int hex = K(Current.ReadAreaHexIndex(new(area)), "security_down_area_hex");
        int raw = K(Current.ReadHexObjectLink(new(hex < 48400 ? hex : 0)), "security_down_object");
        var target = CityBase(raw < 62 ? raw : 0);
        int wallLoss = 0, beforeWall = 0, afterWall = 0;
        if (K(Current.ReadBaseNativeValidity(target), "security_down_base_validity"))
        {
            int wall = BasePostLoad.ReadLinkedField14(target, Current);
            beforeWall = wall;
            int floor = Rule("security-down-wall-floor", 500);
            if (result != 1 && wall >= floor)
                wallLoss = Math.Min(unchecked(BasePostLoad.ReadCapacities(target, Current).Field46 * Rule("security-down-wall-percent", 5)) / 100, wall - floor);
            BasePostLoad.WriteLinkedField14(target, unchecked(wall - wallLoss), Current);
            CityPublishWall(target, K(Current.ReadBasePostLoadFlag66(target), "security_down_damage_tier"));
            afterWall = BasePostLoad.ReadLinkedField14(target, Current);
        }
        WriteAreaDevelopment(area, focus - 1, Math.Clamp(unchecked(CityProgress(area, focus, false) - development), 0, 1000));
        CityChangeSecurity(area, -security);
        int afterSecurity = K(CityArea(area).Security, "security_down_report_after_security");
        int afterDevelopment = CityProgress(area, focus, false);
        int actualSecurity = beforeSecurity - afterSecurity, actualDevelopment = beforeDevelopment - afterDevelopment,
            actualWall = beforeWall - afterWall;
        string fieldName = Package.ReadHistoryDataText(0, 0x2B2 + focus);
        int speechRoot = actualWall != 0 ? 0x4F74 : actualDevelopment != 0 ? 0x4F6D : 0x4F66;
        int historyRoot = actualWall != 0 ? 0x4F7D : actualDevelopment != 0 ? 0x4F7C : 0x4F7B;
        int[]? historyNumbers = actualWall != 0 ? null : actualDevelopment != 0
            ? [afterSecurity, afterDevelopment, 0] : [actualSecurity, afterSecurity, 0];
        EnterScene(0x20);
        Speech(entry, speechRoot, new(Person0: entry.ProposerPersonId, Person1: CityReportRuler(), Area0: area,
            String0: fieldName, Integers: [actualSecurity, actualDevelopment, actualWall]));
        ExitScene();
        int reportHex = K(Current.ReadBaseNativeValidity(target), "security_down_report_base_validity")
            ? Current.ReadBaseCurrentHexIndex(target) : hex;
        History(entry, historyRoot, new(Area0: area, String0: fieldName, Integers: historyNumbers), reportHex);
        Reward(entry.ProposerPersonId, entry.Kind, 2,
            unchecked(development + security + wallLoss) / Rule("security-down-reward-divisor", 5) + Rule("security-down-reward-base", 10), true);
    }

    private (int Development, int Security) CityIncitementLoss(int person, int intelligence, int result)
    {
        int trait = K(TacticRemakeCurrentTraits.PersonContribution(Current, person, 65), "security_down_trait65") > 0 ? 1 : 0;
        int force = K((Current.ReadPersonForce(person) ?? throw Missing("security_down_person_force")).PhysicalSlot, "security_down_person_force_identity");
        int player = K((OwnedCurrentPlayerForce.ReadCurrentReference(State, Current) ?? throw Missing("security_down_player")).PhysicalSlot, "security_down_player_identity");
        int bonus;
        if (force != player) bonus = RequiredRule("security-down-npc-random-base");
        else
        {
            int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(State), "security_down_settings");
            int difficulty = K(BattleRemakeCurrentSettings.ReadInt32(State, selector == 0 ? 0x134 : 0x28), "security_down_difficulty");
            bonus = difficulty is 0 or 1 ? 2 : 1;
        }
        int difference = unchecked(intelligence - 30);
        int development = unchecked(Next(unchecked(bonus + difference / (result == 2 ? 10 : 15))) + trait + 5);
        int security = unchecked(Next(unchecked(bonus + difference / (result == 2 ? 15 : 20))) + trait + 4);
        return (development, security);
    }
}
