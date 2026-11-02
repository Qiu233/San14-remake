using Godot;
using San14.Core;
using San14.Data;

internal sealed partial class DeploymentTargetPresenter
{
    private readonly GeneralUnitTargetPreparation? generalPreparation;

    internal DeploymentTargetPresenter(DeploymentTargetView view, GameResourceRepository repository, PreviewText text,
        GameState world, ArmyCampaignRuleAdapter rules, GeneralUnitTargetPreparation preparation,
        Func<int,Vector2?> project, Func<int,Vector2?> projectMarker,
        Action<IReadOnlyList<DeploymentDraftRow>> accepted, Action back, Action<string> unavailable,
        Action<bool> modalChanged, Func<DeploymentBehavior,Action<GameState>,bool>? saveDefaults = null,
        Func<int>? scrollSpeed = null, bool? tutorialRestrictsFacilities = null,
        Action<DeploymentAction,int,Action<DeploymentAction>>? chooseFalseReportDestination = null,
        Action<IReadOnlyList<MapRoutePreviewPath>>? routePreviewChanged = null,
        AdministrationPageChrome.Resources? uiResources = null)
        : this(view, repository, text, world, rules, 0, preparation.ForceId,
            preparation.Armies.Select(army=>army.Row).ToArray(), project, projectMarker, accepted, back,
            unavailable, modalChanged, saveDefaults: saveDefaults, scrollSpeed: scrollSpeed,
            tutorialRestrictsFacilities: tutorialRestrictsFacilities,
            chooseFalseReportDestination: chooseFalseReportDestination,
            routePreviewChanged: routePreviewChanged, generalPreparation: preparation, uiResources: uiResources)
    { }

    internal IReadOnlyList<GeneralUnitTargetArmy> GeneralArmies => MatchGeneralRows(Draft.Rows);

    private IReadOnlyList<GeneralUnitTargetArmy> MatchGeneralRows(IReadOnlyList<DeploymentDraftRow> rows)
    {
        var source = generalPreparation ?? throw new InvalidOperationException("This target page has no general entry.");
        var origins = source.Armies.ToDictionary(army=>army.Row.Request.OfficerId);
        return rows.Select(row => origins[row.Request.OfficerId] with { Row = row }).ToArray();
    }

    private void ConfigureGeneralTargetView(GeneralUnitTargetPreparation preparation,
        IReadOnlyList<DeploymentTargetRowContent> contents, Func<int>? scrollSpeed,
        AdministrationPageChrome.Resources? uiResources)
    {
        // Group display rows in the retained source order. The actual packet
        // order remains Draft.Rows and is never regrouped for formal commit.
        var byOfficer = contents.ToDictionary(content=>content.OfficerId);
        var groups = preparation.Armies.GroupBy(army=>army.SourceLocationId).Select(group =>
        {
            int location = group.Key;
            string name = world.Cities.GetValueOrDefault(location)?.Name ??
                world.Gates.GetValueOrDefault(location)?.Name ?? throw new InvalidDataException("General source name is unavailable.");
            return new DeploymentTargetGroup(location, name,
                group.Select(army=>byOfficer[army.Row.Request.OfficerId]).ToArray());
        }).ToArray();
        var header = rules.QueryGeneralUnitTargetHeader(preparation, MatchGeneralRows(Draft.Rows));
        view.ConfigureGeneral(repository, text, header, groups, Draft.SelectedOfficers,
            scrollSpeed?.Invoke() ?? 0, officers=>
            {if(disposed)return;CloseMenu();previewHex=null;Draft.SelectOfficers(officers);Refresh();},
            OpenFacilities, RequestBack, scrollSpeed, resources: uiResources);
    }

    private void RefreshGeneralTargetHeader()
        => view.ApplyGeneralHeader(rules.QueryGeneralUnitTargetHeader(generalPreparation!, MatchGeneralRows(Draft.Rows)));
}
