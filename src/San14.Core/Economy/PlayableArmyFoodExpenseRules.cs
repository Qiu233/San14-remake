namespace San14.Core;

public sealed record PlayableArmyFoodExpense(int? Amount, string? MissingOwner = null)
{
    public bool Admitted { get; init; } = true;
    public int? FoodCityId { get; init; }
}

/// <summary>One period quantity cut. Source queries can advance their owned
/// temporary allocator, so its resulting state travels with the quotations.</summary>
public sealed record PlayableArmyFoodExpenseBatch(GameState State,
    IReadOnlyDictionary<int, PlayableArmyFoodExpense>? Expenses)
{
    public IReadOnlyList<PlayableArmyFoodExpenseEntry>? OrderedExpenses { get; init; }
}

public sealed record PlayableArmyFoodExpenseEntry(int UnitId, PlayableArmyFoodExpense Expense);
public sealed record PlayableArmyFoodShortage(int UnitId, int ForceId, int OfficerId, int FoodCityId, int Unpaid);

public interface IPlayableArmyFoodExpenseRules
{
    PlayableArmyFoodExpenseBatch CaptureArmyFoodExpenses(GameState state);
}

public interface IPlayableArmyFoodShortageRules
{
    GameState ApplyArmyFoodShortages(GameState state, IReadOnlyList<PlayableArmyFoodShortage> shortages);
}
