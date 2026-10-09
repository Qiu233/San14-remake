namespace San14.Core;

/// <summary>An explicit current clock; old snapshots never receive an inferred native epoch.</summary>
public sealed record NativeSubdayClock(PeriodDate AnchorDate, long UpdateOrdinal,
    int UpdatesIntoPeriod, string Revision);
public sealed record NativeTacticOwnerReference(int ForceId, DeploymentTargetKind Kind, int Id);
public sealed record NativeArmyTacticRuntime(byte Status10, byte Flags26,
    byte Waiting20, byte Waiting21, byte Waiting22, byte Abnormal23, byte Abnormal24, byte Abnormal25,
    sbyte[] StatModifiers4A, byte[] StatDurations50, NativeTacticOwnerReference? ProvocationOwner,
    long? PrimaryWaitingReadyAtUpdateOrdinal);
public sealed record NativeTacticWaitingAssignment(byte Waiting20, byte Waiting21, byte Waiting22, string Revision);
public sealed record NativeTacticAbnormalAssignment(byte Abnormal23, byte Abnormal24, byte Abnormal25,
    NativeTacticOwnerReference? ProvocationOwner)
{
    public NativeArmyOverrideTargetRuntime? OverrideTarget { get; init; }
}
public sealed record TacticArmyHealingResult(int TroopsAfter, int WoundedAfter);
public sealed record NativeSubdayMoment(PeriodDate AnchorDate, long UpdateOrdinal, int DayOffset, int Hour);
public sealed record NativeSubdayReport(string Kind, long UpdateOrdinal, int UnitId, int ForceId,
    int? NativeIndex = null, int Amount = 0);
public sealed record NativeSubdayUnitResolution(NativeArmyTacticRuntime Runtime, ulong RandomState,
    NativeSubdayReport[] Reports);

/// <summary>Rules borrow the operation's current world for this callback only.
/// They must not mutate or retain its collections. Returned runtime arrays are
/// admitted into the operation by the waiting driver.</summary>
public interface IPlayableNativeSubdayRules
{
    NativeSubdayUnitResolution? AdvanceUnit(GameState state, UnitState unit, NativeSubdayMoment moment);
}

public sealed partial record UnitState
{
    public int? WoundedTroops { get; init; }
    public NativeArmyTacticRuntime? NativeTacticRuntime { get; init; }
}
public sealed partial record GameState
{
    public NativeSubdayClock? NativeClock { get; init; }
    public NativeSubdayReport[]? NativeSubdayReports { get; init; }
}

public static class NativeSubdayStateValidation
{
    public const int UpdatesPerPeriod = 40;
    public static void Validate(GameState state)
    {
        NativeArmySimulationStateValidation.Validate(state);
        NativeMapSeasonStateValidation.Validate(state);
        if (state.NativeClock is { } clock &&
            (clock.UpdateOrdinal < 0 || clock.UpdatesIntoPeriod is < 0 or > UpdatesPerPeriod ||
             clock.UpdateOrdinal < clock.UpdatesIntoPeriod || clock.AnchorDate != state.Date ||
             string.IsNullOrWhiteSpace(clock.Revision)))
            throw new InvalidDataException("Native subday clock is incomplete or belongs to another period.");
        foreach (UnitState unit in state.Units.Values)
        {
            PlayableArmyCreation.Validate(state, unit);
            if (unit.WoundedTroops is < 0 or > ushort.MaxValue)
                throw new InvalidDataException("Unit wounded troops are outside the native quantity range.");
            if (unit.NativeTacticRuntime is { } runtime) ValidateRuntime(state, runtime);
        }
        int[] ownedArmySlots = state.Units.Values.Where(unit => unit.NativeArmyOrigin is not null)
            .Select(unit => unit.NativeArmyOrigin!.NativeSlotIndex!.Value).ToArray();
        if (ownedArmySlots.Distinct().Count() != ownedArmySlots.Length)
            throw new InvalidDataException("Source-created Armies share an owned native pool slot.");
        foreach (NativeSubdayReport report in state.NativeSubdayReports ?? [])
            if (report is null || string.IsNullOrWhiteSpace(report.Kind) || report.UpdateOrdinal < 0 ||
                state.NativeClock is null || report.UpdateOrdinal > state.NativeClock.UpdateOrdinal ||
                report.UnitId < 0 || !state.Forces.ContainsKey(report.ForceId) || report.NativeIndex < 0)
                throw new InvalidDataException("Native subday report has an invalid clock or identity.");
    }
    public static void ValidateRuntime(GameState state, NativeArmyTacticRuntime runtime)
    {
        if (runtime is null || runtime.StatModifiers4A is not { Length: 6 } || runtime.StatDurations50 is not { Length: 6 } ||
            runtime.PrimaryWaitingReadyAtUpdateOrdinal < 0 ||
            runtime.Waiting20 > 0 && runtime.PrimaryWaitingReadyAtUpdateOrdinal is not null ||
            runtime.PrimaryWaitingReadyAtUpdateOrdinal is long ready &&
                (state.NativeClock is null || ready > state.NativeClock.UpdateOrdinal))
            throw new InvalidDataException("Native Army tactic state is incomplete or has an invalid readiness stamp.");
        ValidateOwner(state, runtime.ProvocationOwner);
    }
    public static void ValidateOwner(GameState state, NativeTacticOwnerReference? owner)
    {
        if (owner is not null && (!state.Forces.ContainsKey(owner.ForceId) || owner.Id < 0 ||
            owner.Kind is not (DeploymentTargetKind.Unit or DeploymentTargetKind.Location)))
            throw new InvalidDataException("Native provocation owner has an invalid typed identity.");
        // A captured owner may disappear later. Preserve its identity until the
        // reached source branch explicitly clears it.
    }
}

public static class NativeSubdayRuntime
{
    private static readonly int[] Hours = [4, 10, 16, 22];

    /// <summary>Advances one proven manager update; current rates and gates stay in the provider.</summary>
    public static GameState AdvanceOneUpdate(GameState state, IPlayableNativeSubdayRules rules)
    {
        if (state.NativeClock is not { } clock)
            throw new InvalidDataException("Native subday clock is unresolved.");
        if (clock.UpdatesIntoPeriod == NativeSubdayStateValidation.UpdatesPerPeriod) return state;
        GameState working = GameStateSnapshot.Detach(state);
        if (clock.UpdatesIntoPeriod == 0) working = working with { NativeSubdayReports = [] };
        return AdvanceOwned(working, rules);
    }

    /// <summary>Completes the remaining updates once; a saved cursor never repeats completed ticks.</summary>
    public static GameState CompletePeriod(GameState state, IPlayableNativeSubdayRules? rules)
    {
        if (state.NativeClock is null ||
            state.NativeClock.UpdatesIntoPeriod == NativeSubdayStateValidation.UpdatesPerPeriod) return state;
        return CompletePeriodOwned(GameStateCopy.Copy(state), rules);
    }

    /// <summary>Completes updates inside an existing private period operation.</summary>
    public static GameState CompletePeriodOwned(GameState state, IPlayableNativeSubdayRules? rules)
    {
        if (state.NativeClock is null) return state;
        if (state.NativeClock.UpdatesIntoPeriod == NativeSubdayStateValidation.UpdatesPerPeriod) return state;
        if (rules is null) throw new InvalidDataException("Native subday updater rules are unresolved.");
        GameState working = state;
        if (working.NativeClock!.UpdatesIntoPeriod == 0) working = working with { NativeSubdayReports = [] };
        while (working.NativeClock!.UpdatesIntoPeriod < NativeSubdayStateValidation.UpdatesPerPeriod)
            working = AdvanceOwned(working, rules);
        return working;
    }

    private static GameState AdvanceOwned(GameState state, IPlayableNativeSubdayRules rules)
    {
        if (state.Units.Values.Any(unit => unit.TacticRuntime?.Pending?.NativeIssuedAtUpdateOrdinal is not null))
            throw new InvalidDataException("Native queued activation must settle at its recorded tick before manager updates.");
        NativeSubdayClock clock = state.NativeClock!;
        var nextClock = clock with
        { UpdateOrdinal = checked(clock.UpdateOrdinal + 1), UpdatesIntoPeriod = clock.UpdatesIntoPeriod + 1 };
        var moment = new NativeSubdayMoment(clock.AnchorDate, nextClock.UpdateOrdinal,
            clock.UpdatesIntoPeriod / 4, Hours[clock.UpdatesIntoPeriod % 4]);
        var units = new Dictionary<int, UnitState>(state.Units);
        var reports = (state.NativeSubdayReports ?? []).ToList();
        ulong randomState = state.RandomState;
        foreach (UnitState unit in state.Units.Values.OrderBy(unit => unit.Id))
        {
            if (unit.NativeTacticRuntime is null) continue;
            GameState current = state with { Units = units, RandomState = randomState, NativeClock = nextClock };
            NativeSubdayUnitResolution result = rules.AdvanceUnit(current, units[unit.Id], moment)
                ?? throw new InvalidDataException("Current native subday inputs are unresolved.");
            NativeSubdayStateValidation.ValidateRuntime(current, result.Runtime);
            if (result.Reports is null || result.Reports.Any(report => report is null ||
                    report.UpdateOrdinal != moment.UpdateOrdinal || report.UnitId != unit.Id || report.ForceId != unit.ForceId))
                throw new InvalidDataException("Native subday provider returned invalid update reports.");
            units[unit.Id] = units[unit.Id] with { NativeTacticRuntime = result.Runtime with
            { StatModifiers4A = result.Runtime.StatModifiers4A.ToArray(), StatDurations50 = result.Runtime.StatDurations50.ToArray() } };
            randomState = result.RandomState;
            reports.AddRange(result.Reports);
        }
        GameState next = state with { Units = units, NativeClock = nextClock, RandomState = randomState, NativeSubdayReports = reports.ToArray() };
        return next;
    }
}
