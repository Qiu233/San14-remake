using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record DoctrineResearchPageValues(IReadOnlyList<DoctrineResearchRow> Rows,
    IReadOnlyList<DoctrineResearchPoint> Points, DoctrineResearchBudget? Budget, DoctrineResearchRow? Selected,
    IReadOnlyDictionary<int, string> RulerText, bool CanConfirm, bool CanRedefine, bool CanAutoResearch, string ActionCaption, string Guide);

/// <summary>The original doctrine graph and its detail panes. All values are
/// captured by the page draft; this control never queries or changes a world.</summary>
internal partial class DoctrineResearchView : Control
{
    private static readonly IReadOnlySet<int> NoHiddenNodes = new HashSet<int>();
    private AdministrationPageChrome chrome = null!;
    private Control content = null!, detail = null!, cursor = null!, portraitMount = null!, secondCostIcon = null!;
    private readonly Dictionary<int, DoctrineResearchTileButton> tiles = [];
    private readonly Dictionary<int, UiSourceText> detailText = [], rulerText = [], budgetText = [], pointText = [];
    private readonly List<(int Panel, Control View)> chains = [];
    private UiSourceParagraph description = null!;
    private UiSourceCommandButton research = null!;
    private UiSourceBottomButton effects = null!, redefine = null!, automatic = null!;
    private UiSourceLayout shell = null!, graph = null!;
    private Vector2 graphMount;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? selected;
    private DoctrineResearchPageValues? publication;
    private bool busy, abandoned;
    private GameResourceRepository repository = null!;
    private string noCost = "";
    internal DoctrineResearchPageValues? CurrentPublication => publication;
    internal Func<int, bool>? PanelAdmission { get; set; }
    internal Func<int, bool>? ResearchAdmission { get; set; }
    internal Func<bool>? DecisionAdmission { get; set; }
    internal Func<bool>? AuxiliaryAdmission { get; set; }
    internal Func<bool>? ReturnAdmission { get; set; }

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        Action returned, Action accepted, Action<int> selected, Action research,
        Action redefine, Action automatic, Action effects, IReadOnlyList<LoadedSprite> standingLayers)
    {
        this.repository = repository; this.admitted = admitted; this.returned = returned; this.selected = selected;
        Name = "DoctrineResearch"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0xA3, RequestBack, () =>
        { if (ReadAdmission() && publication?.CanConfirm == true && (DecisionAdmission?.Invoke() ?? true)) accepted(); });
        content = new() { Name = "DoctrineResearchContent", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(content); chrome.Canvas.MoveChild(content, 0);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing doctrine page caption.");
        noCost = Message(0x353);
        shell = new(repository, 0x2A8FF2AB);
        DrawArt(shell, content, default, NoHiddenNodes);
        graph = new(repository, 0x83A79350); graphMount = shell.Rectangle(4).Position;
        DrawArt(graph, content, graphMount, NoHiddenNodes);
        for (int index = 0; index < 6; index++)
        {
            var transform = graph.NodeTransform(89 + index, graphMount);
            var chain = new Control { Position = transform.Origin, Rotation = transform.Rotation,
                Scale = transform.Scale, MouseFilter = MouseFilterEnum.Ignore };
            content.AddChild(chain);
            var source = new UiSourceLayout(repository, 0x292E43CE);
            DrawArt(source, chain, default, NoHiddenNodes);
            chains.Add((DoctrineResearchGraphSlots.ChainPanels[index], chain));
        }
        var points = new UiSourceLayout(repository, 0xA508DFAD); Vector2 pointMount = shell.Rectangle(5).Position;
        DrawArt(points, content, pointMount, new HashSet<int> { 6, 25, 27, 29, 31, 33, 35, 37, 40, 43, 46, 49, 52 });
        var circle = LcskAnimationReader.Read(points.Source.Layout, new HashSet<string> { "Loop_circle" }).Single();
        var rotation = circle.Sample(circle.StartFrame).Single(value => value.NodeId == 6 && value.Channel == 0x2D);
        points.AlphaMaskedArt(content, 6, points.Rectangle(6, pointMount), chrome.Assets, chrome.SpriteShader,
            maskRotationDegrees: (float)rotation.Value);
        int[] pointCaptions = [8, 11, 14, 17, 20, 23], captionIds = [0x831, 0x832, 0x833, 0x834, 0x82F, 0x830];
        for (int index = 0; index < 6; index++) Text(points, content, pointCaptions[index], pointMount, Message(captionIds[index]));
        int[] pointNodes = [21, 9, 18, 15, 12, 24];
        for (int index = 0; index < 6; index++) pointText.Add(index + 1, Text(points, content, pointNodes[index], pointMount, ""));

        var left = new UiSourceLayout(repository, 0xE4508B35); Vector2 leftMount = shell.Rectangle(7).Position;
        portraitMount = new() { Position = left.Rectangle(1, leftMount).Position, MouseFilter = MouseFilterEnum.Ignore };
        content.AddChild(portraitMount);
        if (standingLayers.Count != 0)
        {
            var portrait = new UiSourceStandingPortrait(); portraitMount.AddChild(portrait);
            portrait.Configure(repository, chrome.Assets, chrome.SpriteShader, standingLayers);
        }
        DrawArt(left, content, leftMount, new HashSet<int> { 1, 21 });
        Text(left, content, 9, leftMount, Message(0x244)); Text(left, content, 13, leftMount, Message(0x21B));
        Text(left, content, 16, leftMount, Message(0x829)); Text(left, content, 19, leftMount, Message(0x60C));
        foreach (int node in new[] { 6, 10, 14, 18, 20 }) rulerText[node] = Text(left, content, node, leftMount, "");

        var budget = new UiSourceLayout(repository, 0x78994BD5); Vector2 budgetMount = shell.Rectangle(3).Position;
        DrawArt(budget, content, budgetMount, NoHiddenNodes);
        Text(budget, content, 4, budgetMount, Message(0x292)); Text(budget, content, 9, budgetMount, Message(0x20C));
        foreach (int node in new[] { 5, 7, 8, 10, 12, 13 }) budgetText[node] = Text(budget, content, node, budgetMount, "");

        var selectedDetail = new UiSourceLayout(repository, 0x75878F3D);
        detail = new() { Name = "DoctrineResearchDetail", Position = shell.Rectangle(6).Position,
            Size = selectedDetail.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        content.AddChild(detail);
        DrawArt(selectedDetail, detail, default, new HashSet<int> { 18, 22, 28, 31 });
        secondCostIcon = selectedDetail.Art(detail, 18, selectedDetail.Rectangle(18), chrome.Assets, chrome.SpriteShader);
        Text(selectedDetail, detail, 13, default, Message(0x82A));
        foreach (int node in new[] { 9, 15, 16, 17, 19, 20, 21 }) detailText[node] = Text(selectedDetail, detail, node, default, "");
        var bodyBox = selectedDetail.Rectangle(10);
        description = new() { Name = "DoctrineResearchDescription", Position = bodyBox.Position, Size = bodyBox.Size };
        detail.AddChild(description);
        var bodyProfile = MapFontProfile.FromNode(selectedDetail.Nodes[10], selectedDetail.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[1]) with { RawAlign = 0, RawLineArrange = 1, WidthFitMode = 3, RawCharacterMode = 2 };
        description.Configure(chrome.Font, bodyProfile);
        this.research = new() { Position = selectedDetail.Rectangle(22).Position,
            Size = selectedDetail.Rectangle(22).Size, InputAdmission = ReadAdmission };
        detail.AddChild(this.research);
        this.research.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0xA3),
            () => { if (ReadAdmission() && publication?.Selected is { CanResearch: true } current &&
                (ResearchAdmission?.Invoke(current.PanelId) ?? true)) research(); });

        cursor = new() { Size = new(128, 128), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        content.AddChild(cursor);
        var cursorSource = new UiSourceLayout(repository, 0x81A00B95); DrawArt(cursorSource, cursor, default, NoHiddenNodes);
        var footer = new UiSourceLayout(repository, 0xD8913AB2); Vector2 footerMount = new(0, 904);
        this.effects = Bottom(14, Message(0xA4), effects);
        this.redefine = Bottom(17, Message(0xA5), redefine);
        this.automatic = Bottom(18, Message(0xA6), automatic);
        chrome.ApplyDecision(false, true); chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));

        UiSourceBottomButton Bottom(int node, string caption, Action action)
        {
            var button = new UiSourceBottomButton { Position = footer.Rectangle(node, footerMount).Position, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, () => { if (ReadAdmission()) action(); });
            return button;
        }
    }

    internal void Apply(DoctrineResearchPageValues value)
    {
        if (abandoned) return;
        publication = value;
        foreach (var row in value.Rows)
        {
            if ((uint)row.LayoutSlot >= (uint)DoctrineResearchGraphSlots.Buttons.Length)
                throw new InvalidDataException("Doctrine layout slot is outside its authored graph.");
            int node = DoctrineResearchGraphSlots.Buttons[row.LayoutSlot];
            if (node < 0) continue;
            if (!tiles.TryGetValue(row.PanelId, out var tile))
            {
                tile = new() { Position = graph.Rectangle(node, graphMount).Position, InputAdmission = ReadAdmission };
                content.AddChild(tile); tile.Configure(repository, chrome.Assets, chrome.SpriteShader, row);
                int panelId = row.PanelId;
                tile.Pressed += () => { if (ReadAdmission() && (PanelAdmission?.Invoke(panelId) ?? true)) selected?.Invoke(panelId); };
                tiles.Add(panelId, tile);
            }
            tile.Apply(row); tile.Disabled = busy || !(PanelAdmission?.Invoke(row.PanelId) ?? true);
        }
        foreach (var point in value.Points)
            if (pointText.TryGetValue(point.DoctrineId, out var field))
                field.SetKnownColourLine(point.After == point.Before ? point.After.ToString() : point.After > point.Before
                    ? point.After + "(^02+" + (point.After - point.Before) + "^00)"
                    : point.After + "(^03-" + (point.Before - point.After) + "^00)");
        foreach (var pair in rulerText) pair.Value.Text = value.RulerText.GetValueOrDefault(pair.Key, "");
        foreach (var chain in chains) chain.View.Visible = value.Rows.Any(row => row.PanelId == chain.Panel && !row.Researched);
        if (value.Budget is { } budget)
        {
            budgetText[5].Text = budget.BooksBefore.ToString(); budgetText[7].Text = budget.BooksAfter.ToString();
            budgetText[8].SetKnownColourLine(budget.BookCost == 0 ? "" : "^03" + -budget.BookCost);
            budgetText[10].Text = budget.GoldBefore.ToString(); budgetText[12].Text = budget.GoldAfter.ToString();
            budgetText[13].SetKnownColourLine(budget.GoldCost == 0 ? "" : "^03" + -budget.GoldCost);
        }
        var current = value.Selected; detail.Visible = current is not null;
        if (current is not null)
        {
            detailText[9].Text = current.Name; description.Apply(current.Description);
            secondCostIcon.Visible = current.Costs.Count > (current.FirstDoctrineId == 0 ? 0 : 1);
            for (int index = 0; index < 2; index++)
            {
                int caption = index == 0 ? 15 : 19, main = index == 0 ? 16 : 20, delta = index == 0 ? 17 : 21;
                var cost = index == 0 ? current.FirstDoctrineId == 0 ? null : current.Costs.FirstOrDefault()
                    : current.Costs.ElementAtOrDefault(current.FirstDoctrineId == 0 ? 0 : 1);
                detailText[caption].Text = cost?.Name ?? (index == 0 ? noCost : "");
                detailText[main].Text = cost is not null && cost.Amount != cost.OriginalAmount ? cost.Amount.ToString() : "";
                detailText[delta].SetKnownColourLine(cost is null ? index == 0 ? noCost : "" : cost.Amount != cost.OriginalAmount
                    ? "(^03" + (cost.Amount - cost.OriginalAmount) + "^00)" : cost.Amount.ToString());
            }
            research.Disabled = busy || current.CanResearch != true || !(ResearchAdmission?.Invoke(current.PanelId) ?? true);
            research.Caption = value.ActionCaption;
            if (tiles.TryGetValue(current.PanelId, out var tile))
            { cursor.Position = tile.Position; cursor.Show(); content.MoveChild(cursor, content.GetChildCount() - 1); }
        }
        else cursor.Hide();
        chrome.ApplyDecision(!busy && value.CanConfirm && (DecisionAdmission?.Invoke() ?? true), true);
        chrome.ApplyReturn(!busy && (ReturnAdmission?.Invoke() ?? true));
        bool auxiliaryAllowed = AuxiliaryAdmission?.Invoke() ?? true;
        redefine.Disabled = busy || !value.CanRedefine || !auxiliaryAllowed;
        automatic.Disabled = busy || !value.CanAutoResearch || !auxiliaryAllowed; effects.Disabled = busy || !auxiliaryAllowed;
        chrome.ApplyDefaultHint(value.Guide);
    }

    internal void SetBusy(bool value) { busy = value; if (publication is { } current) Apply(current); }
    internal void RefreshTutorialAdmission() { if (publication is { } current) Apply(current); }
    internal void RequestBack() { if (ReadAdmission() && (ReturnAdmission?.Invoke() ?? true)) returned?.Invoke(); }
    internal void Abandon()
    { abandoned = true; admitted = null; returned = null; selected = null; publication = null;
        PanelAdmission = ResearchAdmission = null; DecisionAdmission = AuxiliaryAdmission = ReturnAdmission = null; chrome.Abandon(); Hide(); }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);

    private UiSourceText Text(UiSourceLayout layout, Control parent, int node, Vector2 mount, string value)
    {
        var name = layout.Nodes[node].Name; int at = name.LastIndexOf("_fc", StringComparison.Ordinal);
        MapTextPreset? preset = at >= 0 && name.Length >= at + 6 && int.TryParse(name.AsSpan(at + 3, 3), out int index)
            ? MapTextPresets.Create()[index] : null;
        return layout.Text(parent, node, mount, value, chrome.Font, preset);
    }

    private void DrawArt(UiSourceLayout layout, Control parent, Vector2 mount, IReadOnlySet<int> hidden)
    {
        foreach (var node in layout.Nodes)
        {
            bool skip = false;
            for (int? at = node.Index; at is int index; at = layout.Nodes[index].ParentIndex)
                if (hidden.Contains(index)) { skip = true; break; }
            if (!skip && node.Type is 1 or 3)
                layout.Art(parent, node.Index, layout.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
        }
    }
}
