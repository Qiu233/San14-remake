using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Common29F220 operation ownership for friendly entry, occupation,
    /// negotiated territory return and Campaign withdrawal. All nested people,
    /// districts and reports borrow the caller's existing current and pool.</summary>
    internal ArmyOrdinaryReturnResolution PrepareOwnedArmyReturnCurrent(GameState state,
        ArmyUnitReference army, BaseReference target, int nativeMode, SourceWorldContext current,
        WorldIndexContext pool, IArmyReturnGovernorCurrentQueries queries,
        ArmyReturnTraitPresentationOwners presentation, byte? transportFlags26 = null,
        NativeMapSeasonDate? operationDate = null, TacticSourceSamplerWorld? sampler = null,
        NativeArmyProgressStageMoment? moment = null)
    {
        var captives = new ArmyReturnCaptivesSource(this, state, new(true));
        var clock = state.NativeMapSeason ?? throw new InvalidDataException("return.operation.calendar");
        var date = moment?.PreWriterThirdDate ?? operationDate ?? clock.ThirdDate;
        long revision = moment?.CalendarCycleOrdinal ?? clock.AdvancedCalendarSteps;
        int Day(GameState reached) => moment is not null ? StatusCampaignDay(reached, moment)
            : sampler?.Day450 ?? FacilityOperation.Playable(reached).ReadCampaignDay(reached);
        return ArmyOrdinaryReturnRuntime.PrepareCurrentResourceOutputs(army, target, nativeMode,
            current, pool, queries, presentation, captives, transportFlags26,
            (reached, person, assignment) => captives.ApplyAssignmentReports(reached, person, assignment, revision, Day),
            (reached, inputs) => captives.BorrowDistrictOperationCurrent(reached, inputs, date, revision, Day, reached => sampler ?? ReadArmyDestructionSampler(reached, current)
                ?? throw new InvalidDataException("return.operation.sampler")));
    }
}
