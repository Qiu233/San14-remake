using Godot;
using San14.Data;

/// <summary>The normal mode-one help directory and its original illustration /
/// explanation detail panes. This page never edits an event or its library.</summary>
internal partial class EventEditorHelpView : DetailsView
{
    private GameResourceRepository repository = null!;
    private EventEditorHelpCatalog catalog = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private UiSourceHeader header = null!;
    private UiSourceLayout detailSource = null!;
    private Control canvas = null!, sourceRoot = null!, directory = null!, detail = null!, pageBody = null!;
    private UiSourceText title = null!, pageCounter = null!;
    private Control pager = null!;
    private EventEditorHelpArrow previous = null!, next = null!;
    private UiSourceRoundButton back = null!;
    private readonly List<EventEditorHelpTopicButton> rows = [];
    private Func<bool>? admitted;
    private Action? returned;
    private EventEditorHelpTopic? topic;
    private int page;
    private bool abandoned;
    internal int? SelectedTopicId => topic?.Id;
    internal int PageIndex => page;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        EventEditorHelpCatalog catalog, Func<bool> admitted, Action returned,
        CampaignHelpAdviser? campaignAdviser = null, Func<int>? currentScrollSpeed = null)
    {
        if (catalog.Locale != text.SourceLocale) throw new InvalidDataException("Help locale differs from its view.");
        this.repository = repository; this.catalog = catalog; this.admitted = admitted; this.returned = returned;
        this.campaignAdviser = campaignAdviser; this.currentScrollSpeed = currentScrollSpeed;
        Name = "EventEditorHelp"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        ConfigureCommonBackdrop(repository);
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        header = new() { Name = "EventHelpHeader" }; canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, catalog.Title);
        ConfigureSourcePage(catalog.Mode == 0 ? "CampaignHelp" : "EventEditorHelp", header, returned);
        ZIndex = 7;
        var root = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "12_01_01_help_pk"));
        Vector2 mount = new(960 + root.Nodes[0].HeaderValues[0], 540 - root.Nodes[0].HeaderValues[1]);
        sourceRoot = new() { Name = "HelpSourceRoot", Position = mount,
            Size = root.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(sourceRoot); root.Art(sourceRoot, 2, root.Rectangle(2), assets, shader);
        directory = new() { Name = "EventHelpDirectory", Size = sourceRoot.Size, MouseFilter = MouseFilterEnum.Pass };
        sourceRoot.AddChild(directory);
        // Mode one binds the event-editor category internally, then hides all
        // eight tabs and the normal gameplay hint selector.
        var selector = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "12_01_01_help_offset_03"));
        ConfigureAdviser(selector);
        if (catalog.Mode == 0) ConfigureCampaignDirectory(root, selector);
        else ConfigureEventDirectory(selector);
        detailSource = new(MapMarkerLayout.LoadNamed(repository, "12_01_01_help_offset_02"));
        detail = new() { Name = "EventHelpDetail", Size = detailSource.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Stop, Visible = false };
        sourceRoot.AddChild(detail);
        foreach (int node in new[] { 2, 3, 5, 6 })
            detailSource.Art(detail, node, detailSource.Rectangle(node), assets, shader);
        title = detailSource.Text(detail, 1, default, "", font);
        pageBody = new() { Name = "HelpSelectedPage", Size = detail.Size, MouseFilter = MouseFilterEnum.Ignore };
        detail.AddChild(pageBody);
        pager = new() { Name = "HelpPager", Size = detail.Size, MouseFilter = MouseFilterEnum.Pass };
        detail.AddChild(pager); detailSource.Art(pager, 9, detailSource.Rectangle(9), assets, shader);
        pageCounter = detailSource.Text(pager, 10, default, "", font);
        previous = Arrow("HelpPreviousPage", 11, false, () => ChangePage(-1));
        next = Arrow("HelpNextPage", 13, true, () => ChangePage(1));
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        back = new() { Name = "ReturnFromEventHelp", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = CanUse };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        SetMeta("source_layout", root.Source.Source.ResourceUri); SetMeta("source_help_mode", catalog.Mode);
        SetMeta("source_dictionary_category", catalog.Mode == 0 ? catalog.Categories[0].Id : 8); Resized += Fit; Fit();
    }
    private void ConfigureEventDirectory(UiSourceLayout selector)
    {
        Rect2 list = selector.Rectangle(6);
        list.Position -= new Vector2(108, 0); list.Size += new Vector2(108, 0);
        var clip = new Control { Name = "HelpTopicRows", Position = list.Position, Size = list.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        directory.AddChild(clip);
        for (int index = 0; index < catalog.Topics.Count; index++)
        {
            var entry = catalog.Topics[index];
            var row = new EventEditorHelpTopicButton { Name = "EventHelpTopic" + entry.Id,
                Position = new(0, index * 50), InputAdmission = () => CanUse() && topic is null };
            clip.AddChild(row); row.Configure(repository, assets, shader, font, entry.Title, () => OpenTopic(entry));
            row.SetMeta("help_topic_id", entry.Id); rows.Add(row);
        }
        var scrollBox = selector.Rectangle(35);
        var scroll = new UiSourceScrollbar { Name = "HelpTopicScroll", Position = scrollBox.Position, Size = scrollBox.Size };
        directory.AddChild(scroll); scroll.ConfigureNoRange(repository, assets, shader);
    }

    private EventEditorHelpArrow Arrow(string name, int node, bool right, Action pressed)
    {
        var box = detailSource.Rectangle(node);
        var button = new EventEditorHelpArrow { Name = name, Position = box.Position,
            InputAdmission = () => CanUse() && topic is not null };
        pager.AddChild(button); button.Configure(repository, assets, shader, right, pressed); return button;
    }
    private void OpenTopic(EventEditorHelpTopic entry)
    {
        if (!CanUse() || topic is not null || entry.Pages.Length == 0) return;
        campaignScrollbar?.CancelDrag();
        topic = entry; page = 0; directory.Hide(); detail.Show(); title.Text = entry.Title;
        ApplyPage();
    }
    private void ChangePage(int delta)
    {
        if (!CanUse() || topic is null) return;
        int selected = page + delta;
        if ((uint)selected >= (uint)topic.Pages.Length) return;
        page = selected; ApplyPage();
    }
    private void ApplyPage()
    {
        if (topic is null) return;
        foreach (var child in pageBody.GetChildren()) { pageBody.RemoveChild(child); child.QueueFree(); }
        var selected = topic.Pages[page];
        if (selected.Image is { } image)
            detailSource.Art(pageBody, 4, detailSource.Rectangle(4), assets, shader,
                commonTexturePack: image.TexturePack, commonSpriteOverride: image.SpriteName);
        AddParagraph(pageBody, detailSource, 7, selected.Text, "HelpBody");
        pageCounter.Text = (page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" +
            topic.Pages.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        pager.Visible = topic.Pages.Length > 0;
        previous.Disabled = page == 0; next.Disabled = page == topic.Pages.Length - 1;
        SetMeta("help_topic_id", topic.Id); SetMeta("help_message_id", selected.MessageId);
        SetMeta("help_page_index", page); SetMeta("help_image_id", selected.ImageId);
    }
    private void AddParagraph(Control parent, UiSourceLayout source, int node, string value, string name)
    {
        var box = source.Rectangle(node);
        var clip = new Control { Name = name, Position = box.Position, Size = box.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Ignore }; parent.AddChild(clip);
        var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri) with
            { WidthFitMode = 0, RawCharacterMode = 0 };
        string wrapped = DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(value,
            line => (int)font.Measure(line, profile).Width, (int)box.Size.X, 128);
        float advance = font.Source.Record('\u3042', profile.Face).Height * profile.ScaleY + profile.VerticalSpace;
        var lines = DeploymentNarrationParagraphPlan.Runs(wrapped);
        var presets = MapTextPresets.Create();
        for (int index = 0; index < lines.Count; index++)
        {
            var line = new UiSourceText { Name = name + "Line" + index,
                Position = new(0, index * advance), Size = new(box.Size.X, advance) };
            clip.AddChild(line); line.Configure(font, profile);
            line.SetRuns(lines[index].Runs.Select(run => new UiSourceTextRun(run.Text, run.Colour switch
            {
                NarrationBodyColour.TextPreset38Body => presets[38].LT.CpuPacked,
                NarrationBodyColour.TextPreset39Body => presets[39].LT.CpuPacked,
                NarrationBodyColour.TextPreset40Body => presets[40].LT.CpuPacked,
                _ => (uint?)null
            })).ToArray());
        }
    }
    private bool CanUse() => !abandoned && IsInsideTree() && IsVisibleInTree() && admitted?.Invoke() == true;
    internal override void RegisterHeader(UiSourceHeaderStack owner) => header?.Publish(owner);
    internal override void RequestClose() => RequestBack();
    internal void RequestBack()
    {
        if (!CanUse()) return;
        if (topic is not null) { topic = null; detail.Hide(); directory.Show(); return; }
        returned?.Invoke();
    }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; returned = null;
        AbandonCampaignDirectory();
        foreach (var row in rows) row.Abandon();
        if (back is not null) { back.InputAdmission = null; back.Disabled = true; }
        if (previous is not null) previous.Abandon(); if (next is not null) next.Abandon(); Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); base._ExitTree();
    }
}

internal partial class EventEditorHelpArrow : UiSourceButton
{
    private Rect2 hit;
    private Action? selected;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        bool right, Action selected)
    {
        this.selected = selected;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,
            right ? "cmn_btn_pad_win_r_arrow" : "cmn_btn_pad_win_l_arrow"));
        Size = source.Rectangle(0).Size; hit = source.Rectangle(1); ActionMode = ActionModeEnum.Release;
        int[][] art = [[3], [5, 6], [8, 9], [11], [13, 14]];
        var states = new Control[5];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "ArrowState" + state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group); states[state] = group;
            foreach (int node in art[state]) source.Art(group, node, source.Rectangle(node), assets, shader);
        }
        ConfigureStates(states); Pressed += () => { if (!Disabled && SourceInputAdmitted) this.selected?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Abandon() { selected = null; InputAdmission = null; Disabled = true; }
}
