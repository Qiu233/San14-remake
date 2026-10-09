using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Independent knowledge of three observed officer behaviors. The
/// uninterpreted channels retain imported information without assigning it
/// unproved game meaning. A missing channel remains unknown.</summary>
public sealed record OfficerBehavior
{
    [JsonConstructor]
    public OfficerBehavior(bool? usesSpecialPersonRules, bool? preservesMaximumLoyalty,
        bool? releaseAfterAiCapture, int? uninterpretedFlags, int? specialValidityMarker)
    {
        if (uninterpretedFlags < 0 || specialValidityMarker < 0)
            throw new InvalidDataException("Officer behavior knowledge cannot be negative.");
        UsesSpecialPersonRules = usesSpecialPersonRules;
        PreservesMaximumLoyalty = preservesMaximumLoyalty;
        ReleaseAfterAiCapture = releaseAfterAiCapture;
        UninterpretedFlags = uninterpretedFlags;
        SpecialValidityMarker = specialValidityMarker;
    }

    public bool? UsesSpecialPersonRules { get; }
    public bool? PreservesMaximumLoyalty { get; }
    public bool? ReleaseAfterAiCapture { get; }
    public int? UninterpretedFlags { get; }
    // Only the nonzero predicate is understood. Values are not normalized to bool.
    public int? SpecialValidityMarker { get; }

    public OfficerBehavior WithSpecialValidityMarker(int? marker) => new(
        UsesSpecialPersonRules, PreservesMaximumLoyalty, ReleaseAfterAiCapture,
        UninterpretedFlags, marker);
}

public static class OfficerBehaviorRules
{
    public static bool? UsesOrdinaryPersonRules(OfficerBehavior? behavior) =>
        behavior?.UsesSpecialPersonRules is bool special ? !special : null;

    public static bool? SpecialPersonIsValid(OfficerBehavior? behavior) =>
        behavior?.SpecialValidityMarker is int marker ? marker != 0 : null;

    public static bool? MustReleaseAfterAiCapture(OfficerBehavior? behavior) => behavior?.ReleaseAfterAiCapture;
    public static bool? PreservesMaximumLoyalty(OfficerBehavior? behavior) => behavior?.PreservesMaximumLoyalty;
}

public sealed partial record OfficerState
{
    public OfficerBehavior? Behavior { get; init; }
}
