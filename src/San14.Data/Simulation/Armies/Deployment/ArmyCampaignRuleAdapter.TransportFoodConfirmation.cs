using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<string> QueryTransportFoodConfirmation(GameState state, int sourceCityId)
    {
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var source = TransportSource(state, current, sourceCityId);
            ushort argument = TK(current.ReadBaseObjectLink(source), "warning-source-base");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            var context = new DeploymentNarrationCurrentPeople(state, current, abilities, null, null,
                cityArgument: argument);
            var messages = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            return AreaAdministrationQuery<string>.Known(messages.ResolveTransportFoodConfirmation(context));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }
}
