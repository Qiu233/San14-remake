using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Reads the resources for the actual saved single-speaker line,
    /// retaining its source-time name and appearance after world changes.</summary>
    public AreaAdministrationQuery<PersonnelPresentationSpeaker> QueryPersonnelPresentationSpeaker(
        GameState state, TalentOfficerPresentationContext speaker)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(speaker);
        if (speaker.OfficerId is < 1 or > 6000 || speaker.NameAtSource is null)
            return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Unknown("personnel.presentation.source_speaker");
        DeploymentNarrationPortraitRoute? standing = null;
        OfficerPortraitRoute? face = null;
        string? missing = null;
        try
        {
            var current = CapturePersonPresentationCurrent(state);
            ushort appearance = speaker.PortraitAppearanceIndex ??
                throw new InvalidDataException("personnel.presentation.source_appearance");
            if (speaker.OfficerId == 5012)
            {
                face = ReadOfficerPortraitAppearance(state, current, _definitions, speaker.OfficerId,
                    appearance, ArmyMapUiSources.Create());
                if (face.Kind != OfficerPortraitRouteKind.StockNamed)
                    missing = face.UnresolvedInput ?? "personnel.presentation.source_face2_route";
            }
            else standing = ReadNarrationPortrait(state, current, _definitions, speaker.OfficerId,
                appearance, requireCurrentAppearance: false);
        }
        catch (InvalidDataException error) { missing = error.Message; }
        return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Known(new(speaker.OfficerId,
            speaker.OfficerId, speaker.NameAtSource, face, standing, missing));
    }
}
