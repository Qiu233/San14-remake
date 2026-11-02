using Godot;
using San14.Data;

/// <summary>The Domestic button's actual CMapFace child, including its empty
/// slot and four retained input states. Static images belong to the app owner.</summary>
internal partial class AreaAdministrationMapFace : UiSourceAnimationView
{
    private static readonly int[] PortraitNodes = [7, 16, 24, 30, 32];
    // The trailing ASCII space in the focus name belongs to the resource.
    private static readonly string[] StateClips = ["0_normal", "1_focus ", "2_push", "3_disable"];
    private static readonly IReadOnlySet<string> ClipNames = StateClips.ToHashSet(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, LcskAnimationClip> stateCurves = null!;
    private double stateSeconds, stateDuration;
    private int state;
    private bool hasState, sampling;

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome.Resources resources,
        OfficerPortraitRoute? portrait, AreaMarkerColor forceColor)
    {
        var source = resources.Assets.SourceLayout(0x7104E19F);
        AutomaticAnimationClock = false;
        stateCurves = resources.Assets.SourceAnimationClips(source, ClipNames);
        ConfigureAnimation(source, stateCurves, source.Nodes.Select(n => n.Index));
        Show(); // The enclosing Domestic button owns near/far visibility after configuration.
        HiddenAnimationPanes.Add(36);
        var palettes = MapSpritePalettes.Create();
        foreach (var node in source.Nodes.Where(n => (n.Type is 1 or 3) && !PortraitNodes.Contains(n.Index) && n.Index != 36))
        {
            MapCornerColors? colors = node.Index switch
            {
                5 => Uniform(forceColor),
                >= 11 and <= 14 => palettes[5],
                >= 19 and <= 22 => palettes[6],
                // This is a runtime literal vertex word, not a startup
                //palette entry to pass through the palette gamma table.
                28 => Uniform(new AreaMarkerColor(0xFF080000, 0xFF080000)),
                _ => null
            };
            var blend = node.Index switch
            {
                12 or 14 or 20 or 22 => UiSourceSprite.Blend.Additive,
                8 or 33 => UiSourceSprite.Blend.AlphaMask,
                _ => UiSourceSprite.Blend.Ordinary
            };
            source.Art(AnimationPanes[node.Index], node.Index,
                new Rect2(Vector2.Zero, new(node.Value44, node.Value46)), resources.Assets, resources.SpriteShader,
                colors: colors, blend: blend, localCoordinates: true);
        }
        if (portrait is null)
        {
            foreach (int node in PortraitNodes) HiddenAnimationPanes.Add(node);
        }
        else
        {
            if (portrait is not { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: { } pack, SpriteName: { } sprite })
                throw new InvalidDataException(portrait.UnresolvedInput ?? "area-map.portrait_route");
            var image = resources.Assets.Portraits.RequireSource(pack, sprite);
            foreach (int index in PortraitNodes)
            {
                var node = source.Nodes[index];
                var face = new UiSourcePortrait { Name = "DomesticPortrait" + index,
                    Size = new(node.Value44, node.Value46), MouseFilter = MouseFilterEnum.Ignore };
                AnimationPanes[index].AddChild(face);
                face.ConfigureLayoutPane(repository, resources.Assets, resources.SpriteShader, image, source, index,
                    materialSaturation: index == 30 ? 0 : 1);
                // The disabled face owns a fixed grey tint. Its resource
                //alpha still comes from the disabled state's actual clip.
                if (index == 30) face.Modulate = new(128 / 255f, 128 / 255f, 128 / 255f, 1);
            }
        }
        SetInteractionState(0);
    }

    internal void SetInteractionState(int sourceState)
    {
        int next = sourceState switch { 0 => 0, 1 => 1, 2 => 2, 4 => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(sourceState)) };
        if (hasState && state == next) return;
        hasState = true; state = next; stateSeconds = 0;
        stateDuration = stateCurves[StateClips[state]].DurationSeconds;
        sampling = stateDuration > 0;
        ApplyAnimationAt(StateClips[state], 0, reveal: false);
    }

    public override void _Process(double delta)
    {
        if (!sampling) return;
        stateSeconds = Math.Min(stateSeconds + delta, stateDuration);
        ApplyAnimationAt(StateClips[state], stateSeconds, reveal: false);
        sampling = stateSeconds < stateDuration;
    }

    private static MapCornerColors Uniform(AreaMarkerColor color) => new(color, color, color, color);
}
