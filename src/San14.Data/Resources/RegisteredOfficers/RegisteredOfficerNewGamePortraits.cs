using San14.Core;

namespace San14.Data;

/// <summary>The new-force card's bank10 appearance comes from the edit draft,
/// before that person has a campaign owner. Ordinary editor appearances are
/// stock 2000..2199 and do not consume the low-appearance, DLC or edit gates.</summary>
public static class RegisteredOfficerNewGamePortraits
{
    private static readonly ArmyMapUiSources Names = ArmyMapUiSources.Create();

    public static ArmyMapPortraitNames Resolve(RegisteredOfficer officer, OfficerEditorPortraitCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(officer);
        if (catalog is not null ? !catalog.Contains(officer.PortraitId) : !RegisteredOfficerPortraitCatalog.OrdinaryPortraits.Any(choice => choice.Id == officer.PortraitId))
            throw new InvalidDataException("registered_officers.new_game.portrait_not_in_editor_catalog");
        if (catalog is not null)
        {
            var route = catalog.ResolveFace(officer.PortraitId);
            return route.Available ? route.Value! : throw new InvalidDataException(route.UnresolvedInput);
        }
        return Names.PortraitNames((ushort)officer.PortraitId);
    }
}
