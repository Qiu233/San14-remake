namespace San14.Data;

/// <summary>Both source effect-description lines, retained even when one is
/// empty. A missing description does not discard a known tactic name.</summary>
public sealed record UnitSourceTactic(int TacticId, string Name, string? EffectsText,
    string? EffectsUnresolvedInput);

/// <summary>Only the parameter page's additional groups. Shared identity,
/// formation and resources stay in the caller's UnitSourceDetails publication.
/// Tactics null means unknown; an empty list is a known empty result.</summary>
public sealed record UnitSourceParameters(int UnitId, bool? LandSelected,
    IReadOnlyList<UnitSourceTactic>? Tactics, int? WaitingCounter, int? WaitingDays,
    bool? WaitingVisible, IReadOnlyDictionary<string, string> Missing);
