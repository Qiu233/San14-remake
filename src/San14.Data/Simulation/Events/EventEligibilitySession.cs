using San14.Data.Formats;

namespace San14.Data;

/// <summary>Mutable state of an event scanner, independent of world event histories.</summary>
public sealed class EventEligibilitySession
{
    private readonly EventFlagHistory _blockedFlags = new();
    private readonly List<S14EventEligibilityResult> _results = [];
    private readonly IReadOnlyList<S14EventEligibilityResult> _resultsView;
    private bool _resultsKnown;

    public EventEligibilitySession() => _resultsView = _results.AsReadOnly();

    public bool? IsPresent { get; set; }
    public bool? HasAvailableResult { get; private set; }
    public IReadOnlyList<S14EventEligibilityResult>? Results => _resultsKnown ? _resultsView : null;

    public bool? ReadBlockedFlag(int eventId) => _blockedFlags.Read(eventId);
    public void SetBlockedFlag(int eventId, bool? value) => _blockedFlags.Set(eventId, value);
    public void ResetBlockedFlags() => _blockedFlags.Clear();
    public void ResetResults()
    {
        _results.Clear();
        _resultsKnown = true;
    }
    public void AppendResult(S14EventEligibilityResult result)
    {
        if (result is null || !_resultsKnown)
            throw new InvalidDataException("Current manager results are unknown; reset is required before append.");
        _results.Add(result);
    }
    public void SetAvailableResult(bool value) => HasAvailableResult = value;
    internal void RestoreAvailableResult(bool? value) => HasAvailableResult = value;
}
