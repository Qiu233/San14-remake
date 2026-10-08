namespace San14.Core;

public enum PlayableEventEffectKind
{
    SetAgriculture, SetCommerce, SetBarracks,
    AddAgriculture, AddCommerce, AddBarracks,
    SetFlag, AddCounter, Message, SetSecurity, AddSecurity
}

/// <summary>Resolved effects supplied by a rules adapter, not guessed native opcodes.</summary>
public sealed record PlayableEventEffect(PlayableEventEffectKind Kind, int TargetId = 0,
    int Amount = 0, string? RuntimeKey = null, string? MessageKey = null,
    int? SourceOperationIndex = null);

public sealed record PlayableEventCandidate(string Key, string Revision, bool RepeatEachPeriod,
    int? ForceId = null, OriginalEventSourceReference? Source = null);

public readonly record struct PlayableEventEligibility(bool? Eligible,
    string? MissingRuleKey = null, string? Reason = null);

public sealed record PlayableEventEffectBatch(PlayableEventEffect[] Effects, ulong RandomState);

/// <summary>
/// Order, eligibility and effects are supplied explicitly. Source adapters must
/// reject the complete execution stream when any required operation is unknown.
/// The completed-period hook is an engine schedule, not a claim about native UI timing.
/// </summary>
public interface IPlayableEventRules
{
    IReadOnlyList<PlayableEventCandidate> Candidates(GameState state);
    PlayableEventEligibility CheckEligibility(GameState state, PlayableEventCandidate candidate);
    PlayableEventEffectBatch ResolveEffects(GameState state, PlayableEventCandidate candidate);
}

/// <summary>No source programs are registered; this does not assert that native events are absent.</summary>
public sealed class UnresolvedEventRules : IPlayableEventRules
{
    public static UnresolvedEventRules Instance { get; } = new();
    private UnresolvedEventRules() { }
    public IReadOnlyList<PlayableEventCandidate> Candidates(GameState state) => [];
    public PlayableEventEligibility CheckEligibility(GameState state, PlayableEventCandidate candidate) =>
        new(null, "event.eligibility", "Original event eligibility has no bound current-state adapter.");
    public PlayableEventEffectBatch ResolveEffects(GameState state, PlayableEventCandidate candidate) =>
        throw new PlayableEventRuleUnavailableException("event.execution", "Original event execution is not bound.");
}

public sealed class PlayableEventRuleUnavailableException(string ruleKey, string message)
    : InvalidOperationException(message)
{
    public string MissingRuleKey { get; } = ruleKey;
}

public static class PlayableEventRuntime
{
    public static GameState InitializePlayableState(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        GameState next = state with
        {
            EventRuntime = state.EventRuntime ?? PlayableEventRuntimeState.Empty(state.PeriodNumber),
            EventReports = state.EventReports ?? []
        };
        PlayableEventStateValidation.Validate(next);
        return next;
    }

    /// <summary>
    /// Each event sees preceding event effects in source order. All changes are
    /// built in owned copies; an unavailable rule or invalid effect leaves the
    /// supplied snapshot unchanged. Same-period calls are idempotent.
    /// </summary>
    public static GameState SettleCompletedPeriod(GameState state, IPlayableEventRules rules)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rules);
        if (state.Phase != TurnPhase.Reporting)
            throw new InvalidOperationException("Events require a completed period in the reporting phase.");
        PlayableEventStateValidation.Validate(state);
        if (state.EventRuntime is not { } previous)
        {
            if (ReferenceEquals(rules, UnresolvedEventRules.Instance)) return state;
            throw new PlayableEventRuleUnavailableException("event.runtime", "Current event runtime is unresolved.");
        }
        if (previous.LastSettledPeriod == state.PeriodNumber) return state;
        if (previous.LastSettledPeriod != state.PeriodNumber - 1)
            throw new PlayableEventRuleUnavailableException("event.schedule", "Event runtime has an unprocessed period gap.");

        var runtime = previous with
        {
            LastSettledPeriod = state.PeriodNumber,
            SourceRevisions = new(previous.SourceRevisions, StringComparer.Ordinal),
            Executions = new(previous.Executions, StringComparer.Ordinal),
            Flags = new(previous.Flags, StringComparer.Ordinal),
            Counters = new(previous.Counters, StringComparer.Ordinal)
        };
        GameState current = state with { Areas = new(state.Areas), EventRuntime = runtime, EventReports = [] };
        var reports = new List<PlayableEventReportEntry>();
        var periodReports = state.PeriodReports.ToList();
        IReadOnlyList<PlayableEventCandidate> candidates = rules.Candidates(current)
            ?? throw new InvalidDataException("Event candidate provider returned an unknown sequence.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (PlayableEventCandidate candidate in candidates)
        {
            ValidateCandidate(candidate, current);
            if (!keys.Add(candidate.Key)) throw new InvalidDataException("Event candidate key is repeated.");
            if (runtime.SourceRevisions.TryGetValue(candidate.Key, out string? revision) && revision != candidate.Revision)
                throw new PlayableEventRuleUnavailableException("event.source_revision",
                    $"Event {candidate.Key} changed revision; an explicit state migration is required.");
            runtime.SourceRevisions[candidate.Key] = candidate.Revision;
            runtime.Executions.TryGetValue(candidate.Key, out PlayableEventExecutionStamp? stamp);
            if (stamp is not null && (!candidate.RepeatEachPeriod || stamp.LastPeriod == state.PeriodNumber)) continue;
            PlayableEventEligibility eligibility = rules.CheckEligibility(current, candidate);
            if (eligibility.Eligible is not bool eligible)
                throw new PlayableEventRuleUnavailableException(eligibility.MissingRuleKey ?? "event.eligibility",
                    eligibility.Reason ?? $"Event {candidate.Key} eligibility is unresolved.");
            if (!eligible) continue;
            PlayableEventEffectBatch batch = rules.ResolveEffects(current, candidate)
                ?? throw new InvalidDataException("Event effect provider returned an unknown batch.");
            if (batch.Effects is null)
                throw new InvalidDataException("Event effect batch is incomplete.");
            // Validate every effect before writing this event's first effect.
            foreach (PlayableEventEffect effect in batch.Effects) ValidateEffect(effect, current);
            foreach (PlayableEventEffect effect in batch.Effects)
            {
                Apply(effect, current, runtime);
                reports.Add(new(effect.Kind.ToString(), candidate.Key, candidate.Revision,
                    state.PeriodNumber, candidate.ForceId,
                    IsAreaValue(effect.Kind) ? effect.TargetId : null, effect.Amount,
                    effect.MessageKey, effect.RuntimeKey, candidate.Source, effect.SourceOperationIndex));
            }
            runtime.Executions[candidate.Key] = new(candidate.Revision, state.PeriodNumber,
                checked((stamp?.ExecutionCount ?? 0) + 1));
            reports.Add(new("triggered", candidate.Key, candidate.Revision, state.PeriodNumber,
                candidate.ForceId, Amount: batch.Effects.Length, Source: candidate.Source));
            if (candidate.ForceId is int force)
                periodReports.Add(new("event_triggered", force, Amount: batch.Effects.Length));
            current = current with { RandomState = batch.RandomState, EventReports = reports.ToArray(),
                PeriodReports = periodReports.ToArray() };
        }
        runtime = runtime with { LastSettledPeriod = state.PeriodNumber };
        current = current with { EventRuntime = runtime, EventReports = reports.ToArray(),
            PeriodReports = periodReports.ToArray() };
        PlayableEventStateValidation.Validate(current);
        return current;
    }

    private static void ValidateCandidate(PlayableEventCandidate candidate, GameState state)
    {
        if (candidate is null || !PlayableEventStateValidation.ValidKey(candidate.Key) ||
            !PlayableEventStateValidation.ValidKey(candidate.Revision) ||
            (candidate.ForceId is int force && !state.Forces.ContainsKey(force)))
            throw new InvalidDataException("Event candidate is invalid.");
        PlayableEventStateValidation.ValidateSource(candidate.Source);
    }

    private static bool IsDevelopment(PlayableEventEffectKind kind) => kind is
        PlayableEventEffectKind.SetAgriculture or PlayableEventEffectKind.SetCommerce or
        PlayableEventEffectKind.SetBarracks or PlayableEventEffectKind.AddAgriculture or
        PlayableEventEffectKind.AddCommerce or PlayableEventEffectKind.AddBarracks;
    private static bool IsAreaValue(PlayableEventEffectKind kind) => IsDevelopment(kind) ||
        kind is PlayableEventEffectKind.SetSecurity or PlayableEventEffectKind.AddSecurity;

    private static void ValidateEffect(PlayableEventEffect effect, GameState state)
    {
        if (effect is null || !Enum.IsDefined(effect.Kind) || effect.SourceOperationIndex < 0)
            throw new InvalidDataException("Event effect is invalid.");
        if (IsAreaValue(effect.Kind))
        {
            if (!state.Areas.TryGetValue(effect.TargetId, out AreaState? area))
                throw new InvalidDataException("Event target is not a current area.");
            if (effect.RuntimeKey is not null || effect.MessageKey is not null)
                throw new InvalidDataException("Area effect carries unrelated fields.");
        }
        else if (effect.Kind is PlayableEventEffectKind.SetFlag or PlayableEventEffectKind.AddCounter)
        {
            if (!PlayableEventStateValidation.ValidKey(effect.RuntimeKey) || effect.MessageKey is not null || effect.TargetId != 0)
                throw new InvalidDataException("Event flag/counter effect is invalid.");
        }
        else if (!PlayableEventStateValidation.ValidKey(effect.MessageKey) || effect.RuntimeKey is not null ||
                 effect.TargetId != 0 || effect.Amount != 0)
            throw new InvalidDataException("Event message effect is invalid.");
    }

    private static void Apply(PlayableEventEffect effect, GameState state, PlayableEventRuntimeState runtime)
    {
        if (IsDevelopment(effect.Kind))
        {
            AreaState area = state.Areas[effect.TargetId];
            AreaDevelopmentValues values = area.Development ?? new(null, null, null);
            bool add = effect.Kind is PlayableEventEffectKind.AddAgriculture or
                PlayableEventEffectKind.AddCommerce or PlayableEventEffectKind.AddBarracks;
            int? old = effect.Kind is PlayableEventEffectKind.SetAgriculture or PlayableEventEffectKind.AddAgriculture
                ? values.Agriculture : effect.Kind is PlayableEventEffectKind.SetCommerce or PlayableEventEffectKind.AddCommerce
                    ? values.Commerce : values.Barracks;
            if (add && old is null)
                throw new PlayableEventRuleUnavailableException("event.area_development",
                    $"Area {effect.TargetId} current development is unresolved.");
            // Explicit effect arithmetic has wide intermediates. The native
            // wrapper's wrapping i32 arithmetic lives in the Data interpreter.
            int value = checked((int)Math.Clamp(add ? (long)old!.Value + effect.Amount : effect.Amount, 0L, 1000L));
            values = effect.Kind is PlayableEventEffectKind.SetAgriculture or PlayableEventEffectKind.AddAgriculture
                ? values with { Agriculture = value }
                : effect.Kind is PlayableEventEffectKind.SetCommerce or PlayableEventEffectKind.AddCommerce
                    ? values with { Commerce = value } : values with { Barracks = value };
            state.Areas[area.Id] = area with { Development = values };
        }
        else if (effect.Kind is PlayableEventEffectKind.SetSecurity or PlayableEventEffectKind.AddSecurity)
        {
            AreaState area = state.Areas[effect.TargetId];
            bool add = effect.Kind == PlayableEventEffectKind.AddSecurity;
            if (add && area.Security is null)
                throw new PlayableEventRuleUnavailableException("event.area_security",
                    $"Area {area.Id} current security is unresolved.");
            int value = checked((int)Math.Clamp(add ? (long)area.Security!.Value + effect.Amount : effect.Amount, 0L, 100L));
            state.Areas[area.Id] = area with { Security = value };
        }
        else if (effect.Kind == PlayableEventEffectKind.SetFlag) runtime.Flags[effect.RuntimeKey!] = effect.Amount;
        else if (effect.Kind == PlayableEventEffectKind.AddCounter)
            runtime.Counters[effect.RuntimeKey!] = checked(runtime.Counters.GetValueOrDefault(effect.RuntimeKey!) + effect.Amount);
    }
}
