namespace San14.Core;

/// <summary>The reached shared formatter slots carried between lesson scripts.
/// Null slots or banks retain unknown values until an actual source writer.</summary>
public sealed record CampaignStoryMessageContext(int?[] People, string[]? Names, int[]? Cities)
{
    public int[]? Forces { get; init; }
    public int[]? Areas { get; init; }
    public int[]? Items { get; init; }
    public int?[]? Numbers { get; init; }
    public int?[]? Provinces { get; init; }
    public CampaignStoryMessageContext Copy() => new(People.ToArray(), Names?.ToArray(), Cities?.ToArray())
        { Forces = Forces?.ToArray(), Areas = Areas?.ToArray(), Items = Items?.ToArray(), Numbers = Numbers?.ToArray(), Provinces = Provinces?.ToArray() };
    internal void Validate()
    {
        if (People is not { Length: 5 } || People.Any(id => id is < 0 or > ushort.MaxValue) ||
            Names is not null && Names.Length != 5 ||
            Cities is not null && (Cities.Length != 5 || Cities.Any(id => id is < 0 or > ushort.MaxValue)) ||
            !BankValid(Forces) || !BankValid(Areas) || !CampaignStoryMessageBanks.IsValid(Items, Numbers, Provinces))
            throw new InvalidDataException("Story message context slots are incomplete or invalid.");
    }
    private static bool BankValid(int[]? values) => values is null ||
        values.Length == 5 && values.All(id => id is >= 0 and <= ushort.MaxValue);
}

internal static class CampaignStoryMessageBanks
{
    internal static bool IsValid(int[]? items, int?[]? numbers, int?[]? provinces = null) =>
        (items is null || items.Length == 5 && items.All(id => id is >= 0 and <= 201)) &&
        (numbers is null || numbers.Length == 5) &&
        (provinces is null || provinces.Length == 10 && provinces.All(value => value is null or >= 0 and <= 22));
}
