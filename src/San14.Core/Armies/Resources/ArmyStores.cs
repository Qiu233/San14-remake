namespace San14.Core;

/// <summary>Carried money and food, independently of troop maintenance charged to a city.</summary>
public sealed record ArmyStores(int Gold, int Food);

/// <summary>Stores retained by a reusable receiver. Each missing amount remains unknown.</summary>
public sealed record ArmyStoreKnowledge(int? Gold, int? Food)
{
    public static ArmyStoreKnowledge Empty { get; } = new(0, 0);
    [System.Text.Json.Serialization.JsonIgnore]
    public ArmyStores? Known => Gold is int gold && Food is int food ? new(gold, food) : null;
}

public enum ArmyStoreResolutionStatus { Known, Unknown, Conflicting }
public readonly record struct ArmyStoreResolution(ArmyStoreResolutionStatus Status, ArmyStores? Stores);
public readonly record struct ArmyCargoLoss(float Ratio, ArmyStores Lost, ArmyStores Remaining);

/// <summary>Army store ownership and proportional transport cargo loss.</summary>
public static class ArmyStoreRules
{
    public const int GoldCapacity = 100000;
    public const int FoodCapacity = 5000000;

    /// <summary>A present receiver owns reached source reads, including unknown amounts.
    /// An independently known unit projection must agree; absence does not invent a conflict.</summary>
    public static ArmyStoreResolution Resolve(ArmyStores? projection, ArmyStoreKnowledge? receiver)
    {
        if (receiver is null)
            return new(projection is null ? ArmyStoreResolutionStatus.Unknown : ArmyStoreResolutionStatus.Known, projection);
        if (receiver.Known is not { } stores) return new(ArmyStoreResolutionStatus.Unknown, null);
        return projection is not null && projection != stores
            ? new(ArmyStoreResolutionStatus.Conflicting, null)
            : new(ArmyStoreResolutionStatus.Known, stores);
    }

    /// <summary>Uses the pre-loss ready soldier count and the already selected casualty amount.
    /// Float32 conversion and signed overflow are part of the retained arithmetic.</summary>
    public static ArmyCargoLoss LoseCargo(ArmyStores stores, int readySoldiers, int casualties)
    {
        float ratio = (float)casualties / (float)readySoldiers;
        int food = Truncate((float)stores.Food * ratio);
        int gold = Truncate((float)stores.Gold * ratio);
        return new(ratio, new(gold, food), new(
            Bound(unchecked(stores.Gold - gold), GoldCapacity),
            Bound(unchecked(stores.Food - food), FoodCapacity)));
    }

    private static int Bound(int value, int capacity) => value < 0 ? 0 : Math.Min(value, capacity);
    private static int Truncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
}

public sealed partial record UnitState
{
    public ArmyStores? CarriedStores { get; init; }
}
