using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One publication for the Army captive button and its supplied
    /// ordinary Person list. Unknown membership stays distinct from no captives;
    /// no global Officer list or map projection is queried.</summary>
    public AreaAdministrationQuery<InformationList> QueryArmyCaptiveInformation(
        GameState state, int playerForceId, int unitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            return AreaAdministrationQuery<InformationList>.Known(
                new InformationSource(this, state, playerForceId).QueryArmyCaptives(unitId));
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<InformationList>.Unknown(error.Message);
        }
    }
}
