using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    /// <summary>Bind the ordinary command providers to the already prepared
    /// lesson world. The lesson controller supplies its actual application
    /// context before any command or paragraph is evaluated.</summary>
    public static CampaignComposition PrepareTutorial(GameResourceRepository repository,
        TutorialLessonPreparation prepared, ApplicationGameplayContext context,
        PersonnelGroupMoveDestinations? retainedPersonnelGroupMoveDestinations = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(context);
        if (prepared.UnresolvedOwner is not null || prepared.Scenario is null ||
            prepared.Map is null || prepared.World is null)
            throw new InvalidDataException(prepared.UnresolvedOwner ?? "tutorial.composition.preparation");
        if (context.Mode != ApplicationGameplayMode.Tutorial)
            throw new InvalidOperationException("A lesson requires its actual tutorial application context.");
        var composition = new CampaignComposition(repository, prepared.Scenario, prepared.Map,
            retainedPersonnelGroupMoveDestinations);
        composition._applicationGameplay.Enter(context);
        composition._army = composition.CreateArmy(prepared.World);
        return composition;
    }

    /// <summary>Install the world returned by real lesson entry. This does
    /// not restart the scenario, choose a ruler or run new-campaign setup.</summary>
    public CampaignLoadResult AttachPreparedTutorial(GameState world, int playerForceId)
    {
        if (_applicationGameplay.Context?.Mode != ApplicationGameplayMode.Tutorial || _army is null)
            throw new InvalidOperationException("No tutorial command providers are attached.");
        ValidateSelection(world);
        PreparedRules prepared = PrepareRules(world, playerForceId, _army);
        GameplaySession session = GameplaySession.Restore(world, playerForceId,
            prepared.Rules, Calendar(prepared.Army), _saveCodec);
        return new(this, ScenarioTerritory.Build(Map.Map, Scenario.Snapshot), session, prepared.Presentation);
    }
}
