using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Displays one retained Force publication without rebuilding its
/// economic or diplomatic values in the view.</summary>
public partial class ForceDetailsView : DetailsView
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

    internal void Configure(GameResourceRepository repository, ForceInformationPage page,
        PreviewText text, Action close, AdministrationPageChrome.Resources resources, InformationDetailSelectionContext? selection = null, Action<int>? select = null)
    {
        this.repository = repository; this.text = text;
        assets = resources.Assets; font = resources.Font; shader = resources.SpriteShader;
        ConfigureCommonBackdrop(repository);
        var header = new UiSourceHeader { Name = "ForceDetailsTitle" };
        ConfigureSourcePage("ForceDetails", header, close);
        canvas = new() { Name = "ForceSourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var bodyMotion = new Node2D { Name = "ForceDetailsBodyMotion" }; canvas.AddChild(bodyMotion);
        bodyCanvas = new() { Name = "ForceDetailsBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        bodyMotion.AddChild(bodyCanvas);
        var headerMotion = new Node2D { Name = "ForceDetailsHeaderMotion" }; canvas.AddChild(headerMotion);
        footerMotion = new() { Name = "ForceDetailsFooterMotion" }; canvas.AddChild(footerMotion);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(bodyCanvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(bodyCanvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        headerMotion.AddChild(header); header.Configure(repository, assets, shader, font, Message(0x45C));
        string basic = Message(ForceInformationSources.BasicTabCaptionId);
        var tab = new UiSourceTabButton { Name = "BasicTab", Position = shell.Rectangle(4, new(191, 147)).Position,
            Size = new(basic.Length * 28 + 36, 64), Selected = true };
        bodyCanvas.AddChild(tab); tab.Configure(repository, assets, shader, font, basic);
        selector = new(); bodyCanvas.AddChild(selector);
        selector.Configure(repository, assets, shader, font, text,
            selection ?? new([new(page.ForceId, page.Name, null)], () => 0),
            InformationListKind.Forces, page.ForceId, () => CanProcess() && IsVisibleInTree() && !IsQueuedForDeletion(), select);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CloseForceDetails", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = () => CanProcess() && IsVisibleInTree() && !IsQueuedForDeletion() };
        footerMotion.AddChild(back); back.ConfigureScenarioReturn(repository, assets, shader, text.SourceLocale, RequestClose);
        Apply(page);
        ConfigureReturnTransition(assets.SourceLayout(0xF9C5DB14), new Dictionary<int, (Node2D, Vector2)>
        { [1] = (bodyMotion, Vector2.Zero), [4] = (headerMotion, Vector2.Zero), [6] = (footerMotion, new(0, 904)) });
        Resized += Fit; Fit();
    }

    internal void Apply(ForceInformationPage page)
    {
        currentIdentity = page.ForceId;
        int bodyIndex = currentBody?.GetIndex() ?? -1;
        if (currentBody is { } previous) { bodyCanvas.RemoveChild(previous); previous.QueueFree(); currentBody = null; }
        BuildBody(page);
        if (bodyIndex >= 0) bodyCanvas.MoveChild(currentBody!, bodyIndex);
        selector.Select(page.ForceId);
        SetMeta("force_id", page.ForceId); SetMeta("source_unknown", page.UnresolvedOwner ?? "");
    }

    private void BuildBody(ForceInformationPage page)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, page.LayoutName));
        var body = new Control { Name = "CurrentForceDetails", Position = new(191, 197), Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        bodyCanvas.AddChild(body); currentBody = body;
        var presets = MapTextPresets.Create();
        if (!Hidden(2) && page.StandingPortrait is { } standing)
        {
            var portrait = new UiSourceStandingPortrait { Name = "ForceRulerStanding", Position = source.Rectangle(2).Position };
            body.AddChild(portrait); portrait.Configure(repository, assets, shader, standing.LoadSources(assets.Portraits.RequireSource));
            portrait.Scale = source.Rectangle(2).Size / portrait.Size;
        }
        foreach (var node in source.Nodes)
        {
            if (Hidden(node.Index) || Replaced(node.Index)) continue;
            if (node.Type is 1 or 3)
                source.Art(body, node.Index, source.Rectangle(node.Index), assets, shader,
                    page.TextColors.TryGetValue(node.Index, out var colours) ? colours : null);
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
            label.Configure(font, profile); label.SetKnownColourLine(value);
            if (page.UnresolvedFields.TryGetValue(node, out var missing)) label.SetMeta("unresolved_owner", missing);
        }
        if (!Hidden(4)) Nameplate();
        if (!Hidden(128) && page.Relation is { } relation) BuildRelation(body, source.Rectangle(128), relation);
        if (!Hidden(176))
        {
            var miniMap = new UiSourceMiniMap { Name = "ForceMiniMap", Position = source.Rectangle(176).Position };
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
                if (current is 2 or 4 or 128 or 176) return true;
            return false;
        }
        void Nameplate()
        {
            var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, ForceInformationSources.NameplateLayoutName));
            var mount = new Control { Name = "ForceNameplate", Position = source.Rectangle(4).Position,
                Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore,
                Visible = page.Nameplate?.ForceValid != false && page.Nameplate?.RulerValid != false };
            body.AddChild(mount);
            foreach (int node in new[] { 1, 2, 3 })
                badge.Art(mount, node, badge.Rectangle(node), assets, shader);
            if (page.Nameplate is { FlagValid: true, FlagAppearance: { } appearance })
            {
                var flag = new UiSourceForceFlag { Name = "NameplateForceFlag", Position = badge.Rectangle(4).Position };
                mount.AddChild(flag); flag.Configure(repository, assets, shader, font, appearance);
            }
            badge.Text(mount, 7, default, page.Nameplate?.RulerName ?? text.Get("detail_unknown"), font);
            badge.Text(mount, 8, default, "", font);
            if (page.Nameplate is null) mount.SetMeta("unresolved_owner", "force-information.nameplate_current");
        }
    }

    private void BuildRelation(Control parent, Rect2 box, ForceInformationRelation relation)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_mrker"));
        var mount = new Control { Name = "ForceRelation", Position = box.Position,
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(mount);
        string sprite = ArmyMapUiSources.Create().IconName(new ArmyMapIconSelection(relation.Style, 0));
        source.Art(mount, 2, source.Rectangle(2), assets, shader, commonSpriteOverride: sprite);
        source.Text(mount, 3, default, relation.Text, font);
        mount.SetMeta("relation_level", relation.Value);
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing force detail Data1/{id:X}.");
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
