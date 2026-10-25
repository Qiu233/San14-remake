using Godot;
using San14.Data;

/// <summary>The source UnitAction balloon groups, shared by actual Army and Base speech.</summary>
internal partial class ArmyMapSpeechBubbleView : Control
{
    private readonly Control[] _groups = new Control[2];
    private readonly UiSourceParagraph[] _bodies = new UiSourceParagraph[2];
    internal int LayoutStyle { get; private set; }
    internal Vector2 ProjectionOffset { get; private set; }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font, int style = 0)
    {
        if (style is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(style));
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new(288, 140);
        var source = new UiSourceLayout(repository, 0x47FB32BE);
        //1BC3C0 selects balloon_a/text_a or balloon_b/text_b; the unused
        //groups, including balloon_c, never become visible with this owner.
        for (int group = 0; group < 2; group++)
        {
            int art = group == 0 ? 5 : 7, text = group == 0 ? 6 : 8;
            var host = new Control { Name = "Balloon_" + group, MouseFilter = MouseFilterEnum.Ignore, Size = Size };
            AddChild(host); _groups[group] = host;
            source.Art(host, art, source.Rectangle(art), assets, shader);
            var rectangle = source.Rectangle(text);
            var profile = MapFontProfile.FromNode(source.Nodes[text], source.Source.Source.ResourceUri)
                .WithPreset(MapTextPresets.Create()[1]) with
                { RawAlign = 0, RawLineArrange = 1, WidthFitMode = 3, RawCharacterMode = 2 };
            var body = new UiSourceParagraph { Name = "ArmySpeechText", Position = rectangle.Position, Size = rectangle.Size };
            host.AddChild(body); body.Configure(font, profile); _bodies[group] = body;
        }
        ApplyStyle(style);
    }

    internal void ApplyStyle(int style)
    {
        if (style is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(style));
        LayoutStyle = style;
        for (int index = 0; index < _groups.Length; index++) _groups[index].Visible = index == style;
    }

    internal void ApplyText(string text) => _bodies[LayoutStyle].Apply(text);

    internal void ConfigureOwnedPortrait(GameResourceRepository repository, UiAssets assets, Shader shader,
        BaseMapSpeechSpeakerPublication speaker)
    {
        var route = speaker.Portrait;
        if (route.Kind != OfficerPortraitRouteKind.StockNamed || route.ResourceName is null || route.SpriteName is null)
            throw new InvalidDataException(route.UnresolvedInput ?? "map-speech.owned_portrait_route");
        var declared = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(route.ResourceName, route.SpriteName);
        var source = new UiSourceLayout(repository, 0x7104E19F);
        var action = new UiSourceLayout(repository, 0x47FB32BE);
        var face = new Control { Name = "OwnedMapFace", Position = action.Rectangle(3).Position,
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(face);
        var colour = MapSpritePalettes.ForceFill(speaker.ForceColor);
        foreach (int node in new[] { 4, 5 })
            source.Art(face, node, source.Rectangle(node), assets, shader,
                colors: node == 5 ? new MapCornerColors(colour, colour, colour, colour) : null);
        var rectangle = source.Rectangle(7);
        var portrait = new UiSourcePortrait { Name = "GovernorPortrait", Position = rectangle.Position, Size = rectangle.Size };
        face.AddChild(portrait); portrait.ConfigureMapFace(repository, assets, shader, declared);
        //1AF720 offsets the entire owned action by its mounted MapFace position.
        ProjectionOffset = -face.Position;
    }
}
