namespace San14.Core;

public enum PlayableTacticTargetRelation { Self, Friendly, Hostile }
public enum PlayableTacticEffectKind
{
    ArmyDamage, MoraleChange, AttackPercent, DefensePercent,
    Immobilized, Confused, Provoked, ClearHarmfulStatuses, ClearAbnormalStatuses, ArmyHealing, NativeAbnormalChange,
    NativeMoraleChange
}

/// <summary>Explicit ruleset values. Neither amounts nor period durations are inferred from S14 bytes.</summary>
public sealed record PlayableTacticEffect(PlayableTacticEffectKind Kind, int Amount,
    int DurationPeriods = 0)
{
    public TacticArmyHealingResult? HealingResult { get; init; }
    public NativeTacticAbnormalAssignment? NativeAbnormalAfter { get; init; }
    public byte? NativeMoraleAfter { get; init; }
}

/// <summary>A resolved profile supplied by an adapter. Its revision must remain stable across a queued use.</summary>
public sealed record PlayableTacticDefinition(int TacticId, string ProfileRevision,
    PlayableTacticTargetRelation TargetRelation, int RangeHexes, int AreaRadiusHexes,
    int CooldownPeriods, PlayableTacticEffect[] Effects)
{
    public NativeTacticWaitingAssignment? NativeActivationWaiting { get; init; }
}

public sealed record QueuedTacticUse(int TacticId, int TargetUnitId,
    int IssuedAtPeriod, string ProfileRevision)
{
    public long? NativeIssuedAtUpdateOrdinal { get; init; }
}
public sealed record UnitTacticStatus(PlayableTacticEffectKind Kind, int Amount,
    int SourceUnitId, int TacticId, int ExpiresAtPeriod, string ProfileRevision);
public sealed record UnitTacticRuntime(QueuedTacticUse? Pending,
    Dictionary<int, int> ReadyAtPeriod, UnitTacticStatus[] Statuses)
{
    public static UnitTacticRuntime Empty() => new(null, [], []);
}

public sealed record TacticReportEntry(string Kind, int Period, int ForceId,
    int UnitId, int OfficerId, int TacticId, int? TargetUnitId = null,
    PlayableTacticEffectKind? Effect = null, int Amount = 0,
    int? ExpiresAtPeriod = null, string? Reason = null)
{
    public long? NativeUpdateOrdinal { get; init; }
}

public sealed partial record UnitState
{
    // Null is uninitialized or legacy unknown. An explicit empty record has no active effects.
    public UnitTacticRuntime? TacticRuntime { get; init; }
}

public sealed partial record GameState
{
    // Detailed reports preserve tactic and target identities even if a unit has since been destroyed.
    public TacticReportEntry[]? TacticReports { get; init; }
}

public static class PlayableTacticStateValidation
{
    public static void Validate(GameState state)
    {
        foreach (UnitState unit in state.Units.Values)
        {
            if (unit.TacticRuntime is not { } runtime) continue;
            if (runtime.ReadyAtPeriod is null || runtime.Statuses is null || runtime.Statuses.Length > 1024)
                throw new InvalidDataException("Unit tactic runtime collections are missing.");
            foreach ((int tacticId, int ready) in runtime.ReadyAtPeriod)
                if (!state.Tactics.ContainsKey(tacticId) || ready < 0)
                    throw new InvalidDataException("Unit tactic cooldown is invalid.");
            if (runtime.Pending is { } pending &&
                (!state.Tactics.ContainsKey(pending.TacticId) ||
                 unit.TacticIds is not { } equipped || !equipped.Contains(pending.TacticId) ||
                 pending.TargetUnitId < 0 || pending.IssuedAtPeriod != state.PeriodNumber ||
                 pending.NativeIssuedAtUpdateOrdinal < 0 ||
                 pending.NativeIssuedAtUpdateOrdinal is long issued &&
                     (state.NativeClock is null || issued != state.NativeClock.UpdateOrdinal) ||
                 state.Phase != TurnPhase.Strategy || string.IsNullOrWhiteSpace(pending.ProfileRevision)))
                throw new InvalidDataException("Queued tactic use is invalid.");
            foreach (UnitTacticStatus status in runtime.Statuses)
                if (status is null || !IsTimed(status.Kind) || !ValidAmount(status.Amount) ||
                    status.SourceUnitId < 0 || !state.Tactics.ContainsKey(status.TacticId) ||
                    status.ExpiresAtPeriod < 1 || string.IsNullOrWhiteSpace(status.ProfileRevision))
                    throw new InvalidDataException("Unit tactic status is invalid.");
        }
        foreach (TacticReportEntry entry in state.TacticReports ?? [])
            if (entry is null || entry.Kind is not ("used" or "effect" or "expired" or "fizzled") ||
                entry.Period < 0 || entry.Period > checked(state.PeriodNumber + 1) ||
                !state.Forces.ContainsKey(entry.ForceId) || entry.UnitId < 0 ||
                !state.Officers.ContainsKey(entry.OfficerId) || !state.Tactics.ContainsKey(entry.TacticId) ||
                entry.TargetUnitId < 0 || (entry.Effect is { } kind && !Enum.IsDefined(kind)) ||
                entry.NativeUpdateOrdinal < 0 || entry.NativeUpdateOrdinal is long update &&
                    (state.NativeClock is null || update > state.NativeClock.UpdateOrdinal) ||
                entry.ExpiresAtPeriod < 1)
                throw new InvalidDataException("Tactic report is invalid.");
    }

    public static void ValidateDefinition(PlayableTacticDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.TacticId < 1 || string.IsNullOrWhiteSpace(definition.ProfileRevision) ||
            !Enum.IsDefined(definition.TargetRelation) || definition.RangeHexes < 0 ||
            definition.AreaRadiusHexes < 0 || definition.CooldownPeriods < 0 ||
            definition.Effects is not { Length: <= 32 })
            throw new InvalidDataException("Tactic profile is invalid.");
        foreach (PlayableTacticEffect effect in definition.Effects)
        {
            if (effect is null || !Enum.IsDefined(effect.Kind) || !ValidAmount(effect.Amount) ||
                (IsTimed(effect.Kind) ? effect.DurationPeriods < 1 : effect.DurationPeriods != 0) ||
                (effect.Kind == PlayableTacticEffectKind.ArmyDamage && effect.Amount < 0) ||
                (effect.Kind == PlayableTacticEffectKind.ArmyHealing
                    ? effect.HealingResult is not { TroopsAfter: >= 1, WoundedAfter: >= 0 and <= ushort.MaxValue }
                    : effect.HealingResult is not null) ||
                (effect.Kind == PlayableTacticEffectKind.NativeAbnormalChange && effect.NativeAbnormalAfter is null) ||
                (effect.NativeAbnormalAfter is not null && effect.Kind is not
                    (PlayableTacticEffectKind.NativeAbnormalChange or PlayableTacticEffectKind.ClearAbnormalStatuses)) ||
                (effect.Kind == PlayableTacticEffectKind.NativeMoraleChange
                    ? effect.NativeMoraleAfter is null : effect.NativeMoraleAfter is not null))
                throw new InvalidDataException("Tactic effect profile is invalid.");
        }
        if (definition.NativeActivationWaiting is { } waiting && string.IsNullOrWhiteSpace(waiting.Revision))
            throw new InvalidDataException("Native tactic waiting assignment revision is unresolved.");
    }

    public static bool IsTimed(PlayableTacticEffectKind kind) => kind is
        PlayableTacticEffectKind.AttackPercent or PlayableTacticEffectKind.DefensePercent or
        PlayableTacticEffectKind.Immobilized or PlayableTacticEffectKind.Confused or
        PlayableTacticEffectKind.Provoked;

    private static bool ValidAmount(int amount) => amount is >= -1_000_000 and <= 1_000_000;
}
