using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record NormalTurnDeathPeopleResult(GameState State, ImmutableArray<int>? Remaining, bool Stop);
    private sealed record NormalTurnEarlyActionsResult(GameState State, bool Stop);

    //The source's sorted temporary Person list is materialized once. Its
    //order and repeated payloads survive the modal successor choice; neither
    //the qualification scan nor any already completed death is replayed.
    private NormalTurnDeathPeopleResult ConsumeNormalTurnDeathPeopleCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, IReadOnlyList<int> people,
        Action<string, GameState>? observe = null)
    {
        for (int index = 0; index < people.Count; index++)
        {
            var death = new PersonDeathSource(this, state, current, pool, observe)
                .ApplyAnnualOrdinary(people[index]);
            state = death.State;
            if (death.NativeReturn is null)
            {
                if (state.PersonDeathContinuation is null)
                    throw AppearanceTurnMissing("annual_death_pending_owner");
                return new(state, people.Skip(index + 1).ToImmutableArray(), false);
            }
            if (death.NativeReturn == 1)
                return new(state, null, true); //The real Force-unregistration terminal exit.
            if (death.NativeReturn != 0)
                throw AppearanceTurnMissing("annual_death_native_return");
        }
        return new(state, null, false);
    }

    private static GameState PauseNormalTurnDeathsCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int action, ImmutableArray<int> remaining, uint restoreApp,
        CampaignGroupingCheckpoint? grouping, ArmyMapGroupingOwner? groups)
    {
        if (state.PersonDeathContinuation is null || action is not (3 or 7 or 8))
            throw AppearanceTurnMissing("annual_death_pause_boundary");
        var checkpoint = new CampaignGroupingCheckpoint(
            groups?.Export() ?? grouping?.Roots ?? throw AppearanceTurnMissing("annual_death_grouping_owner"),
            groups is not null || grouping!.Changed);
        state = state with
        {
            NativeNormalTurnContinuation = new(action, restoreApp, ImmutableArray<int>.Empty, null, checkpoint)
            { RemainingDeathPersonPhysicalSlots = remaining }
        };
        //All source temporaries have already been released. Publish the same
        //pool and current fields before exposing the real player decision.
        return PublishNormalTurnCurrent(state, current, pool);
    }

    //Shared by the initial prefix and a death-list resume. firstAction4/8/9
    //is the actual successor of the consumed action3/7/8, respectively.
    private NormalTurnEarlyActionsResult ApplyNormalTurnEarlyActionsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, bool annual, bool monthStart,
        uint restoreApp, CampaignGroupingCheckpoint? grouping, ArmyMapGroupingOwner? groups,
        int firstAction = 4, Action<int>? reached = null, Action<string, GameState>? observe = null)
    {
        for (int action = firstAction; action <= 16; action++)
        {
            reached?.Invoke(action);
            bool enabled = action is 4 or 5 or 10 ? annual : action is 6 or 7 or 8 or 14 or 15 or 16 ? monthStart : true;
            if (enabled)
            {
                switch (action)
                {
                    case 4:
                        state = new AnnualPersonLifespanNotificationSource(this, state, current, pool, observe).Run();
                        break;
                    case 5:
                        state = new AnnualPersonPortraitTransitionSource(state, current, observe).Run();
                        break;
                    case 6:
                        state = new MonthlyCampaignMaintenanceSource(this, state, current, pool, observe).Run();
                        break;
                    case 7:
                    case 8:
                    {
                        var source = new CampaignNormalTurnPersonnelAgingSource(this, state, current, pool, observe);
                        state = action == 7 ? source.RunLifespan() : source.RunHealth();
                        if (source.RemainingDeathPersonPhysicalSlots is { } remaining)
                        {
                            state = PauseNormalTurnDeathsCurrent(state, current, pool, action, remaining,
                                restoreApp, grouping, groups);
                            observe?.Invoke($"turn.index{action}.waiting-for-player", state);
                            return new(state, false);
                        }
                        if (source.Stopped) return new(PublishNormalTurnCurrent(state, current, pool), true);
                        break;
                    }
                    case 9:
                    {
                        var ending = CheckCampaignNormalTurnEndingCurrent(state, current, pool);
                        state = ending.State ?? throw new InvalidDataException(ending.UnresolvedOwner);
                        observe?.Invoke("turn.index9.completed", state);
                        if (ending.Stop) return new(state, true);
                        continue;
                    }
                    case 10:
                        state = ApplyNormalTurnAnnualCalendarCurrent(state, current, out bool stop);
                        observe?.Invoke("turn.index10.completed", state);
                        if (stop) return new(state, true);
                        continue;
                    case 11:
                        break; //4F8560 is literally return1, with no state or random writer.
                    case 12:
                        state = ApplyNormalTurnForceAbsorptionEventsCurrent(state, current, pool);
                        break;
                    case 13:
                        state = ApplyNormalTurnSpecialForceRelationsCurrent(state, current, pool);
                        break;
                    case 14:
                        state = ApplyNormalTurnMonthlyCaptiveEscapesCurrent(state, current, pool);
                        break;
                    case 15:
                        state = ApplyNormalTurnMonthlyDisastersCurrent(state, current, pool);
                        break;
                    case 16:
                        state = ApplyNormalTurnSelfRecommendationsCurrent(state, current, pool, restoreApp, grouping);
                        if (state.NativeNormalTurnContinuation is not null)
                        {
                            observe?.Invoke("turn.index16.waiting-for-player", state);
                            return new(state, false);
                        }
                        break;
                }
            }
            observe?.Invoke($"turn.index{action}." + (enabled ? "completed" : "skipped"), state);
        }
        return new(state, false);
    }
}
