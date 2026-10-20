namespace San14.Data;

public sealed record AreaAdministrationOfficerAbility(int Dimension,
    AreaAdministrationQuery<int?> Base, AreaAdministrationQuery<int?> Effective);
public sealed record AreaAdministrationOfficerTrait(int PhysicalTraitId, string Name, byte Style)
{
    // Existing value construction has identity lookup by default; source cards
    // publish the actual raw table key independently of the physical receiver.
    public int LookupTraitId { get; init; } = PhysicalTraitId;
}
public sealed record AreaAdministrationOfficerFlag(int ForceId, string Glyph,
    AreaMarkerColor Fill, AreaMarkerColor? Letter);
public sealed record AreaAdministrationOfficerCard(int? AreaId, long Revision,
    int? OfficerId, string Name, IReadOnlyList<AreaAdministrationOfficerAbility> Abilities,
    OfficerPortraitRoute Portrait,
    AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>> Traits,
    AreaAdministrationQuery<AreaAdministrationOfficerFlag> ForceFlag);
