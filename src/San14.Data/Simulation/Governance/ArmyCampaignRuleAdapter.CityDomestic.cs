using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICityDomesticExecution
{
    /// <summary>Case4 menu presence and current admission, without opening a city draft.</summary>
    public AreaAdministrationQuery<CityDomesticMenuAdmission> QueryCityDomesticMenuAdmission(
        GameState state, int cityId, int? settledCameraLevel)
    {
        try
        {
            if (cityId is < 1 or > 51 || !state.Cities.TryGetValue(cityId, out var city) || city.Id != cityId)
                throw MenuMissing("city_domain_identity");
            var current = OwnedArmyReturnCurrentFields.CaptureCityMenuCurrent(state, _definitions);
            var receiver = current.ReadCityTableReference(cityId) ?? throw MenuMissing("city_pointer");
            if (receiver.PhysicalSlot != cityId) throw MenuMissing("city_physical_identity");
            if (!Need(current.ReadBaseNativeValidity(receiver), "presence_city_validity")) return Absent();
            var district = current.ReadCityDistrictReference(receiver) ?? throw MenuMissing("presence_district_pointer");
            bool present = false;
            if (Need(current.ReadDistrictNativeValidity(district), "presence_district_validity"))
            {
                current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
                present = BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, MenuMissing);
            }

            // The updater repeats both native gates before consuming the camera.
            if (!Need(current.ReadBaseNativeValidity(receiver), "updater_city_validity")) return Absent();
            district = current.ReadCityDistrictReference(receiver) ?? throw MenuMissing("updater_district_pointer");
            if (!Need(current.ReadDistrictNativeValidity(district), "updater_district_validity")) return Absent();
            district = current.ReadCityDistrictReference(receiver) ?? throw MenuMissing("retained_district_pointer");
            int level = Need(settledCameraLevel, "settled_camera_level");
            if (level is < 0 or > 6) throw MenuMissing("settled_camera_level_range");
            if (!present) return Absent();

            var (count, books) = ReadCityMenuCandidatesAndBooks(state, current, cityId, district,
                () => WorldIndexContext.ImportOwnedState(current,
                    state.ReadArmyLifecycle()?.Indexes ?? throw MenuMissing("saved_indexes"), state), MenuMissing);
            return AreaAdministrationQuery<CityDomesticMenuAdmission>.Known(new(true, count != 0 && books != 0 && level <= 5));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<CityDomesticMenuAdmission>.Unknown(unknown.Message); }

        static AreaAdministrationQuery<CityDomesticMenuAdmission> Absent() =>
            AreaAdministrationQuery<CityDomesticMenuAdmission>.Known(new(false, false));
        static T Need<T>(T? value, string name) where T : struct => value ?? throw MenuMissing(name);
    }

    private static InvalidDataException MenuMissing(string name) => new("city-domestic.menu." + name);

    // This leaf is shared with the tree projection. The complete collector
    // precedes the unconditional current book-byte read, even when empty.
    private static (int Count, int Books) ReadCityMenuCandidatesAndBooks(GameState state,
        SourceWorldContext current, int cityId, DistrictReference district,
        Action ensureIndexes, Func<string, InvalidDataException> missing)
    {
        ensureIndexes();
        int count = AreaAdministrationExecution.OrderedCandidates(current, cityId).Count;
        int physical = district.PhysicalSlot ?? throw missing("retained_district_identity");
        var owner = state.Districts.GetValueOrDefault(physical) ?? throw missing("district_owner");
        if (owner.Id != physical || state.Cities[cityId].DistrictId != physical) throw missing("district_domain_identity");
        int books = owner.OrdersRemaining ?? throw missing("command_books");
        if (books is < 0 or > byte.MaxValue) throw missing("command_books_range");
        return (count, books);
    }

    public AreaAdministrationQuery<CityDomesticDraft> OpenCityDomesticDraft(GameState state, int cityId)
    {
        try
        {
            CityDomesticParameterCatalog.RequireScenario(_scenario, Seed.Language);
            if (state.CampaignEventWorld?.CurrentUserIndex != -1 &&
                ReadApplicationGameplay()?.UserSelectionServicePresent != false)
                throw new InvalidDataException("city-domestic.tutorial_current_user");
            if (!state.Cities.TryGetValue(cityId, out var city)) throw new InvalidDataException("city-domestic.city");
            var indexes = state.ReadArmyLifecycle()?.Indexes ?? throw new InvalidDataException("city-domestic.saved_indexes");
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            _ = WorldIndexContext.ImportOwnedState(current, indexes, state);
            PersonReturnCommands.BindCurrent(state, current);
            current.MoraleWavePersonPostE7Reader ??= p => OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, p);
            _personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            var card = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario, Seed.Language, _baseCapacityInputs, _repository);
            bool bound = _baseCapacityInputs.BindCurrent(state, current);
            var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, bound);
            return AreaAdministrationQuery<CityDomesticDraft>.Known(new(state, current, city, _definitions, _scenario, Seed.Language,
                _baseCapacityInputs, bound, card, preview, narrationSource: new(state, current, _definitions, _scenario, Seed.Language, _repository, _baseCapacityInputs), governorInputs: _governorInputs, personResources: _repository,
                userSelectionServicePresent: ReadApplicationGameplay()?.UserSelectionServicePresent));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<CityDomesticDraft>.Unknown(unknown.Message); }
    }

    CommandResult ICityDomesticExecution.Complete(GameState state, ForceState force,
        CompleteCityDomesticOrder order, CommandContext context)
    {
        try
        {
            var draft = CreateCityDomesticCommandDraft(state, force, order.CityId, out var current);
            var names = new CityDomesticAssociationReportSource(state, _definitions, _scenario, Seed.Language, _repository);
            return draft.CompleteSource(order, () => Context(current).ReadCurrentSamplerWorld(state)?.Day450, names.Read);
        }
        catch (InvalidDataException unknown)
        { return CommandResult.RuleUnavailable("rules.city_domestic_execution", unknown.Message); }
    }
    CommandResult ICityDomesticExecution.SetDuty(GameState state, ForceState force,
        SetCityDutyOrder order, CommandContext context)
    {
        try
        {
            var draft = CreateCityDomesticCommandDraft(state, force, order.CityId, out var current);
            int physical;
            if (order.OfficerId is int identity)
            {
                if (identity is < 0 or > 6000 || !state.Officers.ContainsKey(identity))
                    return CommandResult.Reject("City duty officer does not exist.", "duty.officer");
                physical = current.ReadPersonLookupReference(identity)?.PhysicalSlot
                    ?? throw new InvalidDataException("city-domestic.legacy_officer_lookup");
                if (_definitions.ResolveOfficerSource(identity) != physical || current.ReadPersonIdentity(physical) != identity)
                    throw new InvalidDataException("city-domestic.legacy_officer_binding");
            }
            else physical = current.ReadTransferPersonTableReference(0)?.PhysicalSlot
                ?? throw new InvalidDataException("city-domestic.physical_zero_pointer");
            ushort raw = current.ReadPersonIdentity(physical) ?? throw new InvalidDataException("city-domestic.legacy_officer_identity");
            var names = new CityDomesticAssociationReportSource(state, _definitions, _scenario, Seed.Language, _repository);
            var operation = new CityDomesticStageOperation(order.Kind,
                order.OfficerId.HasValue ? CityDomesticSelectionKind.SelectReceiver : CityDomesticSelectionKind.Dismiss,
                physical, raw);
            return draft.CompleteSource(new(order.ForceId, order.CityId, [operation], CityDomesticTerminal.Confirm),
                () => Context(current).ReadCurrentSamplerWorld(state)?.Day450, names.Read);
        }
        catch (InvalidDataException unknown)
        { return CommandResult.RuleUnavailable("rules.city_domestic_execution", unknown.Message); }
    }
    private CityDomesticDraft CreateCityDomesticCommandDraft(GameState state, ForceState force, int cityId,
        out SourceWorldContext current)
    {
        CityDomesticParameterCatalog.RequireScenario(_scenario, Seed.Language);
        var city = state.Cities.GetValueOrDefault(cityId) ?? throw new InvalidDataException("city-domestic.city");
        if (city.ForceId != force.Id) throw new InvalidDataException("city-domestic.city_force_binding");
        var indexes = state.ReadArmyLifecycle()?.Indexes ?? throw new InvalidDataException("city-domestic.saved_indexes");
        current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = WorldIndexContext.ImportOwnedState(current, indexes, state);
        PersonReturnCommands.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
        var reference = current.ReadCityTableReference(cityId) ?? throw new InvalidDataException("city-domestic.city_pointer");
        int rawForce = current.ReadBaseForceSlot(reference) ?? throw new InvalidDataException("city-domestic.city_force");
        var sourceForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
            ?? throw new InvalidDataException("city-domestic.force_pointer");
        if (sourceForce.PhysicalSlot != force.Id) throw new InvalidDataException("city-domestic.force_domain_binding");
        // These read-only facades are lazy. Command replay does not query cards,
        // effective abilities, training capacity or incomes, nor install those inputs.
        var card = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario, Seed.Language, _baseCapacityInputs, _repository);
        var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, false);
        return new(state, current, city, _definitions, _scenario, Seed.Language, _baseCapacityInputs, false, card, preview, false,
            userSelectionServicePresent: ReadApplicationGameplay()?.UserSelectionServicePresent);
    }
}
