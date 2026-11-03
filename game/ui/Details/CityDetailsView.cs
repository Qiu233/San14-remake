using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary City page consumes one retained current publication.
/// Its renderer never recalculates forecasts or selects an officer.</summary>
public partial class CityDetailsView : DetailsView
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Control bodyCanvas = null!;
    private bool ownsResources;
    private Control? currentBody;
    private UiSourceDetailSelector selector = null!;
    private int currentIdentity;
    internal InformationDetailSelectionReturn ReadSelectionReturn() => selector.ReadReturn(currentIdentity);

    internal void Configure(GameResourceRepository repository, CityInformationPage page,
        PreviewText text, Action close, bool mapBackdrop = false, InformationDetailSelectionContext? selection = null,
        Action<int>? select = null, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository = repository; this.text = text;
        ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository); font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        ConfigureCommonBackdrop(repository);
        var header = new UiSourceHeader { Name = "CityDetailsTitle" };
        ConfigureSourcePage("CityDetails", header, close);
        canvas = new() { Name = "CitySourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var bodyMotion = new Node2D { Name = "CityDetailsBodyMotion" }; canvas.AddChild(bodyMotion);
        bodyCanvas = new() { Name = "CityDetailsBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        bodyMotion.AddChild(bodyCanvas);
        var headerMotion = new Node2D { Name = "CityDetailsHeaderMotion" }; canvas.AddChild(headerMotion);
        var footerMotion = new Node2D { Name = "CityDetailsFooterMotion" }; canvas.AddChild(footerMotion);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(bodyCanvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(bodyCanvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        headerMotion.AddChild(header); header.Configure(repository, assets, shader, font, Message(CityInformationSources.TitleId));
        var tab = new UiSourceTabButton { Name = "BasicTab", Position = shell.Rectangle(4, new(191, 147)).Position,
            Size = new(Message(CityInformationSources.BasicTabCaptionId).Length * 28 + 36, 64), Selected = true };
        bodyCanvas.AddChild(tab); tab.Configure(repository, assets, shader, font, Message(CityInformationSources.BasicTabCaptionId));
        selector = new(); bodyCanvas.AddChild(selector);
        selector.Configure(repository, assets, shader, font, text,
            selection ?? new([new(page.CityId, page.Name, null)], () => 0),
            InformationListKind.Cities, page.CityId, () => CanProcess() && IsVisibleInTree() && !IsQueuedForDeletion(), select);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CloseCityDetails", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = () => CanProcess() && IsVisibleInTree() && !IsQueuedForDeletion() };
        footerMotion.AddChild(back); back.ConfigureScenarioReturn(repository, assets, shader, text.SourceLocale, RequestClose);
        Apply(page);
        ConfigureReturnTransition(assets.SourceLayout(0xF9C5DB14), new Dictionary<int, (Node2D, Vector2)>
        { [1] = (bodyMotion, Vector2.Zero), [4] = (headerMotion, Vector2.Zero), [6] = (footerMotion, new(0, 904)) });
        Resized += Fit; Fit();
    }

    internal void Apply(CityInformationPage page)
    {
        currentIdentity = page.CityId;
        int bodyIndex = currentBody?.GetIndex() ?? -1;
        if (currentBody is { } previous) { bodyCanvas.RemoveChild(previous); previous.QueueFree(); currentBody = null; }
        BuildBody(page);
        if (bodyIndex >= 0) bodyCanvas.MoveChild(currentBody!, bodyIndex);
        selector.Select(page.CityId);
        SetMeta("city_id", page.CityId); SetMeta("source_unknown", page.UnresolvedOwner ?? "");
        SetMeta("report_context", page.ReportContext);
    }

    private void BuildBody(CityInformationPage page)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, page.LayoutName));
        var body = new Control { Name = "CurrentCityDetails", Position = new(191, 197), Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        bodyCanvas.AddChild(body); currentBody = body;
        var presets = MapTextPresets.Create();
        var palettes = MapSpritePalettes.Create();
        var sprites = new LocalizedUiSpriteSource(repository);
        var icons = ArmyMapUiSources.Create();
        if (!Hidden(2) && page.StandingPortrait is { } standing)
        {
            var portrait = new UiSourceStandingPortrait { Name = "CityGovernorStanding", Position = source.Rectangle(2).Position };
            body.AddChild(portrait); portrait.Configure(repository, assets, shader, standing.LoadSprites(sprites.LoadTexturePackSprite));
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
        if (!Hidden(26) && page.NearbyEnemyPanelVisible == true) Nearby();
        foreach (var gauge in page.Gauges) if (!Hidden(gauge.Node)) Gauge(gauge);
        Duty(page.Recruiter); Duty(page.Trainer);
        if (page.Tactic is { } tactic && !Hidden(tactic.Node))
        {
            var box = source.Rectangle(tactic.Node);
            var marker = new ArmyDetailsTacticMarker { Name = "CityTactic", Position = box.Position, Size = box.Size };
            body.AddChild(marker);
            marker.Configure(repository, assets, shader, font, tactic.Name, tactic.IconSpriteId == 0x222, () => { }, () => { });
        }
        if (!Hidden(194))
        {
            var miniMap = new UiSourceMiniMap { Name = "CityMiniMap", Position = source.Rectangle(194).Position };
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
                if (current is 2 or 4 or 18 or 26 or 36 or 43 or 161 or 169 or 178 or 194) return true;
            return false;
        }
        void Badge()
        {
            var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_03_00_info_city_offset_00"));
            var mount = new Control { Name = "CityNameplate", Position = source.Rectangle(4).Position,
                Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(mount);
            foreach (var node in badge.Nodes)
                if (node.Type is 1 or 3 && node.Index is not (5 or 6))
                    badge.Art(mount, node.Index, badge.Rectangle(node.Index), assets, shader);
            badge.Text(mount, 7, default, page.Name, font);
            if (page.ForceFlag is { } appearance)
            {
                var flag = new UiSourceForceFlag { Name = "CityForceFlag", Position = badge.Rectangle(4).Position };
                mount.AddChild(flag); flag.Configure(repository, assets, shader, font, appearance, embedded: true);
            }
        }
        void Duty(CityInformationDutyPortrait? duty)
        {
            if (duty is null || Hidden(duty.Node)) return;
            if (duty.Portrait is not { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: { } pack, SpriteName: { } sprite }) return;
            var box = source.Rectangle(duty.Node);
            var portrait = new UiSourcePortrait { Name = "CityDutyPortrait" + duty.Node, Position = box.Position, Size = box.Size };
            body.AddChild(portrait); portrait.Configure(repository, assets, shader, sprites.LoadTexturePackSprite(pack, sprite));
            if (duty.UnresolvedOwner is { } missing) portrait.SetMeta("unresolved_owner", missing);
        }
        void Nearby()
        {
            var marker = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_mrker"));
            var box = source.Rectangle(26);
            var mount = new Control { Name = "CityNearbyEnemy", Position = box.Position,
                Size = marker.Rectangle(0).Size, Scale = box.Size / marker.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(mount); marker.Art(mount, 2, marker.Rectangle(2), assets, shader, commonSpriteOverride: "000_cmn_mrk_base_red");
            marker.Text(mount, 3, default, Message(CityInformationSources.NearbyEnemyCaptionId), font);
        }
        void Gauge(CityInformationGauge gauge)
        {
            var bar = new UiSourceLayout(repository, 0xAFC125D8);
            var box = source.Rectangle(gauge.Node);
            var mount = new Control { Name = "CityGauge" + gauge.Node, Position = box.Position,
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
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing city detail Data1/{id:X}.");
    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        if (ownsResources) { font?.Dispose(); shader?.Dispose(); assets?.Dispose(); }
        base._ExitTree();
    }
}
