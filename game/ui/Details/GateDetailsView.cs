using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary gate detail dialog, rendered from one retained current-world publication.</summary>
internal partial class GateDetailsView : DetailsView
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private MapInformationListView list = null!;
    private Control? body;
    private Action? close;
    private Action? officers;
    private UiSourceCommandButton? officersButton;
    private bool abandoned;
    private IReadOnlyList<(InformationDetailSelectionItem Item, int Slot)> candidateOrder = [];
    private int selectedSource = -1;
    internal int CurrentGateId { get; private set; }
    internal InformationDetailSelectionReturn ReadSelectionReturn() =>
        new(candidateOrder.Select(row => row.Item.Id).ToArray(), CurrentGateId, list.NameSortAscending);

    internal void Configure(GameResourceRepository repository, PreviewText text,
        GateInformationPage publication, Action close, Action? officers = null,
        InformationDetailSelectionContext? selection = null, Action<int>? select = null)
    {
        this.repository = repository; this.text = text; this.close = close; this.officers = officers;
        Name = "GateDetails"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        list = new() { Name = "GateDetailSelection" }; AddChild(list);
        var items = selection?.Items ?? [new InformationDetailSelectionItem(publication.GateId, publication.Name, null)];
        candidateOrder = items.Select((item, slot) => (Item: item, Slot: slot)).ToArray();
        bool sortable = selection is not null && items.All(item => item.SortUnresolvedOwner is null);
        list.Configure(repository, text, Message(GateInformationSources.TitleId),
            [new("name", Message(0x41D), 104, sortable)], Rows(),
            ReadAdmission, RequestBack, source =>
            {
                if (!ReadAdmission() || select is null) return;
                selectedSource = source; select(items[source].Id);
            }, selection?.ScrollSpeed ?? (() => 0),
            (key, ascending) =>
            {
                var comparer = Comparer<InformationDetailSelectionItem>.Create((left,right) => ascending
                    ? InformationDetailSelectionSorting.Compare(InformationListKind.Gates,left,right)
                    : InformationDetailSelectionSorting.Compare(InformationListKind.Gates,right,left));
                candidateOrder = candidateOrder.OrderBy(row => row.Item, comparer).ToArray();
                return Rows();
            }, tabCaptions: new[] { Message(GateInformationSources.BasicTabCaptionId) },
            completeColumnProfile: true, normalColumnLayout: true, narrowTable: true,
            initialSortColumn: selection?.InitialNameSortAscending is bool ? "name" : null,
            initialSortAscending: selection?.InitialNameSortAscending ?? true);
        IReadOnlyList<MapInformationListRow> Rows() => candidateOrder.Select(row =>
            new MapInformationListRow(row.Slot,[row.Item.Caption ?? text.Get("detail_unknown")],select is not null)).ToArray();
        Apply(publication);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        string caption = Message(GateInformationSources.OfficersCaptionId);
        officersButton = new() { Name = "GateResidentOfficers",
            Position = footer.Rectangle(21, new(0, 904)).Position + new Vector2(45, -103),
            Size = new(Math.Max(96, caption.Length * 28 + 20), 48),
            InputAdmission = ReadAdmission, Disabled = officers is null };
        list.SourceCanvas.AddChild(officersButton);
        officersButton.Configure(repository, list.SourceAssets, list.SourceShader, list.SourceFont,
            caption, () => { if (ReadAdmission()) this.officers?.Invoke(); });
    }

    internal override void RegisterHeader(UiSourceHeaderStack stack) => list.RegisterHeader(stack);

    internal void Apply(GateInformationPage page)
    {
        if (abandoned) return;
        CurrentGateId = page.GateId;
        if (selectedSource < 0 || candidateOrder.All(row => row.Slot != selectedSource || row.Item.Id != page.GateId))
            selectedSource = candidateOrder.First(row => row.Item.Id == page.GateId).Slot;
        list.SelectSourceIndex(selectedSource);
        if (body is { } previous) { previous.Hide(); previous.QueueFree(); }
        body = new() { Name = "CurrentGateDetails", Position = new(191, 197), Size = new(1540, 688),
            MouseFilter = MouseFilterEnum.Ignore };
        list.SourceCanvas.AddChild(body);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, page.LayoutName));
        var presets = MapTextPresets.Create();
        var palettes = MapSpritePalettes.Create();

        if (!Hidden(2) && page.StandingPortrait is { } route)
        {
            var sprites = new LocalizedUiSpriteSource(repository);
            var portrait = new UiSourceStandingPortrait { Name = "GateGovernorStanding", Position = source.Rectangle(2).Position };
            body.AddChild(portrait);
            portrait.Configure(repository, list.SourceAssets, list.SourceShader, route.LoadSprites(sprites.LoadTexturePackSprite));
            portrait.Scale = source.Rectangle(2).Size / portrait.Size;
        }
        foreach (var node in source.Nodes)
        {
            if (Hidden(node.Index) || Replaced(node.Index)) continue;
            if (node.Type is 1 or 3)
                source.Art(body, node.Index, source.Rectangle(node.Index), list.SourceAssets, list.SourceShader,
                    page.TextColors.TryGetValue(node.Index, out var colors) ? colors : null);
            else if (node.Type == 6) source.Blur(body, node.Index, source.Rectangle(node.Index));
        }
        foreach (var (node, message) in GateInformationSources.Captions)
            if (!Hidden(node)) Label(node, Message(message), node is 61 or 68 ? 0 : 13);
        var captionNodes = GateInformationSources.Captions.Select(binding => binding.Node).ToHashSet();
        foreach (var (node, value) in page.Text)
            if (!captionNodes.Contains(node) && !Hidden(node) && !Replaced(node)) Label(node, value, 0);
        foreach (var (node, missing) in page.UnresolvedFields)
            if ((uint)node < (uint)source.Nodes.Count && source.Nodes[node].Type == 2 &&
                !page.Text.ContainsKey(node) && !captionNodes.Contains(node) && !Hidden(node) && !Replaced(node))
                Label(node, text.Get("detail_unknown"), 0);

        if (!Hidden(7) && page.ForceFlag is { } flag)
        {
            var control = new UiSourceForceFlag { Name = "GateForceFlag", Position = source.Rectangle(7).Position };
            body.AddChild(control);
            control.Configure(repository, list.SourceAssets, list.SourceShader, list.SourceFont, flag);
            control.Scale = source.Rectangle(7).Size / control.Size;
        }
        if (!Hidden(17) && page.DistrictNumber is int number)
            source.Art(body, 17, source.Rectangle(17), list.SourceAssets, list.SourceShader,
                commonSpriteOverride: ArmyMapUiSources.Create().IconName(ArmyMapUiSources.DistrictIcon(checked((byte)number))));
        if (!Hidden(25) && page.NearbyEnemyPanelVisible == true) NearbyEnemy(source.Rectangle(25));
        foreach (var gauge in page.Gauges)
            if (!Hidden(gauge.Node)) Gauge(source.Rectangle(gauge.Node), gauge);
        if (!Hidden(87))
        {
            var miniMap = new UiSourceMiniMap { Name = "GateMiniMap", Position = source.Rectangle(87).Position };
            body.AddChild(miniMap);
            miniMap.Configure(repository, list.SourceAssets, list.SourceShader, page.MiniMap);
        }
        if (!Hidden(86) && page.Tactic is { } tactic)
        {
            var box = source.Rectangle(86);
            var marker = new ArmyDetailsTacticMarker { Name = "GateTactic", Position = box.Position, Size = box.Size };
            body.AddChild(marker);
            marker.Configure(repository, list.SourceAssets, list.SourceShader, list.SourceFont,
                tactic.Name, tactic.IconSpriteId == 0x222, () => { }, () => { });
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("gate_id", page.GateId);
        SetMeta("source_unknown", page.UnresolvedOwner ?? "");

        bool Hidden(int index)
        {
            for (int? cursor = index; cursor is int current; cursor = source.Nodes[current].ParentIndex)
                if (page.HiddenNodes.Contains(current)) return true;
            return false;
        }
        bool Replaced(int index)
        {
            for (int? cursor = index; cursor is int current; cursor = source.Nodes[current].ParentIndex)
                if (current is 2 or 7 or 17 or 25 or 35 or 40 or 86 or 87) return true;
            return false;
        }
        void Label(int node, string value, int preset)
        {
            if (page.TextStyles.TryGetValue(node, out int currentPreset)) preset = currentPreset;
            var label = source.Text(body, node, default, value, list.SourceFont, presets[preset]);
            var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri).WithPreset(presets[preset]);
            if (page.TextColors.TryGetValue(node, out var colors))
                profile = profile with { ColourLT = colors.LT.CpuPacked, ColourRT = colors.RT.CpuPacked,
                    ColourLB = colors.LB.CpuPacked, ColourRB = colors.RB.CpuPacked };
            if (node == 13 && page.ForceNameColor is { } forceColor)
                profile = profile with { ColourLT = forceColor.CpuPacked, ColourRT = forceColor.CpuPacked,
                    ColourLB = forceColor.CpuPacked, ColourRB = forceColor.CpuPacked };
            label.Configure(list.SourceFont, profile); label.SetKnownColourLine(value);
            if (page.UnresolvedFields.TryGetValue(node, out var missing)) label.SetMeta("unresolved_owner", missing);
        }
        void Gauge(Rect2 box, GateInformationGauge values)
        {
            var bar = new UiSourceLayout(repository, 0xAFC125D8);
            var control = new Control { Name = "GateGauge" + values.Node, Position = box.Position,
                Size = bar.Rectangle(0).Size, Scale = box.Size / bar.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(control);
            bar.Art(control, 1, bar.Rectangle(1), list.SourceAssets, list.SourceShader);
            if (values.Maximum <= 0) return;
            int width = (int)((long)bar.Nodes[1].Value44 * Math.Clamp(values.Current, 0, values.Maximum) / values.Maximum);
            if (width <= 0) return;
            var fill = new UiSourceLayout(bar.Source, new Dictionary<int, UiSourceNodeGeometry>
                { [2] = new(Size: new(width, bar.Nodes[2].Value46)) });
            fill.Art(control, 2, fill.Rectangle(2), list.SourceAssets, list.SourceShader, palettes[values.Style]);
        }
        void NearbyEnemy(Rect2 box)
        {
            // cmn_mrker_status is the parent's placeholder name; kind13 loads cmn_mrker.
            var marker = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_mrker"));
            var control = new Control { Name = "GateNearbyEnemy", Position = box.Position,
                Size = marker.Rectangle(0).Size, Scale = box.Size / marker.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(control);
            marker.Art(control, 2, marker.Rectangle(2), list.SourceAssets, list.SourceShader,
                commonSpriteOverride: "000_cmn_mrk_base_red");
            marker.Text(control, 3, default, Message(GateInformationSources.NearbyEnemyCaptionId), list.SourceFont);
        }
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing gate detail Data1/{id:X}.");
    private bool ReadAdmission() => !abandoned && IsVisibleInTree();
    internal void RequestBack() { if (ReadAdmission()) close?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; close = null; officers = null; officersButton?.Abandon(); list?.Abandon(); Hide();
    }
    internal override void RequestClose() => RequestBack();
    public override void _ExitTree() { Abandon(); base._ExitTree(); }
}
