namespace San14.Core;

public sealed partial record ItemDefinition
{
    /// <summary>The current definition's independent name-order text. Null
    /// is unknown; changing the display name does not recompute this key.</summary>
    public string? NameSortKey { get; init; }
}
