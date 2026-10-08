namespace San14.Core;

/// <summary>Resource receivers for the current food accounting cut. A missing
/// City with no missing-owner reason is a known invalid/placeholder payer;
/// a missing-owner reason records an unresolved source input.</summary>
public sealed record PlayableFoodPayerResolution(int? GarrisonCityId, int? ArmyCityId,
    string? GarrisonMissingOwner, string? ArmyMissingOwner, string RuleId);

public interface IPlayableFoodOwnerRules
{
    IReadOnlyDictionary<int, PlayableFoodPayerResolution> CaptureFoodOwners(GameState state);
}
