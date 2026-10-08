namespace San14.Core;

public enum NativeArmyProgressStageBoundary { StageEntry, AfterArmyWaiting }
public sealed record NativeArmyProgressStageMoment(long CalendarCycleOrdinal,
    NativeMapSeasonDate PreWriterThirdDate, byte SchedulerStageIndex, int CycleWithinAtomicBlock,
    NativeArmyProgressStageBoundary Boundary);

/// <summary>Current owned stage work over a private transaction. Null is unknown
/// or busy. In particular stage8 may not be skipped to manufacture a later
/// combat capture. The ordinary Army waiting update is manager(+60), invoking
/// TC302520/SC3023F0. Stage2 entry owns any earlier work; AfterArmyWaiting owns
/// the later manager(+150) work. Manager(+80) invokes the different302290 leaf.
/// Other hour/stage-specific branches, including the separate hour6 stage2
/// callback, belong to the stage provider. Returned provenance must identify
/// the supported current branch; this interface asserts no full-world coverage.</summary>
public sealed record NativeArmyProgressStageResolution(GameState State, string SourceProvenance);
public interface IPlayableNativeArmyProgressStageRules
{
    NativeArmyProgressStageResolution? AdvanceStage(GameState state, NativeArmyProgressStageMoment moment);
    IReadOnlyList<int>? ReadCompleteWaitingArmyOrder(GameState state, NativeArmyProgressStageMoment moment);
}

/// <summary>One actual stage2 Army-manager update over a detached state.
/// The complete current actor order belongs to the caller's native owner;
/// logical IDs and pool slots are not fallback ordering keys.</summary>
public static class NativeArmyWaitingStageAtomicRuntime
{
    public static GameState? TryAdvance(GameState state, IPlayableNativeSubdayRules rules,
        NativeSubdayMoment moment, IReadOnlyList<int> completeCurrentArmyOrder)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(moment); ArgumentNullException.ThrowIfNull(completeCurrentArmyOrder);
        NativeSubdayStateValidation.Validate(state);
        var clock = state.NativeClock ?? throw new InvalidDataException("Waiting stage requires its actual owned clock.");
        if (clock.UpdatesIntoPeriod == NativeSubdayStateValidation.UpdatesPerPeriod ||
            moment.AnchorDate != clock.AnchorDate || moment.UpdateOrdinal != checked(clock.UpdateOrdinal + 1) ||
            moment.DayOffset != clock.UpdatesIntoPeriod / 4 || moment.Hour != 4 + 6 * (clock.UpdatesIntoPeriod % 4))
            throw new InvalidDataException("Waiting stage does not match its reached ordinary source manager boundary.");
        int[] expected = state.Units.Values.Where(u => u.NativeTacticRuntime is not null).Select(u => u.Id).ToArray();
        if (completeCurrentArmyOrder.Count != expected.Length || completeCurrentArmyOrder.Distinct().Count() != expected.Length ||
            !completeCurrentArmyOrder.Order().SequenceEqual(expected.Order()))
            throw new InvalidDataException("Waiting stage requires every current Army manager actor exactly once in its actual order.");
        if (state.Units.Values.Any(u => u.TacticRuntime?.Pending?.NativeIssuedAtUpdateOrdinal is not null))
            return null;
        GameState working = GameStateSnapshot.Detach(state);
        var nextClock = clock with { UpdateOrdinal = moment.UpdateOrdinal, UpdatesIntoPeriod = clock.UpdatesIntoPeriod + 1 };
        var units = new Dictionary<int, UnitState>(working.Units);
        var reports = (clock.UpdatesIntoPeriod == 0 ? [] : working.NativeSubdayReports ?? []).ToList();
        ulong random = working.RandomState;
        foreach (int id in completeCurrentArmyOrder)
        {
            UnitState unit = units[id];
            GameState current = working with { Units = units, NativeClock = nextClock, RandomState = random };
            NativeSubdayUnitResolution? resolved = rules.AdvanceUnit(current, unit, moment);
            if (resolved is null) return null;
            NativeSubdayStateValidation.ValidateRuntime(current, resolved.Runtime);
            if (resolved.Reports is null || resolved.Reports.Any(r => r is null || r.UnitId != id ||
                r.ForceId != unit.ForceId || r.UpdateOrdinal != moment.UpdateOrdinal))
                throw new InvalidDataException("Waiting source returned reports for another owned stage actor.");
            units[id] = unit with { NativeTacticRuntime = resolved.Runtime with {
                StatModifiers4A = resolved.Runtime.StatModifiers4A.ToArray(), StatDurations50 = resolved.Runtime.StatDurations50.ToArray() } };
            random = resolved.RandomState; reports.AddRange(resolved.Reports);
        }
        GameState result = working with { Units = units, NativeClock = nextClock,
            RandomState = random, NativeSubdayReports = reports.ToArray() };
        NativeSubdayStateValidation.Validate(result);
        return GameStateSnapshot.Detach(result);
    }
}
