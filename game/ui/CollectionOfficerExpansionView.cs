using Godot;
using San14.Data;

/// <summary>The collection's full-size static portrait. Paging is dialog-local;
/// it never writes the detail's temporary appearance or a campaign person.</summary>
internal partial class CollectionOfficerExpansionView : Control
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, portrait = null!, pager = null!;
    private UiSourceText pageNumber = null!, pageCount = null!;
    private GameResourceRepository repository = null!;
    private CollectionOfficerCatalog catalog = null!;
    private UiSourceLayout source = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private int officerId, variant;
    private bool abandoned;
    private bool ownsResources;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionOfficerCatalog catalog, int officerId, int variant,
        Func<bool> admitted, Action returned, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository = repository; this.catalog = catalog; this.officerId = officerId; this.variant = variant;
        this.admitted = admitted; this.returned = returned;
        Name = "CollectionOfficerExpansion"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository);
        font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        source = assets.SourceLayout(0xB7B5EAA0);
        portrait = new() { Name = "CollectionOfficerExpandedPortrait", Size = canvas.Size, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(portrait);
        Rect2 frameBox = source.Rectangle(3);
        var frame = new UiSourceFaceFrame { Name = "CollectionOfficerPortraitFrame",
            Position = frameBox.Position, Size = frameBox.Size };
        canvas.AddChild(frame); frame.Configure(repository, assets, shader);
        var pagerSource = assets.SourceLayout(0x6148B858);
        Rect2 pagerBox = source.Rectangle(2);
        pager = new() { Name = "CollectionOfficerPortraitPager", Position = pagerBox.Position,
            Size = pagerBox.Size, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(pager);
        foreach (var node in pagerSource.Nodes.Where(node => node.Type is 1 or 3))
            pagerSource.Art(pager, node.Index, pagerSource.Rectangle(node.Index), assets, shader);
        pageNumber = pagerSource.Text(pager, 2, default, "", font);
        pagerSource.Text(pager, 3, default, "/", font);
        pageCount = pagerSource.Text(pager, 4, default, "", font);
        AddArrow(5, "CollectionOfficerPortraitPrevious");
        AddArrow(7, "CollectionOfficerPortraitNext");
        var footer = assets.SourceLayout(0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CollectionOfficerExpansionReturn",
            Position = footer.Rectangle(1, new(0, 904)).Position, InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        ApplyVariant();
        SetMeta("source_layout", source.Source.Source.ResourceUri); SetMeta("officer_id", officerId);
        Resized += Fit; Fit();

        void AddArrow(int node, string name)
        {
            Rect2 box = pagerSource.Rectangle(node);
            var button = new UiSourceHitButton { Name = name, Position = box.Position, Size = box.Size,
                FocusMode = FocusModeEnum.None, IgnoreTextureSize = true,
                ActionMode = BaseButton.ActionModeEnum.Release };
            pager.AddChild(button);
            button.Pressed += () =>
            {
                if (!ReadAdmission()) return;
                int count = catalog.GetVariantCount(officerId);
                if (count < 2) return;
                this.variant = (this.variant + 1) % count; ApplyVariant();
            };
        }
    }

    private void ApplyVariant()
    {
        var publication = catalog.ReadExpansion(officerId, variant);
        foreach (Node child in portrait.GetChildren()) { portrait.RemoveChild(child); child.QueueFree(); }
        if (publication.Image is { } image)
        {
            if (ownsResources)
                source.Art(portrait, 1, source.Rectangle(1), assets, shader,
                    commonSpriteOverride: image.SpriteName, commonTexturePack: image.TexturePack);
            else
                source.EncodedArt(portrait, 1, source.Rectangle(1), assets, shader,
                    assets.Portraits.RequireSource(image.TexturePack, image.SpriteName));
        }
        if (publication.UnresolvedOwner is { } missing) SetMeta("unresolved_owner", missing);
        else RemoveMeta("unresolved_owner");
        pager.Visible = publication.HasAlternate;
        // Catalog variants are relative to the static person's appearance. The
        // source pager numbers the actual primary/alternate portrait instead.
        var currentPortrait = catalog.ReadOfficer(officerId, variant).Portrait;
        int absolutePage = publication.HasAlternate && currentPortrait is
            { CurrentAppearanceIndex: ushort appearance, ResolvedAppearanceIndex: int resolved } && appearance != resolved ? 2 : 1;
        pageNumber.Text = absolutePage.ToString(System.Globalization.CultureInfo.InvariantCulture);
        pageCount.Text = publication.VariantCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SetMeta("variant_index", publication.VariantIndex);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon() { abandoned = true; admitted = null; returned = null; Hide(); }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        Abandon();
        if (ownsResources) { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
    }
}
