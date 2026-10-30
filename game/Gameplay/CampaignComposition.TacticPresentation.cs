using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private TacticPresentationViews? _tacticPresentationViews;

    internal void ConfigureTacticPresentationViews(TacticPresentationViews views)
        => Volatile.Write(ref _tacticPresentationViews, views);

    private sealed class TacticPresentationAdmission(CampaignComposition owner) : IManualTacticPresentationAdmission
    {
        internal ArmyCampaignRuleAdapter Rules { private get; set; } = null!;
        public bool? ApplicationPresent => owner._applicationGameplay.Context?.UserSelectionServicePresent;
        // This campaign host constructs no optional user-selection service.
        // Its absent service never reads a saved user index or assumes player0.
        public int? ApplicationUserIndex => null;

        public bool? ReadUniqueDirectionAdmission(GameState state, UnitState actor, int tacticId)
        {
            var views = Volatile.Read(ref owner._tacticPresentationViews);
            var result = Rules.QueryUniqueTacticDirectionAdmission(state, actor, tacticId, views?.Read(actor.Id));
            return result.Available ? result.Value : null;
        }

        public bool? ReadUniqueDirectionAdmission(GameState state, int casterUnitId, int armyPhysical, int tacticId)
        {
            var views = Volatile.Read(ref owner._tacticPresentationViews);
            var result = Rules.QueryUniqueTacticDirectionAdmission(state, armyPhysical, tacticId, views?.Read(casterUnitId));
            return result.Available ? result.Value : null;
        }
    }
}
