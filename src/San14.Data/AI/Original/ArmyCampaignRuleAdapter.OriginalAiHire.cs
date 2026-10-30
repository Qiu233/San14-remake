using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal readonly record struct OriginalAiHireTarget(int ForceId, int DistrictId, int DestinationBaseId);

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelHireExecutionRules
{
    internal IReadOnlyList<OriginalAiHireOffer> ReadOriginalAiHireOffersCurrent(GameState state, int force)
    {
        var owner = state.PersonnelHire ?? throw AiHireMissing("manager_owner");
        var roster = owner.Forces.GetValueOrDefault(force) ?? throw AiHireMissing("Force250_offers");
        var result = new List<OriginalAiHireOffer>();
        //6D9490 copies the actual Force250 order and applies only native validity.
        foreach (int template in roster.AvailableTemplateOfficerIds)
            if (OriginalAiHireOfferValid(state, template)) result.Add(new(template));
        return result;
    }

    internal OriginalAiHireTarget? ReadOriginalAiHireTargetCurrent(GameState state, SourceWorldContext current,
        int baseKey, int count)
    {
        var target = BaseLookup.Find(baseKey, current);
        if (!AiHireNeed(current.ReadBaseNativeValidity(target), "destination_validity")) return null;
        var district = current.ReadBaseDistrictReference(target) ?? throw AiHireMissing("destination_District_pointer");
        if (!AiHireNeed(current.ReadDistrictNativeValidity(district), "destination_District_validity")) return null;
        int districtId = AiHireNeed(district.PhysicalSlot, "destination_District_identity");
        int books = AiHireNeed(current.ReadDistrictRawByte(districtId, 0x14), "District14");
        if (unchecked(books - HireParameter(state, "command-book-cost")) < 0) return null;
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(target, current);
        if (unchecked(gold - count * HireParameter(state, "gold-per-person")) < 0) return null;
        int forceKey = AiHireNeed(current.ReadDistrictForceSlot(district), "District10_Force");
        var force = current.ReadForceTableReference(forceKey <= 51 ? forceKey : 0) ?? throw AiHireMissing("destination_Force_pointer");
        int forceId = AiHireNeed(force.PhysicalSlot, "destination_Force_identity");
        int location = AiHireNeed(current.ReadBaseObjectLink(target), "destination_Base4E");
        if ((uint)location > 61) throw AiHireMissing("destination_Base4E_format");
        return new(forceId, districtId, location);
    }

    CommandResult IPersonnelHireExecutionRules.ExecutePersonnelHire(GameState state, ForceState force,
        ExecutePersonnelHireOrder order, CommandContext context)
    {
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var selected = order.TemplateIds.Select(id => new OriginalAiHireOffer(id)).ToArray();
            if (ReadOriginalAiHireTargetCurrent(state, current, order.DestinationBaseId, selected.Length) is not { } binding)
                return CommandResult.Reject("The hire destination cannot pay this execution.", "personnel.hire.execution_gate");
            if (binding.ForceId != force.Id || binding.DistrictId != order.DistrictId || binding.DestinationBaseId != order.DestinationBaseId)
                return CommandResult.Reject("The hire destination is outside the authorized district.", "personnel.hire.execution_scope");
            var destination = BaseLookup.Find(order.DestinationBaseId, current);
            //Both final payments were computed before the first creator call.
            int requestedGold = unchecked(BaseCurrencyAdditionSource.ReadCurrentGold(destination, current) -
                selected.Length * HireParameter(state, "gold-per-person"));
            int bookCost = HireParameter(state, "command-book-cost");
            int created = 0;
            var working = state;
            foreach (var offer in selected)
            {
                if (!OriginalAiHireOfferValid(working, offer.TemplateId)) return FailedPrefix("personnel.hire.inactive_template");
                int before = (working.PersonnelHire ?? throw AiHireMissing("creator_manager")).RemainingSlots;
                var next = CreatePersonnelHireCurrent(working, current, world.Pool, force.Id, offer.TemplateId, destination);
                //32F720 decrements this actual manager counter exactly once
                //after successful creation. Its known no-allocation guards do
                //not decrement it. This is not a whole-state equality test.
                int after = (next.PersonnelHire ?? throw AiHireMissing("creator_manager_after")).RemainingSlots;
                if (after == before) return FailedPrefix("personnel.hire.creation_unavailable");
                working = next;
                created++;
            }
            working = SpendStrategicCommandBooksCurrent(working, current, order.DistrictId, bookCost);
            var currency = BaseCurrencyAdditionSource.CurrencyCity(destination, current);
            int cap = BasePostLoad.ReadResourceCapacities(currency, current).Gold;
            int gold = requestedGold < 0 ? 0 : Math.Min(requestedGold, cap);
            int cityId = AiHireNeed(currency.PhysicalSlot, "treasury_City_pointer");
            var city = currency.Kind == BaseKind.City ? working.Cities.GetValueOrDefault(cityId) : null;
            if (city is null) throw AiHireMissing("treasury_City_owner");
            current.SetBaseResource(currency, BaseResourceKind.Gold, gold);
            working = working with { Cities = new(working.Cities) { [cityId] = city with { Gold = gold } } };
            return CommandResult.Accept(Publish(working)) with { PersonnelHireExecution = new(true) };

            CommandResult FailedPrefix(string reason) => created == 0
                ? CommandResult.Reject("The selected hire could not be created.", reason)
                : CommandResult.Accept(Publish(working)) with { PersonnelHireExecution = new(false) };
            GameState Publish(GameState value)
            {
                value = OwnedArmyReturnCurrentFields.PublishCurrent(value, current);
                value = OwnedArmyLifecycleDetachedInputs.PublishCurrent(value, current);
                return value.WithArmyLifecycle((value.ReadArmyLifecycle() ?? throw AiHireMissing("publication_lifecycle")) with
                    { Indexes = world.Pool.ExportOwnedState() });
            }
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    internal int? ReadOriginalAiWorldPersonAbilitySum(GameState state, SourceWorldContext current, int officer)
    {
        int person = (officer is >= 0 and <= 6000 ? current.ReadPersonLookupReference(officer) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw AiHireMissing("Person_pointer");
        if (!AiHireNeed(current.ReadPersonNativeValidity(person), "Person_validity")) return null;
        int identity = AiHireNeed(current.ReadPersonIdentity(person), "Person10");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int result = 0;
        for (int dimension = 0; dimension < 5; dimension++)
            result = unchecked(result + AiHireNeed(abilities.ResolveDimension(state, identity, dimension,
                (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)), "Person_effective_ability"));
        return result;
    }

    internal bool ReadOriginalAiHireOfferValidity(GameState state, int templateId) => OriginalAiHireOfferValid(state, templateId);
    internal int? ReadOriginalAiHireOfferAbilitySum(GameState state, SourceWorldContext current, int templateId) =>
        ReadOriginalAiHireOfferAbilities(state, current, templateId, null);
    internal int? ReadOriginalAiHireOfferAbility(GameState state, SourceWorldContext current, int templateId, int dimension)
    {
        if ((uint)dimension >= 5) throw new ArgumentOutOfRangeException(nameof(dimension));
        return ReadOriginalAiHireOfferAbilities(state, current, templateId, dimension);
    }

    private int? ReadOriginalAiHireOfferAbilities(GameState state, SourceWorldContext current, int templateId, int? selectedDimension)
    {
        if (!OriginalAiHireOfferValid(state, templateId)) return null;
        var person = HireOffer(state, templateId).Person;
        //Offer construction/reset owns post0, doctrine0 and special4000
        //behavior. Items are different:0921B0 walks the actual World110 list
        //by numeric Person10, even for an independent template receiver.
        if (OfficerBehaviorRules.UsesOrdinaryPersonRules(person.Behavior) != false ||
            OfficerAppointmentRules.ReadAbilityOfficeId(person.Appointment) != 0 ||
            OfficerDoctrineRules.ReadDoctrineId(person.DoctrineId, person.AbilityState) != 0)
            throw AiHireMissing("offer_effective_ability_context");
        var basic = person.BaseAbilities ?? throw AiHireMissing("offer_base_abilities");
        var limits = person.AbilityState ?? throw AiHireMissing("offer_ability_caps");
        int health = (person.Health is { } condition ? OfficerHealthRules.AbilityModifier(condition) :
            throw AiHireMissing("offer_health")) switch
        {
            HealthAbilityModifier.Healthy => Parameter(SimulationParameterKeys.Tactics.HealthyAbilityPercent),
            HealthAbilityModifier.InjuryLevel1 => Parameter(SimulationParameterKeys.Tactics.InjuryLevel1AbilityPercent),
            HealthAbilityModifier.InjuryLevel2 => Parameter(SimulationParameterKeys.Tactics.InjuryLevel2AbilityPercent),
            HealthAbilityModifier.InjuryLevel3 => Parameter(SimulationParameterKeys.Tactics.InjuryLevel3AbilityPercent),
            _ => 0
        };
        int secondary = Parameter(SimulationParameterKeys.Tactics.AbilitySecondaryBaseline);
        var items = ReadOriginalAiHireOfferItems(state, current, person.Id, selectedDimension);
        int[] values = [basic.Leadership, basic.Strength, basic.Intelligence, basic.Politics, basic.Charm];
        int sum = 0;
        for (int dimension = 0; dimension < 5; dimension++)
        {
            if (selectedDimension is int selected && dimension != selected) continue;
            if ((uint)values[dimension] > 255) throw AiHireMissing("offer_base_ability_range");
            int cap = AiHireNeed(dimension == 4 ? limits.CharmLimit : limits.OrdinaryLimit, "offer_ability_cap");
            sum = unchecked(sum + EffectiveOfficerAbilityResolver.Calculate(values[dimension], health, secondary, 0, 0,
                items.Bonuses[dimension], cap, dimension == 4 ? items.ForcedCharm : null));
        }
        return sum;
        int Parameter(string key)
        {
            if (state.NativeArmySimulation is { } simulation && simulation.Parameters.Int32Values.TryGetValue(key, out int value)) return value;
            throw AiHireMissing(key);
        }
    }

    private static (int[] Bonuses, int? ForcedCharm) ReadOriginalAiHireOfferItems(GameState state,
        SourceWorldContext current, int identity, int? selectedDimension)
    {
        int[] bonuses = new int[5];
        int? charm = null;
        if (identity is < 1 or > 6000) return (bonuses, charm); //Actual iterator's identity gate.
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw AiHireMissing("Item110_registry"))
        {
            if (entry is not { Kind: RegistryKind.Item, PhysicalSlot: int physical }) throw AiHireMissing("Item110_pointer");
            if (AiHireNeed(current.ReadCaptureItemOwner4A(physical), "Item4A_holder") != identity) continue;
            //The iterator adds no IsPresent/native-validity gate. Its actual
            //registered membership and current4A word own participation.
            var item = state.Items?.GetValueOrDefault(physical) ?? throw AiHireMissing("Item_current");
            var definition = state.ItemDefinitions?.GetValueOrDefault(item.DefinitionId) ?? throw AiHireMissing("Item_definition");
            var values = definition.AbilityBonus ?? throw AiHireMissing("Item_ability_bonus");
            int[] additions = [values.Leadership, values.Strength, values.Intelligence, values.Politics, values.Charm];
            for (int dimension = 0; dimension < 5; dimension++)
            {
                if (selectedDimension is int selected && selected != dimension) continue;
                if (additions[dimension] < 0) throw AiHireMissing("Item_ability_range");
                bonuses[dimension] = Math.Max(bonuses[dimension], Math.Min(additions[dimension], 10));
            }
            if (selectedDimension is null or 4 && physical == 123) charm = 100;
        }
        return (bonuses, charm);
    }

    private static PersonnelHireTemplate HireOffer(GameState state, int id) =>
        state.PersonnelHire?.Templates?.GetValueOrDefault(id) ?? throw AiHireMissing("template_catalogue_entry");
    private static bool OriginalAiHireOfferValid(GameState state, int id)
    {
        var person = HireOffer(state, id).Person;
        if ((uint)person.Id > 65535) throw AiHireMissing("template_identity_format");
        return AiHireNeed(NativeValidity.PersonWithBehavior(id, (ushort)person.Id, person.Behavior,
            OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus)), "template_validity");
    }
    private static T AiHireNeed<T>(T? value, string field) where T : struct => value ?? throw AiHireMissing(field);
    private static InvalidDataException AiHireMissing(string field) => new("original_ai.hire." + field);
}
