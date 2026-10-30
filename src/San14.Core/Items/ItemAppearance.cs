namespace San14.Core;

/// <summary>The current game's item definition. It is detached from title
/// archives and the installed definition after new-game initialization.</summary>
public sealed record ItemAppearance(int Kind, int AbilityKind, int AbilityValue, int EffectKind, int EffectValue,
    int ImageIndex, string Description)
{
    public void Validate()
    {
        if (Kind is < 0 or > 26 || AbilityKind is < 0 or > 5 || AbilityValue is < 0 or > 10 ||
            EffectKind is < 0 or > 2 || EffectValue is < 0 or > 10 ||
            ImageIndex is < 0 or > 456 || Description is null || Description.Contains('\0'))
            throw new InvalidDataException("Current item appearance is invalid.");
    }
}

public sealed partial record ItemDefinition
{
    // Saves through 187 did not retain this owner. Missing remains unknown;
    // changing a title library cannot establish an old campaign's definition.
    public ItemAppearance? Appearance { get; init; }
}
