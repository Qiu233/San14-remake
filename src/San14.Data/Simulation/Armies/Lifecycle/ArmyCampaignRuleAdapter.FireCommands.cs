using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyFireCommandTrial(int UnitId, int TargetHexId, int Rate, int Sample,
    bool Success, bool Backfire, int? Duration);
public sealed record ArmyFireCommandStageResult(GameState? State, ArmyFireCommandTrial[] Trials,
    HexFireChange[] Changes, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached10-hour fire-command pass, in saved manager order.
    /// Ordinary Army/City/Gate and ground targets share one private transaction;
    /// unresolved special/reporting tails reject all earlier successful casts.</summary>
    public ArmyFireCommandStageResult TryAdvanceOrdinaryFireCommands(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.FireCommands ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Fire commands require source stage21 entry.");
        if (moment.PreWriterThirdDate.Hour / 2 != 5) return new(state, [], [], null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            CaptureBaseThreatInputs.BindCurrent(state, current);
            //Same ordinary application lifetime as CaptureOwnedArmyReturnWorld:
            //this client owns no native challenge/tutorial570850 receiver.
            current.ArmyReturnTransferMode = new(false, null);
            var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs, Seed.Language, SimulationRuleIdentity.Version);
            var order = queries.ReadRegistry() ?? throw OccupationMissing("fire_command_registry");
            var working = state;
            var changes = new List<HexFireChange>();
            var trials = new List<ArmyFireCommandTrial>();
            var announcements = new List<ArmyFireAnnouncement>(state.PendingArmyFireAnnouncements);
            var casts = new List<ArmyFireCastPresentation>(state.PendingArmyFireCasts);
            var reports = new List<PeriodReportEntry>(state.PendingCommandReports);
            TacticSourceSamplerWorld? sampler = null;
            foreach (int physical in order)
            {
                if (OccupationKnown(queries.IsAtHome2C96E0(physical), "fire_command_home") ||
                    OccupationKnown(queries.IsExcluded233560(physical), "fire_command_excluded")) continue;
                int unitId = ArmyDestructionQueue.ResolveUnitId(working, physical) ?? throw OccupationMissing("fire_command_unit");
                if (!working.Units.TryGetValue(unitId, out var unit)) throw OccupationMissing("fire_command_active_unit");
                if (OccupationKnown(ArmyConfigurationSourceAdapter.ReadAction(working, unit), "fire_command_action") != 16) continue;
                var runtime = unit.NativeTacticRuntime ?? throw OccupationMissing("fire_command_abnormal");
                if (runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0) continue;
                var target = OwnedArmyLifecycleTargets.Read(working, working.ReadArmyLifecycle()!.Slots[physical].ReceiverId)
                    ?? throw OccupationMissing("fire_command_goal");
                int goal = Goal(OccupationKnown(target.Kind38, "fire_command_goal_kind"), OccupationKnown(target.Identity3A, "fire_command_goal_value"));
                goal = goal <= 48399 ? goal : 0;
                if (!Hex(goal).PhysicalSlot.HasValue) continue;
                BaseReference city = City(goal);
                if (OccupationKnown(current.ReadBaseNativeValidity(city), "fire_command_city_validity")) goal = current.ReadBaseCurrentHexIndex(city);
                int link = OccupationKnown(current.ReadHexObjectLink(Hex(goal)), "fire_command_gate_link");
                BaseReference gate = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0) ?? throw OccupationMissing("fire_command_gate");
                if (OccupationKnown(current.ReadBaseNativeValidity(gate), "fire_command_gate_validity")) goal = current.ReadBaseCurrentHexIndex(gate);
                if (OccupationKnown(current.ReadCaptureHexWord16(goal), "fire_command_existing_fire") != 0) continue;
                var waypoints = ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(unit.March) ?? throw OccupationMissing("fire_command_waypoints");
                if (waypoints.Any(value => value != 48400)) continue;
                //22FC70's actual Object lookup and asymmetric force permission
                //precede distance. Definition category4 bypasses this gate.
                int objectLink = OccupationKnown(current.ReadHexObjectLink(Hex(goal)), "fire_command_object_link");
                var obj = current.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0) ?? throw OccupationMissing("fire_command_object");
                if (OccupationKnown(current.ReadObjectNativeValidity(obj), "fire_command_object_validity"))
                {
                    int kind = OccupationKnown(current.ReadPhysicalObject(OccupationKnown(obj.PhysicalSlot, "fire_command_object_physical"))?.RawField12, "fire_command_object_kind");
                    var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0) ?? throw OccupationMissing("fire_command_object_definition");
                    if (OccupationKnown(current.ReadObjectDefinitionValidity(definition), "fire_command_definition_validity") &&
                        OccupationKnown(current.ReadObjectDefinitionRaw2C(definition), "fire_command_definition_category") != 4 &&
                        !OccupationKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(working, unit.ForceId,
                            OccupationKnown(current.ReadObjectForceSlot13(obj), "fire_command_object_force")), "fire_command_object_permission")) continue;
                }
                city = City(goal);
                int distance = OccupationKnown(current.ReadBaseNativeValidity(city), "fire_command_distance_city") ? 2 : 1;
                int ownHex = OccupationKnown(ArmyMarchSourceAdapter.ReadCurrentHex(working, unit), "fire_command_position");
                if (FireCommandDistance(ownHex, goal) != distance) continue;
                var army = current.ReadArmyUnitTableReference(physical) ?? throw OccupationMissing("fire_command_army");
                if (!OccupationKnown(current.ReadArmyUnitNativeValidity(army), "fire_command_army_validity")) continue;
                int person = OccupationKnown(current.ReadPersonLookupReference(unit.OfficerId <= 6000 ? unit.OfficerId : 0)?.PhysicalSlot, "fire_command_person");
                if (!OccupationKnown(current.ReadPersonNativeValidity(person), "fire_command_person_validity")) continue;
                int occupant = unchecked((short)OccupationKnown(current.ReadHexRaw1C(Hex(goal)), "fire_command_target_army"));
                //2C6D20 only admits signed1C1..500. Army0/negative values do
                //not select an occupant; validity is a separate later query.
                ArmyUnitReference? targetArmy = null;
                if (occupant is >= 1 and <= 500)
                {
                    var candidate = current.ReadArmyUnitTableReference(occupant) ?? throw OccupationMissing("fire_command_target_army_pointer");
                    if (OccupationKnown(current.ReadArmyUnitNativeValidity(candidate), "fire_command_target_army_validity")) targetArmy = candidate;
                }
                var surface = Surface(goal);
                var defense = ReadFireCommandDefense(working, current, obj, targetArmy, person, goal, moment, reports, P);
                int? intelligence = null;
                int Intelligence() => intelligence ??= FireCommandIntelligence(working, current, person);
                int rate;
                if (defense.Protected) rate = 0;
                else if (OccupationKnown(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 25), "fire_command_trait25")) rate = 100;
                else
                {
                    if (OccupationKnown(TacticRemakeCurrentBattleControl.Read(working, 69), "fire_command_tutorial") )
                        throw OccupationMissing("fire_command_tutorial_selected_army");
                    int baseline = Math.Max(unchecked(Intelligence() / 2 - defense.Intelligence / 2 +
                        TerrainRate(surface.RawField41, false) + defense.AbnormalBonus), P(SimulationParameterKeys.FireCommand.MinimumRate));
                    //22A200(selector1) only accepts the Person's current Army
                    //when its commander is the same Person.
                    int position = OccupationKnown(current.ReadPersonSecondLocation(person), "fire_command_person_position");
                    int associated = position is >= 62 and <= 561 ? position - 61 : 0;
                    var associatedArmy = current.ReadArmyUnitTableReference(associated) ?? throw OccupationMissing("fire_command_associated_army");
                    int correction = 0;
                    if (OccupationKnown(current.ReadArmyUnitNativeValidity(associatedArmy), "fire_command_associated_validity") &&
                        OccupationKnown(current.ReadArmyUnitCommanderId(associatedArmy), "fire_command_associated_commander") == unit.OfficerId)
                    {
                        int[] traits = TacticRemakeCurrentTraits.ArmyContributions(current, person,
                            current.ReadArmyReturnTraitFields(associatedArmy)?.Traits64) ?? throw OccupationMissing("fire_command_trait26");
                        correction = unchecked(traits[26] * P(SimulationParameterKeys.FireCommand.TraitRateScale) * baseline) / 100;
                    }
                    rate = unchecked(baseline + correction + OccupationKnown(_baseCapacityInputs.ReadPanelModifier(working, unit.ForceId, 47), "fire_command_panel47"));
                }
                sampler ??= ReadFireStageSampler(state, moment);
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, goal, unchecked(2 * ownHex), unit.OfficerId, 0, sampler);
                bool success = sample <= rate, backfire = false;
                int? ignition = null, duration = null;
                casts.Add(new(unit.Id, unit.OfficerId, ownHex)); // Unplayed effect56 intent.
                if (success)
                {
                    var merit = _pendingMerit.TryApplyWithinArmyOperation(working, current, unit.OfficerId,
                        unchecked(P(SimulationParameterKeys.FireCommand.SuccessMerit) * 10));
                    if (merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw OccupationMissing("fire_command_pending_merit");
                    working = merit.State;
                    ignition = goal;
                }
                else
                {
                    if ((Surface(ownHex).RawField2C & 3) != 0)
                    {
                        int backfireDefense = ReadFireCommandBackfireDefense(working, current, obj, targetArmy);
                        int backfireRate = Math.Max(unchecked(TerrainRate(surface.RawField41, true) / 5 +
                            unchecked(P(SimulationParameterKeys.FireCommand.BackfireDefenseScale) * backfireDefense) / 100 - Intelligence() / 2 +
                            P(SimulationParameterKeys.FireCommand.BackfireBase)), P(SimulationParameterKeys.FireCommand.BackfireMinimum));
                        //The second pure call has exactly the same arguments.
                        backfire = sample <= backfireRate;
                        if (backfire) ignition = ownHex;
                    }
                    var morale = ArmyMoraleNonzeroCurrent.Apply(working, current, army, -P(SimulationParameterKeys.FireCommand.FailureMorale));
                    if (morale.NativeReturn is null || morale.Receipt is null) throw OccupationMissing(morale.UnresolvedOwner ?? "fire_command_morale");
                    working = working with { Units = new(working.Units) { [unit.Id] = ArmyMoraleSourceAdapter.WithCurrent(unit, morale.Receipt.After1A) } };
                }
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw OccupationMissing("fire_command_player");
                bool playerValid = OccupationKnown(current.ReadForceNativeValidity(player), "fire_command_player_validity");
                int district = OccupationKnown(current.ReadPersonDistrictSlot(person), "fire_command_district");
                var districtRef = current.ReadDistrictTableReference(district <= 51 ? district : 0) ?? throw OccupationMissing("fire_command_district_pointer");
                if (playerValid && OccupationKnown(current.ReadDistrictForceSlot(districtRef), "fire_command_district_force") == player.PhysicalSlot)
                {
                    byte? choice = success ? unit.NativeCommandChoice41 ?? throw OccupationMissing("fire_command_choice41") : null;
                    reports.Add(new(success ? "fire_command_succeeded" : backfire ? "fire_command_backfired" : "fire_command_failed",
                        unit.ForceId, OfficerId: unit.OfficerId, FromHexId: ownHex, ToHexId: ownHex)
                    { UnitId = unit.Id, DistrictId = district, CampaignDay = sampler.Day450, CommandChoiceId = choice,
                        RuleId = "army.progress.fire-command" });
                }
                else if (success)
                {
                    //C354/C355 additionally choose the current player speaker.
                    //Until that writer is owned, do not silently drop a required
                    //notification when a nonplayer burns the player's target.
                    bool targetsPlayerArmy = false;
                    if (targetArmy is not null)
                    {
                        int targetPerson = FireCommandPerson(current, OccupationKnown(current.ReadArmyUnitCommanderId(targetArmy), "fire_command_notify_commander"));
                        int targetDistrict = OccupationKnown(current.ReadPersonNativeValidity(targetPerson), "fire_command_notify_person_validity")
                            ? OccupationKnown(current.ReadPersonDistrictSlot(targetPerson), "fire_command_notify_district") : 0;
                        var selectedDistrict = current.ReadDistrictTableReference(targetDistrict <= 51 ? targetDistrict : 0) ?? throw OccupationMissing("fire_command_notify_district_pointer");
                        targetsPlayerArmy = playerValid && OccupationKnown(current.ReadDistrictForceSlot(selectedDistrict), "fire_command_notify_force") == player.PhysicalSlot;
                    }
                    if (targetsPlayerArmy) throw OccupationMissing("fire_command_nonplayer_target_report_speaker");
                    var notifyBase = FireCommandBase(current, obj);
                    bool targetsPlayerBase = false;
                    if (notifyBase is not null && OccupationKnown(current.ReadBaseNativeValidity(notifyBase), "fire_command_notify_Base_validity"))
                    {
                        var targetDistrict = current.ReadBaseDistrictReference(notifyBase) ?? throw OccupationMissing("fire_command_notify_Base_district");
                        if (playerValid && OccupationKnown(current.ReadDistrictForceSlot(targetDistrict), "fire_command_notify_Base_district_force") == player.PhysicalSlot)
                        {
                            int leader = FireCommandPerson(current, OccupationKnown(current.ReadDistrictLeader(
                                OccupationKnown(targetDistrict.PhysicalSlot, "fire_command_notify_Base_district_physical")), "fire_command_notify_Base_district_leader"));
                            targetsPlayerBase = OccupationKnown(current.ReadPersonNativeValidity(leader), "fire_command_notify_Base_leader_validity") &&
                                OccupationKnown(current.ReadPersonStatus(leader), "fire_command_notify_Base_leader_status") == 1;
                        }
                    }
                    if (targetsPlayerBase) throw OccupationMissing("fire_command_nonplayer_target_report_speaker");
                }
                if (ignition is int burn)
                {
                    int divisor = P(SimulationParameterKeys.FireCommand.IntelligenceDivisor);
                    if (divisor == 0) throw OccupationMissing("fire_command_duration_divisor");
                    float baseDuration = (float)P(SimulationParameterKeys.FireCommand.DurationBase) * ((float)Intelligence() / divisor + 1f);
                    int policy = OccupationKnown(BattleRemakeCurrentForcePolicies.ReadForceEffect(working, working.Forces[unit.ForceId], 19, current), "fire_command_policy19");
                    duration = NativeTruncate((float)unchecked(policy * P(SimulationParameterKeys.FireCommand.DurationPolicyScale)) * baseDuration / 100f + baseDuration);
                    IgniteCurrentHex(working, current, burn, duration.Value, true, moment, changes, announcements, reports);
                    if (!backfire)
                    {
                        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, true)) throw OccupationMissing("fire_command_flags150");
                        int targetForce = OccupationKnown(current.ReadHexForceSlot(Hex(goal)), "fire_command_target_force");
                        var force = current.ReadForceTableReference(targetForce <= 51 ? targetForce : 0) ?? throw OccupationMissing("fire_command_target_force_pointer");
                        if (OccupationKnown(current.ReadForceNativeValidity(force), "fire_command_target_force_validity") &&
                            force.PhysicalSlot != unit.ForceId && force.PhysicalSlot is not (>= 46 and <= 51))
                            working = ApplyFireCommandRelations(working, current, unit.ForceId,
                                OccupationKnown(force.PhysicalSlot, "fire_command_relation_target"), P);
                    }
                }
                trials.Add(new(unit.Id, goal, rate, sample, success, backfire, duration));
            }
            working = BaseCaptureCurrentInputs.PublishCurrent(working, current).WithArmyLifecycle(
                OwnedArmyDestructorFlags.PublishCurrent(working.ReadArmyLifecycle()!, current));
            return new(working with { PendingArmyFireAnnouncements = announcements.ToArray(), PendingArmyFireCasts = casts.ToArray(),
                PendingCommandReports = reports.ToArray() }, trials.ToArray(), changes.ToArray(), null);

            int P(string key) => working.NativeArmySimulation?.Parameters is { } bank && !bank.Float32Values.ContainsKey(key) &&
                bank.Int32Values.TryGetValue(key, out int value) ? value : throw OccupationMissing(key);
            HexReference Hex(int id) => current.ReadHexTableReference(id) ?? throw OccupationMissing("fire_command_hex");
            BaseReference City(int id) { int link = OccupationKnown(current.ReadHexObjectLink(Hex(id)), "fire_command_city_link"); return current.ReadCityTableReference(link <= 51 ? link : 0) ?? throw OccupationMissing("fire_command_city"); }
            S14TerrainTypeDefinition Surface(int id)
            {
                int code = OccupationKnown(current.ReadHexSurfaceCode(Hex(id)), "fire_command_surface");
                return _terrain.Catalog.Types[OccupationKnown(current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)?.PhysicalSlot, "fire_command_terrain")];
            }
            int TerrainRate(int category, bool backfire) => category switch {
                0 when backfire => P(SimulationParameterKeys.FireCommand.TerrainZeroRate),
                1 => P(SimulationParameterKeys.FireCommand.TerrainOneRate), 2 => P(SimulationParameterKeys.FireCommand.TerrainTwoRate),
                3 => P(SimulationParameterKeys.FireCommand.TerrainThreeRate), _ => 0 };
            int Goal(byte kind, ushort value)
            {
                if (kind == 9) return value;
                if (kind == 27) return OccupationKnown(current.ReadArmyUnitRawHex2A(current.ReadArmyUnitTableReference(value <= 500 ? value : 0)!), "fire_command_goal_army");
                if (kind is 5 or 6)
                {
                    BaseReference b = kind == 5 ? current.ReadCityTableReference(value <= 51 ? value : 0)! : current.ReadGateTableReference(value <= 10 ? value : 0)!;
                    value = OccupationKnown(current.ReadBaseObjectLink(b), "fire_command_goal_base");
                }
                else if (kind != 11) return 48400;
                return OccupationKnown(current.ReadObjectHexIndex(current.ReadObjectTableReference(value <= 3000 ? value : 0)!), "fire_command_goal_object");
            }
        }
        catch (InvalidDataException error) { return new(null, [], [], error.Message); }
    }

    private static int FireCommandDistance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220, dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
}
