using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary Area page consumes one retained current publication.
/// Its renderer never recalculates forecasts or selects an officer.</summary>
public partial class AreaDetailsView : DetailsView
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Control bodyCanvas = null!;
    private Node2D footerMotion = null!;
    private Control? currentBody;
    private UiSourceDetailSelector selector = null!;
    private int currentIdentity;
    internal InformationDetailSelectionReturn ReadSelectionReturn() => selector.ReadReturn(currentIdentity);

    internal void Configure(GameResourceRepository repository, AreaInformationPage page,
        PreviewText text, Action close, AdministrationPageChrome.Resources resources, InformationDetailSelectionContext? selection = null, Action<int>? select = null)
    {
        this.repository = repository; this.text = text;
        assets = resources.Assets; font = resources.Font; shader = resources.SpriteShader;
        ConfigureCommonBackdrop(repository);
        var header = new UiSourceHeader { Name = "AreaDetailsTitle" };
        ConfigureSourcePage("AreaDetails", header, close);
        canvas = new() { Name = "AreaSourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var bodyMotion = new Node2D { Name = "AreaDetailsBodyMotion" }; canvas.AddChild(bodyMotion);
        bodyCanvas = new() { Name = "AreaDetailsBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        bodyMotion.AddChild(bodyCanvas);
        var headerMotion = new Node2D { Name = "AreaDetailsHeaderMotion" }; canvas.AddChild(headerMotion);
        footerMotion = new() { Name = "AreaDetailsFooterMotion" }; canvas.AddChild(footerMotion);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(bodyCanvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(bodyCanvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        headerMotion.AddChild(header); header.Configure(repository, assets, shader, font, Message(AreaInformationSources.TitleId));
        var tab = new UiSourceTabButton { Name = "BasicTab", Position = shell.Rectangle(4, new(191, 147)).Position,
            Size = new(Message(AreaInformationSources.BasicTabCaptionId).Length * 28 + 36, 64), Selected = true };
        bodyCanvas.AddChild(tab); tab.Configure(repository, assets, shader, font, Message(AreaInformationSources.BasicTabCaptionId));
        selector = new(); bodyCanvas.AddChild(selector);
        selector.Configure(repository, assets, shader, font, text,
            selection ?? new([new(page.AreaId, page.Name, null)], () => 0),
            InformationListKind.Areas, page.AreaId, () => CanProcess() && IsVisibleInTree() && !IsQueuedForDeletion(), select);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CloseAreaDetails", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = () => CanProcess() && IsVisibleInTree() && !IsQueuedForDeletion() };
        footerMotion.AddChild(back); back.ConfigureScenarioReturn(repository, assets, shader, text.SourceLocale, RequestClose);
        Apply(page);
        ConfigureReturnTransition(assets.SourceLayout(0xF9C5DB14), new Dictionary<int, (Node2D, Vector2)>
        { [1] = (bodyMotion, Vector2.Zero), [4] = (headerMotion, Vector2.Zero), [6] = (footerMotion, new(0, 904)) });
        Resized += Fit; Fit();
    }

    internal void Apply(AreaInformationPage page)
    {
        currentIdentity = page.AreaId;
        int bodyIndex = currentBody?.GetIndex() ?? -1;
        if (currentBody is { } previous) { bodyCanvas.RemoveChild(previous); previous.QueueFree(); currentBody = null; }
        BuildBody(page);
        if (bodyIndex >= 0) bodyCanvas.MoveChild(currentBody!, bodyIndex);
        selector.Select(page.AreaId);
        SetMeta("area_id", page.AreaId); SetMeta("source_unknown", page.UnresolvedOwner ?? "");
    }

    private void BuildBody(AreaInformationPage page)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, page.LayoutName));
        var body = new Control { Name = "CurrentAreaDetails", Position = new(191, 197), Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        bodyCanvas.AddChild(body); currentBody = body;
        var presets = MapTextPresets.Create();
        var palettes = MapSpritePalettes.Create();
        var icons = ArmyMapUiSources.Create();
        if (!Hidden(2) && page.StandingPortrait is { } standing)
        {
            var portrait = new UiSourceStandingPortrait { Name = "AreaOverseerStanding", Position = source.Rectangle(2).Position };
            body.AddChild(portrait); portrait.Configure(repository, assets, shader, standing.LoadSources(assets.Portraits.RequireSource));
            portrait.Scale = source.Rectangle(2).Size / portrait.Size;
        }
        foreach (var node in source.Nodes)
        {
            if (Hidden(node.Index) || Replaced(node.Index)) continue;
            if (node.Type is 1 or 3)
                source.Art(body, node.Index, source.Rectangle(node.Index), assets, shader,
                    page.TextColors.TryGetValue(node.Index, out var colours) ? colours : null,
                    commonSpriteOverride: page.SpriteIds.TryGetValue(node.Index, out int spriteId)
                        ? icons.IconName(new ArmyMapIconSelection(spriteId, 0)) : null);
            else if (node.Type == 6) source.Blur(body, node.Index, source.Rectangle(node.Index));
        }
        var displayText = new Dictionary<int, string>(page.Text);
        foreach (var (node, _) in page.UnresolvedFields)
            if ((uint)node < (uint)source.Nodes.Count && source.Nodes[node].Type == 2)
                displayText.TryAdd(node, text.Get("detail_unknown"));
        foreach (var (node, value) in displayText)
        {
            if (Hidden(node) || Replaced(node)) continue;
            MapTextPreset? preset = page.TextStyles.TryGetValue(node, out int style) ? presets[style] : null;
            var label = source.Text(body, node, default, value, font, preset);
            var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri);
            if (preset is not null) profile = profile.WithPreset(preset);
            if (page.TextColors.TryGetValue(node, out var colours))
                profile = profile with { ColourLT = colours.LT.CpuPacked, ColourRT = colours.RT.CpuPacked,
                    ColourLB = colours.LB.CpuPacked, ColourRB = colours.RB.CpuPacked };
            if (page.TextAlignments.TryGetValue(node, out int alignment))
                profile = profile with { RawAlign = checked((byte)alignment) };
            if (node == 14 && page.ForceNameColor is { } forceColour)
                profile = profile with { ColourLT = forceColour.CpuPacked, ColourRT = forceColour.CpuPacked,
                    ColourLB = forceColour.CpuPacked, ColourRB = forceColour.CpuPacked };
            label.Configure(font, profile); label.SetKnownColourLine(value);
            if (page.UnresolvedFields.TryGetValue(node, out var missing)) label.SetMeta("unresolved_owner", missing);
        }
        if (!Hidden(4)) Badge();
        if (!Hidden(18) && page.DistrictNumber is int district)
            source.Art(body, 18, source.Rectangle(18), assets, shader,
                commonSpriteOverride: ArmyMapUiSources.Create().IconName(ArmyMapUiSources.DistrictIcon(checked((byte)district))));
        if (!Hidden(24) && page.NearbyEnemyPanelVisible == true) Nearby();
        foreach (var gauge in page.Gauges) if (!Hidden(gauge.Node)) Gauge(gauge);
        foreach (var trait in page.Traits)
        {
            if (Hidden(trait.Node)) continue;
            var box = source.Rectangle(trait.Node);
            var marker = new AreaAdministrationTraitMarker { Name = "AreaTrait" + trait.Node, Position = box.Position };
            body.AddChild(marker); marker.Configure(repository, assets, shader, font, box.Size);
            marker.Apply(trait.TraitId, trait.Name, trait.Style);
        }
        foreach (var neighbor in page.Neighbors)
        {
            int node = 132 + 6 * neighbor.Slot;
            if (Hidden(node)) continue;
            if (neighbor.Flag is { } appearance)
            {
                var flag = new UiSourceForceFlag { Name = "AreaNeighborFlag" + neighbor.Slot, Position = source.Rectangle(node).Position };
                body.AddChild(flag); flag.Configure(repository, assets, shader, font, appearance, neighbor: true);
                if (neighbor.UnresolvedOwner is { } missing) flag.SetMeta("unresolved_owner", missing);
            }
        }
        if (!Hidden(179))
        {
            var miniMap = new UiSourceMiniMap { Name = "AreaMiniMap", Position = source.Rectangle(179).Position };
            body.AddChild(miniMap); miniMap.Configure(repository, assets, shader, page.MiniMap);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        bool Hidden(int index)
        {
            for (int? cursor = index; cursor is int current; cursor = source.Nodes[current].ParentIndex)
                if (page.HiddenNodes.Contains(current)) return true;
            return false;
        }
        bool Replaced(int index)
        {
            for (int? cursor = index; cursor is int current; cursor = source.Nodes[current].ParentIndex)
                if (current is 2 or 4 or 18 or 24 or 61 or 92 or 93 or 94 or 95 or 96 or 179 ||
                    current >= 132 && current <= 174 && (current - 132) % 6 == 0) return true;
            return false;
        }
        void Badge()
        {
            var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, AreaInformationSources.NameplateLayoutName));
            var mount = new Control { Name = "AreaNameplate", Position = source.Rectangle(4).Position,
                Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(mount);
            foreach (var node in badge.Nodes)
                if (node.Type is 1 or 3 && node.Index is not (5 or 6))
                    badge.Art(mount, node.Index, badge.Rectangle(node.Index), assets, shader);
            badge.Text(mount, 7, default, page.Name, font);
            badge.Text(mount, 8, default, "", font);
            if (page.ForceFlag is { } appearance)
            {
                // Area paints the embedded flag directly, retaining its fractional source mount.
                var fill = appearance.Fill;
                badge.Art(mount, 5, badge.Rectangle(5), assets, shader, new(fill, fill, fill, fill));
                var glyph = badge.Text(mount, 6, default, appearance.Glyph, font);
                uint colour = appearance.Letter.CpuPacked;
                glyph.Configure(font, MapFontProfile.FromNode(badge.Nodes[6], badge.Source.Source.ResourceUri)
                    with { ColourLT = colour, ColourRT = colour, ColourLB = colour, ColourRB = colour });
            }
            if (page.UnresolvedFields.TryGetValue(4, out var missing)) mount.SetMeta("unresolved_owner", missing);
        }
        void Nearby()
        {
            var marker = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_mrker"));
            var box = source.Rectangle(24);
            var mount = new Control { Name = "AreaNearbyEnemy", Position = box.Position,
                Size = marker.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(mount); marker.Art(mount, 2, marker.Rectangle(2), assets, shader, commonSpriteOverride: "000_cmn_mrk_base_red");
            marker.Text(mount, 3, default, Message(AreaInformationSources.NearbyEnemyCaptionId), font);
        }
        void Gauge(CityInformationGauge gauge)
        {
            var bar = new UiSourceLayout(repository, 0xAFC125D8);
            var box = source.Rectangle(gauge.Node);
            var mount = new Control { Name = "AreaGauge" + gauge.Node, Position = box.Position,
                Size = bar.Rectangle(0).Size, Scale = box.Size / bar.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(mount); bar.Art(mount, 1, bar.Rectangle(1), assets, shader);
            if (gauge.Maximum <= 0) return;
            int width = (int)((long)bar.Nodes[1].Value44 * Math.Clamp(gauge.Current, 0, gauge.Maximum) / gauge.Maximum);
            if (width <= 0) return;
            var fill = new UiSourceLayout(bar.Source, new Dictionary<int, UiSourceNodeGeometry>
                { [2] = new(Size: new(width, bar.Nodes[2].Value46)) });
            fill.Art(mount, 2, fill.Rectangle(2), assets, shader, palettes[gauge.Style]);
        }
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing area detail Data1/{id:X}.");
    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        base._ExitTree();
    }
}
