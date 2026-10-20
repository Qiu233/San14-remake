using San14.Core;

namespace San14.Data;

public sealed record SubdayStatusCounterResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Current Army registry order for the existing ordinary counter writer.
    /// A valid pending image needs its own lifecycle writer and is not silently omitted.</summary>
    public IReadOnlyList<int>? ReadOrdinaryWaitingArmyOrder(GameState state)
    {
        var lifecycle = state.ReadArmyLifecycle();
        if (lifecycle?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Army) is not { } registry) return null;
        var order = new List<int>();
        var slots = lifecycle.ReceiverSlots();
        foreach (var receiver in registry)
        {
            if (receiver.Kind != WorldEntityKind.Army || receiver.Id is not int id) return null;
            if (state.Units.TryGetValue(id, out var unit))
            {
                if (unit.NativeTacticRuntime is null) return null;
                order.Add(id);
            }
            else
            {
                int? physical = WorldIndexContext.FindArmyPhysical(state, id);
                if (physical is null || slots[physical.Value].Commander12 is not ushort commander ||
                    slots[physical.Value].Status10 is not byte status) return null;
                if (commander != 0 && status != 0) return null;
            }
        }
        return order.Count == state.Units.Count && order.Distinct().Count() == order.Count ? order : null;
    }

    /// <summary>TC302290, after the entire Army manager. All bases and their
    /// expiry reports are staged together; no presentation is performed.</summary>
    public SubdayStatusCounterResult TryAdvanceBaseStatusCounters(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != 2 || moment.Boundary != NativeArmyProgressStageBoundary.AfterArmyWaiting ||
            !NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(moment.PreWriterThirdDate))
            throw new InvalidDataException("Base counters require the reached post-Army stage2 boundary.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            CaptureBaseThreatInputs.BindCurrent(state, current);
            var reports = new List<PeriodReportEntry>();
            foreach (var target in current.ReadRegisteredBases() ?? throw OccupationMissing("counter_Base_registry"))
            {
                if (target is null) throw OccupationMissing("counter_Base_pointer");
                if (!OccupationKnown(current.ReadBaseNativeValidity(target), "counter_Base_validity")) continue;
                int location = OccupationKnown(current.ReadBaseObjectLink(target), "counter_Base_location");
                if (location is < 0 or > 61) continue;
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OccupationMissing("counter_player");
                int playerPhysical = OccupationKnown(current.ReadForcePhysicalSlot(player), "counter_player_identity");
                int force = OccupationKnown(current.ReadBaseForceSlot(target), "counter_Base_force");
                var fields = current.ReadCaptureBaseThreatFields(target) ?? throw OccupationMissing("counter_Base_fields");
                byte[] before = [OccupationKnown(fields.Byte49, "counter_Base_confusion"),
                    OccupationKnown(fields.Byte4A, "counter_Base_second_abnormal"), OccupationKnown(fields.Byte4B, "counter_Base_provocation")];
                var after = before.Select(value => TacticEffectSourceTickMath.DecayAbnormal(value,
                    CounterParameter(state, SimulationParameterKeys.SubdayStatus.BaseAbnormalUpper))).ToArray();
                string textKind = "base_status_expired_without_text";
                for (int i = 0; i < 3; i++)
                    if (force == playerPhysical && before[i] != 0 && after[i] == 0)
                    {
                        int district = OccupationKnown(current.ReadBaseDistrictSlot(target), "counter_Base_report_district");
                        var obj = current.ReadObjectTableReference(location) ?? throw OccupationMissing("counter_Base_report_object");
                        int hex = OccupationKnown(current.ReadObjectNativeValidity(obj), "counter_Base_report_object_validity")
                            ? OccupationKnown(current.ReadObjectHexIndex(obj), "counter_Base_report_hex") : 48400;
                        // ReportPool clamps the sentinel map cell to48399.
                        hex = Math.Clamp(hex, 0, 48399);
                        int governor = OccupationKnown(current.ReadArmyReturnBaseGovernor2E(target), "counter_Base_report_governor");
                        if (!state.Forces.ContainsKey(force) || governor != 0 && !state.Officers.ContainsKey(governor))
                            throw OccupationMissing("counter_Base_report_domain_identity");
                        // One buffer spans all three counters. Index1 retains
                        // index0's text if emitted, otherwise its empty buffer.
                        if (i == 0) textKind = "base_confusion_expired";
                        if (i == 2) textKind = "base_provocation_expired";
                        //Source generic speaker5012 is presentation metadata,
                        //not a fabricated officer in the campaign roster.
                        reports.Add(new(textKind, force, OfficerId: governor == 0 ? null : governor,
                            Amount: i, ToHexId: hex) { LocationId = location, DistrictId = district == 0 ? null : district,
                            CampaignDay = StatusCampaignDay(state, moment), RuleId = "army.progress.base-status-expiry" });
                    }
                int cooldown = OccupationKnown(fields.Word4C, "counter_Base_cooldown");
                int cooldownUpper = CounterParameter(state, SimulationParameterKeys.SubdayStatus.BaseCooldownUpper);
                int reduced = cooldown - 1;
                ushort nextCooldown;
                if (reduced >= 0 && Math.Min(reduced, cooldownUpper) < 0) nextCooldown = 0;
                else nextCooldown = unchecked((ushort)Math.Min(Math.Max(reduced, 0), cooldownUpper));
                current.SetCaptureBaseThreatFields(target, fields with { Byte49 = after[0], Byte4A = after[1],
                    Byte4B = after[2], Word4C = nextCooldown });
            }
            var result = CaptureBaseThreatInputs.PublishCurrent(state, current);
            return new(result with { PendingCommandReports = [.. result.PendingCommandReports, .. reports] }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    /// <summary>TC457AE0 at6h. Reuses the saved rumor bytes that district
    /// transfer already clears; a save missing those values remains unresolved.</summary>
    public SubdayStatusCounterResult TryAdvanceOfficerRumorCounters(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != 2 || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Officer rumor counters require stage2 entry.");
        if (moment.PreWriterThirdDate.Hour != 6) return new(state, null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            var registry = state.ReadArmyLifecycle()?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Officer)
                ?? throw OccupationMissing("rumor_Person_registry");
            var simulation = state.NativeArmySimulation ?? throw OccupationMissing("rumor_saved_owner");
            var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
            var reports = new List<PeriodReportEntry>();
            foreach (var entry in registry)
            {
                if (entry.Kind != WorldEntityKind.Officer || entry.Id is not int id) throw OccupationMissing("rumor_Person_identity");
                var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw OccupationMissing("rumor_Person_pointer");
                int physical = OccupationKnown(person.PhysicalSlot, "rumor_Person_receiver");
                if (!OccupationKnown(current.ReadPersonNativeValidity(physical), "rumor_Person_validity")) continue;
                ushort counters = OccupationKnown(current.ReadPersonResetWord1FF(physical), "rumor_Person_counters");
                byte first = (byte)counters, second = (byte)(counters >> 8);
                if (first != 0)
                {
                    first = Decay(first);
                    if (first == 0)
                    {
                        var force = current.ReadPersonForce(physical) ?? throw OccupationMissing("rumor_current_force");
                        bool own = IsPlayer(force);
                        if (!own)
                        {
                            byte former = OccupationKnown(current.ReadPersonResetByte1FE(physical), "rumor_source_force");
                            force = current.ReadForceTableReference(former < 52 ? former : 0) ?? throw OccupationMissing("rumor_source_force_pointer");
                        }
                        if (own || IsPlayer(force))
                        {
                            int forceId = OccupationKnown(current.ReadForcePhysicalSlot(force), "rumor_report_force");
                            reports.Add(new(own ? "officer_rumor_expired" : "foreign_officer_rumor_expired", forceId, OfficerId: id,
                                ToHexId: 0) { DistrictId = PrimaryDistrict(force), CampaignDay = StatusCampaignDay(state, moment),
                                RuleId = "army.progress.officer-rumor-expiry" });
                        }
                        values[PersonAssignmentPrecallInputs.PersonResetByte1FEKey(physical)] = 0;
                    }
                }
                if (second != 0) second = Decay(second);
                values[PersonAssignmentPrecallInputs.PersonResetWord1FFKey(physical)] = first | second << 8;
            }
            return new(state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } },
                PendingCommandReports = [.. state.PendingCommandReports, .. reports] }, null);

            byte Decay(byte value) => (byte)Math.Clamp(unchecked(value -
                CounterParameter(state, SimulationParameterKeys.SubdayStatus.OfficerRumorDecay)), 0, 255);
            bool IsPlayer(ForceReference force)
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OccupationMissing("rumor_player");
                int identity = OccupationKnown(current.ReadForcePhysicalSlot(player), "rumor_player_identity");
                return identity != 0 && identity == OccupationKnown(current.ReadForcePhysicalSlot(force), "rumor_candidate_identity");
            }
            int PrimaryDistrict(ForceReference force)
            {
                int identity = OccupationKnown(current.ReadForcePhysicalSlot(force), "rumor_report_force_identity");
                int ruler = identity == 0 ? 5014 : OccupationKnown(current.ReadForceRecord(identity)?.RawRuler10, "rumor_report_ruler");
                foreach (var district in current.ReadArmyReturnRegisteredDistricts() ?? throw OccupationMissing("rumor_report_district_registry"))
                {
                    int at = OccupationKnown(district?.PhysicalSlot, "rumor_report_district");
                    if (OccupationKnown(current.ReadDistrictForceId(at), "rumor_report_district_force") != identity) continue;
                    if (OccupationKnown(current.ReadDistrictLeader(at), "rumor_report_district_leader") == ruler ||
                        OccupationKnown(current.ReadDistrictNumber(at), "rumor_report_district_number") == 1) return at;
                }
                return OccupationKnown(current.ReadDistrictTableReference(0)?.PhysicalSlot, "rumor_report_district_fallback");
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static int CounterParameter(GameState state, string key) => state.NativeArmySimulation?.Parameters is { } bank &&
        !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value) ? value : throw OccupationMissing(key);

    private static int StatusCampaignDay(GameState state, NativeArmyProgressStageMoment moment)
    {
        var season = state.NativeMapSeason ?? throw OccupationMissing("counter_calendar");
        var epoch = state.NativeArmySimulation?.World.SamplerEpoch ?? throw OccupationMissing("counter_epoch");
        if (moment.CalendarCycleOrdinal != season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock ||
            moment.CycleWithinAtomicBlock is < 0 or >= NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock)
            throw OccupationMissing("counter_calendar_boundary");
        var date = season.ThirdDate; long days = season.AdvancedDays;
        for (int i = 0; i < moment.CycleWithinAtomicBlock; i++)
        { var next = MapSeasonRuntime.StepCalendarWriter(date); date = next.Date; if (next.DayAdvanced) days++; }
        if (date != moment.PreWriterThirdDate) throw OccupationMissing("counter_calendar_date");
        return unchecked(epoch.Day450 + (int)days);
    }
}
