using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal static bool PersonnelHireServiceAllowedCurrent(GameState state)
    {
        int selector = HireNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        int setting = HireNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x50C : 0x130), "settings_hire_option");
        if (setting == 1) return false;
        return (state.CampaignEventWorld?.CurrentUserIndex ?? throw HireMissing("App_current_user")) == -1;
    }

    internal CommandResult ApplyPersonnelHire(GameState state, ForceState force, int districtId,
        int destinationCityId, IReadOnlyList<int> templateIds, CommandContext context)
    {
        if (templateIds.Count == 0 || templateIds.Distinct().Count() != templateIds.Count)
            return CommandResult.Reject("Select distinct hire offers.", "personnel.hire.selection");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var rules = ReadPersonnelHireRulesCurrent(state, current, districtId);
            if (rules.ForceId != force.Id || !rules.Cities.Any(city => city.CityId == destinationCityId))
                return CommandResult.Reject("The hiring district or destination is no longer controlled.", "personnel.hire.control");
            if (templateIds.Count > rules.MaximumSelection ||
                templateIds.Any(id => !rules.Candidates.Any(candidate => candidate.TemplateId == id)))
                return CommandResult.Reject("A selected offer or hire slot is no longer available.", "personnel.hire.qualification");
            if (rules.Books - CommandBookAvailability.Reserved(state, districtId) < rules.BookCost)
                return CommandResult.Reject("Insufficient command books.", "personnel.hire.command_books");
            var destination = BaseLookup.Find(checked((ushort)destinationCityId), current);
            var working = state;
            //6CEF10 preserves selection order and debits the selected count,
            //even when an individual32F720 returns without allocating a Person.
            foreach (int template in templateIds)
                working = CreatePersonnelHireCurrent(working, current, world.Pool, force.Id, template, destination);
            int books = working.Districts[districtId].OrdersRemaining ?? throw HireMissing("debit_District14");
            int afterBooks = Math.Clamp(unchecked(books - rules.BookCost), 0, 200);
            current.SetDistrictRawByte(districtId, 0x14, (byte)afterBooks);
            var saved = working.NativeForceUnregisterCurrent ?? throw HireMissing("District_saved_fields");
            var window = saved.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray() ?? throw HireMissing("District_saved_window");
            window[4] = (byte)afterBooks;
            working = working with
            {
                Districts = new(working.Districts) { [districtId] = working.Districts[districtId] with { OrdersRemaining = afterBooks } },
                NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10) { [districtId] = window } }
            };
            //The caller reads virtual90 then invokes virtual98 directly. This
            //is not027300's extra addition-capacity stage.
            var home = ReadPersonnelHireTreasuryCurrent(current, districtId);
            int beforeGold = BaseCurrencyAdditionSource.ReadCurrentGold(home, current);
            int requested = unchecked(beforeGold - templateIds.Count * rules.GoldPerPerson);
            var currency = BaseCurrencyAdditionSource.CurrencyCity(ReadPersonnelHireTreasuryCurrent(current, districtId), current);
            int capacity = BasePostLoad.ReadResourceCapacities(currency, current).Gold;
            int afterGold = requested < 0 ? 0 : Math.Min(requested, capacity);
            int cityId = HireNeed(currency.PhysicalSlot, "treasury_City_pointer");
            var city = currency.Kind == BaseKind.City ? working.Cities.GetValueOrDefault(cityId) : null;
            if (city is null) throw HireMissing("treasury_City_owner");
            current.SetBaseResource(currency, BaseResourceKind.Gold, afterGold);
            working = working with { Cities = new(working.Cities) { [cityId] = city with { Gold = afterGold } } };
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            var effects = new List<CampaignEffectRequest>();
            working = ApplyPersonnelHireReportsCurrent(working, current,
                HireNeed(current.ReadBaseObjectLink(destination), "report_destination4E"), templateIds.Count, effects);
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw HireMissing("pending_owner"))
                .Append(CampaignEffectSource.PersonnelAdministrationCommands,
                    working.NativeMapSeason?.AdvancedCalendarSteps ?? throw HireMissing("calendar_owner"), effects) };
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = world.Pool.ExportOwnedState() }));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private GameState CreatePersonnelHireCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int forceId, int templateId, BaseReference destination)
    {
        if (!PersonnelHireServiceAllowedCurrent(state)) return state;
        var owner = state.PersonnelHire ?? throw HireMissing("create_manager");
        if (owner.RemainingSlots <= 0) return state;
        var template = owner.Templates?.GetValueOrDefault(templateId) ?? throw HireMissing("create_template");
        if (!HireNeed(current.ReadBaseNativeValidity(destination), "create_destination_validity")) return state;
        var district = current.ReadBaseDistrictReference(destination) ?? throw HireMissing("create_destination_District");
        if (!HireNeed(current.ReadDistrictNativeValidity(district), "create_destination_District_validity")) return state;
        int destinationForce = HireNeed(current.ReadDistrictForceSlot(district), "create_destination_Force");
        var force = current.ReadForceTableReference(destinationForce < 52 ? destinationForce : 0) ?? throw HireMissing("create_Force_pointer");
        if (!HireNeed(current.ReadForceNativeValidity(force), "create_Force_validity")) return state;
        int dynamicId = 0, physical = 0;
        for (int id = 4201; id <= 4400; id++)
        {
            var pointer = current.ReadPersonLookupReference(id) ?? throw HireMissing("dynamic_Person_lookup");
            int candidate = HireNeed(pointer.PhysicalSlot, "dynamic_Person_pointer");
            if (HireNeed(current.ReadPersonNativeValidity(candidate), "dynamic_Person_validity")) continue;
            dynamicId = id; physical = candidate; break;
        }
        if (dynamicId == 0) return state;
        int year = Math.Clamp((int)(state.NativeMapSeason?.ThirdDate.Year ?? throw HireMissing("create_year")), 0, 350);
        var source = template.Person;
        //31C520 copies the independent template, never the same-ID historical
        //officer. Detach only the selected mutable leaves from the catalogue.
        var copied = source with
        {
            Id = dynamicId, AppearanceYear = (ushort)year,
            BaseFormationIds = source.BaseFormationIds?.ToArray(), BaseTacticIds = source.BaseTacticIds?.ToArray(),
            CurrentTacticSlots = source.CurrentTacticSlots?.ToArray(), CurrentTraitIds = source.CurrentTraitIds?.ToArray(),
            AbilityState = new(120, 100, source.AbilityState?.ObservedDoctrineId)
        };
        var working = state with { Officers = new(state.Officers) { [dynamicId] = copied } };
        current.BindPersonnelHirePersonCurrent(physical, copied);
        working = InstallPersonnelHirePersonInputsCurrent(working, physical, dynamicId, year, template);
        _personRelationInputs.BindCurrent(() => working, current);
        var join = new PersonAppearanceSource(this, working, current, pool);
        working = join.ApplyHireJoinCurrent(physical, destination, force);
        if (!(current.ReadRegisteredPersonPhysicalSlots() ?? throw HireMissing("Person_registry")).Contains(physical))
            _ = ObjectRegistration.Register(new(RegistryKind.Person, physical), current);
        //29E080's reached current target governor refresh is shared with
        //other actual Person entries; it does not capture or export a pool.
        ApplyPersonFreedomGovernorCurrent(working, current, destination);
        var aftermath = current.ReadOfficerAftermath(physical) ?? throw HireMissing("join_resentment_owner");
        int ruler = current.ReadForceRecord(destinationForce)?.RawRuler10 ?? throw HireMissing("join_ruler_identity");
        if (aftermath.Resentment?.TargetOfficerId == ruler)
        {
            current.SetOfficerAftermath(physical, aftermath with { Resentment = new(0, 0),
                SourceKnowledge = aftermath.SourceKnowledge is { } residue ? residue with { UnmappedResentmentTarget = null } : null });
            working = current.PublishOfficerAftermath(working, dynamicId);
        }
        var roster = owner.Forces.GetValueOrDefault(forceId) ?? throw HireMissing("create_Force_roster");
        var changed = roster with { AvailableTemplateOfficerIds = roster.AvailableTemplateOfficerIds.Remove(templateId),
            HiredOfficerIds = roster.HiredOfficerIds.Add(dynamicId) };
        var resetTemplate = ResetPersonnelHireTemplateSnapshot(template);
        working = working with { PersonnelHire = owner with
        {
            RemainingSlots = owner.RemainingSlots - 1,
            Forces = owner.Forces.SetItem(forceId, changed),
            Contracts = owner.Contracts.SetItem(dynamicId, new(dynamicId, templateId, year)),
            Templates = owner.Templates!.SetItem(templateId, resetTemplate)
        } };
        return working;
    }
}
