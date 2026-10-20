namespace San14.Core;

public enum NativeArmyProgressStageBoundary { StageEntry, AfterArmyWaiting }
public enum NativeArmyProgressStage : byte
{
    PersonnelReturn = 0,
    MapMusic = 1,
    Waiting = 2,
    SpecialArmyActions = 3,
    RestoreArmyCommands = 4,
    DuelOpportunity = 5,
    ManualTacticIntervention = 6,
    PrepareMovement = 7,
    Movement = 8,
    Appearance = 9,
    ArmyNotifications = 10,
    Occupation = 11,
    Demolition = 12,
    Combat = 13,
    FacilityEffects = 14,
    FacilityConstruction = 15,
    FacilityRepair = 16,
    PersonalityMaintenance = 17,
    ArmyMoraleFatigue = 18,
    BaseWoundedRecovery = 19,
    HexFire = 20,
    FireCommands = 21,
    FacilityTraps = 22,
    FireDamage = 23,
    TerrainDamage = 24,
    Defeat = 25,
    ReleaseRetired = 26,
    MapObjectRetirement = 27,
    MaintainArmyCommands = 28,
    MapArmyGrouping = 29,
    OfficerGrowthNotifications = 30,
    TaskSceneFinalization = 31,
    Count = 32
}
public sealed record NativeArmyProgressStageMoment(long CalendarCycleOrdinal,
    NativeMapSeasonDate PreWriterThirdDate, byte SchedulerStageIndex, int CycleWithinAtomicBlock,
    NativeArmyProgressStageBoundary Boundary);

/// <summary>One completed stage and the rule branch that performed it.</summary>
public sealed record NativeArmyProgressStageResolution(GameState State, string RuleId);
/// <summary>Creates temporary rule state for an entire attempted progress transaction,
/// which may contain several consecutive six-hour save blocks.</summary>
public interface IPlayableNativeArmyProgressStageSource
{
    IPlayableNativeArmyProgressStageRules BeginOperation();
}

/// <summary>Rules receive an operation-owned world and transfer the resulting
/// world back to that operation. They may update it, but must not retain mutable
/// world collections after the operation or return borrowed external collections.
/// Calendar advancement belongs to the driver. Null means unresolved or busy.</summary>
public interface IPlayableNativeArmyProgressStageRules
{
    NativeArmyProgressStageResolution? AdvanceStage(GameState state, NativeArmyProgressStageMoment moment);
    /// <summary>Reads the full current manager order without changing the supplied world.</summary>
    IReadOnlyList<int>? ReadCompleteWaitingArmyOrder(GameState state, NativeArmyProgressStageMoment moment);
    /// <summary>Publishes derived presentation state only after the entire
    /// numerical transaction succeeds. Failed attempts never call this hook.</summary>
    void PublishDerivedState(GameState committedState) { }
}

/// <summary>One waiting update over the complete caller-supplied manager order.</summary>
public static class NativeArmyWaitingStageAtomicRuntime
{
    public static GameState? TryAdvance(GameState state, IPlayableNativeSubdayRules rules,
        NativeSubdayMoment moment, IReadOnlyList<int> completeCurrentArmyOrder)
        => TryAdvanceOwned(GameStateCopy.Copy(state), rules, moment, completeCurrentArmyOrder);

    /// <summary>Uses the caller's private operation world; failure must discard that operation.</summary>
    public static GameState? TryAdvanceOwned(GameState state, IPlayableNativeSubdayRules rules,
        NativeSubdayMoment moment, IReadOnlyList<int> completeCurrentArmyOrder)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(moment); ArgumentNullException.ThrowIfNull(completeCurrentArmyOrder);
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
        GameState working = state;
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
        return result;
    }
}
