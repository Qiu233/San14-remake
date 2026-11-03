using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CollectionDetailEntry(int Id, string Name);

/// <summary>The dedicated Collection bodies, with the original single-column
/// object selector. Catalog order is retained while changing the inspected object.</summary>
internal partial class CollectionDetailView : Control
{
    private MapInformationListView selector = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private bool abandoned;
    private Control? detailBody;
    private Action<CollectionSceneryDetail>? updateScenery;

    internal void Configure(GameResourceRepository repository, PreviewText text, CollectionCatalog? catalog,
        CollectionPoemDetail? poem, CollectionSceneryDetail? scenery,
        IReadOnlyList<CollectionDetailEntry> entries, Func<bool> admitted, Action returned,
        Action<int> select, Func<int> scrollSpeed, Action? appreciate = null, SceneryCatalogInformation? currentScenery = null,
        AdministrationPageChrome.Resources? resources = null)
    {
        if ((poem is null) == (scenery is null)) throw new ArgumentException("A Collection detail needs one object.");
        this.admitted = admitted; this.returned = returned;
        Name = poem is not null ? "CollectionPoemDetail" : "CollectionSceneryDetail";
        MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        int selected = poem?.ItemId ?? scenery!.SceneryId;
        var firstColumn = currentScenery is not null ? new CollectionColumn(0,currentScenery.NameCaption,188,false)
            : (poem is not null ? catalog!.PoemColumns : catalog!.SceneryColumns)[0];
        string basic = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[0x3CD].Text
            ?? throw new InvalidDataException("Collection basic tab caption is missing.");
        selector = new() { Name = "CollectionDetailSelector" }; AddChild(selector);
        selector.Configure(repository, text, currentScenery?.Title ?? (poem is not null ? catalog!.PoemsTitle : catalog!.SceneryTitle),
            [new("name", firstColumn.Caption, firstColumn.Width, false)],
            entries.Select(row => new MapInformationListRow(row.Id, [row.Name], true)).ToArray(),
            ReadAdmission, RequestBack, select, scrollSpeed, tabCaptions: [basic],
            completeColumnProfile: true, normalColumnLayout: true, narrowTable: true,
            returnNode: poem is not null ? 4 : 1, resources: resources);
        selector.SelectSourceIndex(selected);
        var canvas = selector.SourceCanvas;
        var assets = selector.SourceAssets;
        var shader = selector.SourceShader;
        var font = selector.SourceFont;
        RenderBody(poem,scenery);
        updateScenery = page =>
        {
            if(abandoned)return;
            if(detailBody is { } previous){previous.Hide();previous.QueueFree();}
            selector.SelectSourceIndex(page.SceneryId);
            RenderBody(null,page);
        };
        void RenderBody(CollectionPoemDetail? currentPoem,CollectionSceneryDetail? selectedScenery)
        {
            var body = new Control { Name = "CollectionDetailBody", Position = new(191, 197), Size = new(1540, 688),
                MouseFilter = MouseFilterEnum.Ignore };
            detailBody=body; canvas.AddChild(body);
            var source = assets.SourceLayout(currentPoem is not null
                ? "02_07_06_info_collection_sibun_cn" : "02_07_07_info_collection_meishou_cn");
            int imageNode = currentPoem is not null ? 2 : 5;
            var image = currentPoem?.Image ?? selectedScenery!.Image;
            foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
            {
                if (node.Index == imageNode || selectedScenery is not null && node.Index == 11) continue;
                source.Art(body, node.Index, source.Rectangle(node.Index), assets, shader);
            }
            if (image.Index > 0)
                source.Art(body, imageNode, source.Rectangle(imageNode), assets, shader,
                    commonTexturePack: image.TexturePack, commonSpriteOverride: image.SpriteName);
            var presets = MapTextPresets.Create();
            if (currentPoem is not null)
            {
                Text(9, currentPoem.Name); Text(11, currentPoem.Author); Text(12, currentPoem.Era);
                Text(16, (currentScenery?.DescriptionCaption ?? catalog!.DescriptionCaption), 13); Paragraph(17, currentPoem.Description);
                var footer = assets.SourceLayout(0xD8913AB2);
                var appreciation = new UiSourceRoundButton { Name = "CollectionPoemAppreciation",
                    Position = footer.Rectangle(5, new(0, 904)).Position, Disabled = appreciate is null, InputAdmission = ReadAdmission };
                canvas.AddChild(appreciation);
                appreciation.ConfigureCollectionAppreciation(repository, assets, shader, text.SourceLocale,
                    () => { if (ReadAdmission()) appreciate?.Invoke(); });
                SetMeta("item_id", currentPoem.ItemId);
            }
            else
            {
                Text(13, selectedScenery!.Name); Text(19, (currentScenery?.CityCaption ?? catalog!.CityCaption), 13); Text(20, selectedScenery.CityName);
                Text(24, (currentScenery?.AreaCaption ?? catalog!.AreaCaption), 13); Text(25, selectedScenery.AreaName);
                Text(30, (currentScenery?.DescriptionCaption ?? catalog!.DescriptionCaption), 13); Paragraph(31, selectedScenery.Description);
                var miniMap = new UiSourceMiniMap { Name = "CollectionSceneryMiniMap", Position = source.Rectangle(26).Position };
                body.AddChild(miniMap); miniMap.Configure(repository, assets, shader, selectedScenery.MiniMap);
                if (selectedScenery.MiniMapUnresolvedOwner is { } missing) miniMap.SetMeta("unresolved_owner", missing);
                SetMeta("scenery_id", selectedScenery.SceneryId);
            }
            SetMeta("source_layout", source.Source.Source.ResourceUri);
            SetMeta("collection_mode", currentScenery is null);

            void Text(int node, string value, int preset = 0) => source.Text(body, node, default, value, font, presets[preset]);
            void Paragraph(int node, string value)
            {
                Rect2 box = source.Rectangle(node);
                var paragraph = new UiSourceParagraph { Name = "CollectionDescription", Position = box.Position, Size = box.Size };
                body.AddChild(paragraph);
                var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
                    .WithPreset(presets[0]) with { RawAlign = 0, RawLineArrange = 1 };
                paragraph.Configure(font, profile); paragraph.Apply(value);
            }
        }
    }

    internal void ApplyScenery(CollectionSceneryDetail page)=>updateScenery?.Invoke(page);
    internal void RegisterHeader(UiSourceHeaderStack owner)=>selector.RegisterHeader(owner);

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; updateScenery=null;
        selector?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
