using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>
/// Current activation and suppression of doctrine-panel reform effects. These
/// identities are distinct from the administration PolicyEffectDefinition
/// catalog and its levels. Each missing set means unknown knowledge.
/// </summary>
public sealed class ForcePolicyEffects
{
    private readonly HashSet<int>? _active;
    private readonly HashSet<int>? _suppressed;

    [JsonConstructor]
    public ForcePolicyEffects(int[]? activeEffectIds, int[]? suppressedEffectIds)
    {
        _active = Snapshot(activeEffectIds);
        _suppressed = Snapshot(suppressedEffectIds);
    }

    public static ForcePolicyEffects Empty { get; } = new([], []);

    // Independent arrays are exposed for persistence; rule queries use the sets.
    public int[]? ActiveEffectIds => _active?.Order().ToArray();
    public int[]? SuppressedEffectIds => _suppressed?.Order().ToArray();
    [JsonIgnore]
    public bool HasActivationKnowledge => _active is not null;

    public bool? ReadActivation(int effectId) => _active?.Contains(effectId);
    public bool? ReadSuppression(int effectId) => _suppressed?.Contains(effectId);

    /// <summary>Inactive effects return false without requiring suppression knowledge.</summary>
    public bool? IsEnabled(int effectId) => ReadActivation(effectId) switch
    {
        false => false,
        true => ReadSuppression(effectId) is bool suppressed ? !suppressed : null,
        null => null
    };

    private static HashSet<int>? Snapshot(int[]? values)
    {
        if (values is null) return null;
        var result = new HashSet<int>(values);
        if (result.Count != values.Length || result.Any(id => id <= 0))
            throw new InvalidDataException("Policy effect identities must be unique positive IDs.");
        return result;
    }
}

public sealed partial record ForceState
{
    public ForcePolicyEffects? PolicyEffects { get; init; }
}
