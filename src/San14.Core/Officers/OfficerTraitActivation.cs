using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Temporary activation of an officer's trait slots. Activation and
/// duration knowledge are independent; a missing collection is unresolved.
/// Slot identities are retained even when their trait meaning is unknown.</summary>
public sealed class OfficerTraitActivation
{
    private readonly HashSet<int>? _activeSlots;
    private readonly int[]? _remainingDurations;

    [JsonConstructor]
    public OfficerTraitActivation(int[]? activeSlots, int[]? remainingDurations)
    {
        if (activeSlots is not null)
        {
            _activeSlots = new(activeSlots);
            if (_activeSlots.Count != activeSlots.Length || _activeSlots.Any(slot => slot < 0))
                throw new InvalidDataException("Activated trait slots must be unique nonnegative identities.");
        }
        if (remainingDurations is not null)
        {
            if (remainingDurations.Any(duration => duration < 0))
                throw new InvalidDataException("Trait activation durations cannot be negative.");
            _remainingDurations = remainingDurations.ToArray();
        }
    }

    // Persistence receives detached values. Simulation queries never expose mutable storage.
    public int[]? ActiveSlots => _activeSlots?.Order().ToArray();
    public int[]? RemainingDurations => _remainingDurations?.ToArray();
    [JsonIgnore]
    public int? DurationSlotCount => _remainingDurations?.Length;

    public bool? IsActive(int slot) => _activeSlots?.Contains(slot);
    public int? RemainingDuration(int slot) => _remainingDurations is not null &&
        slot >= 0 && slot < _remainingDurations.Length ? _remainingDurations[slot] : null;

    /// <summary>Returns a replacement after one known activation. Callers stage
    /// replacements until the complete business operation can be published.</summary>
    public OfficerTraitActivation Activate(int slot, int duration)
    {
        if (_activeSlots is null || _remainingDurations is null)
            throw new InvalidOperationException("Trait activation requires current activation and duration knowledge.");
        var active = new HashSet<int>(_activeSlots) { slot };
        var timers = _remainingDurations.ToArray();
        timers[slot] = duration;
        return new(active.ToArray(), timers);
    }
}

public sealed partial record OfficerState
{
    public OfficerTraitActivation? TraitActivation { get; init; }
}
