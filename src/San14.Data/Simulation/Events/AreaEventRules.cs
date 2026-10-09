using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Registers a complete original directory stream. The stable engine key,
/// repetition policy, timing and current-condition adapter are explicit inputs.
/// They are not inferred from a record's raw +34 or a source filename.
/// </summary>
public sealed record AreaEventRegistration(string Key, int EventId,
    bool RepeatEachPeriod, int? ForceId = null);

/// <summary>
/// A caller-owned execution projection. Reads must observe earlier writes in
/// the same execution, while GetResolvedEffects returns independent engine
/// effects including the full setter side effects required by its profile.
/// </summary>
public interface IAreaEventExecutionBridge : IS14AreaEventExecutionState
{
    IReadOnlyList<S14EventExecutionArea> InitialAreaSelection { get; }
    S14ConditionDispatchInputs ReadDispatchInputs(S14ConditionContext context);
    PlayableEventEffectBatch GetResolvedEffects();
}

/// <summary>
/// Binds the supported original area bytecode to the independent core's period
/// scheduler. No native current-state or initialization fallback is supplied.
/// Unsupported source instructions fail before a bridge is constructed.
/// </summary>
public sealed class AreaEventRules : IPlayableEventRules
{
    private readonly IReadOnlyDictionary<string, AreaEventRegistration> _registrations;
    private readonly Dictionary<string, S14EventExecutionProgram> _programs = new(StringComparer.Ordinal);
    private readonly PlayableEventCandidate[] _candidates;
    private readonly S14EventDocument _document;
    private readonly Func<GameState, PlayableEventCandidate, PlayableEventEligibility> _eligibility;
    private readonly Func<GameState, PlayableEventCandidate, IAreaEventExecutionBridge> _bridge;

    public AreaEventRules(S14EventDocument document, string resourceUri,
        IEnumerable<AreaEventRegistration> registrations,
        Func<GameState, PlayableEventCandidate, PlayableEventEligibility> eligibility,
        Func<GameState, PlayableEventCandidate, IAreaEventExecutionBridge> executionBridge)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(registrations);
        _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
        _bridge = executionBridge ?? throw new ArgumentNullException(nameof(executionBridge));
        _document = S14EventReader.Read(document.RawBytes);
        if (_document.SourceSha256 != document.SourceSha256)
            throw new InvalidDataException("Event registration document has a mismatched resource hash.");
        var owned = new Dictionary<string, AreaEventRegistration>(StringComparer.Ordinal);
        var candidates = new List<PlayableEventCandidate>();
        foreach (AreaEventRegistration registration in registrations)
        {
            if (registration is null || string.IsNullOrWhiteSpace(registration.Key) ||
                !owned.TryAdd(registration.Key, registration))
                throw new InvalidDataException("Original event registration key is missing or repeated.");
            long directoryIndex = (long)registration.EventId - _document.NativeBaseId;
            if (directoryIndex < 0 || directoryIndex >= _document.ExecutionStreams.Count)
                throw new InvalidDataException("Original event registration is outside the execution directory.");
            var source = new EventSourceReference(resourceUri, _document.SourceSha256,
                (int)directoryIndex, registration.EventId);
            PlayableEventStateValidation.ValidateSource(source);
            candidates.Add(new(registration.Key, _document.SourceSha256, registration.RepeatEachPeriod,
                registration.ForceId, source));
        }
        _registrations = owned;
        _candidates = candidates.ToArray();
    }

    public IReadOnlyList<PlayableEventCandidate> Candidates(GameState state) => Array.AsReadOnly(_candidates);

    public PlayableEventEligibility CheckEligibility(GameState state, PlayableEventCandidate candidate)
    {
        CheckCandidate(candidate);
        return _eligibility(state, candidate);
    }

    public PlayableEventEffectBatch ResolveEffects(GameState state, PlayableEventCandidate candidate)
    {
        CheckCandidate(candidate);
        if (!_programs.TryGetValue(candidate.Key, out S14EventExecutionProgram? program))
        {
            try { program = S14EventExecutionProgram.ReadEvent(_document, _registrations[candidate.Key].EventId); }
            catch (InvalidDataException error)
            {
                throw new PlayableEventRuleUnavailableException("event.execution_opcode", error.Message);
            }
            _programs.Add(candidate.Key, program);
        }
        IAreaEventExecutionBridge bridge = _bridge(state, candidate)
            ?? throw new PlayableEventRuleUnavailableException("event.current_execution_state",
                "Original event execution bridge is unavailable.");
        var runtime = new S14AreaEventExecutionRuntime(bridge, bridge.ReadDispatchInputs,
            bridge.InitialAreaSelection, bridge as IS14AudioEventExecutionState);
        runtime.Execute(program);
        PlayableEventEffectBatch effects = bridge.GetResolvedEffects()
            ?? throw new InvalidDataException("Event execution bridge produced an unknown effect batch.");
        if (effects.Effects is null || effects.Effects.Any(effect => effect is null ||
            effect.SourceOperationIndex is not int index || index < 0 || index >= program.DispatchProgram.Operations.Count))
            throw new InvalidDataException("Original event effects lack an exact source instruction reference.");
        return effects with { Effects = effects.Effects?.ToArray() ?? throw new InvalidDataException("Event effects are unknown.") };
    }

    private void CheckCandidate(PlayableEventCandidate candidate)
    {
        if (candidate is null || !_registrations.ContainsKey(candidate.Key) ||
            !_candidates.Contains(candidate))
            throw new InvalidDataException("Candidate does not belong to this original event source binding.");
    }
}
