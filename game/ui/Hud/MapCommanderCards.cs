using Godot;
using San14.Data;

internal sealed record MapCommanderCard(int Slot, int? OfficerId, string Name, string Shortcut,
    bool? Unlocked, OfficerPortraitRoute? Portrait);

/// <summary>The five current commander cards. Only this publication's actual
/// persons are clickable; roster changes belong to the formal command owner.</summary>
internal partial class MapCommanderCards : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!, cards = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Action<int>? focus;
    private Action? select;
    private readonly Dictionary<(string Pack, string Sprite), LoadedSprite> portraits = [];

    internal void Configure(GameResourceRepository repository, PreviewText text, Action<int> focus, Action select)
    {
        this.repository = repository; this.text = text; this.focus = focus; this.select = select;
        Name = "CommanderCards"; MouseFilter = MouseFilterEnum.Ignore; ZIndex = 2;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Size = new(1920,1080), MouseFilter = MouseFilterEnum.Ignore }; AddChild(canvas);
        cards = new() { MouseFilter = MouseFilterEnum.Ignore }; canvas.AddChild(cards);
        var source = new UiSourceLayout(repository,0x1CDA8363);
        Vector2 extent = source.Rectangle(0).Size;
        Vector2 mount = new(1920-extent.X-20,1080-extent.Y-24);
        var change = new UiSourceBottomButton { Name = "ChooseCommanders", Position = source.Rectangle(2,mount).Position };
        canvas.AddChild(change);
        change.Configure(repository, assets, shader, font, text.Get("play_commander_cards_select"), () => this.select?.Invoke());
        Resized += Fit; Fit();
    }

    internal void Apply(IReadOnlyList<MapCommanderCard> publication, bool canSelect)
    {
        foreach (Node child in cards.GetChildren()) { cards.RemoveChild(child); child.QueueFree(); }
        canvas.GetNode<UiSourceBottomButton>("ChooseCommanders").Disabled = !canSelect;
        var source = new UiSourceLayout(repository, 0x1CDA8363);
        int[] slots = [6,9,10,11,12]; //883950's five actual child mounts.
        Vector2 extent = source.Rectangle(0).Size;
        Vector2 mount = new(1920 - extent.X - 20, 1080 - extent.Y - 24);
        for (int ordinal = 0; ordinal < 5; ordinal++)
        {
            MapCommanderCard card = publication[ordinal];
            Rect2 place = source.Rectangle(slots[ordinal], mount);
            var button = new UiSourceBottomButton { Name = "CommanderCard" + ordinal, Position = place.Position };
            cards.AddChild(button);
            string caption = card.OfficerId.HasValue ? card.Name : card.Unlocked is null ? text.Get("detail_unknown") :
                text.Get(card.Unlocked.Value ? "play_commander_cards_empty" : "play_commander_cards_locked");
            button.Configure(repository, assets!, shader!, font!, caption, () => focus?.Invoke(card.Slot));
            button.Disabled = !card.OfficerId.HasValue;
            button.TooltipText = card.Shortcut;
            button.SetMeta("commander_slot", card.Slot);
            button.SetMeta("officer_id", card.OfficerId ?? -1);
            if (card.Portrait is { Kind: OfficerPortraitRouteKind.StockNamed } route)
            {
                var key = (route.ResourceName!, route.SpriteName!);
                if (!portraits.TryGetValue(key, out var sprite))
                { sprite = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(key.Item1,key.Item2); portraits.Add(key,sprite); }
                var portrait = new UiSourcePortrait { Name = "CommanderPortrait", Position = new(-44, 2) };
                button.AddChild(portrait); portrait.ConfigureMapHeader(repository, assets!, shader!, sprite);
                float scale = Math.Min(42 / portrait.Size.X, 50 / portrait.Size.Y);
                portrait.Scale = Vector2.One * scale;
            }
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920,1080) * scale) / 2;
    }
    public override void _ExitTree()
    { focus = null; select = null; portraits.Clear(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
