using San14.Core;

namespace San14.Data;

/// <summary>Reached current Army/World/parameter queries. Null is unresolved;
/// all callbacks observe the staged state at their native read boundary.</summary>
public interface ITacticEffectSubdaySourceContext
{
    bool? ReadArmyValidity(GameState state, UnitState unit, NativeSubdayMoment moment);
    bool? ReadExpirationLoggingForceNonzero(GameState state, UnitState unit);
    int? ReadAbnormalUpperBound(GameState state, UnitState unit, int nativeIndex);
    bool? ReadSpecialWaitingModeActive(GameState state, UnitState unit);
    int? ReadOrdinaryWaitingRate(GameState state, UnitState unit);
    int? ReadSelectedCommanderWaitingRate(GameState state, UnitState unit, int nativeIndex) => null;
    int? ReadWaitingUpperBound(GameState state, UnitState unit, int nativeIndex);
}

/// <summary>SC3023F0 ordinary Army updater at the four proven daily manager
/// calls. This independently advances all three abnormal counters, six modifier
/// timers and the mask02-enabled primary waiting counter. No native code runs.</summary>
public sealed class TacticEffectSourceSubdayRules : IPlayableNativeSubdayRules
{
    private readonly ITacticEffectSubdaySourceContext _context;
    public TacticEffectSourceSubdayRules(ITacticEffectSubdaySourceContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public NativeSubdayUnitResolution? AdvanceUnit(GameState state, UnitState unit,
        NativeSubdayMoment moment)
    {
        if (!TacticEffectSourceWaiting.IsOrdinaryWaitingUpdateHour(moment.Hour) ||
            moment.DayOffset is < 0 or > 9 || moment.UpdateOrdinal < 1 ||
            state.NativeClock?.UpdateOrdinal != moment.UpdateOrdinal)
            throw new InvalidDataException("Source Army update has a different native clock boundary.");
        if (unit.NativeTacticRuntime is not { } runtime) return null;
        bool? valid = _context.ReadArmyValidity(state, unit, moment);
        if (valid is null) return null;
        if (!valid.Value) return new(runtime with
            { StatModifiers4A = runtime.StatModifiers4A.ToArray(), StatDurations50 = runtime.StatDurations50.ToArray() },
            state.RandomState, []);
        bool? logging = _context.ReadExpirationLoggingForceNonzero(state, unit);
        if (logging is null) return null;
        var reports = new List<NativeSubdayReport>();
        NativeArmyTacticRuntime changed = runtime with
        { StatModifiers4A = runtime.StatModifiers4A.ToArray(), StatDurations50 = runtime.StatDurations50.ToArray() };
        UnitState current = unit with { NativeTacticRuntime = changed };
        GameState staged = Stage(state, current);
        for (int index = 0; index < 3; index++)
        {
            byte former = Abnormal(changed, index);
            int? limit = _context.ReadAbnormalUpperBound(staged, current, index);
            if (limit is null) return null;
            byte next = TacticEffectSourceTickMath.DecayAbnormal(former, limit.Value);
            changed = index switch
            {
                0 => changed with { Abnormal23 = next },
                1 => changed with { Abnormal24 = next },
                _ => changed with { Abnormal25 = next }
            };
            // Natural expiry has no native3C/3E owner write.
            if (logging.Value && former > 0 && next == 0)
                reports.Add(new("abnormal_expired", moment.UpdateOrdinal, unit.Id, unit.ForceId, index));
            current = current with { NativeTacticRuntime = changed };
            staged = Stage(staged, current);
        }
        for (int index = 0; index < 6; index++)
        {
            changed.StatDurations50[index] = TacticEffectSourceTickMath.DecayModifierDuration(
                changed.StatDurations50[index]);
            if (changed.StatDurations50[index] == 0) changed.StatModifiers4A[index] = 0;
        }
        current = current with { NativeTacticRuntime = changed };
        staged = Stage(staged, current);
        // The instruction is TEST byte[Army+26],2: mask02, bit index1.
        if ((changed.Flags26 & 0x02) != 0)
        {
            bool? special = _context.ReadSpecialWaitingModeActive(staged, current);
            if (special is null) return null;
            int waitingIndex = 0;
            if (special.Value)
            {
                //TC302430 takes the first nonzero byte, in20/21/22 order.
                //An active selected mode with no waiting has no counter write.
                if (changed.Waiting20 == 0)
                    waitingIndex = changed.Waiting21 != 0 ? 1 : changed.Waiting22 != 0 ? 2 : -1;
                if (waitingIndex < 0) return new(changed, state.RandomState, reports.ToArray());
            }
            int? rate = special.Value
                ? _context.ReadSelectedCommanderWaitingRate(staged, current, waitingIndex)
                : _context.ReadOrdinaryWaitingRate(staged, current);
            if (rate is null) return null;
            //224110 captures caps2 and1 before selecting cap0, even for
            //the selected index. Preserve those reads before its two cap reads.
            if (_context.ReadWaitingUpperBound(staged, current, 2) is null) return null;
            if (_context.ReadWaitingUpperBound(staged, current, 1) is null) return null;
            int? firstCap = _context.ReadWaitingUpperBound(staged, current, waitingIndex);
            if (firstCap is null) return null;
            int? secondCap = _context.ReadWaitingUpperBound(staged, current, waitingIndex);
            if (secondCap is null) return null;
            byte formerWaiting = waitingIndex switch { 0 => changed.Waiting20, 1 => changed.Waiting21, _ => changed.Waiting22 };
            byte next = TacticEffectSourceWaiting.AddCounter(formerWaiting,
                unchecked(-rate.Value), firstCap.Value, secondCap.Value);
            long? ready = changed.PrimaryWaitingReadyAtUpdateOrdinal;
            if (formerWaiting > 0 && next == 0)
            {
                if (waitingIndex == 0) ready = moment.UpdateOrdinal;
                reports.Add(new("waiting_ready", moment.UpdateOrdinal, unit.Id, unit.ForceId, waitingIndex));
            }
            else if (waitingIndex == 0 && next > 0) ready = null;
            changed = waitingIndex switch
            {
                0 => changed with { Waiting20 = next, PrimaryWaitingReadyAtUpdateOrdinal = ready },
                1 => changed with { Waiting21 = next },
                _ => changed with { Waiting22 = next }
            };
        }
        return new(changed, state.RandomState, reports.ToArray());
    }

    private static byte Abnormal(NativeArmyTacticRuntime runtime, int index) => index switch
    { 0 => runtime.Abnormal23, 1 => runtime.Abnormal24, _ => runtime.Abnormal25 };

    private static GameState Stage(GameState state, UnitState unit) => state with
    { Units = new(state.Units) { [unit.Id] = unit } };
}
