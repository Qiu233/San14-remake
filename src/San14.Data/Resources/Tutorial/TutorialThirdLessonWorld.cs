using System.Buffers.Binary;
using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>The installed third-course target callbacks. No candidate list or
/// world scan is rebuilt during pointer queries.</summary>
internal static class TutorialThirdLessonTargets
{
    internal static bool AllowsHex(GameState control, SourceWorldContext current, int hexId, int? activeWaypointIndex = null)
    {
        var p = control.TutorialLessonProgress ?? throw Missing("progress");
        IReadOnlyList<int>? areas = p.ActiveParagraphIndex switch
        {
            4 when p.ParagraphPhases[4] == 3 => TutorialThirdLessonPlan.KanuTargetAreaIds,
            5 when p.ParagraphPhases[5] == 2 => TutorialThirdLessonPlan.ChouhiTargetAreaIds,
            _ => null
        };
        if (areas is not null)
        {
            int index = activeWaypointIndex ?? throw Missing("active_waypoint_index");
            if ((uint)index >= (uint)areas.Count) return false;
            var area = current.ReadAreaTableReference(areas[index]) ?? throw Missing("target_Area");
            return hexId == (current.ReadAreaHexIndex(area) ?? throw Missing("target_representative"));
        }
        if (p.ActiveParagraphIndex == 8 && p.ParagraphPhases[8] == 1)
        {
            int person = (current.ReadTransferPersonTableReference(149) ?? throw Missing("enemy_pointer")).PhysicalSlot
                ?? throw Missing("enemy_Person");
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw Missing("enemy_Army");
            if (!(current.ReadArmyUnitNativeValidity(army) ?? throw Missing("enemy_validity"))) return false;
            return hexId == (current.ReadArmyUnitRawHex2A(army) ?? throw Missing("enemy_Hex"));
        }
        return true;
    }

    internal static bool AllowsArmy(GameState control, int physical, byte? action)
    {
        var p = control.TutorialLessonProgress ?? throw Missing("progress");
        var excluded = p.RestrictedArmyPhysicalSlots ?? throw Missing("Army_filter_snapshot");
        return !excluded.Contains(physical);
    }
    private static InvalidDataException Missing(string field) => new("tutorial.third_lesson.targets." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyThirdTutorialParagraphWorldCurrent(GameState state, int index, int phase)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: 2 } progress || (uint)index >= 13)
            throw ThirdTutorialMissing("paragraph_owner");
        if (index == 0 && phase == 0) state = InitializeThirdTutorialOpeningCurrent(state);
        if (index == 1 && phase == 0) state = SpawnThirdTutorialEnemyCurrent(state);
        if (index == 3 && phase == 1) state = WriteFirstTutorialControl(state, 74, true);
        if (index == 3 && phase == 8) state = WriteFirstTutorialControl(state, 68, true);
        if (index == 5 && phase == 4)
        {
            //4B2330 traverses the actual World Army registry in linked order
            //and compares22A730's Home against City7. There is no separate
            //City Army iterator or native-validity filter at this boundary.
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var home = current.ReadCityTableReference(7) ?? throw ThirdTutorialMissing("filter_City7");
            var entries = current.ReadRegistryReferences(RegistryKind.Army) ?? throw ThirdTutorialMissing("filter_Army_registry");
            var slots = ImmutableArray.CreateBuilder<int>();
            foreach (var entry in entries)
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                    throw ThirdTutorialMissing("filter_Army_registry_pointer");
                if (current.FindArmyUnitCurrentBase(new(physical)) == home && !slots.Contains(physical))
                    slots.Add(physical);
            }
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with
                { RestrictedArmyPhysicalSlots = slots.ToImmutable() } };
            state = WriteFirstTutorialControl(state, 4, false);
        }
        if (index == 7 && phase == -1 && progress.ParagraphPhases[7] == 1)
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with
                { RestrictedArmyPhysicalSlots = ImmutableArray<int>.Empty } };
        if (index == 8 && phase is 0 or 2)
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            if (phase == 0)
            {
                var enemy = PersonArmyLocationSource.ReadCommanderArmy(current, ThirdPerson(current, 149))
                    ?? throw ThirdTutorialMissing("attack_enemy_Army");
                if (!ThirdNeed(current.ReadArmyUnitNativeValidity(enemy), "attack_enemy_validity")) return state;
            }
            int person = ThirdPerson(current, phase == 0 ? 656 : 147);
            int physical = (PersonArmyLocationSource.ReadCommanderArmy(current, person)
                ?? throw ThirdTutorialMissing("attack_filter_Army")).PhysicalSlot
                ?? throw ThirdTutorialMissing("attack_filter_physical");
            var p = state.TutorialLessonProgress!;
            var slots = p.RestrictedArmyPhysicalSlots ?? throw ThirdTutorialMissing("attack_filter_snapshot");
            if (!slots.Contains(physical)) state = state with { TutorialLessonProgress = p with
                { RestrictedArmyPhysicalSlots = slots.Add(physical) } };
            if (phase == 2) state = WriteFirstTutorialControl(state, 4, false);
        }
        if (index == 9 && phase == -1 && progress.InstructionIndex > 1)
        {
            //Only4857B0's completed callback changes these flags. The
            //enemy-already-gone branch skips the callback entirely.
            state = WriteFirstTutorialControl(state, 72, false);
            state = WriteFirstTutorialControl(state, 73, true);
        }
        if (index == 11 && phase == -1)
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with { CompletionCourseIndex = 2 } };
        return state;
    }

    private GameState InitializeThirdTutorialOpeningCurrent(GameState state)
    {
        foreach (int flag in new[] { 78, 79, 81, 62 }) state = WriteFirstTutorialControl(state, flag, true);
        //Opening invokes only33DCC0,360times. It does not run120 Army
        //updates, consume a period or advance Core's command date/clock.
        var season = state.NativeMapSeason ?? throw ThirdTutorialMissing("opening_calendar");
        var simulation = state.NativeArmySimulation ?? throw ThirdTutorialMissing("opening_simulation");
        var epoch = simulation.World.SamplerEpoch ?? throw ThirdTutorialMissing("opening_day_epoch");
        var events = state.CampaignEventWorld ?? throw ThirdTutorialMissing("opening_period_epoch");
        var date = season.ThirdDate;
        int days = 0, periods = 0, months = 0;
        for (int i = 0; i < 360; i++)
        {
            var step = MapSeasonRuntime.StepCalendarWriter(date); date = step.Date;
            if (step.DayAdvanced) days++;
            if (step.PeriodAdvanced) periods++;
            if (step.MonthAdvanced) months++;
        }
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        if (!values.TryGetValue(CollectionEventMonthOffsetKey, out int month))
        {
            var source = _scenario.Snapshot.WorldParameters ?? throw ThirdTutorialMissing("opening_month_source");
            if (source.RawBytes.Length < 92) throw ThirdTutorialMissing("opening_month_field");
            month = BinaryPrimitives.ReadInt32LittleEndian(source.RawBytes.AsSpan(88, 4));
        }
        values[CollectionEventMonthOffsetKey] = unchecked(month + months);
        state = state with
        {
            NativeMapSeason = season with { ThirdDate = date },
            CampaignEventWorld = events with { ElapsedPeriodOffset = unchecked(events.ElapsedPeriodOffset + periods) },
            NativeArmySimulation = simulation with
            {
                World = simulation.World with { SamplerEpoch = epoch with { Day450 = unchecked(epoch.Day450 + days) } },
                Parameters = simulation.Parameters with { Int32Values = values }
            },
            TutorialLessonProgress = state.TutorialLessonProgress! with { RestrictedArmyPhysicalSlots = ImmutableArray<int>.Empty }
        };
        var current = CaptureCurrentDefinitions(state) ?? throw ThirdTutorialMissing("opening_current");
        int person = ThirdPerson(current, 952);
        if (ThirdNeed(current.ReadPersonNativeValidity(person), "opening_Person_validity"))
        {
            var force = current.ReadPersonForce(person) ?? throw ThirdTutorialMissing("opening_Force");
            if (ThirdNeed(current.ReadForceNativeValidity(force), "opening_Force_validity"))
            {
                int id = ThirdNeed(force.PhysicalSlot, "opening_Force_physical");
                current.SetForceRankSlot47(id, 7);
                var owner = state.Forces.GetValueOrDefault(id) ?? throw ThirdTutorialMissing("opening_Force_owner");
                simulation = state.NativeArmySimulation!;
                state = state with
                {
                    Forces = new(state.Forces) { [id] = owner with { RankId = 7 } },
                    NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values) { [OwnedArmyMoraleWaveInputs.ForceRank47Key(id)] = 7 } } }
                };
                state = ApplyConfirmedPlayerCommanderCapacity(state, current);
            }
        }
        foreach (int flag in new[] { 61, 58, 72 }) state = WriteFirstTutorialControl(state, flag, true);
        state = SetFirstTutorialCommandMask(state, 0);
        var city = current.ReadCityTableReference(7) ?? throw ThirdTutorialMissing("opening_City7");
        int troops = Math.Clamp(10000, 0, BasePostLoad.ReadTroopCapacity(city, current));
        int food = Math.Clamp(100000, 0, BasePostLoad.ReadSuppliesCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Troops, troops);
        current.SetBaseResource(city, BaseResourceKind.Supplies, food);
        var saved = state.Cities.GetValueOrDefault(7) ?? throw ThirdTutorialMissing("opening_City_owner");
        return state with { Cities = new(state.Cities) { [7] = saved with { Troops = troops, Supplies = food } } };
    }

    //A lesson text/phase update must not recapture the world or rerun the
    //nearby-army query. Only changed business owners invalidate this capture.
    private sealed class ThirdTutorialWorldCapture(GameState state, SourceWorldContext current)
    {
        internal readonly GameState State = state;
        internal readonly SourceWorldContext Current = current;
        internal TutorialThirdLessonObservation? Enemy;
        internal bool? Related;
        internal bool Matches(GameState other) => ReferenceEquals(State.Units, other.Units) &&
            ReferenceEquals(State.Officers, other.Officers) && ReferenceEquals(State.Forces, other.Forces) &&
            ReferenceEquals(State.Cities, other.Cities) && ReferenceEquals(State.Districts, other.Districts) &&
            ReferenceEquals(State.NativeArmySimulation?.World, other.NativeArmySimulation?.World) &&
            ReferenceEquals(State.ResourceState, other.ResourceState) &&
            ReferenceEquals(State.ReadArmyLifecycle(), other.ReadArmyLifecycle());
    }
    private ThirdTutorialWorldCapture? thirdTutorialCapture;
    private ThirdTutorialWorldCapture ThirdCapture(GameState state)
    {
        if (thirdTutorialCapture?.Matches(state) != true)
            thirdTutorialCapture = new(state, CaptureOwnedArmyReturnWorld(state).Context);
        return thirdTutorialCapture;
    }

    public int? QueryThirdTutorialKanuHex(GameState state)
    {
        var current = ThirdCapture(state).Current;
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, ThirdPerson(current, 147))
            ?? throw ThirdTutorialMissing("Kanu_Army");
        if (!ThirdNeed(current.ReadArmyUnitNativeValidity(army), "Kanu_Army_validity")) return null;
        int raw = ThirdNeed(current.ReadArmyUnitRawHex2A(army), "Kanu_Hex");
        return (current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw ThirdTutorialMissing("Kanu_Hex_pointer")).PhysicalSlot;
    }

    public int QueryThirdTutorialAreaHex(GameState state, int areaId)
    {
        var current = ThirdCapture(state).Current;
        var area = current.ReadAreaTableReference(areaId) ?? throw ThirdTutorialMissing("target_Area_pointer");
        return ThirdNeed(current.ReadAreaHexIndex(area), "target_Area_representative");
    }

    internal TutorialThirdLessonObservation ReadThirdTutorialObservationCurrent(GameState state,
        TutorialThirdLessonObservation observed)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: 2 } progress) throw ThirdTutorialMissing("observation_course");
        if (progress.Stage < 40) return observed;
        var capture = ThirdCapture(state);
        var current = capture.Current;
        var facts = capture.Enemy;
        if (facts is null)
        {
            int person = ThirdPerson(current, 149);
            bool validPerson = ThirdNeed(current.ReadPersonNativeValidity(person), "enemy_Person_validity");
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw ThirdTutorialMissing("enemy_Army");
            bool validArmy = ThirdNeed(current.ReadArmyUnitNativeValidity(army), "enemy_Army_validity");
            bool commanderValid = false; int? status = null; bool? supply = null; bool attacks = false;
            if (validArmy)
            {
                int physical = ThirdNeed(army.PhysicalSlot, "enemy_Army_physical");
                var image = FArmy(state, physical) ?? throw ThirdTutorialMissing("enemy_Army_image");
                supply = ((image.NativeTacticRuntime ?? throw ThirdTutorialMissing("enemy_flags26")).Flags26 & 2) != 0;
                int id = ThirdNeed(current.ReadArmyUnitCommanderId(army), "enemy_commander_identity");
                var commander = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                    ?? throw ThirdTutorialMissing("enemy_commander_pointer");
                if (commander.PhysicalSlot is int p)
                {
                    commanderValid = ThirdNeed(current.ReadPersonNativeValidity(p), "enemy_commander_validity");
                    if (commanderValid) status = ThirdNeed(current.ReadPersonStatus(p), "enemy_commander_status");
                }
                var kanuArmy = PersonArmyLocationSource.ReadCommanderArmy(current, ThirdPerson(current, 147))
                    ?? throw ThirdTutorialMissing("Kanu_Army");
                if (ThirdNeed(current.ReadArmyUnitNativeValidity(kanuArmy), "Kanu_Army_validity"))
                {
                    int k = ThirdNeed(kanuArmy.PhysicalSlot, "Kanu_Army_physical");
                    var imageK = FArmy(state, k) ?? throw ThirdTutorialMissing("Kanu_image");
                    byte action = ThirdNeed(ArmyConfigurationSourceAdapter.ReadAction(state, imageK), "Kanu_action37");
                    if (action == 7)
                    {
                        var target = current.ReadArmyLifecycleTargetFields(k) ?? throw ThirdTutorialMissing("Kanu_target");
                        attacks = target.Kind38 == 27 && ThirdNeed(target.Identity3A, "Kanu_target_identity") == physical;
                    }
                }
            }
            facts = new(observed.Common) { EnemyOfficerValid = validPerson, EnemyArmyValid = validArmy,
                EnemyArmyCommanderValid = commanderValid, EnemyArmyCommanderStatus = status,
                EnemyArmySupplyFlag = supply, KanuAttacksEnemy = attacks };
            capture.Enemy = facts;
        }
        bool? related = observed.KanuRelatedArmiesContainChouhi;
        if (progress.Stage == 47)
        {
            if (capture.Related is null)
            {
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, ThirdPerson(current, 147))
                    ?? throw ThirdTutorialMissing("chain_Kanu_Army");
                bool found = false;
                if (ThirdNeed(current.ReadArmyUnitNativeValidity(army), "chain_Kanu_validity"))
                {
                    var image = FArmy(state, ThirdNeed(army.PhysicalSlot, "chain_Kanu_physical"))
                        ?? throw ThirdTutorialMissing("chain_Kanu_image");
                    var chain = ArmyChainCurrentCandidates.Read(state, current, image);
                    foreach (var row in chain.ReachedPrefix)
                        if (ThirdNeed(current.ReadArmyUnitCommanderId(new(row.PhysicalSlot)), "chain_commander") == 656) { found = true; break; }
                    if (!found && chain.UnresolvedOwner is not null) throw new InvalidDataException(chain.UnresolvedOwner);
                }
                capture.Related = found;
            }
            related = capture.Related;
        }
        bool? focus = observed.AreaSupportFocusMatchesArmyHex;
        if (progress.Stage == 44) focus = QueryThirdTutorialKanuHex(state) is int hex && observed.CameraFocusHexId == hex;
        return observed with { EnemyOfficerValid = facts.EnemyOfficerValid, EnemyArmyValid = facts.EnemyArmyValid,
            EnemyArmyCommanderValid = facts.EnemyArmyCommanderValid, EnemyArmyCommanderStatus = facts.EnemyArmyCommanderStatus,
            EnemyArmySupplyFlag = facts.EnemyArmySupplyFlag, KanuAttacksEnemy = facts.KanuAttacksEnemy,
            KanuRelatedArmiesContainChouhi = related, AreaSupportFocusMatchesArmyHex = focus };
    }

    private static int ThirdPerson(SourceWorldContext current, int slot) =>
        (current.ReadTransferPersonTableReference(slot) ?? throw ThirdTutorialMissing("Person_pointer")).PhysicalSlot
        ?? throw ThirdTutorialMissing("Person_physical");
    private static T ThirdNeed<T>(T? value, string field) where T : struct => value ?? throw ThirdTutorialMissing(field);
    private static InvalidDataException ThirdTutorialMissing(string field) => new("tutorial.third_lesson.world." + field);
}
