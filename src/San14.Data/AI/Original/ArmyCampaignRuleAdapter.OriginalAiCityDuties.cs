using San14.Core;

namespace San14.Data;

internal readonly record struct OriginalAiCityDutyPreparation(bool Success, SetCityDutyOrder? Order);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Source 03DAB0/03DB50 and 03E2B0/03E350 admission. The formal
    /// shared command owns publication, command books and appointment cleanup.</summary>
    internal OriginalAiCityDutyPreparation CreateOriginalAiCityDutyCurrent(GameState state,
        SourceWorldContext current, int cityId, int? officerId, CityDutyKind kind)
    {
        var city = current.ReadCityTableReference(cityId is >= 0 and <= 51 ? cityId : 0)
            ?? throw OriginalAiCityDutyMissing("City_pointer");
        int physicalCity = OriginalAiCityDutyNeed(city.PhysicalSlot, "City_identity");
        int existing = OriginalAiCityDutyNeed(kind == CityDutyKind.Recruitment
            ? current.ReadAssignmentCityCA(physicalCity) : current.ReadAssignmentCityCC(physicalCity), "City_duty");
        int? person = officerId is int selected ? OriginalAiCityDutyNeed(
            (selected is >= 0 and <= 6000 ? current.ReadPersonLookupReference(selected) :
                current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "incoming_Person_pointer") : null;
        int desired = person is int incoming ? OriginalAiCityDutyNeed(current.ReadPersonIdentity(incoming), "incoming_Person10") : 0;
        if (existing == desired) return new(true, null);

        int cost = CityDomesticParameters.Read(state,
            kind == CityDutyKind.Recruitment ? "recruiterCommandCost" : "trainerCommandCost")
            ?? throw OriginalAiCityDutyMissing("command_cost_current");
        // RecruitSet alone first gates on the incoming PersonE0 district.
        // The shared formal command still charges the target city's district.
        DistrictReference budgetDistrict;
        if (kind == CityDutyKind.Recruitment && person is int recruiter)
        {
            int raw = OriginalAiCityDutyNeed(current.ReadPersonDistrictSlot(recruiter), "incoming_PersonE0");
            budgetDistrict = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                ?? throw OriginalAiCityDutyMissing("incoming_District_pointer");
        }
        else budgetDistrict = TargetDistrict();
        if (Books(budgetDistrict) < cost) return new(false, null);
        if (!OriginalAiCityDutyNeed(current.ReadBaseNativeValidity(city), "City_validity") ||
            person is int candidate && !OriginalAiCityDutyNeed(current.ReadPersonNativeValidity(candidate), "incoming_Person_validity"))
            return new(true, null);
        // A remove node ignores the native formal function's result. A set
        // node returns Failure when the target district cannot pay the command.
        if (officerId.HasValue && Books(TargetDistrict()) < cost) return new(false, null);
        int force = OriginalAiCityDutyNeed(current.ReadBaseForceSlot(city), "City_Force");
        if (!state.Cities.TryGetValue(physicalCity, out var owner) || owner.Id != physicalCity || owner.ForceId != force)
            throw OriginalAiCityDutyMissing("City_domain_binding");
        return new(true, new(force, physicalCity, kind, officerId));

        DistrictReference TargetDistrict() => current.ReadCityDistrictReference(city)
            ?? throw OriginalAiCityDutyMissing("City_District_pointer");
        int Books(DistrictReference district)
        {
            int physical = OriginalAiCityDutyNeed(district.PhysicalSlot, "budget_District_identity");
            int? value = state.Districts.GetValueOrDefault(physical)?.OrdersRemaining;
            return value is >= 0 and <= byte.MaxValue ? value.Value : throw OriginalAiCityDutyMissing("District14." + physical);
        }
    }

    internal int ReadOriginalAiCityDutyAbility(GameState state, SourceWorldContext current, int officerId, int dimension)
    {
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        return abilities.ResolveDimension(state, officerId, dimension,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
            ?? throw OriginalAiCityDutyMissing("effective_Person_ability." + dimension);
    }

    private static T OriginalAiCityDutyNeed<T>(T? value, string field) where T : struct => value ?? throw OriginalAiCityDutyMissing(field);
    private static InvalidDataException OriginalAiCityDutyMissing(string field) => new("original-ai.city-duty." + field);
}
