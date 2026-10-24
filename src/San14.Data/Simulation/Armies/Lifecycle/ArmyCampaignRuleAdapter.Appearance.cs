using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Source stage9's midnight appearance batch. Actual at-home
    /// qualification and hold/physical ordering precede all writes; any unknown
    /// reached branch discards the whole private batch. Calendar belongs to the
    /// existing driver, and every other unimplemented stage remains unresolved.</summary>
    public OrdinaryArmyAppearanceResult TryAdvanceOrdinaryAppearance(GameState state,
        NativeArmyProgressStageMoment moment)
        => ConsumeOrdinaryAppearance(state, moment, null);

    private OrdinaryArmyAppearanceResult ConsumeOrdinaryAppearance(GameState state,
        NativeArmyProgressStageMoment moment, SourceWorldContext? operationCurrent)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Appearance ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army appearance requires its reached source stage9.");
        return ConsumeOrdinaryAppearance(state, moment.PreWriterThirdDate, operationCurrent);
    }

    /// <summary>Runs the appearance business at the calendar's actual date.
    /// The coarse bridge does not manufacture a native scheduler moment.</summary>
    internal OrdinaryArmyAppearanceResult CompletePlayableArmyAppearance(GameState state, NativeMapSeasonDate date)
    {
        if (date.Hour is < 0 or > 22 || (date.Hour & 1) != 0)
            throw new InvalidDataException("Army appearance requires an actual two-hour date.");
        if (date.Hour != 0) return new(state, [], null);
        try
        {
            //One actual midnight operation owns one current/pool. Each exit
            //updates occupancy and raw2A for the following actor in this batch.
            var current = CaptureWritableTransportRouteCurrent(state);
            return ConsumeOrdinaryAppearance(state, date, current);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private OrdinaryArmyAppearanceResult ConsumeOrdinaryAppearance(GameState state,
        NativeMapSeasonDate date, SourceWorldContext? operationCurrent)
    {
        if (date.Hour is < 0 or > 22 || (date.Hour & 1) != 0)
            throw new InvalidDataException("Army appearance requires an actual two-hour date.");
        if (date.Hour != 0) return new(state, [], null);
        try
        {
            var current = operationCurrent ?? CaptureWritableTransportRouteCurrent(state);
            var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.Version);
            var selected = new List<(int Physical, int Unit, byte Hold)>();
            foreach (int physical in queries.ReadRegistry() ?? throw OrdinaryArmyAppearanceSource.Missing("registry"))
            {
                var receiver = queries.ReadReceiver(physical);
                if (!(receiver.IsValid ?? throw OrdinaryArmyAppearanceSource.Missing("registry_Army_validity"))) continue;
                if (!(queries.IsAtHome2C96E0(physical) ?? throw OrdinaryArmyAppearanceSource.Missing("at_home_qualification"))) continue;
                if (receiver.UnitId is not int id || !state.Units.TryGetValue(id, out var unit))
                    throw OrdinaryArmyAppearanceSource.Missing("active_Army_owner");
                selected.Add((physical, id, unit.NativeRouteHold56 ?? throw OrdinaryArmyAppearanceSource.Missing("sort_hold")));
            }
            var working = state;
            var receipts = new List<OrdinaryArmyAppearanceReceipt>();
            foreach (var actor in selected.OrderBy(a => a.Hold).ThenBy(a => a.Physical))
            {
                //CAppearArmyState snapshots once; update checks validity again,
                //without re-running the at-home filter after earlier actors.
                current = operationCurrent ?? CaptureWritableTransportRouteCurrent(working);
                if (!(current.ReadArmyUnitNativeValidity(new(actor.Physical)) ??
                      throw OrdinaryArmyAppearanceSource.Missing("update_Army_validity"))) continue;
                if (!working.Units.TryGetValue(actor.Unit, out var army))
                    throw OrdinaryArmyAppearanceSource.Missing("update_Army_owner");
                var result = OrdinaryArmyAppearanceSource.Depart(working, current, army, operationCurrent is not null);
                working = result.State; receipts.Add(result.Receipt);
            }
            return new(working, receipts.ToArray(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    /// <summary>Source2C96E0 still owns an at-home Army's exit. The period
    /// approximation must not move past a hold or a genuinely blocked exit.</summary>
    internal IReadOnlySet<int> CaptureArmiesAwaitingAppearance(GameState state)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.Version);
        var waiting = new HashSet<int>();
        foreach (int physical in queries.ReadRegistry() ?? throw OrdinaryArmyAppearanceSource.Missing("movement_registry"))
        {
            var receiver = queries.ReadReceiver(physical);
            if (!(receiver.IsValid ?? throw OrdinaryArmyAppearanceSource.Missing("movement_Army_validity"))) continue;
            if (!(queries.IsAtHome2C96E0(physical) ?? throw OrdinaryArmyAppearanceSource.Missing("movement_at_home"))) continue;
            if (receiver.UnitId is not int id || !state.Units.ContainsKey(id))
                throw OrdinaryArmyAppearanceSource.Missing("movement_active_Army_owner");
            waiting.Add(id);
        }
        return waiting;
    }
}
