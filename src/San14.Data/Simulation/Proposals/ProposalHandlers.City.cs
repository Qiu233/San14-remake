using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    // Every handler object uses the same five positions: cost, auxiliary,
    // target, and two handler-specific values. These are the actual object
    // configuration slots, rather than a reinterpretation of the UI sentence.
    internal bool TryGenerateCity(ProposalKind kind, int proposerPersonId, int cost,
        out ProposalArguments arguments)
    {
        arguments = default;
        int person = PersonPhysical(proposerPersonId);
        if (kind is ProposalKind.DevelopmentUp or ProposalKind.SecurityUp or ProposalKind.Investment)
        {
            int area = CityAssignedArea(person);
            var owner = CityArea(area);
            if (kind == ProposalKind.DevelopmentUp)
            {
                byte focus = K(Current.ReadAssignmentArea72(area), "development_focus");
                if (K(Current.ReadAreaNativeValidity(new(area)), "development_area_validity") && focus == 0) return false;
                if (CityProgress(area, focus, false) > 1000 - Rule("development-offer-room", 10)) return false;
            }
            if (kind == ProposalKind.SecurityUp && K(owner.Security, "security") > 100 - Rule("security-offer-room", 2)) return false;
            if (kind == ProposalKind.Investment && K(Current.ReadAssignmentArea6D(area), "investment_level") > 2) return false;
            arguments = new(Math.Max(0, cost), 0, area, CityAreaForce(area), 0);
            return true;
        }
        if (kind == ProposalKind.Conscription)
        {
            if (CityDuty(person) != 2) return false;
            var city = CityPersonCity(person);
            int troops = CityResource(city, BaseResourceKind.Troops);
            if (troops > BasePostLoad.ReadTroopCapacity(city, Current) - Rule("conscription-offer-room", 100)) return false;
            int id = K(city.PhysicalSlot, "conscription_city");
            if (!CityAreas().Any(area => CityConscriptionArea(area, id, proposerPersonId))) return false;
            arguments = new(Math.Max(0, cost), 0, K(Current.ReadBaseObjectLink(city), "conscription_city_link"), 0, 0);
            return true;
        }
        if (kind == ProposalKind.SpecialTraining)
        {
            if (CityDuty(person) != 3) return false;
            var home = CityPersonCity(person);
            var force = Current.ReadPersonForce(person) ?? throw Missing("training_person_force");
            int forceId = K(force.PhysicalSlot, "training_force");
            var candidates = CityBases().Where(target => K(Current.ReadBaseNativeValidity(target), "training_base_validity") &&
                K(Current.ReadBaseForceSlot(target), "training_base_force") == forceId &&
                !CityDuplicate(kind, K(Current.ReadBaseObjectLink(target), "training_base_link")) &&
                CitySameBase(CityAssociatedCity(target), home) &&
                CityResource(target, BaseResourceKind.Troops) >= Rule("training-minimum-troops", 500) &&
                CityMorale(target) <= CityMoraleCap(target) - Rule("training-offer-room", 3)).ToList();
            //41B0D0: city receivers precede gates, then more soldiers, then id.
            candidates = candidates.OrderBy(target => K(Current.ReadBaseObjectLink(target), "training_sort_link") >= 52)
                .ThenByDescending(target => CityResource(target, BaseResourceKind.Troops))
                .ThenBy(target => K(Current.ReadBaseObjectLink(target), "training_sort_link")).ToList();
            if (candidates.Count == 0) return false;
            arguments = new(Math.Max(0, cost), 0, K(Current.ReadBaseObjectLink(candidates[0]), "training_target"), 0, 0);
            return true;
        }
        return TryGenerateCityOther(kind, proposerPersonId, cost, out arguments);
    }

    internal bool IsCityValid(ProposalEntry entry, bool completed)
    {
        if (completed) return entry.Kind != ProposalKind.SecurityDown ||
            CitySecurityDownTargets(PersonPhysical(entry.ProposerPersonId)).Contains(entry.Arguments.Parameter2);
        int person = PersonPhysical(entry.ProposerPersonId), target = entry.Arguments.Parameter2;
        if (entry.Kind is ProposalKind.DevelopmentUp or ProposalKind.SecurityUp or ProposalKind.Investment or
            ProposalKind.Conscription or ProposalKind.SpecialTraining)
        {
            int requiredDuty = entry.Kind == ProposalKind.Conscription ? 2 : entry.Kind == ProposalKind.SpecialTraining ? 3 : 1;
            if (CityDuty(person) != requiredDuty) return false;
        }
        switch (entry.Kind)
        {
            case ProposalKind.DevelopmentUp:
                if (CityAssignedArea(person) != target) return false;
                byte focus = K(Current.ReadAssignmentArea72(target), "development_focus");
                return focus != 0 && (focus > 3 || CityProgress(target, focus, false) < 1000);
            case ProposalKind.SecurityUp:
                return CityAssignedArea(person) == target && K(CityArea(target).Security, "security") < 100;
            case ProposalKind.Investment:
                return true; //4112B0 checks duty only; its source does not re-test the saved target.
            case ProposalKind.Conscription:
            {
                var city = CityAssociatedCity(CityBase(target));
                return CitySameBase(CityPersonCity(person), city) && CityResource(city, BaseResourceKind.Troops) < BasePostLoad.ReadTroopCapacity(city, Current);
            }
            case ProposalKind.SpecialTraining:
            {
                var receiver = CityBase(target);
                return CitySameBase(CityPersonCity(person), CityAssociatedCity(receiver)) &&
                    CityMorale(receiver) < CityMoraleCap(receiver) && CityResource(receiver, BaseResourceKind.Troops) > 0;
            }
            case ProposalKind.Exploit:
            case ProposalKind.Requisition:
            {
                var city = CityReference(target);
                var resource = entry.Kind == ProposalKind.Exploit ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
                return CityOwnedByPerson(city, person) && CityResource(city, resource) < CityCapacity(city, resource);
            }
            case ProposalKind.RepairEndurance:
            {
                var receiver = CityBase(target);
                return CityOwnedByPerson(receiver, person) && BasePostLoad.ReadLinkedField14(receiver, Current) < BasePostLoad.ReadCapacities(receiver, Current).Field46;
            }
            case ProposalKind.SecurityDown:
                return K(CityArea(target).Security, "security_down_security") != 0 && CitySecurityDownTargets(person).Contains(target);
            case ProposalKind.BuyFood:
            case ProposalKind.SellFood:
            {
                var city = CityReference(target);
                if (!CityOwnedByPerson(city, person)) return false;
                int food = entry.Arguments.Parameter3, gold = entry.Arguments.Parameter4;
                int currentFood = CityResource(city, BaseResourceKind.Supplies), currentGold = CityResource(city, BaseResourceKind.Gold);
                return entry.Kind == ProposalKind.BuyFood
                    ? gold <= currentGold && unchecked(food + currentFood) <= CityCapacity(city, BaseResourceKind.Supplies)
                    : food <= currentFood && unchecked(gold + currentGold) <= CityCapacity(city, BaseResourceKind.Gold);
            }
            default: throw Missing("city_handler_kind");
        }
    }

    internal void ApplyCity(ProposalEntry entry)
    {
        switch (entry.Kind)
        {
            case ProposalKind.DevelopmentUp: CityApplyDevelopment(entry); break;
            case ProposalKind.SecurityUp: CityApplySecurity(entry); break;
            case ProposalKind.Investment: CityApplyInvestment(entry); break;
            case ProposalKind.Conscription: CityApplyConscription(entry); break;
            case ProposalKind.SpecialTraining: CityApplyTraining(entry); break;
            default: ApplyCityOther(entry); break;
        }
    }

    private AreaState CityArea(int area) => State.Areas.GetValueOrDefault(area) ?? throw Missing("city_area_owner");
    private int CityAreaForce(int area) => K(Current.ReadAreaCurrentForceSlot(new(area)), "city_area_force");
    private int CityProgress(int area, int focus, bool noneIsComplete) => focus switch
    {
        0 => noneIsComplete ? 1000 : 0,
        1 => K(CityArea(area).Development?.Commerce, "city_commerce"),
        2 => K(CityArea(area).Development?.Agriculture, "city_agriculture"),
        3 => K(CityArea(area).Development?.Barracks, "city_barracks"), _ => 0
    };
    private BaseReference CityBase(int location) => BaseLookup.Find(location, Current);
    private BaseReference CityReference(int physical) => Current.ReadCityTableReference(physical < 52 ? physical : 0) ?? throw Missing("city_pointer");
    private BaseReference CityAssociatedCity(BaseReference target) => CurrentGateMapQueries.FindCity(target, Current);
    private BaseReference CityPersonCity(int person) => CityAssociatedCity(CityBase(K(Current.ReadPersonFirstLocation(person), "city_person_home")));
    private static bool CitySameBase(BaseReference one, BaseReference two) => one.Kind == two.Kind && one.PhysicalSlot == two.PhysicalSlot;
    private int CityResource(BaseReference receiver, BaseResourceKind resource) => K(Current.ReadBaseResource(receiver, resource), "city_resource." + resource);
    private int CityMorale(BaseReference receiver) => K(Current.ReadBaseMorale44(receiver), "city_morale");
    private int CityMoraleCap(BaseReference receiver)
    {
        int force = K(Current.ReadBaseForceSlot(receiver), "city_morale_force");
        return BasePostLoad.ReadMoraleUpper(Current.ReadForceTableReference(force < 52 ? force : 0) ?? throw Missing("city_morale_force_pointer"), Current);
    }
    private int CityCapacity(BaseReference receiver, BaseResourceKind resource) => resource switch
    {
        BaseResourceKind.Gold => BasePostLoad.ReadGoldCapacity(receiver, Current),
        BaseResourceKind.Supplies => BasePostLoad.ReadSuppliesCapacity(receiver, Current),
        _ => BasePostLoad.ReadTroopCapacity(receiver, Current)
    };
    private bool CityOwnedByPerson(BaseReference receiver, int person) => K(Current.ReadBaseForceSlot(receiver), "city_target_force") ==
        K((Current.ReadPersonForce(person) ?? throw Missing("city_person_force")).PhysicalSlot, "city_person_force_identity");
    private IEnumerable<int> CityAreas() => Enumerable.Range(0, 501).Where(id => K(Current.ReadAreaNativeValidity(new(id)), "city_area_validity"));
    private IEnumerable<BaseReference> CityBases() => (Current.ReadRegisteredBases() ?? throw Missing("city_base_registry"))
        .Select(item => item ?? throw Missing("city_registered_base_pointer"));
    private bool CityDuplicate(ProposalKind kind, int target) => (State.Proposals ?? throw Missing("city_proposal_owner")).Slots
        .Any(item => item.Kind == kind && item.Arguments.Parameter2 == target && item.Status != ProposalStatus.Empty);

    private int CityDuty(int person)
    {
        if (!K(Current.ReadPersonNativeValidity(person), "duty_person_validity")) return 0;
        var home = CityBase(K(Current.ReadPersonFirstLocation(person), "duty_person_home"));
        if (K(Current.ReadBaseNativeValidity(home), "duty_home_validity"))
        {
            var city = CityAssociatedCity(home);
            if (K(Current.ReadBaseNativeValidity(city), "duty_city_validity"))
            {
                int id = K(city.PhysicalSlot, "duty_city_identity"), personId = PersonIdentity(person);
                if (K(Current.ReadAssignmentCityCC(id), "duty_trainer") == personId) return 3;
                if (K(Current.ReadAssignmentCityCA(id), "duty_recruiter") == personId) return 2;
            }
        }
        return CityAssignedAreas(person).Any() ? 1 : 0;
    }
    private IEnumerable<int> CityAssignedAreas(int person)
    {
        var vector = Current.ReadAssignmentPersonAreaVector(person) ?? throw Missing("city_person_area_vector");
        if (vector.ActiveCount1E8 > vector.Values.Length) throw Missing("city_person_area_count");
        for (int i = 0; i < vector.ActiveCount1E8; i++)
        {
            int raw = vector.Values[i];
            var area = Current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("city_person_area_pointer");
            if (K(Current.ReadAreaNativeValidity(area), "city_person_area_validity")) yield return K(area.PhysicalSlot, "city_person_area_identity");
        }
    }
    private int CityAssignedArea(int person)
    {
        var candidates = CityAssignedAreas(person).ToList();
        //2A6C40 / 253120: the two security classes precede development and id.
        candidates.Sort((one, two) =>
        {
            int a = K(CityArea(one).Security, "assigned_security"), b = K(CityArea(two).Security, "assigned_security");
            int threshold = Rule("assigned-area-security-priority", 60);
            if ((a < threshold) != (b < threshold)) return a.CompareTo(b);
            int first = CityProgress(one, K(Current.ReadAssignmentArea72(one), "assigned_focus"), true);
            int second = CityProgress(two, K(Current.ReadAssignmentArea72(two), "assigned_focus"), true);
            return first != second ? first.CompareTo(second) : one.CompareTo(two);
        });
        return candidates.Count != 0 ? candidates[0] : throw Missing("city_person_assigned_area");
    }
    private int CityQuote(AreaAdministrationQuery<int?> query) => query.Available
        ? K(query.Value, "city_quote_value") : throw Missing(query.UnresolvedInput ?? "city_quote");
    private int CityTasks(int person) => K(Current.ReadAssignmentPersonAreaVector(person)?.ActiveCount1E8, "city_task_count");
    private bool CityConscriptionArea(int area, int city, int proposerId) => K(Current.ReadAreaCitySlot(new(area)), "conscription_area_city") == city &&
        K(CityArea(area).NativeNormalAftermath?.Quantity52, "conscription_area_population") >= Rule("conscription-offer-room", 100) + Rule("conscription-reserved-population", 5000) &&
        K(CityArea(area).Security, "conscription_area_security") >= Rule("conscription-minimum-security", 10) &&
        CityQuote(Preview.Soldiers(area, checked((ushort)proposerId), 1)) > Rule("conscription-minimum-income", 200);
}
