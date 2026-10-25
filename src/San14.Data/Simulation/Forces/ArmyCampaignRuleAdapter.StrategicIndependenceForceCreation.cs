using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record StrategicIndependenceForceCreationResult(GameState State, ForceReference Force);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2E6010/2A4980's 2D8050(person,actual Home City,0,1,0).
    /// This is a new Force in the existing world, not a fresh campaign.</summary>
    internal StrategicIndependenceForceCreationResult CreateStrategicIndependenceForceCurrent(
        GameState input, SourceWorldContext current, WorldIndexContext pool, int rulerPhysical,
        BaseReference home, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        var working = input;
        int id = IndependenceForceNeed(current.ReadPersonIdentity(rulerPhysical), "ruler_ID");
        if (home.Kind != BaseKind.City || home.PhysicalSlot is not (>= 1 and <= 51))
            throw IndependenceForceMissing("2E6010_actual_Home_City");
        var created = SelectStrategicIndependenceForceCurrent(working, current, rulerPhysical);
        if (created is null) return new(working, current.ReadForceTableReference(0)
            ?? throw IndependenceForceMissing("no_free_Force_sentinel"));
        int forceId = IndependenceForceNeed(created.PhysicalSlot, "selected_Force");
        var original = current.ReadPersonForce(rulerPhysical) ?? throw IndependenceForceMissing("old_Force");
        bool originalValid = IndependenceForceNeed(current.ReadForceNativeValidity(original), "old_Force_validity");
        int oldForce = IndependenceForceNeed(original.PhysicalSlot, "old_Force_identity");
        int status = IndependenceForceNeed(current.ReadPersonStatus(rulerPhysical), "old_status");
        if (status is >= 1 and <= 4)
        {
            int oldRuler = IndependenceForceNeed(current.ReadForceRecord(oldForce)?.RawRuler10, "old_ruler");
            var former = current.ReadPersonLookupReference(oldRuler <= 6000 ? oldRuler : 0)
                ?? throw IndependenceForceMissing("old_ruler_pointer");
            TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current)
                ?? throw IndependenceForceMissing("World450_45C");
            working = ApplyCaptiveFreedomCurrent(working, current, pool, rulerPhysical, date,
                () => Sampler().Day450, Sampler, effects, former, home);
        }
        int oldReady = IndependenceForceNeed(current.ReadBaseResource(home, BaseResourceKind.Troops), "snapshot_ready");
        int oldGold = IndependenceForceNeed(current.ReadBaseResource(home, BaseResourceKind.Gold), "snapshot_gold");
        int oldFood = IndependenceForceNeed(current.ReadBaseResource(home, BaseResourceKind.Supplies), "snapshot_food");
        int oldWall = BasePostLoad.ReadLinkedField14(home, current);
        int oldWounded = IndependenceForceNeed(current.ReadArmyReturnBaseWounded40(home), "snapshot_wounded");
        var bases = current.CreateNearbyTemporary();
        DistrictReference district;
        try
        {
            current.AppendCaptureBaseTemporary(bases, home);
            district = CreateStrategicIndependenceDistrictCurrent(ref working, current, pool,
                created, rulerPhysical, home, bases, date, effects);

            //227070(Force,0) follows the complete District creation. This
            //clears only this receiver's relation arrays, not every other row.
            working = ResetStrategicIndependenceForceCurrent(working, current, forceId);
            current.SetOwnedForceRulerSlot10(forceId, (ushort)Math.Min(id, 6000));
            current.SetForceRankSlot47(forceId, 9);
            byte color = SelectStrategicIndependenceColorCurrent(working, current, forceId);
            var ruler = working.Officers.GetValueOrDefault(id) ?? throw IndependenceForceMissing("ruler_saved_owner");
            int doctrine = IndependenceForceNeed(OfficerDoctrineRules.ReadDoctrineId(ruler.DoctrineId, ruler.AbilityState), "ruler_doctrine");
            var force = working.Forces.GetValueOrDefault(forceId) ?? throw IndependenceForceMissing("selected_Force_owner");
            working = working with { Forces = new(working.Forces) { [forceId] = force with
            { IsActive = true, RulerId = id, RankId = 9, ReportColorIndex = color, Name = ruler.Name, DoctrineId = doctrine } } };
            working = IndependenceForceParameter(working, BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x10), id);
            working = IndependenceForceParameter(working, OwnedArmyLifecycleDetachedInputs.Key("Force", forceId, "10"), id);
            var resetOwner = working.NativeForceUnregisterCurrent ?? throw IndependenceForceMissing("reset_image_owner");
            var image = resetOwner.ForceResetImages[forceId].ToArray();
            image[0x10] = (byte)id; image[0x11] = (byte)(id >> 8); image[0x47] = 9; image[0x49] = color;
            working = working with { NativeForceUnregisterCurrent = resetOwner with
                { ForceResetImages = new(resetOwner.ForceResetImages) { [forceId] = image } } };
            _baseCapacityInputs.BindCurrent(working, current);
            current.BaseForcePolicyEffectReader = (f, effect) =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, f, effect, true).Value;

            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw IndependenceForceMissing("Force_registry"))
            {
                int other = IndependenceForceNeed(entry?.PhysicalSlot, "relation_Force_pointer");
                byte relation = other == oldForce ? (byte)0 : (byte)50;
                working = AbsorptionWriteRelation(working, current, forceId, other, false, relation);
                working = AbsorptionWriteRelation(working, current, other, forceId, false, relation);
            }
            current.WritePersonStatus(rulerPhysical, 1);
            var latest = working;
            var operation = CreateCaptureDistrictOperationQueries(current, () => latest, value => latest = value, date, effects);
            DistrictCreationTransferPerson(current, rulerPhysical, IndependenceForceNeed(district.PhysicalSlot, "primary_District"), operation);
            working = latest;
            current.WritePersonFirstLocation(rulerPhysical, (ushort)IndependenceForceNeed(home.PhysicalSlot, "ruler_Home_City"));
            working = new PersonAppearanceSource(this, working, current, pool).ApplyInitialLoyaltyCurrent(rulerPhysical, new(rulerPhysical));
            current.SetMoraleWavePersonPostE7(rulerPhysical, 0);
            working = IndependenceForceParameter(working, OwnedArmyGovernorComparatorInputs.PersonWord166Key(rulerPhysical), Math.Clamp((int)date.Year, 0, 350));
            var personOwner = working.Officers[id];
            working = working with { Officers = new(working.Officers) { [id] = personOwner with
            { Appointment = OfficerAppointment.Synchronize(0), ForceId = forceId, DistrictId = district.PhysicalSlot,
                LocationId = home.PhysicalSlot, ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(1), Role = OfficerRole.Ruler } } };
            working = ApplyStrategicIndependencePlacementCurrent(working, current, pool, rulerPhysical, home, date);
            current.SetArmyReturnBaseGovernor2E(home, (ushort)Math.Min(id, 6000));

            //231FE0 iterates this representative Area's actual Hex members.
            //The initial object/Army occupancy gates precede185D00.
            var area = CurrentGateMapQueries.FindArea(home, current);
            int areaId = IndependenceForceNeed(area.PhysicalSlot, "home_Area");
            int cursor = AreaHexIterator.FindNextIndex(0, areaId, current);
            while (cursor < 48400)
            {
                var hex = current.ReadHexTableReference(cursor) ?? throw IndependenceForceMissing("Area_Hex_pointer");
                int objectId = IndependenceForceNeed(current.ReadHexObjectLink(hex), "Area_Hex_Object");
                if (objectId == 0)
                {
                    int armyRaw = IndependenceForceNeed(current.ReadHexRaw1C(hex), "Area_Hex_Army");
                    if (armyRaw is < 1 or > 500) working = TransferAbsorptionHexCurrent(working, current, hex, forceId, 0);
                }
                cursor = AreaHexIterator.FindNextIndex(cursor + 1, areaId, current);
            }

            //TC file-backed scalars141A4AC38=80,141A4AC44=80,
            //141A4AC4C=20 are read offline, never from the executable here.
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            int leadership = IndependenceForceNeed(abilities.ResolveDimension(working, id, 0,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect)), "ruler_Leadership");
            int charm = IndependenceForceNeed(abilities.ResolveDimension(working, id, 4,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect)), "ruler_Charm");
            BasePostLoad.WriteMorale(home, Math.Max((int)IndependenceForceNeed(current.ReadBaseMorale44(home), "home_Morale"),
                Math.Min(BasePostLoad.ReadMoraleUpper(created, current), leadership / 10 + 80)), current);
            int security = Math.Clamp(Math.Max(working.Areas[areaId].Security ?? throw IndependenceForceMissing("Area_security"),
                Math.Min(100, charm / 10 + 80)), 0, 100);
            working = working with { Areas = new(working.Areas) { [areaId] = working.Areas[areaId] with { Security = security } } };
            current.ConditionAreaSecurityReader = physical => working.Areas.GetValueOrDefault(physical)?.Security is int value && value is >= 0 and <= 255 ? (byte)value : null;
            if (originalValid)
            {
                int intelligence = IndependenceForceNeed(abilities.ResolveDimension(working, id, 2,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect)), "ruler_Intelligence");
                current.SetBaseResource(home, BaseResourceKind.Troops,
                    Math.Clamp(unchecked((charm + intelligence) * 20 + oldReady), 0, BasePostLoad.ReadResourceCapacities(home, current).Troops));
                current.SetBaseResource(home, BaseResourceKind.Gold, Math.Min(oldGold, BasePostLoad.ReadResourceCapacities(home, current).Gold));
                current.SetBaseResource(home, BaseResourceKind.Supplies, Math.Min(oldFood, BasePostLoad.ReadResourceCapacities(home, current).Supplies));
                BasePostLoad.WriteLinkedField14(home, Math.Min(oldWall, BasePostLoad.ReadCapacities(home, current).Field46), current);
                current.SetArmyReturnBaseWounded40(home, Math.Clamp(oldWounded, 0, BasePostLoad.ReadResourceCapacities(home, current).Troops));
                current.SetBasePostLoadFlag66(home, BasePostLoad.ReadFlag66(home, current));
            }
            else InitializeUnownedIndependenceCityResourcesCurrent(working, current, home, created, date, charm, abilities);
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            ObjectRegistration.Register(new(RegistryKind.Force, forceId), current);
            working = InitializeDoctrineResearchForceCurrent(working, current, forceId);

            //2D8050's334EF0 guard is the hire option itself. It does not
            //rerun335D00's initialization or reset all existing Force offers.
            int selector = IndependenceForceNeed(BattleRemakeCurrentSettings.ReadSelectorE58(working), "hire_selector");
            if (IndependenceForceNeed(BattleRemakeCurrentSettings.ReadInt32(working, selector == 0 ? 0x50C : 0x130), "hire_option") != 1)
            {
                BindPersonnelHireGenerationEconomyCurrent(working, current);
                var generation = CreatePersonnelHireGenerationSession(working, current);
                working = GeneratePersonnelHireTemplatesCurrent(working, current, forceId, generation);
                working = CompletePersonnelHireGeneration(working, generation);
            }
            uint population = 0;
            foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw IndependenceForceMissing("population_Area_registry"))
            {
                var at = entry ?? throw IndependenceForceMissing("population_Area_pointer");
                if (!IndependenceForceNeed(current.ReadAreaNativeValidity(at), "population_Area_validity") ||
                    IndependenceForceNeed(current.ReadAreaCurrentForceSlot(at), "population_Area_Force") != forceId) continue;
                int physical = IndependenceForceNeed(at.PhysicalSlot, "population_Area_identity");
                population = unchecked(population + IndependenceForceNeed(working.Areas.GetValueOrDefault(physical)?.NativeNormalAftermath?.Quantity52, "population_Area52"));
            }
            var native = working.NativeArmySimulation ?? throw IndependenceForceMissing("population_native_owner");
            working = working with { NativeArmySimulation = native with { Forces = new(native.Forces)
                { [forceId] = native.Forces[forceId] with { PopulationTotal = Math.Max(unchecked((int)population), 0) } } } };
            working = ForceUnregisterSource.RankCurrent(working, current, pool).State;
        }
        finally { current.ClearNearbyTemporary(bases, true); }
        return new(working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw IndependenceForceMissing("lifecycle")) with
            { Indexes = pool.ProjectOwnedRoles() }), created);
    }

    private void InitializeUnownedIndependenceCityResourcesCurrent(GameState state,
        SourceWorldContext current, BaseReference home, ForceReference force, NativeMapSeasonDate date,
        int rulerCharm, IEffectiveOfficerAbilities abilities)
    {
        //2D8050's no-former-Force branch quotes the current month and an
        //explicit Person0. It does not quote next-month City landmark income.
        int forceId = IndependenceForceNeed(force.PhysicalSlot, "initial_economy_Force");
        int gold = 0, food = 0;
        var recruitment = new AreaAdministrationPreviewSource(state, current, _scenario,
            Seed.Language, _baseCapacityInputs, true);
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw IndependenceForceMissing("initial_economy_Area_registry"))
        {
            var area = entry ?? throw IndependenceForceMissing("initial_economy_Area_pointer");
            if (IndependenceForceNeed(current.ReadAreaCitySlot(area), "initial_economy_Area35") != home.PhysicalSlot ||
                IndependenceForceNeed(current.ReadAreaCurrentForceSlot(area), "initial_economy_Area_Force") != forceId) continue;
            gold = unchecked(gold + ReadHireAreaGoldIncomeCurrent(state, current, area, home, abilities,
                _ => throw IndependenceForceMissing("initial_economy_Person0_tasks"), personOverride: 0));
            food = unchecked(food + CityFoodIncomeSource.ReadArea(state, current, area, home, force,
                date.Month, abilities, personOverride: 0));
            //The original executes this quote but discards its result. For
            //an ordinary new Force, the explicit null recruiter returns0.
            _ = HireEconomyValue(recruitment.Soldiers(
                IndependenceForceNeed(area.PhysicalSlot, "initial_economy_Area"), recruiterOverride: 0));
        }
        //TC offline data:141A4AC54=6,141A4AC60=6,
        //141A4AC6C=100,141A4AC78=10. No executable is read at runtime.
        int goldFloor = Math.Min(BasePostLoad.ReadGoldCapacity(home, current), unchecked(gold * 6 + 3000));
        current.SetBaseResource(home, BaseResourceKind.Gold, Math.Max(
            IndependenceForceNeed(current.ReadBaseResource(home, BaseResourceKind.Gold), "initial_economy_Gold"), goldFloor));
        int foodFloor = Math.Min(BasePostLoad.ReadSuppliesCapacity(home, current), unchecked(food * 6 + 30000));
        current.SetBaseResource(home, BaseResourceKind.Supplies, Math.Max(
            IndependenceForceNeed(current.ReadBaseResource(home, BaseResourceKind.Supplies), "initial_economy_Food"), foodFloor));
        int areaId = IndependenceForceNeed(CurrentGateMapQueries.FindArea(home, current).PhysicalSlot, "initial_economy_representative_Area");
        int population = state.Areas.GetValueOrDefault(areaId)?.NativeNormalAftermath?.Quantity52
            ?? throw IndependenceForceMissing("initial_economy_representative_Area52");
        int startYear = state.NativeMapSeason?.SecondDate.Year ?? throw IndependenceForceMissing("initial_economy_World2E_year");
        int troops = unchecked(population / 10 + unchecked((date.Year - startYear + rulerCharm) * 100));
        troops = Math.Max(troops, IndependenceForceNeed(current.ReadBaseResource(home, BaseResourceKind.Troops), "initial_economy_Troops"));
        current.SetBaseResource(home, BaseResourceKind.Troops,
            Math.Clamp(troops, 0, BasePostLoad.ReadTroopCapacity(home, current)));
    }

    private static ForceReference? SelectStrategicIndependenceForceCurrent(GameState state, SourceWorldContext current, int ruler)
    {
        int id = IndependenceForceNeed(current.ReadPersonIdentity(ruler), "selection_Person10");
        int bloodline = OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, ruler, 0xB2)
            ?? throw IndependenceForceMissing("selection_PersonB2");
        var candidates = new List<(ForceReference Force, int Faction)>();
        for (int slot = 0; slot < 52; slot++)
        {
            var force = current.ReadForceTableReference(slot) ?? throw IndependenceForceMissing("selection_Force_table");
            int physical = IndependenceForceNeed(force.PhysicalSlot, "selection_Force_pointer");
            if (physical == 0 || IndependenceForceNeed(current.ReadForceNativeValidity(force), "selection_Force_validity") || physical >= 46) continue;
            int faction = state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.FactionOfficerId
                ?? throw IndependenceForceMissing("selection_FactionOfficer_current");
            if (faction == id) return force;
            candidates.Add((force, faction));
        }
        ForceReference? fallback = null;
        foreach (var candidate in candidates)
        {
            if (candidate.Faction == bloodline) return candidate.Force;
            int physical = IndependenceForceNeed(candidate.Force.PhysicalSlot, "selection_fallback");
            if (physical is >= 38 and <= 45 && (fallback is null || physical > fallback.PhysicalSlot)) fallback = candidate.Force;
        }
        return fallback;
    }

    private static byte SelectStrategicIndependenceColorCurrent(GameState state, SourceWorldContext current, int force)
    {
        byte prior = state.Forces[force].ReportColorIndex ?? throw IndependenceForceMissing("retained_color49");
        if (force is < 38 or > 45) return prior;
        var used = new bool[10];
        for (int slot = 0; slot < 52; slot++)
        {
            var at = current.ReadForceTableReference(slot) ?? throw IndependenceForceMissing("color_Force_table");
            if (!IndependenceForceNeed(current.ReadForceNativeValidity(at), "color_Force_validity")) continue;
            int physical = IndependenceForceNeed(at.PhysicalSlot, "color_Force_identity");
            byte color = state.Forces[physical].ReportColorIndex ?? throw IndependenceForceMissing("active_color49");
            if (color is >= 50 and <= 59) used[color - 50] = true;
        }
        for (int index = 0; index < used.Length; index++) if (!used[index]) return (byte)(index + 50);
        return prior;
    }

    private DistrictReference CreateStrategicIndependenceDistrictCurrent(ref GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force, int ruler,
        BaseReference home, HexListHandle bases, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        var working = state;
        var operation = CreateCaptureDistrictOperationQueries(current, () => working, value => working = value, date, effects);
        DistrictReference? created = null;
        for (int slot = 0; slot < 52; slot++)
        {
            var at = current.ReadDistrictTableReference(slot) ?? throw IndependenceForceMissing("District_table");
            int physical = IndependenceForceNeed(at.PhysicalSlot, "District_pointer");
            if (physical == 0 || physical >= 46 || IndependenceForceNeed(current.ReadDistrictNativeValidity(at), "District_validity")) continue;
            created = at; break;
        }
        if (created is null) throw IndependenceForceMissing("no_free_District");
        int district = IndependenceForceNeed(created.PhysicalSlot, "created_District");
        int forceId = IndependenceForceNeed(force.PhysicalSlot, "created_Force");
        current.ResetDistrict(district);
        current.SetDistrictRawByte(district, 0x10, (byte)forceId);
        int number = 1;
        var existing = (current.ReadArmyReturnRegisteredDistricts() ?? throw IndependenceForceMissing("District_registry"))
            .Select(at => at ?? throw IndependenceForceMissing("District_registry_pointer"))
            .Where(at => IndependenceForceNeed(current.ReadDistrictForceSlot(at), "District_Force") == forceId)
            .Select(at => IndependenceForceNeed(current.ReadDistrictNumber(IndependenceForceNeed(at.PhysicalSlot, "District_identity")), "District_number")).Order().ToArray();
        foreach (int value in existing) if (value == number) number++;
        current.SetDistrictRawByte(district, 0x11, (byte)Math.Clamp(number, 0, 8));
        operation.TouchedDistricts.Add(district);
        int? residentRuler = null;
        var node = current.ReadRegistryHead(new(bases.Identity)) ?? throw IndependenceForceMissing("Base_list_head");
        while (node.Identity.HasValue)
        {
            var target = current.ReadBaseRegistryNodePayload(node) ?? throw IndependenceForceMissing("Base_list_payload");
            if (IndependenceForceNeed(current.ReadBaseForceSlot(target), "Base_Force") == forceId)
                TransferDistrictAdministrationBaseCurrent(current, target, created, operation);
            else working = ApplyForceBaseTransferCurrent(working, current, pool, target, created, new(0), date, effects,
                preserveResources: false, suppressMaintenance: false, fullCentralRadius: false);
            foreach (int person in DistrictCreationHomePeople(current, target))
                if (IndependenceForceNeed(current.ReadPersonStatus(person), "resident_status") == 1) residentRuler = person;
                else current.WritePersonStatus(person, 4);
            node = current.ReadRegistryNodeNext(node) ?? throw IndependenceForceMissing("Base_list_next");
        }
        int selected = residentRuler ?? ruler;
        current.WriteDistrictLeader(district, (ushort)Math.Min((int)IndependenceForceNeed(current.ReadPersonIdentity(selected), "leader_ID"), 6000));
        if (residentRuler.HasValue) current.WritePersonStatus(ruler, 4);
        else if (IndependenceForceNeed(current.ReadPersonStatus(ruler), "ruler_status_before_leader") != 1) current.WritePersonStatus(ruler, 2);
        DistrictCreationTransferPerson(current, ruler, district, operation);
        if (IndependenceForceNeed(current.ReadBaseNativeValidity(home), "governor_Base_validity"))
        {
            var governor = ArmyReturnGovernorUpdater.PrepareCurrent(home, current, operation);
            ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, governor.Packet ?? throw IndependenceForceMissing(governor.UnresolvedOwner ?? "governor"));
        }
        MaintainDistrictAdministrationAppointmentsCurrent(current, home);
        ObjectRegistration.Register(new(RegistryKind.District, district), current);
        working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
        working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current, operation.ChangedAreas, operation.ChangedGates);
        state = working with { Districts = new(working.Districts) { [district] = new(district, forceId,
            IndependenceForceNeed(current.ReadDistrictNumber(district), "published_District_number"),
            IndependenceForceNeed(current.ReadDistrictLeader(district), "published_District_leader"))
            { OrdersRemaining = IndependenceForceNeed(current.ReadDistrictRawByte(district, 0x14), "published_District_books") } } };
        return created;
    }

    private static GameState ResetStrategicIndependenceForceCurrent(GameState state, SourceWorldContext current, int force)
    {
        var native = state.NativeArmySimulation ?? throw IndependenceForceMissing("reset_native");
        var owner = native.Forces.GetValueOrDefault(force) ?? throw IndependenceForceMissing("reset_Force");
        var module = state.NativeForceUnregisterCurrent ?? throw IndependenceForceMissing("reset_module");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (int field in new[] { 0x10, 0x52, 0x16A, 0x16C }) values[BattleRemakeCurrentForcePolicies.ForceFieldKey(force, field)] = 0;
        values[BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x15E)] = 0xB;
        values[OwnedArmyLifecycleDetachedInputs.Key("Force", force, "10")] = 0;
        values[OwnedForce164Inputs.ForceValueKey(force)] = 0;
        values[OwnedPersonRelationInputs.Force178Key(force)] = 0;
        for (int group = 0; group < 5; group++)
        {
            for (int member = 0; member < 7; member++)
            {
                values[BattleRemakeCurrentForcePolicies.DepartmentPersonKey(force, group, member)] = 0;
                values[BattleRemakeCurrentForcePolicies.DepartmentFilterKey(force, group, member)] = 0;
            }
            values[BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(force, group)] = 0;
        }
        for (int slot = 0; slot < 3; slot++)
        {
            current.WriteForceAssociationPersonId(new(force), slot, 0); current.WriteForceAssociationTargetSlot(new(force), slot, 0);
            values[PersonAssignmentPrecallInputs.AssociationPersonKey(force, slot)] = 0;
            values[PersonAssignmentPrecallInputs.AssociationTargetKey(force, slot)] = 0;
        }
        for (int other = 0; other < 52; other++)
        { current.SetRulerReleaseRelation13(force, other, 0); current.SetForceRelationByteEA(force, other, 0); }
        current.SetOwnedForceRulerSlot10(force, 0); current.SetAssignmentForceAdvisor52(force, 0);
        current.SetForceRankSlot47(force, 0); current.SetForceKingdomNameIndex(force, 0); current.SetForce164(force, 0);
        var domain = state.Forces[force];
        var after = owner with { FactionOfficerId = 0, PopulationTotal = 0, Flags12 = 0, Flags193 = 0, Flags194 = 0,
            ForeignTradeProgress = new(ImmutableArray.Create<ushort>(0, 0, 0, 0, 0), ImmutableArray.Create<byte>(0, 0, 0, 0, 0)),
            DiplomacyFlags178 = 0, DiplomacyCooldownDate = new(0, 1, 1, 0), RelationsEA = new byte[52],
            NormalAftermath = new(new byte[52], 0) };
        return state with { NativeArmySimulation = native with { Forces = new(native.Forces) { [force] = after },
                Parameters = native.Parameters with { Int32Values = values } },
            Forces = new(state.Forces) { [force] = domain with { IsActive = false, RulerId = null, RankId = 0,
                KingdomNameIndex = 0, CombatObjective = ForceCombatObjective.Inactive, PolicyEffects = ForcePolicyEffects.Empty,
                DoctrineResearch = new(ImmutableHashSet<int>.Empty), Governance = null } },
            ForceCreditReserves = (state.ForceCreditReserves ?? throw IndependenceForceMissing("reset_credit_reserves")) with
                { PointsByForce = state.ForceCreditReserves!.PointsByForce.SetItem(force, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0)) },
            NativeForceUnregisterCurrent = module with { ForceResetImages = new(module.ForceResetImages) { [force] = ForceUnregisterSource.ForceResetImage() },
                ForceRankings = new(module.ForceRankings) { [force] = new(0, 0) } } };
    }

    /// <summary>26DA10's 26CDF0 HomeArmy pass. 2B15E0 selects another
    /// actual City in the Commander's District by successful mode1 path-node
    /// count. An absent route reaches the existing real cause4 destruction.</summary>
    private GameState TransferStrategicIndependenceHomeArmiesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference transferred,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        int location = IndependenceForceNeed(current.ReadBaseObjectLink(transferred), "HomeArmy_Base4E");
        foreach (var entry in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            if (IndependenceForceNeed(current.ReadOwnedArmyExclusion233560(entry), "HomeArmy_excluded")) continue;
            int armySlot = IndependenceForceNeed(entry.PhysicalSlot, "HomeArmy_pointer");
            var army = new ArmyUnitReference(armySlot);
            int id = IndependenceForceNeed(current.ReadArmyUnitCommanderId(army), "HomeArmy_Commander_ID");
            int person = IndependenceForceNeed((current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                ?? throw IndependenceForceMissing("HomeArmy_Commander_lookup")).PhysicalSlot, "HomeArmy_Commander");
            if (!IndependenceForceNeed(current.ReadPersonNativeValidity(person), "HomeArmy_Commander_validity") ||
                IndependenceForceNeed(current.ReadPersonFirstLocation(person), "HomeArmy_Commander_Home") != location ||
                !IndependenceForceNeed(current.ReadArmyUnitNativeValidity(army), "HomeArmy_validity")) continue;
            int rawDistrict = IndependenceForceNeed(current.ReadPersonDistrictSlot(person), "HomeArmy_DistrictE0");
            var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                ?? throw IndependenceForceMissing("HomeArmy_District_pointer");
            int districtSlot = IndependenceForceNeed(district.PhysicalSlot, "HomeArmy_District_identity");
            int force = IndependenceForceNeed(current.ReadDistrictForceSlot(district), "HomeArmy_District_Force");
            //234690 checks the physical identity, Force10 and Number11;
            //unlike virtual validity it does not inspect leader12.
            if (districtSlot == 0 || force == 0 || IndependenceForceNeed(current.ReadDistrictNumber(districtSlot), "HomeArmy_District_number") == 0) continue;
            var origin = BaseLookup.Find(IndependenceForceNeed(current.ReadPersonFirstLocation(person), "HomeArmy_current_Home"), current);
            BaseReference? excluded = IndependenceForceNeed(current.ReadBaseForceSlot(origin), "HomeArmy_Home_Force") == force ? null : origin;
            int chosen = ChooseStrategicIndependenceArmyHomeCurrent(current, districtSlot, excluded);
            if (chosen is >= 1 and <= 61)
            {
                current.WritePersonFirstLocation(person, (ushort)chosen);
                var captives = current.CreateNearbyTemporary();
                try
                {
                    foreach (int? item in current.ReadRegisteredPersonPhysicalSlots() ?? throw IndependenceForceMissing("HomeArmy_captive_registry"))
                    {
                        int captive = IndependenceForceNeed(item, "HomeArmy_captive_pointer");
                        if (IndependenceForceNeed(current.ReadPersonStatus(captive), "HomeArmy_captive_status") == 6 &&
                            IndependenceForceNeed(current.ReadPersonSecondLocation(captive), "HomeArmy_captive_E4") == armySlot + 61)
                            current.AppendPersonReturnTemporary(captives, captive);
                    }
                    var node = current.ReadHexListHead(captives) ?? throw IndependenceForceMissing("HomeArmy_captive_head");
                    while (node.Identity.HasValue)
                    {
                        int captive = IndependenceForceNeed(current.ReadPersonRelationListPayload(node)?.PhysicalSlot, "HomeArmy_captive_payload");
                        ushort home = IndependenceForceNeed(current.ReadPersonFirstLocation(person), "HomeArmy_fresh_Commander_Home");
                        current.WritePersonFirstLocation(captive, (ushort)Math.Min((int)home, 61));
                        node = current.ReadHexListNodeNext(node) ?? throw IndependenceForceMissing("HomeArmy_captive_next");
                    }
                }
                finally { current.ClearNearbyTemporary(captives, true); }
                var receiver = state.ReadArmyLifecycle()?.ReceiverSlots()[armySlot]
                    ?? throw IndependenceForceMissing("HomeArmy_current_receiver");
                if (receiver.ActiveUnitId is int unit && state.Units.TryGetValue(unit, out var image))
                    state = state with { Units = new(state.Units) { [unit] = image with { SourceLocationId = chosen } } };
            }
            else
            {
                var result = new OwnedArmyDestructionStage(this).ApplyAbsorptionCommanderCause4Current(state, current, pool, armySlot, date, effects);
                state = result.State ?? throw IndependenceForceMissing(result.UnresolvedOwner ?? "HomeArmy_cause4");
            }
        }
        state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        return OwnedArmyLifecycleDetachedInputs.PublishCurrent(state, current);
    }

    private static int ChooseStrategicIndependenceArmyHomeCurrent(SourceWorldContext current,
        int district, BaseReference? excluded)
    {
        var cities = new List<BaseReference>();
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw IndependenceForceMissing("Home_choice_City_table");
            if (district != 0 && IndependenceForceNeed(current.ReadBaseDistrictSlot(city), "Home_choice_City_District") == district) cities.Add(city);
        }
        bool originValid = excluded is not null && IndependenceForceNeed(current.ReadBaseNativeValidity(excluded), "Home_choice_origin_validity");
        int required = originValid && IndependenceForceNeed(current.ReadBaseDistrictSlot(excluded!), "Home_choice_origin_District") == district ? 2 : 1;
        if (cities.Count < required) return 0;
        int distance = 48400, chosen = 0;
        foreach (var city in cities)
        {
            if (originValid && current.ReadBaseObjectLink(city) == current.ReadBaseObjectLink(excluded!)) continue;
            var path = current.CreateNearbyTemporary();
            try
            {
                if (!originValid || !IndependenceForceNeed(current.ReadBaseNativeValidity(city), "Home_choice_City_validity")) continue;
                var route = PersonReturnPathSource.FillCityHomeChoicePath(current,
                    CurrentGateMapQueries.FindHex(excluded!, current), CurrentGateMapQueries.FindHex(city, current), path);
                bool found = route.NativePathFound ?? throw IndependenceForceMissing(route.UnresolvedOwner ?? "Home_choice_2AEDC0");
                if (!found) continue;
                int count = IndependenceForceNeed(route.OutputNodeCount, "Home_choice_path_count");
                if (count >= distance) continue;
                chosen = IndependenceForceNeed(current.ReadBaseObjectLink(city), "Home_choice_City4E"); distance = count;
            }
            finally { current.ClearNearbyTemporary(path, true); }
        }
        return chosen;
    }

    private static GameState IndependenceForceParameter(GameState state, string key, int value)
    {
        var native = state.NativeArmySimulation ?? throw IndependenceForceMissing("parameter_owner");
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with
            { Int32Values = new(native.Parameters.Int32Values) { [key] = value } } } };
    }
    private static T IndependenceForceNeed<T>(T? value, string field) where T : struct
        => value ?? throw IndependenceForceMissing(field);
    private static InvalidDataException IndependenceForceMissing(string field)
        => new("strategy-independence.Force2D8050." + field);
}
