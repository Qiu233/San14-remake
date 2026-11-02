using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The gallery's two original person bodies, sharing the retained
/// catalog order and the source's name-only selector.</summary>
internal partial class CollectionOfficerDetailView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CollectionOfficerDetail publication = null!;
    private MapInformationListView selector = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? selected, actionSelected;
    private Func<int> scrollSpeed = null!;
    private CollectionOfficerCatalog catalog = null!;
    private IReadOnlyList<CollectionDetailEntry> entries = [];
    private readonly Dictionary<(string Pack, string Name), LoadedSprite> sprites = [];
    private UiSourceSimpleTooltip? tooltip;
    private CollectionTacticBalloonView? tacticBalloon;
    private bool abandoned;
    internal int SelectedPage { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionOfficerCatalog catalog, CollectionOfficerDetail publication,
        IReadOnlyList<CollectionDetailEntry> entries, Func<bool> admitted, Action returned,
        Action<int> selected, Func<int> scrollSpeed, Action<int> actionSelected, int selectedPage = 0)
    {
        this.repository = repository; this.text = text; this.catalog = catalog;
        this.publication = publication; this.entries = entries; this.admitted = admitted;
        this.returned = returned; this.selected = selected; this.scrollSpeed = scrollSpeed;
        this.actionSelected = actionSelected;
        Name = "CollectionOfficerDetail"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ShowPage(Math.Clamp(selectedPage, 0, publication.Pages.Count - 1));
        SetMeta("collection_mode", true); SetMeta("officer_id", publication.OfficerId);
        SetMeta("unresolved_owners", publication.UnresolvedOwners.ToArray());
    }

    private void ShowPage(int index)
    {
        if (selector is { } old) { old.Abandon(); old.Hide(); old.QueueFree(); }
        SelectedPage = index;
        var page = publication.Pages[index];
        var nameColumn = catalog.Tabs.SelectMany(tab => tab.Columns).First(column => column.Id == 182);
        selector = new() { Name = "CollectionOfficerSelector" }; AddChild(selector);
        selector.Configure(repository, text, catalog.Title,
            [new("name", nameColumn.Caption, 132, false)],
            entries.Select(row => new MapInformationListRow(row.Id, [row.Name], true)).ToArray(),
            ReadAdmission, RequestBack, id => { if (ReadAdmission()) selected?.Invoke(id); }, scrollSpeed,
            tabCaptions: publication.Pages.Select(value => value.Caption).ToArray(), selectedTab: index,
            selectTab: target => { if (target != SelectedPage && ReadAdmission()) ShowPage(target); },
            completeColumnProfile: true, normalColumnLayout: true, narrowTable: true);
        selector.SelectSourceIndex(publication.OfficerId);
        var body = new Control { Name = "CollectionOfficerBody", Position = new(191, 197),
            Size = new(1540, 688), MouseFilter = MouseFilterEnum.Ignore };
        selector.SourceCanvas.AddChild(body);
        BuildBody(body, page);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 mount = footer.Rectangle(21, new(0, 904)).Position + new Vector2(45, -103);
        foreach (var action in publication.Actions)
        {
            float width = Math.Max(96, action.Caption.Length * 28 + 20);
            var button = new UiSourceCommandButton { Name = "CollectionOfficerAction" + action.Index,
                Position = mount, Size = new(width, 48), InputAdmission = ReadAdmission, Disabled = !action.Enabled };
            selector.SourceCanvas.AddChild(button);
            button.Configure(repository, selector.SourceAssets, selector.SourceShader, selector.SourceFont,
                action.Caption, () => { if (ReadAdmission() && action.Enabled) actionSelected?.Invoke(action.Index); });
            if (action.UnresolvedOwner is { } unresolved) button.SetMeta("unresolved_owner", unresolved);
            mount.X += width;
        }
        tooltip = new(); selector.SourceCanvas.AddChild(tooltip);
        tooltip.Configure(repository, selector.SourceAssets, selector.SourceShader, selector.SourceFont);
        tacticBalloon = new(); selector.SourceCanvas.AddChild(tacticBalloon);
        tacticBalloon.Configure(repository, selector.SourceAssets, selector.SourceShader, selector.SourceFont);
        SetMeta("selected_page", index); SetMeta("source_layout", page.LayoutName);
    }

    private void BuildBody(Control body, CollectionOfficerDetailPage page)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, page.LayoutName));
        var hidden = page.HiddenNodes.ToHashSet();
        foreach (int? node in new[] { page.PortraitNodeIndex, page.NameplateNodeIndex, page.AbilityChartNodeIndex })
            if (node is int value) hidden.Add(value);
        foreach (var trait in page.Traits) hidden.Add(trait.NodeIndex);
        foreach (var tactic in page.Tactics) hidden.Add(tactic.NodeIndex);
        foreach (var formation in page.Formations) hidden.Add(formation.NodeIndex);
        foreach (var portrait in page.RelatedPortraits) hidden.Add(portrait.NodeIndex);
        if (page.PortraitNodeIndex is int portraitNode && publication.StandingPortrait is { } route)
        {
            var rectangle = source.Rectangle(portraitNode);
            var portrait = new UiSourceStandingPortrait { Position = rectangle.Position };
            body.AddChild(portrait);
            portrait.Configure(repository, selector.SourceAssets, selector.SourceShader,
                route.LoadSources(selector.SourceAssets.Portraits.RequireSource));
            portrait.Scale = rectangle.Size / portrait.Size;
        }
        Paint(source, body, hidden);
        var presets = MapTextPresets.Create();
        foreach (var (node, value) in page.DetailFields)
        {
            if (IsHidden(source, node, hidden)) continue;
            if (page.ParagraphNodes.Contains(node))
            {
                if (value.Length == 0) continue;
                var rectangle = source.Rectangle(node);
                var paragraph = new UiSourceParagraph { Position = rectangle.Position, Size = rectangle.Size };
                body.AddChild(paragraph);
                paragraph.Configure(selector.SourceFont,
                    MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
                        .WithPreset(presets[0]) with { RawAlign = 0, RawLineArrange = 1 });
                paragraph.Apply(value);
            }
            else source.Text(body, node, default, value, selector.SourceFont,
                presets[page.CaptionNodes.Contains(node) ? 13 : 0]);
        }
        if (page.NameplateNodeIndex is int nameplateNode) AddNameplate(body, source.Rectangle(nameplateNode));
        if (page.AbilityChartNodeIndex is int chartNode)
        {
            var rectangle = source.Rectangle(chartNode);
            var chart = new AreaAdministrationOfficerChart { Position = rectangle.Position };
            body.AddChild(chart);
            chart.Configure(repository, selector.SourceAssets, selector.SourceShader, selector.SourceFont, text);
            chart.Scale = rectangle.Size / chart.Size;
            chart.Apply(page.Abilities.Select(value => new AreaAdministrationAbilityValue(value.Value, value.Value)).ToArray());
        }
        foreach (var related in page.RelatedPortraits)
        {
            if (related.Portrait is not { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: { } pack, SpriteName: { } name }) continue;
            var rectangle = source.Rectangle(related.NodeIndex);
            var portrait = new UiSourcePortrait { Position = rectangle.Position, Size = rectangle.Size };
            body.AddChild(portrait); body.MoveChild(portrait, Math.Min(1, body.GetChildCount() - 1));
            portrait.Configure(repository, selector.SourceAssets, selector.SourceShader, Sprite(pack, name));
        }
        foreach (var trait in page.Traits)
        {
            var rectangle = source.Rectangle(trait.NodeIndex);
            var marker = new AreaAdministrationTraitMarker { Name = "CollectionOfficerTrait" + trait.Slot,
                Position = rectangle.Position };
            body.AddChild(marker);
            marker.Configure(repository, selector.SourceAssets, selector.SourceShader, selector.SourceFont, rectangle.Size);
            marker.Apply(trait.DefinitionId, trait.Name, trait.Style);
            if (trait.Description is { } description)
            {
                marker.MouseEntered += () =>
                { if (ReadAdmission()) { tacticBalloon?.Display(null); tooltip?.DisplayKnownColourText(description); } };
                marker.MouseExited += () => tooltip?.DisplayText(null);
            }
        }
        AddMilitary(body, source, page);
        body.SetMeta("unresolved_owners", page.UnresolvedOwners.ToArray());
    }

    private void AddNameplate(Control body, Rect2 rectangle)
    {
        var source = new UiSourceLayout(repository, 0x2E73723A);
        var nameplate = new Control { Name = "CollectionOfficerNameplate", Position = rectangle.Position,
            Size = rectangle.Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(nameplate); Paint(source, nameplate, new HashSet<int> { 4 });
        var name = source.Text(nameplate, 7, default, publication.Name, selector.SourceFont);
        name.Configure(selector.SourceFont,
            MapFontProfile.FromNode(source.Nodes[7], source.Source.Source.ResourceUri) with { WidthFitMode = 3 });
        source.Text(nameplate, 8, default, "", selector.SourceFont);
        source.Text(nameplate, 9, default, publication.CourtesyName, selector.SourceFont);
    }

    private LoadedSprite Sprite(string pack, string name)
    {
        if (!sprites.TryGetValue((pack, name), out var sprite))
            sprites.Add((pack, name), sprite = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(pack, name));
        return sprite;
    }
    private static bool IsHidden(UiSourceLayout source, int index, IReadOnlySet<int> hidden)
    {
        for (int? at = index; at is int node; at = source.Nodes[node].ParentIndex)
            if (hidden.Contains(node)) return true;
        return false;
    }
    private void Paint(UiSourceLayout source, Control body, IReadOnlySet<int> hidden)
    {
        foreach (var node in source.Nodes)
        {
            if (IsHidden(source, node.Index, hidden)) continue;
            if (node.Type is 1 or 3) source.Art(body, node.Index, source.Rectangle(node.Index), selector.SourceAssets, selector.SourceShader);
            else if (node.Type == 6) source.Blur(body, node.Index, source.Rectangle(node.Index));
        }
    }
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; selected = null; actionSelected = null;
        selector?.Abandon(); tooltip?.DisplayText(null); tacticBalloon?.Display(null); Hide();
    }
    public override void _Process(double delta)
    {
        if (tooltip?.IsVisibleInTree() == true) tooltip.UpdatePointer(selector.SourceCanvas.GetLocalMousePosition());
        if (tacticBalloon?.IsVisibleInTree() == true) tacticBalloon.UpdatePointer(selector.SourceCanvas.GetLocalMousePosition());
    }
    public override void _ExitTree() { Abandon(); sprites.Clear(); }
}
