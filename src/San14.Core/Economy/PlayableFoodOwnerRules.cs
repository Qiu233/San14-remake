namespace San14.Core;

/// <summary>Resource receivers for the current food accounting cut. A missing
/// City with no missing-owner reason is a known invalid/placeholder payer;
/// a missing-owner reason records an unresolved source input.</summary>
public sealed record PlayableFoodPayerResolution(int? GarrisonCityId, int? ArmyCityId,
    string? GarrisonMissingOwner, string? ArmyMissingOwner, string RuleId)
{
    // Null retains a legacy provider. A present quote with null Amount is an
    // explicitly unresolved current expense and never selects legacy arithmetic.
    public PlayableGarrisonFoodExpense? GarrisonExpense { get; init; }
}

public sealed record PlayableGarrisonFoodExpense(int? Amount, string? MissingOwner = null);

public interface IPlayableFoodOwnerRules
{
    IReadOnlyDictionary<int, PlayableFoodPayerResolution> CaptureFoodOwners(GameState state);
}

/// <summary>A single original Base bill; its payer may be another City.</summary>
public sealed record PlayableGarrisonFoodShortage(int LocationId, int FoodCityId,
    int ForceId, int Expense, int AvailableFood, int ShortagePercent);

public delegate GameState PlayableGarrisonFoodShortageSettlement(GameState state, PlayableGarrisonFoodShortage shortage);

public interface IPlayableGarrisonFoodShortageRules
{
    // One operation-local capture is shared by all bills in this settlement.
    PlayableGarrisonFoodShortageSettlement BeginGarrisonFoodShortageSettlement();
}
