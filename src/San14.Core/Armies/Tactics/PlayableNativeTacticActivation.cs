namespace San14.Core;

public sealed record PlayableNativeTacticActivationResult(GameState State,
    TacticReportEntry[] Reports, PeriodReportEntry[] SummaryReports);

/// <summary>Dispatch one already-queued native use at its recorded current
/// update. Automatic selection and the normal-combat frame/application belong
/// to the source stage owner; this component does not grant those permissions.</summary>
public static class PlayableNativeTacticActivation
{
    public static PlayableNativeTacticActivationResult SettleQueuedAtCurrentTick(
        GameState state, int unitId, PlayableLandRoutes routes, IPlayableTacticRules profiles)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles is not IPlayableTacticUseSource { UsesSourceEffectOrder: true })
            throw new InvalidDataException("Current-tick native activation requires an ordered source-effect provider.");
        if (state.NativeClock is not { } clock || !state.Units.TryGetValue(unitId, out UnitState? caster) ||
            caster.NativeTacticRuntime is null || caster.TacticRuntime?.Pending is not { } pending ||
            pending.NativeIssuedAtUpdateOrdinal != clock.UpdateOrdinal)
            throw new InvalidDataException("The selected native pending use does not belong to this current update.");

        GameState current = PlayableTacticRuntime.SettleNativeUseAtCurrentTick(state,
            unitId, routes, profiles, out TacticReportEntry[] reports,
            out PeriodReportEntry[] summaries);
        return new(current, reports, summaries);
    }
}
