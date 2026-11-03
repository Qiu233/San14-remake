using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Territory transfer can borrow the thin Turn current before ordinary
    // Army occupation. Fixed definitions and their sparse Hex aliases are
    // prepared once; flags already belong to this actual current capture.
    private void EnsureOccupationSceneryDefinitionsCurrent(SourceWorldContext current)
    {
        if (current.SceneryDefinitions is null) current.MaterializeFixedScenery(_landmarkDefinitions);
        current.BindMissingFixedConstructionSceneryLinks();
    }

    private void BindOccupationSceneryCurrent(GameState state, SourceWorldContext current)
    {
        if (current.SceneryDefinitions is null)
        {
            current.MaterializeFixedScenery(_landmarkDefinitions);
        }
        current.BindFixedConstructionSceneryLinks();
        //Fixed placement is not the mutable discovery/control flag. A reached
        //unknown saved flag remains unknown rather than inheriting the scenario.
        foreach (var row in _landmarkDefinitions.Catalog.Sceneries)
            current.SetSceneryFlags47(row.Slot, state.LandmarkInvestments?.GetValueOrDefault(row.Slot)?.Flags);
    }

    //257670(Force,Hex,0,FF,0,0): selected-force tracking affects only
    //the ignored return value here. It cannot mark a defeat without param6.
    private GameState ApplyOccupationObjectiveCurrent(GameState state, SourceWorldContext current,
        int force, HexReference hex)
    {
        var queries = new ForceCombatObjectiveCurrent(state, force, current,
            new NormalArmyForceResultDefinitions(current), () => null);
        if (!OccupationKnown(queries.ReadAttackerValid(), "objective_Force_validity") ||
            !OccupationKnown(queries.ReadObjectiveEnabled(), "objective_enabled")) return state;
        int before = OccupationKnown(queries.ReadObjectiveProgress(), "objective_progress");
        if (before >= ForceCombatObjectiveRules.ProgressLimit) return state;
        int target = OccupationKnown(queries.ReadObjectiveTargetLocationId(), "objective_target_Base");
        int targetForce = OccupationKnown(queries.ResolveObjectiveTargetForceId(target), "objective_target_Force");
        if (OccupationKnown(current.ReadHexForceSlot(hex), "objective_Hex_force") != targetForce) return state;
        int areaId = OccupationKnown(current.ReadHexAreaSlot(hex), "objective_Area");
        int rawCity = OccupationKnown(current.ReadAreaCitySlot(new(areaId)), "objective_Area_City");
        var city = current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0) ?? throw OccupationMissing("objective_City_pointer");
        if (OccupationKnown(current.ReadBaseForceSlot(city), "objective_City_force") != targetForce) return state;
        //CE_TC and CE_SC independently supply ordinary Hex1 plus center4.
        //These are offline game rule constants, not scenario defaults.
        if (Seed.Language is not ("TC" or "SC")) throw OccupationMissing("objective_Hex_rule_" + Seed.Language);
        int gain = 1 + (OccupationKnown(current.ReadHexSurfaceCode(hex), "objective_surface") is >= 15 and <= 18 ? 4 : 0);
        int after = Math.Clamp(unchecked(before + gain), 0, ForceCombatObjectiveRules.ProgressLimit);
        var owner = state.Forces.GetValueOrDefault(force) ?? throw OccupationMissing("objective_Force_owner");
        return state with { Forces = new(state.Forces) { [force] = owner with
            { CombatObjective = (owner.CombatObjective ?? throw OccupationMissing("objective_owner")) with { Progress = after } } } };
    }

    //Collect before185D00. Report text, District and advisor are deliberately
    //read later, after the whole transfer and commander reward batch.
    private void CollectOccupationReportsCurrent(GameState state, SourceWorldContext current,
        UnitState army, HexReference hex, List<int> centers, List<int> sceneries)
    {
        int previous = OccupationKnown(current.ReadHexForceSlot(hex), "report_previous_Force");
        bool previousPlayer = TalentForceIsPlayerCurrent(state, current,
            current.ReadForceTableReference(previous <= 51 ? previous : 0)
                ?? throw OccupationMissing("report_previous_Force_pointer"));
        bool actorPlayer = TalentForceIsPlayerCurrent(state, current,
            current.ReadArmyUnitForceReference(new(OccupationKnown(army.NativeArmyOrigin?.NativeSlotIndex, "report_Army")))
                ?? throw OccupationMissing("report_actor_Force_pointer"));
        bool? previousLocal = null;
        bool PreviousLocal()
        {
            if (previousLocal is bool known) return known;
            int area = OccupationKnown(current.ReadHexAreaSlot(hex), "report_Area");
            int raw = OccupationKnown(current.ReadAreaCitySlot(new(area)), "report_Area_City");
            var city = current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw OccupationMissing("report_City_pointer");
            var district = current.ReadBaseDistrictReference(city) ?? throw OccupationMissing("report_City_District");
            return (previousLocal = TalentDistrictIsPlayerRulerCurrent(state, current, district)).Value;
        }
        if (OccupationKnown(current.ReadHexSurfaceCode(hex), "report_surface") is >= 15 and <= 18 &&
            !OccupationArmyIsSpecialCurrent(current, army) &&
            (actorPlayer ? !previousPlayer || PreviousLocal() : previousPlayer && PreviousLocal()))
            centers.Add(OccupationKnown(hex.PhysicalSlot, "report_center_Hex"));
        var scenery = ReadOccupationSceneryCurrent(current, hex);
        if (!OccupationKnown(current.ReadSceneryNativeValidity(scenery), "report_Scenery_validity") ||
            (OccupationKnown(current.ReadSceneryFlags47(scenery), "report_Scenery_flags") & 1) == 0) return;
        bool actorLocal = IsPlayerRulerLedDistrict(state, current, army);
        if (actorLocal ? !previousPlayer || PreviousLocal() : previousPlayer && PreviousLocal())
            sceneries.Add(OccupationKnown(hex.PhysicalSlot, "report_scenery_Hex"));
    }

    private static bool OccupationArmyIsSpecialCurrent(SourceWorldContext current, UnitState army)
    {
        var receiver = new ArmyUnitReference(OccupationKnown(army.NativeArmyOrigin?.NativeSlotIndex, "report_special_Army"));
        int force = OccupationKnown(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(receiver)), "report_special_Force");
        return force == 51 || force is >= 46 and <= 50;
    }

    private static SceneryReference ReadOccupationSceneryCurrent(SourceWorldContext current, HexReference hex)
    {
        int raw = OccupationKnown(current.ReadHexScenerySlot(hex), "Scenery_Hex_link");
        return current.ReadSceneryTableReference(raw <= 100 ? raw : 0) ?? throw OccupationMissing("Scenery_pointer");
    }

    //185D00's shared1B4AB0 action31 home-Army notification.
    private GameState PrepareOccupationSceneryTransferCurrent(GameState state, SourceWorldContext current,
        HexReference hex, int force, int cityId)
    {
        var scenery = ReadOccupationSceneryCurrent(current, hex);
        if (!OccupationKnown(current.ReadSceneryNativeValidity(scenery), "transfer_Scenery_validity") ||
            (OccupationKnown(current.ReadSceneryFlags47(scenery), "transfer_Scenery_flags") & 1) == 0 ||
            force is < 1 or > 45 || cityId is < 1 or > 51) return state;
        var city = current.ReadCityTableReference(cityId) ?? throw OccupationMissing("transfer_Scenery_City");
        if (!OccupationKnown(current.ReadBaseNativeValidity(city), "transfer_Scenery_City_validity")) return state;
        if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(state, current);
        return QueueAbsorptionScenerySpeech(state, current, city, hex,
            OccupationKnown(scenery.PhysicalSlot, "transfer_Scenery_identity"));
    }

    private GameState QueueRegionOccupationSpeechCurrent(GameState state, SourceWorldContext current,
        int unit, int officer, int area, NativeMapSeasonDate? sourceDate = null)
    {
        if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(state, current);
        int person = OccupationKnown(current.ResolveOfficerSource(officer), "center_speaker_pointer");
        var date = sourceDate ?? state.NativeMapSeason?.ThirdDate ?? throw OccupationMissing("center_speaker_date");
        int position = PersonReturnCommands.ResolvePersonPosition(state, current, person, date).PhysicalSlot ?? 48400;
        state = QueueArmyMapSpeechCurrent(state, current, unit, officer, 30, position, contextAreaId: area);
        return state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw OccupationMissing("center_speaker_command_owner") };
    }

    private GameState ConsumeLegacyRegionAnnouncementsCurrent(GameState state, SourceWorldContext current)
    {
        var pending = state.RegionOccupationFollowups ?? throw OccupationMissing("center_followup_owner");
        if (pending.Announcements.Count == 0) return state;
        //Older remake saves retained these source-time announcement operands.
        //Use them once; do not pretend admission already happened.
        foreach (var request in pending.Announcements)
            state = QueueArmyMapSpeechCurrent(state, current, request.UnitId, request.OfficerId, 30,
                request.HexId, contextAreaId: request.AreaId);
        return state with { RegionOccupationFollowups = new(pending.EncirclementSeeds, [], pending.EncirclementPresentations) };
    }

    private static void AppendOccupationAppearanceCurrent(SourceWorldContext current, HexReference hex,
        int force, bool center, List<CampaignEffectRequest> effects)
    {
        if (!OccupationKnown(current.ReadForceNativeValidity(new(force)), "appearance_Force_validity")) return;
        effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.TerritoryOccupation,
            center ? 4 : 5, null, null) { ForceId = force, HexId = OccupationKnown(hex.PhysicalSlot, "appearance_Hex") } });
    }

    private static GameState AppendOccupationEffectsCurrent(GameState state, List<CampaignEffectRequest> effects)
    {
        if (effects.Count == 0) return state;
        var pending = state.CampaignEffectRequests ?? throw OccupationMissing("effects_owner");
        long revision = state.NativeClock?.UpdateOrdinal ?? throw OccupationMissing("effects_clock");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.ArmyMovement, revision, effects) };
    }

    //429370 calls294FD0(Object,Army0,1). The category4 branch has no
    //commander reward/speech/history; it still clears Hex1A, queues the real
    //receiver retirement and submits both destruction presentation intents.
    private GameState DestroyEncirclementObstacleCurrent(GameState state, SourceWorldContext current,
        ObjectReference target, List<CampaignEffectRequest> effects)
    {
        var removed = ApplyDemolitionNumbers(state, current, target, new(0));
        if (removed is null) return state;
        string definition = MapObjectSourceDirectory.DefinitionKey(removed.DefinitionSlot);
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
            removed.Receiver, removed.HexId, definition) });
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
            removed.Receiver, removed.HexId, definition) });
        return removed.State;
    }

    private GameState AppendOrdinaryOccupationHistoryCurrent(GameState state, SourceWorldContext current,
        UnitState army, IReadOnlyList<int> centers, IReadOnlyList<int> sceneries, List<CampaignEffectRequest> effects)
    {
        if (centers.Count == 0 && sceneries.Count == 0) return state;
        var actor = new ArmyUnitReference(OccupationKnown(army.NativeArmyOrigin?.NativeSlotIndex, "history_Army"));
        bool actorPlayer = TalentForceIsPlayerCurrent(state, current,
            current.ReadArmyUnitForceReference(actor) ?? throw OccupationMissing("history_actor_Force"));
        int actorDistrict = ReadFacilityHistoryDistrict(current, actor);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OccupationMissing("history_player");
        var pool = current.ReadOwnedCurrentListPool() ?? throw OccupationMissing("history_pool");
        DistrictReference? primary = null;
        DistrictReference Primary() => primary ??= ForcePrimaryDistrictSource.Resolve(current, player);
        var selector = new PersonDeathHistorySelection(state, current, pool, SearchAbilities(current), _governorInputs,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
        TransferPersonReference? advisor = null;
        int Advisor()
        {
            if (advisor is null)
            {
                _baseCapacityInputs.BindCurrent(state, current);
                advisor = selector.Select(Primary());
            }
            return OccupationKnown(current.ReadPersonIdentity(OccupationKnown(advisor.PhysicalSlot,
                "history_advisor_pointer")), "history_advisor_identity");
        }
        int day = (ReadArmyDestructionSampler(state, current) ?? throw OccupationMissing("history_day")).Day450;
        int commander = OccupationKnown(current.ReadArmyUnitCommanderId(actor), "history_Commander");
        foreach (int h in centers)
        {
            var hex = new HexReference(h);
            int area = OccupationKnown(current.ReadHexAreaSlot(hex), "history_Area");
            var district = AreaDistrict(area);
            int districtId = OccupationKnown(district.PhysicalSlot, "history_Area_District");
            int ruler = OccupationKnown(current.ReadMoraleWaveRuler22EAC0(current.ReadArmyUnitForceReference(actor) ?? throw OccupationMissing("history_ruler_Force")), "history_ruler");
            if (actorPlayer)
            {
                string title = BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, District(actorDistrict), OccupationMissing)
                    ? Format(0x5F6B, new(Army0: actor, Area0: area, Base0: checked((ushort)
                        OccupationKnown(current.ReadAreaCitySlot(new(area)), "history_title_City"))))
                    : CampaignHistoryWriter.Supplemental(state) ?? throw OccupationMissing("history_empty_title");
                string main = Format(0x5365, new(Person0: commander, Area0: area, Person1: ruler));
                state = WriteHistory(state, current, new(5, actorDistrict, h, day, title, main, commander, MarkBit4: 1), effects);
            }
            else
            {
                string title = Format(0x5F6C, new(Army0: actor, Force0: current.ReadArmyUnitRawForceSlot(actor), Area0: area, District0: districtId));
                int reporter = Advisor();
                string main = Format(0x536C, new(Force0: current.ReadArmyUnitRawForceSlot(actor), Person0: reporter, Area0: area,
                    Person1: ruler, Person2: commander));
                state = WriteHistory(state, current, new(5, districtId,
                    h, day, title, main, reporter, MarkBit0: 1, MarkBit4: 1), effects);
            }
        }
        bool actorLocal = IsPlayerRulerLedDistrict(state, current, army);
        foreach (int h in sceneries)
        {
            int area = OccupationKnown(current.ReadHexAreaSlot(new(h)), "scenery_history_Area");
            if (!actorLocal)
            {
                //The source selects the player advisor even if no site's
                //post-transfer City still passes the local-District gate.
                int reporter = Advisor();
                if (!OccupationKnown(current.ReadForceNativeValidity(player), "scenery_history_player_validity") ||
                    !OccupationKnown(current.ReadPersonNativeValidity(OccupationKnown(advisor?.PhysicalSlot, "scenery_history_advisor")), "scenery_history_advisor_validity") ||
                    !OccupationKnown(current.ReadArmyUnitNativeValidity(actor), "scenery_history_Army_validity")) break;
                if (!BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, AreaDistrict(area), OccupationMissing)) continue;
                string main = Format(0x349F, new(Force0: current.ReadArmyUnitRawForceSlot(actor), String0: SceneryName(h)));
                state = WriteHistory(state, current, new(5, OccupationKnown(Primary().PhysicalSlot, "scenery_history_primary"),
                    h, day, main, CampaignHistoryWriter.Supplemental(state), reporter, MarkBit4: 1), effects);
            }
            else
            {
                string main = Format(0x349E, new(String0: SceneryName(h)));
                state = WriteHistory(state, current, new(5, actorDistrict, h, day, main,
                    CampaignHistoryWriter.Supplemental(state), commander, MarkBit4: 1), effects);
            }
        }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });

        DistrictReference District(int raw) => current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw OccupationMissing("history_District_pointer");
        DistrictReference AreaDistrict(int area)
        {
            int raw = OccupationKnown(current.ReadAreaCitySlot(new(area)), "history_City_slot");
            var city = current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw OccupationMissing("history_City_pointer");
            return current.ReadBaseDistrictReference(city) ?? throw OccupationMissing("history_City_District");
        }
        string SceneryName(int h)
        {
            int id = OccupationKnown(ReadOccupationSceneryCurrent(current, new(h)).PhysicalSlot, "history_Scenery");
            return current.SceneryDefinitions?.Sceneries[id].Name ?? throw OccupationMissing("history_Scenery_name");
        }
        string Format(int root, HistoryMessageOperands operands)
        {
            var result = FormatOccupationReport(state, current, root, operands);
            state = result.State;
            return result.Text;
        }
    }
}
