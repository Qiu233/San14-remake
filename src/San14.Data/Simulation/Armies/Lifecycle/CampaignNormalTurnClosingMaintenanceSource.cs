using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnClosingMaintenanceCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int action, ArmyMapGroupingOwner? groups = null)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw ClosingMissing("shared_pool");
        return action switch
        {
            61 => RefreshNormalTurnSelectedUserCurrent(state),
            62 => RecordNormalTurnDistrictResourcesCurrent(state, current),
            63 => ConsumeNormalTurnChallengeStopCurrent(state, current),
            64 => ReportNormalTurnBaseDefendersCurrent(state, current),
            65 => RefreshNormalTurnCollectionServiceCurrent(state, current),
            66 => RefreshNormalTurnFoodWarningsCurrent(state, current,
                groups ?? throw ClosingMissing("food_quote_grouping_owner")),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    //326230 initializes all four ten-entry district histories to signed -1.
    //This producer is called only during explicit new-world construction.
    internal static GameState InstallFreshNormalTurnDistrictHistory(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.PlayerDistrictResourceHistory is not null)
            throw ClosingMissing("fresh_district_history_owner");
        var empty = Enumerable.Repeat(-1, 10).ToImmutableArray();
        return state with { PlayerDistrictResourceHistory = Enumerable.Range(1, 8).ToImmutableDictionary(id => id,
            _ => new NormalTurnDistrictResourceHistory(empty, empty, empty, empty)) };
    }

    //4E8A70 and its successful4E3E70 both initialize the independent
    //challenge service stop dword+8 to zero. Only the application producer
    //may call this; loading a saved world never reconstructs the signal.
    internal static GameState InstallFreshNormalTurnChallengeService(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.ChallengeServiceTurnStopPending is not null)
            throw ClosingMissing("fresh_challenge_stop_owner");
        return state with { ChallengeServiceTurnStopPending = false };
    }

    private static GameState RecordNormalTurnDistrictResourcesCurrent(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ClosingMissing("history_player_Force");
        int force = ClosingNeed(current.ReadForcePhysicalSlot(player), "history_player_ordinal");
        //CForceDistrictIterator only admits ordinals1..51. It does not add a
        //native-validity test before the registered District10 comparison.
        if (force is < 1 or > 51) return state;
        var histories = state.PlayerDistrictResourceHistory ?? throw ClosingMissing("district_resource_history");
        var builder = histories.ToBuilder();
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw ClosingMissing("history_DistrictD0"))
        {
            int district = ClosingNeed(entry?.PhysicalSlot, "history_District_pointer");
            if (ClosingNeed(current.ReadDistrictForceId(district), "history_District10") != force) continue;
            int gold = 0, food = 0, troops = 0;
            foreach (var target in ClosingBases(current))
            {
                if (ClosingNeed(current.ReadBaseDistrictSlot(target), "history_Base_District") != district) continue;
                if (!ClosingNeed(current.ReadBaseNativeValidity(target), "history_Base_validity")) continue;
                if (ClosingNeed(current.ReadBaseObjectLink(target), "history_Base4E") < 52)
                {
                    gold = unchecked(gold + ClosingNeed(current.ReadBaseResource(target, BaseResourceKind.Gold), "history_City_gold"));
                    food = unchecked(food + ClosingNeed(current.ReadBaseResource(target, BaseResourceKind.Supplies), "history_City_food"));
                }
                troops = unchecked(troops + ClosingNeed(current.ReadBaseResource(target, BaseResourceKind.Troops), "history_Base_troops3C"));
            }
            int number = ClosingNeed(current.ReadDistrictNumber(district), "history_District11");
            if (number is < 1 or > 8) continue; //Source writer ignores other numbers.
            if (!builder.TryGetValue(number, out var old)) throw ClosingMissing("district_history_row");
            builder[number] = new(Shift(old.Gold, gold), Shift(old.Food, food), Shift(old.Troops, troops),
                Shift(old.MilitaryIntelligence, ReadDistrictAdministrationIntelligenceCurrent(current, district)));
        }
        return state with { PlayerDistrictResourceHistory = builder.ToImmutable() };

        static ImmutableArray<int> Shift(ImmutableArray<int> previous, int value)
        {
            if (previous.IsDefault || previous.Length != 10) throw ClosingMissing("district_history_series");
            return previous.RemoveAt(0).Add(value);
        }
    }

    private GameState ConsumeNormalTurnChallengeStopCurrent(GameState state, SourceWorldContext current)
    {
        BindApplicationServiceCurrent(state, current);
        var mode = current.ReadTransferModeState() ?? throw ClosingMissing("challenge_service_mode");
        //548010 clears +8 only when the real service's first dword is nonzero.
        if (mode.Present && ClosingNeed(mode.Value, "challenge_service_mode_value") != 0)
            state = state with { ChallengeServiceTurnStopPending = false };
        if (!ClosingNeed(state.ChallengeServiceTurnStopPending, "challenge_service_stop8")) return state;
        return state with { CampaignTurnStop = new(CampaignTurnStopReason.ChallengeService, null, false) };
    }

    private static IEnumerable<BaseReference> ClosingBases(SourceWorldContext current)
    {
        foreach (var target in current.ReadRegisteredBases() ?? throw ClosingMissing("Base80"))
            yield return target ?? throw ClosingMissing("Base80_pointer");
    }
    private static T ClosingNeed<T>(T? value, string field) where T : struct => value ?? throw ClosingMissing(field);
    private static InvalidDataException ClosingMissing(string field) => new("normal.turn.closing." + field);
}
