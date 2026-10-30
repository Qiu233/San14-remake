using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record ArmyDetailsRadar(IReadOnlyList<int?> Before, IReadOnlyList<int?> After,
    IReadOnlyList<int?> Modifiers);

internal sealed record ArmyDetailsContent(IReadOnlyDictionary<int, string> Values,
    string CommanderName, string? CommanderRuby, bool? NameplateVisible, DetailForceAppearance? Force,
    DeploymentFormationPresentation? Formation, IReadOnlyList<LoadedSprite>? PortraitLayers,
    bool? LandSelected, bool CanPreviewLand, bool CanPreviewNaval, int? Morale, int? MaximumMorale,
    ArmyDetailsRadar? Radar = null)
{
    internal int? CommanderId { get; init; }
    internal bool? CaptivesAvailable { get; init; }
    internal string? CaptivesUnresolvedOwner { get; init; }
    internal IReadOnlyDictionary<int,int> NodePalettes { get; init; } = new Dictionary<int,int>();
}

/// <summary>The existing army's source detail page. Preview selection is local;
/// the caller supplies current army values without creating deployment state.</summary>
internal partial class ArmyDetailsView : DetailsView
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!, body = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Func<bool?, ArmyDetailsContent>? query;
    private Func<bool?, UnitSourceParameters>? queryParameters;
    private UiSourceTabButton basic = null!, parameter = null!;
    private bool parameterPage;
    private bool? previewLand;
    private bool closed;
    private UiSourceCommandButton commanderButton = null!, captivesButton = null!;
    private Action<int>? inspectCommander;
    private Action? inspectCaptives;
    private int? currentCommander;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<bool?, ArmyDetailsContent> query, Action close, Func<bool?, UnitSourceParameters>? parameters = null,
        Action<int>? inspectCommander = null, Action? inspectCaptives = null)
    {
        this.inspectCommander = inspectCommander; this.inspectCaptives = inspectCaptives;
        this.repository = repository; this.text = text; this.query = query; queryParameters = parameters;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        ConfigureCommonBackdrop(repository);
        canvas = new Control { Name = "ArmyDetailsCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var header = new UiSourceHeader { Name = "ArmyDetailsTitle" }; canvas.AddChild(header);
        ConfigureSourcePage("ArmyDetails", header, close);
        header.Configure(repository, assets, shader, font, Message(0x462));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        Vector2 mount = new(191, 147);
        shell.Blur(canvas, 2, shell.Rectangle(2, mount));
        shell.Art(canvas, 3, shell.Rectangle(3, mount), assets, shader);
        string caption = Message(0x3CD);
        basic = new UiSourceTabButton { Name = "ArmyDefaultTab", Position = shell.Rectangle(4, mount).Position,
            Size = new(caption.Length * 28 + 36, 64), Selected = true };
        canvas.AddChild(basic); basic.Configure(repository, assets, shader, font, caption, 0x172EDC24);
        string parameterCaption = Message(0x452);
        parameter = new UiSourceTabButton { Name = "ArmyParameterTab", Position = basic.Position + new Vector2(basic.Size.X, 0),
            Size = new(parameterCaption.Length * 28 + 36, 64), Disabled = parameters is null };
        canvas.AddChild(parameter); parameter.Configure(repository, assets, shader, font, parameterCaption, 0xD45873AA);
        basic.Pressed += () => SelectPage(false);
        parameter.Pressed += () => SelectPage(true);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CloseArmyDetails", Position = footer.Rectangle(1, new(0, 904)).Position };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestClose, UiAudioCue.Cancel);
        float x = footer.Rectangle(21).Position.X + 45;
        float y = footer.Rectangle(21).Position.Y - 103;
        commanderButton = FooterButton("ArmyOfficerDetails", 0x211, () =>
        { if (currentCommander is int commander) this.inspectCommander?.Invoke(commander); });
        captivesButton = FooterButton("ArmyCaptives", 0x3EC, () => this.inspectCaptives?.Invoke());
        Render(query(null));

        UiSourceCommandButton FooterButton(string name, int captionId, Action action)
        {
            string label = Message(captionId);
            float width = Math.Max(96, label.Length * 28 + 20);
            var button = new UiSourceCommandButton { Name = name, Position = new(x, y), Size = new(width, 48),
                InputAdmission = () => !closed && IsVisibleInTree() && CanProcess() };
            canvas.AddChild(button);
            button.Configure(repository, assets, shader, font, label, action);
            x += width;
            return button;
        }
        Resized += Fit; Fit();
        SetMeta("parameter_page_available", parameters is not null);
    }

    internal void RefreshCurrent()
    {
        if (!closed && query is { } read) Render(read(previewLand));
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing army detail caption.");

    private void Render(ArmyDetailsContent content)
    {
        previewLand = content.LandSelected;
        currentCommander = content.CommanderId;
        commanderButton.Disabled = inspectCommander is null || currentCommander is null;
        captivesButton.Disabled = inspectCaptives is null || content.CaptivesAvailable != true;
        captivesButton.SetMeta("unresolved_owner", content.CaptivesUnresolvedOwner ?? "");
        if (body is not null) { canvas.RemoveChild(body); body.QueueFree(); }
        body = new Control { Name = parameterPage ? "ArmyParameterPage" : "ArmyDefaultPage", Position = new(191, 197), Size = new(1540, 688), MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(body);
        var source = new UiSourceLayout(repository, parameterPage ? 0x1243B03Cu : 0x946BF43Bu);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        // Replaced children, conditional markers and unresolved dynamic art
        // must never display the sample values embedded in the layout.
        var omitted = new HashSet<int>(Enumerable.Range(4, 10).Concat(Enumerable.Range(24, 15))
            .Concat(Enumerable.Range(43, 10)).Concat(Enumerable.Range(91, 13)).Concat(Enumerable.Range(105, 5)))
            { 19, 40, 57, 72, 77, 82 };
        if (parameterPage) omitted.UnionWith([114, 115, 116, 121, 129]);
        else omitted.UnionWith(Enumerable.Range(158, 5).Append(166));
        foreach (var node in source.Nodes)
        {
            if (omitted.Contains(node.Index)) continue;
            if (node.Type is 1 or 3) source.Art(body, node.Index, source.Rectangle(node.Index), assets!, shader!);
        }
        // These are the original authored supply/status sprites. Their
        // actual current palette changes; their sprite and visibility do not.
        var currentPalettes = MapSpritePalettes.Create();
        foreach (var (node, palette) in content.NodePalettes)
            source.Art(body, node, source.Rectangle(node), assets!, shader!, currentPalettes[palette]).Name = "ArmyPalette" + node;
        int[][] captions = [[14,0x1FD],[16,0x219],[18,0x1FE],[39,0x22B],[42,0x333],[56,600],
            [59,0x225],[65,0x20E],[68,0x226],[71,0x25E],[76,0x25C],[81,0x260],[86,0x20C],[89,0x20D]];
        foreach (var binding in captions) source.Text(body, binding[0], default, Message(binding[1]), font!, presets[13]);
        if (parameterPage)
        {
            foreach (var binding in new[] { (113, 0x234), (118, 0x461), (125, 0x411) })
                source.Text(body, binding.Item1, default, Message(binding.Item2), font!, presets[13]);
        }
        else
        {
            foreach (var binding in new[] { (115,0x3E0),(120,0x299),(125,0x3E1),(130,0x20A),(135,0x3EC),
                (141,0x279),(146,0x27A),(151,0x27B),(156,0x27C) })
                source.Text(body, binding.Item1, default, Message(binding.Item2), font!, presets[13]);
        }
        source.Text(body, parameterPage ? 128 : 165, default, repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[0].Records[0x114].Text ?? throw new InvalidDataException("Missing technology caption."), font!, presets[13]);
        foreach (var value in content.Values)
        {
            if (omitted.Contains(value.Key) || parameterPage && value.Key >= 110) continue;
            source.Text(body, value.Key, default, value.Value, font!, presets[0]).Name = "ArmyField" + value.Key;
        }
        if (content.Formation is { } formation)
        {
            source.Art(body, 40, source.Rectangle(40), assets!, shader!, MapSpritePalettes.Create()[formation.Icon.Palette],
                commonSpriteOverride: ArmyMapUiSources.Create().IconName(formation.Icon), expansionCommonSprite: formation.Icon.SpriteId == 0x3F0);
        }
        if (content.PortraitLayers is { } layers)
        {
            var rectangle = source.Rectangle(2);
            var portrait = new UiSourceStandingPortrait { Name = "ArmyCommanderPortrait", Position = rectangle.Position };
            // Keep the source character behind the independently mounted nameplate.
            body.AddChild(portrait); portrait.Configure(repository, assets!, shader!, layers);
            portrait.Scale = rectangle.Size / portrait.Size;
            body.MoveChild(portrait, 0);
        }
        if (content.NameplateVisible != false) Nameplate(content, source.Rectangle(4).Position);
        if (content.Morale is int morale && content.MaximumMorale is int maximum && maximum > 0)
            MoraleGauge(source.Rectangle(63), morale, maximum);
        var radar = new DeploymentCombatChart { Name = "ArmyCombatChart", Position = source.Rectangle(91).Position };
        body.AddChild(radar);
        radar.Configure(repository, assets!, shader!, font!, text, content.Radar?.Modifiers ?? new int?[5]);
        radar.ApplyComparison(content.Radar?.Before, content.Radar?.After);
        Radio("ArmyLandPreview", 25, 0xA97E37BE, 0x368, true, content.CanPreviewLand);
        Radio("ArmyNavalPreview", 32, 0x01649C44, 0x369, false, content.CanPreviewNaval);
        if (parameterPage && queryParameters is { } readParameters) RenderParameters(source, readParameters(previewLand));
        SetMeta("land_preview", content.LandSelected.HasValue ? content.LandSelected.Value ? 1 : 0 : -1);

        void Radio(string name, int node, uint layout, int caption, bool land, bool enabled)
        {
            var rectangle = source.Rectangle(node);
            var button = new UiSourceCaptionButton { Name = name, Position = rectangle.Position, Size = rectangle.Size,
                Selected = content.LandSelected == land, Disabled = !enabled };
            body.AddChild(button); button.Configure(repository, assets!, shader!, font!, layout, Message(caption), () =>
            { if (!closed && previewLand != land && query is { } read) Render(read(land)); });
        }
    }

    private void SelectPage(bool selected)
    {
        if (closed || query is null || parameterPage == selected || selected && queryParameters is null) return;
        parameterPage = selected; basic.Selected = !selected; parameter.Selected = selected;
        Render(query(previewLand));
    }

    private void MoraleGauge(Rect2 rectangle, int value, int maximum)
    {
        var source = new UiSourceLayout(repository, 0xAFC125D8);
        int width = source.Nodes[1].Value44 * Math.Clamp(value, 0, maximum) / maximum;
        var gauge = new Control { Name = "ArmyMoraleGauge", Position = rectangle.Position,
            Size = source.Rectangle(0).Size, Scale = rectangle.Size / source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(gauge);
        source.Art(gauge, 1, source.Rectangle(1), assets!, shader!);
        if (width > 0)
        {
            var filled = new UiSourceLayout(source.Source, new Dictionary<int, UiSourceNodeGeometry>
                { [2] = new(Size: new(width, source.Nodes[2].Value46)) });
            filled.Art(gauge, 2, filled.Rectangle(2), assets!, shader!, MapSpritePalettes.Create()[value <= 40 ? 2 : 1]);
        }
    }

    private void Nameplate(ArmyDetailsContent content, Vector2 mount)
    {
        var source = new UiSourceLayout(repository, 0xE5FA7ED8);
        foreach (int node in new[] { 1, 2, 3 }) source.Art(body, node, source.Rectangle(node, mount), assets!, shader!);
        source.Text(body, 7, mount, content.CommanderName, font!);
        source.Text(body, 8, mount, content.CommanderRuby ?? "", font!);
        if (content.Force is { } flag)
        {
            var fill = flag.Fill;
            source.Art(body, 5, source.Rectangle(5, mount), assets!, shader!, new(fill, fill, fill, fill));
            var label = source.Text(body, 6, mount, flag.Glyph, font!);
            uint colour = flag.Letter.CpuPacked;
            label.Configure(font!, MapFontProfile.FromNode(source.Nodes[6], source.Source.Source.ResourceUri) with
                { ColourLT = colour, ColourRT = colour, ColourLB = colour, ColourRB = colour });
        }
    }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree()
    { closed = true; query = null; queryParameters = null; inspectCommander = null; inspectCaptives = null; font?.Dispose(); assets?.Dispose(); shader?.Dispose(); base._ExitTree(); }
}
