using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Page-owned, qualified portrait routes from one normal preview.
/// Face1 uses bank10; the ruler pane uses bank11's distinct unique_otomo names.</summary>
internal sealed class NewGameForcePageContent
{
    private readonly UiAssets.PortraitImages portraits;
    private readonly IReadOnlyDictionary<int, OfficerPortraitRoute> routes;
    private readonly IReadOnlyDictionary<int, OfficerPortraitRoute> rulerRoutes;
    private readonly Dictionary<(string Pack, string Sprite), LoadedUiSpriteSource> loaded = [];
    internal IReadOnlyList<NewGameForceFaceContent> Faces { get; }

    internal NewGameForcePageContent(GameResourceRepository repository, ArmyCampaignRuleAdapter army,
        GameState world, NewGameForceSelectionPreview publication, UiAssets.PortraitImages portraits, CancellationToken cancellationToken = default)
    {
        this.portraits=portraits;
        var rulers = publication.Rows.Concat(publication.ByForceId.Values)
            .Where(row => row.RulerValid == true && row.RulerId.HasValue)
            .Select(row => row.RulerId!.Value).Distinct().ToArray();
        routes = army.QueryOfficerPortraitRoutes(world, rulers, bank: 10);
        rulerRoutes = army.QueryOfficerPortraitRoutes(world, rulers, bank: 11);
        Faces = publication.Rows.Select(row =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.RulerId is not int ruler)
                throw new InvalidDataException("Force avatar lacks its actual ruler identity.");
            LoadedUiSpriteSource? portrait = row.RulerValid == false ? null : LoadFace(RequireRoute(row));
            return new NewGameForceFaceContent(row.ForceId, ruler, row.RulerForceId, portrait);
        }).ToArray();
    }

    internal void PrepareRulers(IEnumerable<NewGameForcePreview> rows, CancellationToken cancellationToken)
    {
        foreach (var row in rows.Where(row => row.RulerValid == true))
        { cancellationToken.ThrowIfCancellationRequested(); _ = ResolveRuler(row); }
    }

    internal LoadedUiSpriteSource? ResolveRuler(NewGameForcePreview row)
    {
        if (row.RulerValid == false) return null;
        // The current-person provider owns each bank's independent alias,
        // DLC archive and registered-user route. Bank10's resource name or
        // appearance55 suffix cannot manufacture bank11's declaration.
        var route = RequireRoute(row, rulerRoutes);
        return Load(route.ResourceName!, route.SpriteName!);
    }

    internal LoadedUiSpriteSource ResolveConfirmationRuler(NewGameForcePreview row)
    {
        if (row.RulerValid == false)
        {
            var fallback = ArmyMapUiSources.Create().PortraitNames(2198);
            return Load(fallback.ResourceName, fallback.SpriteName);
        }
        return LoadFace(RequireRoute(row));
    }

    private OfficerPortraitRoute RequireRoute(NewGameForcePreview row) => RequireRoute(row, routes);
    private static OfficerPortraitRoute RequireRoute(NewGameForcePreview row,
        IReadOnlyDictionary<int, OfficerPortraitRoute> bankRoutes)
    {
        if (row.RulerValid != true || row.RulerId is not int ruler || !bankRoutes.TryGetValue(ruler, out var route))
            throw new InvalidDataException("Force portrait current-person qualification is unavailable.");
        if (route.Kind != OfficerPortraitRouteKind.StockNamed || route.CurrentAppearanceIndex is null ||
            route.ResolvedAppearanceIndex is null || route.ResourceName is null || route.SpriteName is null)
            throw new InvalidDataException("Force portrait resource route is unavailable: " + route.UnresolvedInput);
        if (route.CurrentAppearanceIndex != row.PortraitId)
            throw new InvalidDataException("Force portrait publication no longer matches its current-person route.");
        return route;
    }

    private LoadedUiSpriteSource LoadFace(OfficerPortraitRoute route) => Load(route.ResourceName!, route.SpriteName!);
    private LoadedUiSpriteSource Load(string pack, string sprite)
    {
        if (!loaded.TryGetValue((pack, sprite), out var result))
        { result = portraits.RequireSource(pack, sprite); loaded.Add((pack, sprite), result); }
        return result;
    }
}
