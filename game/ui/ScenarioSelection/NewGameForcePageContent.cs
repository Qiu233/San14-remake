using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Page-owned, qualified portrait routes from one normal preview.
/// Face1 uses bank10; the ruler pane uses bank11's distinct unique_otomo names.</summary>
internal sealed class NewGameForcePageContent
{
    private readonly LocalizedUiSpriteSource sprites;
    private readonly IReadOnlyDictionary<int, OfficerPortraitRoute> routes;
    private readonly Dictionary<(string Pack, string Sprite), LoadedSprite> loaded = [];
    internal IReadOnlyList<NewGameForceFaceContent> Faces { get; }

    internal NewGameForcePageContent(GameResourceRepository repository, ArmyCampaignRuleAdapter army,
        GameState world, NewGameForceSelectionPreview publication, CancellationToken cancellationToken = default)
    {
        sprites = new(repository);
        routes = army.QueryOfficerPortraitRoutes(world, publication.Rows.Concat(publication.ByForceId.Values)
            .Where(row => row.RulerValid == true && row.RulerId.HasValue)
            .Select(row => row.RulerId!.Value).Distinct().ToArray());
        Faces = publication.Rows.Select(row =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.RulerId is not int ruler)
                throw new InvalidDataException("Force avatar lacks its actual ruler identity.");
            LoadedSprite? portrait = row.RulerValid == false ? null : LoadFace(RequireRoute(row));
            return new NewGameForceFaceContent(row.ForceId, ruler, row.RulerForceId, portrait);
        }).ToArray();
    }

    internal void PrepareRulers(IEnumerable<NewGameForcePreview> rows, CancellationToken cancellationToken)
    {
        foreach (var row in rows.Where(row => row.RulerValid == true))
        { cancellationToken.ThrowIfCancellationRequested(); _ = ResolveRuler(row); }
    }

    internal LoadedSprite? ResolveRuler(NewGameForcePreview row)
    {
        if (row.RulerValid == false) return null;
        OfficerPortraitRoute route = RequireRoute(row);
        int current = route.CurrentAppearanceIndex!.Value;
        int resolved = route.ResolvedAppearanceIndex!.Value;
        string index = resolved.ToString("D4", CultureInfo.InvariantCulture);
        string alias = resolved != current ? "b" : "";
        // Bank11 has no bank10 appearance55 extra-b exception.
        return Load("texture_chara_unique_otomo_" + index + alias, "face_" + index + alias + "_unique");
    }

    internal LoadedSprite ResolveConfirmationRuler(NewGameForcePreview row)
    {
        if (row.RulerValid == false)
        {
            var fallback = ArmyMapUiSources.Create().PortraitNames(2198);
            return Load(fallback.ResourceName, fallback.SpriteName);
        }
        return LoadFace(RequireRoute(row));
    }

    private OfficerPortraitRoute RequireRoute(NewGameForcePreview row)
    {
        if (row.RulerValid != true || row.RulerId is not int ruler || !routes.TryGetValue(ruler, out var route))
            throw new InvalidDataException("Force portrait current-person qualification is unavailable.");
        if (route.Kind != OfficerPortraitRouteKind.StockNamed || route.CurrentAppearanceIndex is null ||
            route.ResolvedAppearanceIndex is null || route.ResourceName is null || route.SpriteName is null)
            throw new InvalidDataException("Force portrait resource route is unavailable: " + route.UnresolvedInput);
        if (route.CurrentAppearanceIndex != row.PortraitId)
            throw new InvalidDataException("Force portrait publication no longer matches its current-person route.");
        return route;
    }

    private LoadedSprite LoadFace(OfficerPortraitRoute route) => Load(route.ResourceName!, route.SpriteName!);
    private LoadedSprite Load(string pack, string sprite)
    {
        if (!loaded.TryGetValue((pack, sprite), out var result))
        { result = sprites.LoadTexturePackSprite(pack, sprite); loaded.Add((pack, sprite), result); }
        return result;
    }
}
