using San14.Data;

internal sealed record DiplomacyPersonVisual(int OfficerId,string Name,LoadedSprite? Portrait,
    DiplomacyArrivalSpecialTrait? Trait,string? TraitUnresolvedInput,string? UnresolvedInput);
internal sealed record DiplomacyPeopleVisuals(DiplomacyPersonVisual? Ruler,IReadOnlyList<DiplomacyPersonVisual> Captives,
    string? RulerUnresolvedInput,string? CaptivesUnresolvedInput)
{
    internal DetailForceAppearance? DisplayedForceAppearance { get; init; }
}
