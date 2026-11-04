using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private readonly LoadedSceneries _landmarkDefinitions;
    private readonly LandmarkDevelopmentRules _landmarkDevelopment;
    private GameState InstallFreshLandmarkInvestments(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.LandmarkInvestments is not null)
            throw new InvalidDataException("Landmark construction needs a fresh owner.");
        state = _landmarkDevelopment.InstallFreshParameters(state);
        var epoch = state.NativeArmySimulation!.World.SamplerEpoch ?? throw OccupationMissing("landmark_fresh_epoch");
        var sampler = new TacticSourceSamplerWorld(epoch.Day450, epoch.Salt45C);
        var current = CaptureWritableTransportRouteCurrent(state);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        // This fresh-only producer has no saved bank yet. Borrow its actual
        // resident scenario flags explicitly; restores keep missing flags null.
        current.MaterializeStaticSceneryState(_scenario);
        var pool = current.ReadOwnedCurrentListPool() ?? throw OccupationMissing("landmark_fresh_indexes");
        current.SetRegistryDescriptor(RegistryKind.Scenery, new(pool.AllocateDescriptor().Identity));
        var values = new Dictionary<int, LandmarkInvestmentState>();
        for (int i = 0; i < 101; i++)
        {
            var reference = current.ReadSceneryTableReference(i) ?? throw OccupationMissing("landmark_fresh_pointer");
            bool valid = OccupationKnown(current.ReadSceneryNativeValidity(reference), "landmark_fresh_validity");
            byte phase = valid && (OccupationKnown(current.ReadSceneryFlags47(reference), "landmark_fresh_flags") & 1) != 0
                ? (byte)1 : (byte)0;
            //The fresh-only4C6F20 loop visits physical slots in order. Future
            //slots still have the constructor's zero type when counts are read.
            var development = phase == 1 ? _landmarkDevelopment.Initialize(state, i,
                OccupationKnown(current.ReadSceneryHexIndex(reference), "landmark_fresh_hex"),
                values.Values.Where(value => value.Phase == 1).Select(value => value.Development!), sampler)
                : new LandmarkDevelopmentState(0, 0, 0);
            values.Add(i, new(phase, 0, 0) { Development = development,
                Flags = OccupationKnown(current.ReadSceneryFlags47(reference), "landmark_fresh_flags") });
            _ = ObjectRegistration.Register(new(RegistryKind.Scenery, i), current);
        }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() })
            with { LandmarkInvestments = values };
    }

    /// <summary>The final457250 manager. A due investment updates its owned
    /// level/effect and report atomically. Max-level event execution remains a
    /// separate required consumer, never replaced by an invented notification.</summary>
    public SubdayStatusCounterResult TryAdvanceLandmarkInvestmentTimers(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != 2 || moment.Boundary != NativeArmyProgressStageBoundary.AfterArmyWaiting ||
            !NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(moment.PreWriterThirdDate))
            throw new InvalidDataException("Landmark investment timers require the post-Army stage2 boundary.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            current.MaterializeFixedScenery(_landmarkDefinitions);
            var working = state;
            var registry = state.ReadArmyLifecycle()?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Landmark)
                ?? throw OccupationMissing("landmark_registry");
            var investments = state.LandmarkInvestments is { } owned ? new Dictionary<int, LandmarkInvestmentState>(owned)
                : throw OccupationMissing("landmark_saved_investment_owner");
            foreach (var entry in registry)
            {
                if (entry.Kind != WorldEntityKind.Landmark || entry.Id is not int id) throw OccupationMissing("landmark_registry_identity");
                var landmark = current.ReadSceneryTableReference(id) ?? throw OccupationMissing("landmark_pointer");
                if (!OccupationKnown(current.ReadSceneryNativeValidity(landmark), "landmark_validity") ||
                    (OccupationKnown(current.ReadSceneryFlags47(landmark), "landmark_flags") & 1) == 0) continue;
                var investment = investments.GetValueOrDefault(id) ?? throw OccupationMissing("landmark_investment");
                if (investment.Phase is not (2 or 3) || investment.CompletionDay <= 0 || investment.AssignedOfficerId == 0) continue;
                var date = moment.PreWriterThirdDate;
                int day = date.Day - 31 + (date.Month + date.Year * 12) * 30;
                if (day < investment.CompletionDay)
                {
                    if (investment.Phase == 2) investments[id] = investment with { Phase = 3 };
                    continue;
                }
                var development = _landmarkDevelopment.Advance(working,
                    investment.Development ?? throw OccupationMissing("landmark_saved_development"));
                investments[id] = investment with { Phase = development.Level < 6 ? (byte)1 : (byte)4,
                    AssignedOfficerId = 0, CompletionDay = 0, Development = development };
                working = working with { LandmarkInvestments = investments };
                working = AddLandmarkCompletionReport(working, current, landmark, development.Level, moment);
                if (development.Level == 6)
                {
                    working = CompleteMaximumLandmark(working with { LandmarkInvestments = investments }, current, landmark, id, moment);
                    investments = new(working.LandmarkInvestments!);
                }
            }
            return new(working with { LandmarkInvestments = investments }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private GameState AddLandmarkCompletionReport(GameState state,
        SourceWorldContext current, SceneryReference landmark, byte level, NativeArmyProgressStageMoment moment)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OccupationMissing("landmark_player");
        int playerId = OccupationKnown(current.ReadForcePhysicalSlot(player), "landmark_player_identity");
        var primary = current.ReadDistrictTableReference(0) ?? throw OccupationMissing("landmark_primary_fallback");
        if (playerId is >= 1 and <= 51)
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw OccupationMissing("landmark_district_registry"))
            {
                var district = entry ?? throw OccupationMissing("landmark_district_pointer");
                int physical = OccupationKnown(district.PhysicalSlot, "landmark_district_identity");
                if (OccupationKnown(current.ReadDistrictForceId(physical), "landmark_district_force") != playerId) continue;
                int ruler = OccupationKnown(current.ReadForceRecord(playerId)?.RawRuler10, "landmark_player_ruler");
                if (OccupationKnown(current.ReadDistrictLeader(physical), "landmark_district_leader") == ruler ||
                    OccupationKnown(current.ReadDistrictNumber(physical), "landmark_district_number") == 1) { primary = district; break; }
            }
        if (!OccupationKnown(current.ReadDistrictNativeValidity(primary), "landmark_primary_validity")) return state;
        int primaryId = OccupationKnown(primary.PhysicalSlot, "landmark_primary_identity");
        int hexId = OccupationKnown(current.ReadSceneryHexIndex(landmark), "landmark_report_hex");
        var hex = current.ReadHexTableReference(hexId < 48400 ? hexId : 0) ?? throw OccupationMissing("landmark_report_hex_pointer");
        int owner = hex.PhysicalSlot is null ? 0 : OccupationKnown(current.ReadHexForceSlot(hex), "landmark_owner");
        int force = OccupationKnown(current.ReadDistrictForceId(primaryId), "landmark_primary_force");
        if (force != owner) return state;
        //22AA10: the area's attached city is available only while the landmark
        //and the area's representative hex have the same actual owner.
        int rawArea = hex.PhysicalSlot is null ? 0 : OccupationKnown(current.ReadHexAreaSlot(hex), "landmark_area");
        var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw OccupationMissing("landmark_area_pointer");
        int areaHex = OccupationKnown(current.ReadAreaHexIndex(area), "landmark_area_center");
        var center = current.ReadHexTableReference(areaHex < 48400 ? areaHex : 0) ?? throw OccupationMissing("landmark_area_center_pointer");
        int cityId = OccupationKnown(current.ReadHexForceSlot(center), "landmark_area_owner") == owner
            ? OccupationKnown(current.ReadAreaCitySlot(area), "landmark_city") : 0;
        var city = current.ReadCityTableReference(cityId < 52 ? cityId : 0) ?? throw OccupationMissing("landmark_city_pointer");
        var cityDistrict = current.ReadCityDistrictReference(city) ?? throw OccupationMissing("landmark_city_district");
        if (!OccupationKnown(current.ReadDistrictNativeValidity(cityDistrict), "landmark_city_district_validity")) return state;
        //233940→233A90 requires a valid player force and a valid district
        //leader whose current service status is ruler(1).
        if (!OccupationKnown(current.ReadForceNativeValidity(player), "landmark_player_validity") ||
            OccupationKnown(current.ReadDistrictForceSlot(cityDistrict), "landmark_city_force") != playerId) return state;
        int leader = OccupationKnown(current.ReadDistrictLeader(OccupationKnown(cityDistrict.PhysicalSlot, "landmark_city_district_identity")), "landmark_city_leader");
        int leaderPhysical = Person(leader);
        if (!OccupationKnown(current.ReadPersonNativeValidity(leaderPhysical), "landmark_city_leader_validity") ||
            OccupationKnown(current.ReadPersonStatus(leaderPhysical), "landmark_city_leader_status") != 1) return state;
        int advisorId = OccupationKnown(current.ReadAssignmentForceAdvisor52(force < 52 ? force : 0), "landmark_advisor");
        int advisor = Person(advisorId);
        if (!OccupationKnown(current.ReadPersonNativeValidity(advisor), "landmark_advisor_validity"))
            throw OccupationMissing("landmark_advisor_reselection");
        int physicalLandmark = OccupationKnown(landmark.PhysicalSlot, "landmark_report_scenery_identity");
        var definitions = current.SceneryDefinitions ?? throw OccupationMissing("landmark_report_scenery_names");
        if ((uint)physicalLandmark >= (uint)definitions.Sceneries.Count)
            throw OccupationMissing("landmark_report_scenery_name_lookup");
        string name = definitions.Sceneries[physicalLandmark].Name ?? throw OccupationMissing("landmark_report_scenery_name");
        //33DCE0 materializes349D first, then binds the advisor/ruler and
        //new level for53D4. The second text is the real supplemental operand.
        var main = FormatHistoryReport(state, current, 0x349D, new HistoryMessageOperands(String0: name));
        state = main.State;
        int advisorIdentity = OccupationKnown(current.ReadPersonIdentity(advisor), "landmark_advisor_format_identity");
        byte rawForce = OccupationKnown(current.ReadDistrictForceSlot(primary), "landmark_report_fresh_force");
        var reportForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
            ?? throw OccupationMissing("landmark_report_fresh_force_pointer");
        int currentRuler = OccupationKnown(current.ReadMoraleWaveRuler22EAC0(reportForce), "landmark_report_fresh_ruler");
        var supplemental = FormatHistoryReport(state, current, 0x53D4,
            new HistoryMessageOperands(Person0: advisorIdentity, Person1: currentRuler, String0: name, Integers: [level]));
        state = supplemental.State;
        int reportHex = OccupationKnown(current.ReadSceneryHexIndex(landmark), "landmark_report_fresh_hex44");
        int payload = OccupationKnown(current.ReadPersonIdentity(advisor), "landmark_report_payload10");
        int reportDistrict = OccupationKnown(current.ReadDistrictPhysicalSlot(primary), "landmark_report_District_ordinal");
        var requests = new List<CampaignEffectRequest>();
        state = WriteHistory(state, current, new ReportCommand(1, reportDistrict, reportHex, 0, main.Text,
            supplemental.Text, payload, MarkBit0: 1, MarkBit4: 1), requests);
        return requests.Count == 0 ? state : state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw OccupationMissing("landmark_report_effect_owner"))
                .Append(CampaignEffectSource.LandmarkInvestment, moment.CalendarCycleOrdinal, requests) };
        int Person(int raw) => OccupationKnown((raw <= 6000 ? current.ReadPersonLookupReference(raw) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "landmark_person_pointer");
    }
}
