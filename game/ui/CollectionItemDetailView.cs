using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The item encyclopedia's own detail body and retained name selector.</summary>
internal partial class CollectionItemDetailView : Control
{
    private MapInformationListView selector = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionItemCatalog catalog, CollectionItemDetail item,
        IReadOnlyList<CollectionDetailEntry> entries, Func<bool> admitted, Action returned,
        Action<int> select, Func<int> scrollSpeed, Action openPoem, AdministrationPageChrome.Resources? resources = null)
    {
        this.admitted = admitted; this.returned = returned;
        Name = "CollectionItemDetail"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var nameColumn = catalog.Tabs.SelectMany(tab => tab.Columns).First(column => column.Id == 0);
        string basic = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[0x3CD].Text
            ?? throw new InvalidDataException("Collection basic tab caption is missing.");
        selector = new() { Name = "CollectionItemSelector" }; AddChild(selector);
        selector.Configure(repository, text, catalog.Title,
            [new("name", nameColumn.Caption, nameColumn.Width, false)],
            entries.Select(row => new MapInformationListRow(row.Id, [row.Name], true)).ToArray(),
            ReadAdmission, RequestBack, select, scrollSpeed, tabCaptions: [basic],
            completeColumnProfile: true, normalColumnLayout: true, narrowTable: true, resources: resources);
        selector.SelectSourceIndex(item.ItemId);
        var body = new Control { Name = "CollectionItemBody", Position = new(191, 197), Size = new(1540, 688),
            MouseFilter = MouseFilterEnum.Ignore };
        selector.SourceCanvas.AddChild(body);
        var source = selector.SourceAssets.SourceLayout("11_01_08_00_collection_item_cn");
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
        {
            if (node.Index is 2 or 7) continue;
            source.Art(body, node.Index, source.Rectangle(node.Index), selector.SourceAssets, selector.SourceShader);
        }
        if (item.Image is { Index: > 0 } image)
            source.Art(body, 2, source.Rectangle(2), selector.SourceAssets, selector.SourceShader,
                commonTexturePack: image.TexturePack, commonSpriteOverride: image.SpriteName);
        var presets = MapTextPresets.Create();
        foreach (var (node, value) in item.DetailFields)
        {
            if (node == 42)
            {
                var rectangle = source.Rectangle(node);
                var paragraph = new UiSourceParagraph { Name = "CollectionItemDescription",
                    Position = rectangle.Position, Size = rectangle.Size };
                body.AddChild(paragraph);
                paragraph.Configure(selector.SourceFont,
                    MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
                        .WithPreset(presets[0]) with { RawAlign = 0, RawLineArrange = 1 });
                paragraph.Apply(value);
            }
            else source.Text(body, node, default, value, selector.SourceFont,
                presets[node is 16 or 21 or 26 or 31 or 36 or 41 ? 13 : 0]);
        }
        if (item.CanAppreciatePoem)
        {
            var footer = selector.SourceAssets.SourceLayout(0xD8913AB2);
            string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
                .Messages.Sections[1].Records[0xAE].Text
                ?? throw new InvalidDataException("Collection item poem caption is missing.");
            var poem = new UiSourceCommandButton { Name = "CollectionItemPoem",
                Position = footer.Rectangle(21, new(0, 904)).Position + new Vector2(45, -103),
                Size = new(96, 48), InputAdmission = ReadAdmission };
            selector.SourceCanvas.AddChild(poem);
            poem.Configure(repository, selector.SourceAssets, selector.SourceShader, selector.SourceFont,
                caption, () => { if (ReadAdmission()) openPoem(); });
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("collection_mode", true); SetMeta("item_id", item.ItemId);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null;
        selector?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
