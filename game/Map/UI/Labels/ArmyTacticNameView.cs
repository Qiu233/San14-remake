using Godot;
using San14.Data;

/// <summary>The actual Army name child, including stock commander faces and
/// localized tactic artwork. A Base uses its own UnitAction text instead.</summary>
internal partial class ArmyTacticNameView : UiSourceAnimationView
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private string selected = "senpo1";
    private readonly Dictionary<int, Node> content = [];

    internal void Configure(GameResourceRepository repo, UiAssets sharedAssets, Shader sharedShader)
    {
        repository = repo; assets = sharedAssets; shader = sharedShader;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repo, "00_81_09_senpo_offset_05"));
        ConfigureAnimation(source, new HashSet<string> { "senpo1", "senpo2", "senpo3", "senpo4", "senpo5" },
            Enumerable.Range(0, source.Nodes.Count));
        foreach (var node in source.Nodes.Where(node => node.Type == 1 && node.Index is not (4 or 6 or 8 or 10 or 16 or 18 or 19 or 20 or 21)))
            source.Art(AnimationPanes[node.Index], node.Index, new(Vector2.Zero, new(node.Value44, node.Value46)),
                assets, shader, localCoordinates: true);
    }

    internal void StartName(OrdinaryTacticNamePublication publication)
    {
        if (!publication.Visible) return;
        //1B5000 leaves an active selection alone; its caller still invokes
        //1B4270 on that existing selected clip.
        if (!IsPlaying)
        {
            foreach (var node in content.Values) { node.GetParent()?.RemoveChild(node); node.QueueFree(); }
            content.Clear();
            if (publication.Caster is { } caster) Face(16, caster.Portrait);
            int[] slots = [10, 8, 6, 4];
            for (int index = 0; index < publication.LinkedPortraits.Count; index++)
                Face(slots[index], publication.LinkedPortraits[index].Portrait);
            var art = publication.TacticArt ?? throw new InvalidDataException("tactic.name.art_route");
            foreach (int index in publication.IsUnique ? new[] { 19, 21 } : new[] { 18, 20 })
            {
                var node = AnimationLayout.Nodes[index];
                content.Add(index, AnimationLayout.Art(AnimationPanes[index], index,
                    new(Vector2.Zero, new(node.Value44, node.Value46)), assets, shader,
                    commonSpriteOverride: art.SpriteName, commonTexturePack: art.ResourceName, localCoordinates: true));
            }
            selected = publication.AnimationName;
        }
        PlayAnimation(selected);
    }

    private void Face(int index, OfficerPortraitRoute route)
    {
        if (route.Kind != OfficerPortraitRouteKind.StockNamed || route.ResourceName is null || route.SpriteName is null)
            throw new InvalidDataException(route.UnresolvedInput ?? "tactic.name.portrait_route");
        var sprite = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(route.ResourceName, route.SpriteName);
        var node = AnimationLayout.Nodes[index];
        var face = new UiSourcePortrait { Name = "TacticFace", Size = new(node.Value44, node.Value46),
            MouseFilter = MouseFilterEnum.Ignore };
        AnimationPanes[index].AddChild(face);
        face.ConfigureTacticFace(repository, assets, shader, sprite, AnimationLayout, index);
        content.Add(index, face);
    }
}
