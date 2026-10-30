using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

internal static class TutorialSixthLessonTargets
{
    internal static bool AllowsHex(GameState control, SourceWorldContext current, int hexId, int? activeWaypointIndex = null)
    {
        var p = control.TutorialLessonProgress ?? throw new InvalidDataException("tutorial.sixth_lesson.progress");
        if (p.ActiveParagraphIndex != 1) return true;
        var city = current.ReadCityTableReference(TutorialSixthLessonPlan.InformationCityId)
            ?? throw new InvalidDataException("tutorial.sixth_lesson.City47");
        return hexId == current.ReadBaseCurrentHexIndex(city);
    }
    internal static bool AllowsArmy(GameState control, int physical, byte? action) => true;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplySixthTutorialParagraphWorldCurrent(GameState state, int index, int phase)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex: 5 } || (uint)index >= 25)
            throw SixthTutorialMissing("paragraph_owner");
        if (index == 0 && phase == 0) state = InitializeSixthTutorialOpeningCurrent(state);
        if (index == 0 && phase == -1) state = WriteFirstTutorialControl(state, 58, false);
        if (index == 1 && phase == 0)
        {
            state = WriteFirstTutorialControl(state, 57, true);
            state = WriteFirstTutorialControl(state, 60, false);
        }
        if (index is 3 or 14 && phase == 1) state = WriteFirstTutorialControl(state, 60, false);
        if (index == 10 && phase == 1 || index == 20 && phase == 3)
            state = WriteFirstTutorialControl(state, 4, false);
        if (index == 12 && phase == 1) state = WriteFirstTutorialControl(state, 56, true);
        if (index == 14 && phase == 1)
        {
            state = WriteFirstTutorialControl(state, 56, false);
            state = WriteFirstTutorialControl(state, 55, true);
            var native = state.NativeArmySimulation ?? throw SixthTutorialMissing("trade_native");
            var force = native.Forces.GetValueOrDefault(2) ?? throw SixthTutorialMissing("trade_Force2");
            var trade = force.ForeignTradeProgress ?? throw SixthTutorialMissing("trade_progress");
            //489AA0 sets country4 friendship on Force2 to clamp(threshold-1,0,1000).
            //The TC source threshold is1, so the requested value is0.
            state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                { [2] = force with { ForeignTradeProgress = trade with { FriendshipPoints = trade.FriendshipPoints.SetItem(4, 0) } } } } };
        }
        if (index is 20 or 21 && phase == 1) state = AdvanceSixthTutorialCaravanCalendarCurrent(state);
        if (index == 23 && phase == -1)
        {
            state = WriteFirstTutorialControl(state, 54, false);
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with { CompletionCourseIndex = 5 } };
        }
        return state;
    }

    private GameState InitializeSixthTutorialOpeningCurrent(GameState state)
    {
        foreach (int flag in new[] { 78,79,81,62,54,58 }) state = WriteFirstTutorialControl(state, flag, true);
        state = SetFirstTutorialCommandMask(state, 0);
        var current = CaptureCurrentDefinitions(state) ?? throw SixthTutorialMissing("opening_current");
        int person = SixthNeed((current.ReadTransferPersonTableReference(952) ?? throw SixthTutorialMissing("opening_Person952")).PhysicalSlot, "opening_Person_physical");
        if (SixthNeed(current.ReadPersonNativeValidity(person), "opening_Person_validity"))
        {
            var force = current.ReadPersonForce(person) ?? throw SixthTutorialMissing("opening_Person_Force");
            if (SixthNeed(current.ReadForceNativeValidity(force), "opening_Force_validity"))
            {
                int id = SixthNeed(force.PhysicalSlot, "opening_Force_identity");
                current.SetForceRankSlot47(id, 9);
                var owner = state.Forces.GetValueOrDefault(id) ?? throw SixthTutorialMissing("opening_Force_owner");
                var native = state.NativeArmySimulation ?? throw SixthTutorialMissing("opening_native");
                state = state with { Forces = new(state.Forces) { [id] = owner with { RankId = 9 } },
                    NativeArmySimulation = native with { Parameters = native.Parameters with
                    { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyMoraleWaveInputs.ForceRank47Key(id)] = 9 } } } };
            }
        }
        //World77F30 points to Item131 in the Item table.
        var items = state.Items ?? throw SixthTutorialMissing("opening_Items");
        var item = items.GetValueOrDefault(131) ?? throw SixthTutorialMissing("opening_Item131");
        var life = item.Lifecycle ?? throw SixthTutorialMissing("opening_Item_lifecycle");
        state = state with { Items = new(items) { [131] = item with { Lifecycle = life with { Status = ItemStatusKnowledge.Exact(4) } } } };
        current.SetRegistryItemRawField50(131, 4);
        var city = current.ReadCityTableReference(7) ?? throw SixthTutorialMissing("opening_City7");
        int gold = Math.Clamp(5000, 0, BasePostLoad.ReadGoldCapacity(city, current));
        int food = Math.Clamp(100000, 0, BasePostLoad.ReadSuppliesCapacity(city, current));
        int troops = Math.Clamp(10000, 0, BasePostLoad.ReadTroopCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Gold, gold);
        current.SetBaseResource(city, BaseResourceKind.Supplies, food);
        current.SetBaseResource(city, BaseResourceKind.Troops, troops);
        var saved = state.Cities.GetValueOrDefault(7) ?? throw SixthTutorialMissing("opening_City_owner");
        return state with { Cities = new(state.Cities) { [7] = saved with { Gold = gold, Supplies = food, Troops = troops } } };
    }

    /// <summary>48C7B0/488C80: advance only the native campaign calendar and
    /// its offsets by complete tens of actual associated-task remaining days.</summary>
    private GameState AdvanceSixthTutorialCaravanCalendarCurrent(GameState state)
    {
        int officer = ReadTutorialPhysicalOfficerIdentityCurrent(state, TutorialSixthLessonPlan.CaravanOfficerPhysicalSlot);
        int remaining = ReadForeignTradeTaskRemainingDaysCurrent(state, officer);
        int days = remaining / 10 * 10;
        if (days == 0) return state;
        var season = state.NativeMapSeason ?? throw SixthTutorialMissing("calendar_owner");
        var native = state.NativeArmySimulation ?? throw SixthTutorialMissing("calendar_native");
        var epoch = native.World.SamplerEpoch ?? throw SixthTutorialMissing("calendar_day_epoch");
        var events = state.CampaignEventWorld ?? throw SixthTutorialMissing("calendar_period_epoch");
        int ordinal = PersonReturnCommands.CalendarOrdinal(season.ThirdDate);
        var date = PersonReturnCommands.DateFromOrdinal(unchecked(ordinal + days)) with { Hour = season.ThirdDate.Hour };
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        if (!values.TryGetValue(CollectionEventMonthOffsetKey, out int month))
        {
            var source = _scenario.Snapshot.WorldParameters ?? throw SixthTutorialMissing("calendar_month_source");
            if (source.RawBytes.Length < 92) throw SixthTutorialMissing("calendar_month_source_field");
            month = BinaryPrimitives.ReadInt32LittleEndian(source.RawBytes.AsSpan(88, 4));
        }
        values[CollectionEventMonthOffsetKey] = unchecked(month + days / 30);
        return state with { NativeMapSeason = season with { ThirdDate = date },
            CampaignEventWorld = events with { ElapsedPeriodOffset = unchecked(events.ElapsedPeriodOffset + days / 10) },
            NativeArmySimulation = native with { World = native.World with { SamplerEpoch = epoch with { Day450 = unchecked(epoch.Day450 + days) } },
                Parameters = native.Parameters with { Int32Values = values } } };
    }

    /// <summary>8228C0's tail, after either actual goodwill result branch.
    /// The original applies this for every active tutorial, regardless of course.</summary>
    private GameState RecordTutorialGoodwillResolutionCurrent(GameState state)
    {
        var control = ReadTutorialControlState(state);
        if (TacticRemakeCurrentBattleControl.ReadHeader(control) is not int header || header == 0 ||
            TacticRemakeCurrentBattleControl.ReadBitmapPresence(control) != true) return state;
        return WriteFirstTutorialControl(state, 41, true);
    }

    public int QuerySixthTutorialCityHex(GameState state, int cityPhysical)
    {
        if ((uint)cityPhysical >= 52) throw new ArgumentOutOfRangeException(nameof(cityPhysical));
        var current = CaptureCurrentDefinitions(state) ?? throw SixthTutorialMissing("city_current");
        return current.ReadBaseCurrentHexIndex(current.ReadCityTableReference(cityPhysical) ?? throw SixthTutorialMissing("city_pointer"));
    }
    internal TutorialSixthLessonObservation ReadSixthTutorialObservationCurrent(GameState state, TutorialSixthLessonObservation observed)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: 5 }) throw SixthTutorialMissing("observation_owner");
        //This course's running predicates are real UI controls and accepted
        //ordinary-task events; a UI poll does not need a world capture.
        return observed;
    }
    public bool CanAcceptSixthTutorialGoodwillOfficer(GameState state, int officerId) =>
        officerId == ReadTutorialPhysicalOfficerIdentityCurrent(state, TutorialSixthLessonPlan.GoodwillOfficerPhysicalSlot);
    public bool CanAcceptSixthTutorialCaravanOfficer(GameState state, int officerId) =>
        officerId == ReadTutorialPhysicalOfficerIdentityCurrent(state, TutorialSixthLessonPlan.CaravanOfficerPhysicalSlot);
    private static T SixthNeed<T>(T? value, string field) where T : struct => value ?? throw SixthTutorialMissing(field);
    private static InvalidDataException SixthTutorialMissing(string field) => new("tutorial.sixth_lesson.world." + field);
}
