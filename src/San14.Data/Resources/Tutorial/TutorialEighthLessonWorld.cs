using System.Buffers.Binary;
using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal static class TutorialEighthLessonTargets
{
    internal static bool AllowsHex(GameState control,SourceWorldContext current,int hexId,int? activeWaypointIndex = null)
    {
        var p = control.TutorialLessonProgress ?? throw new InvalidDataException("tutorial.eighth_lesson.progress");
        int? target = p.ActiveParagraphIndex switch
        {
            3 => current.ReadBaseCurrentHexIndex(current.ReadCityTableReference(TutorialEighthLessonPlan.DomesticCityPhysicalSlot)
                ?? throw new InvalidDataException("tutorial.eighth_lesson.DomesticCity")),
            4 or 7 => current.ReadAreaHexIndex(current.ReadAreaTableReference(p.ActiveParagraphIndex == 4
                ? TutorialEighthLessonPlan.FirstAreaPhysicalSlot : TutorialEighthLessonPlan.SecondAreaPhysicalSlot)
                ?? throw new InvalidDataException("tutorial.eighth_lesson.Area")),
            15 when p.ParagraphPhases[15] is 0 or 1 or 5 or 11 or 12 => current.ReadSceneryHexIndex(
                current.ReadSceneryTableReference(TutorialEighthLessonPlan.SceneryPhysicalSlot)
                ?? throw new InvalidDataException("tutorial.eighth_lesson.Scenery")),
            _ => null
        };
        return target is null || target == hexId;
    }
    internal static bool AllowsArmy(GameState control,int physical,byte? action) => true;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyEighthTutorialParagraphWorldCurrent(GameState state,int index,int phase)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex:7 } || (uint)index >= 18)
            throw EighthTutorialMissing("paragraph_owner");
        if (index == 0 && phase == 0)
        {
            foreach (int flag in new[] {78,79,81,62,58,72,101,102,98,100}) state = WriteFirstTutorialControl(state,flag,true);
            state = SetFirstTutorialCommandMask(state,0);
        }
        //4BD5D0 places these stores after event237, not before its narration.
        if (index == 0 && phase == 1) state = InitializeEighthTutorialCreditCurrent(state);
        if (index == 3 && phase == 1)
        {
            state = SetFirstTutorialCommandMask(state,(1<<3)|(1<<2));
            state = WriteFirstTutorialControl(state,60,false);
        }
        if (index is 4 or 7 && phase == 0)
        {
            state = WriteFirstTutorialControl(state,66,false);
            state = WriteFirstTutorialControl(state,13,false);
        }
        if (index == 12 && phase == 0) state = SetFirstTutorialCommandMask(state,0);
        //48CA60's callback adds independent doctrine reserves to Force2.
        if (index == 12 && phase == 1)
        {
            var reserves = state.ForceCreditReserves ?? throw EighthTutorialMissing("doctrine_reserves");
            if (!reserves.PointsByForce.TryGetValue(2,out var points) || points.Length != 6)
                throw EighthTutorialMissing("Force2_six_reserves");
            var next = points.ToBuilder();
            next[5] = (byte)Math.Min(99,next[5]+1);
            next[0] = (byte)Math.Min(99,next[0]+5);
            state = state with { ForceCreditReserves = reserves with { PointsByForce = reserves.PointsByForce.SetItem(2,next.ToImmutable()) } };
        }
        if (index == 13 && phase == 8) state = SetEighthTutorialCalendarCurrent(state,new(196,9,1,0));
        if (index == 14 && phase == 7)
        {
            state = WriteFirstTutorialControl(state,95,false);
            state = WriteFirstTutorialControl(state,4,false);
            state = SetFirstTutorialCommandMask(state,0);
        }
        if (index == 15 && phase is 1 or 5 or 12) state = WriteFirstTutorialControl(state,58,false);
        if (index == 15 && phase == 9)
        {
            var sites = state.LandmarkInvestments ?? throw EighthTutorialMissing("investment_owner");
            var site = sites.GetValueOrDefault(TutorialEighthLessonPlan.SceneryPhysicalSlot)
                ?? throw EighthTutorialMissing("Scenery23_investment");
            var date = state.NativeMapSeason?.ThirdDate ?? throw EighthTutorialMissing("investment_date");
            //4BF710 writes4E and50 only; phase, development and flags remain
            //the ordinary investment command/manager's actual business state.
            int due = Math.Max(0,unchecked(PersonReturnCommands.CalendarOrdinal(date)+8));
            state = state with { LandmarkInvestments = new(sites)
                { [TutorialEighthLessonPlan.SceneryPhysicalSlot] = site with { AssignedOfficerId = 8,CompletionDay = due } } };
            state = WriteFirstTutorialControl(state,4,false);
            state = SetFirstTutorialCommandMask(state,0);
        }
        if (phase == -1)
        {
            if (index == 3) state = SetFirstTutorialCommandMask(state,0);
            if (index == 16) state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with { CompletionCourseIndex = 7 } };
        }
        return state;
    }

    private GameState InitializeEighthTutorialCreditCurrent(GameState state)
    {
        var current = CaptureCurrentDefinitions(state) ?? throw EighthTutorialMissing("opening_current");
        var ranks = CreditRanks();
        int selector = EighthNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state),"merit_settings_selector");
        int option = EighthNeed(BattleRemakeCurrentSettings.ReadInt32(state,selector == 0 ? 0x4C4 : 0x128),"merit_settings_option");
        int merit = Math.Max(ranks[option == 1 ? 2 : 1].SignedThreshold,Math.Min(4000,(int)ranks[6].SignedThreshold));
        foreach (int slot in new[] {185,147,656})
        {
            var reference = current.ReadTransferPersonTableReference(slot) ?? throw EighthTutorialMissing("opening_Person_pointer");
            int person = EighthNeed(reference.PhysicalSlot,"opening_Person_physical");
            int id = EighthNeed(current.ReadPersonIdentity(person),"opening_Person_identity");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw EighthTutorialMissing("opening_Person_owner");
            var before = owner.MeritState ?? throw EighthTutorialMissing("opening_credit_cycle");
            if (slot == 185)
            {
                current.SetPersonFirstLocation(person,9);
                current.WritePersonSecondLocation(person,9);
                owner = owner with { LocationId = 9,LocationState = current.ReadOfficerLocation(id)
                    ?? throw EighthTutorialMissing("opening_Person_position") };
                state = OwnedArmyLifecycleDetachedInputs.PublishCurrentPersonFirstLocation(state,current,person);
            }
            state = state with { Officers = new(state.Officers) { [id] = owner with
                { MeritState = new(merit,before.PendingMerit,3,before.FirstSelectionCount,before.SecondSelectionCount,0) } } };
        }
        return state;
    }

    /// <summary>33C190 moves the actual calendar and its day/period/month
    /// offsets, without executing intervening simulation or a new turn.</summary>
    private GameState SetEighthTutorialCalendarCurrent(GameState state,NativeMapSeasonDate date)
    {
        var season = state.NativeMapSeason ?? throw EighthTutorialMissing("calendar_owner");
        var native = state.NativeArmySimulation ?? throw EighthTutorialMissing("calendar_native");
        var epoch = native.World.SamplerEpoch ?? throw EighthTutorialMissing("calendar_day_epoch");
        var events = state.CampaignEventWorld ?? throw EighthTutorialMissing("calendar_period_epoch");
        int days = unchecked(PersonReturnCommands.CalendarOrdinal(date)-PersonReturnCommands.CalendarOrdinal(season.ThirdDate));
        var values = new Dictionary<string,int>(native.Parameters.Int32Values);
        if (!values.TryGetValue(CollectionEventMonthOffsetKey,out int month))
        {
            var raw = _scenario.Snapshot.WorldParameters?.RawBytes ?? throw EighthTutorialMissing("calendar_month_source");
            if (raw.Length < 92) throw EighthTutorialMissing("calendar_month_field");
            month = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(88,4));
        }
        values[CollectionEventMonthOffsetKey] = unchecked(month+days/30);
        return state with { NativeMapSeason = season with { ThirdDate = date },
            CampaignEventWorld = events with { ElapsedPeriodOffset = unchecked(events.ElapsedPeriodOffset+days/10) },
            NativeArmySimulation = native with { World = native.World with { SamplerEpoch = epoch with { Day450 = unchecked(epoch.Day450+days) } },
                Parameters = native.Parameters with { Int32Values = values } } };
    }

    internal TutorialEighthLessonObservation ReadEighthTutorialObservationCurrent(GameState state,TutorialEighthLessonObservation observed)
    {
        if (state.TutorialLessonProgress is not { CourseIndex:7 }) throw EighthTutorialMissing("observation_owner");
        return observed;
    }
    public int QueryEighthTutorialSceneryHex(GameState state)
    {
        var current = CaptureCurrentDefinitions(state) ?? throw EighthTutorialMissing("Scenery_current");
        return EighthNeed(current.ReadSceneryHexIndex(current.ReadSceneryTableReference(TutorialEighthLessonPlan.SceneryPhysicalSlot)
            ?? throw EighthTutorialMissing("Scenery23_pointer")),"Scenery23_hex");
    }
    private static T EighthNeed<T>(T? value,string name) where T:struct => value ?? throw EighthTutorialMissing(name);
    private static InvalidDataException EighthTutorialMissing(string name) => new("tutorial.eighth_lesson."+name);
}
