using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Read-only qualified advantages from the current force preview.
/// The source order and duplicates remain intact. Native column geometry is
/// still pending; this page supplies the ordinary information-list navigation.</summary>
internal partial class NewGameGeographicAdvantagesView : Control
{
    private AdministrationPageChrome chrome = null!;
    internal NewGameForcePreview Publication { get; private set; } = null!;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        NewGameForcePreview publication, Action returned)
    {
        Publication = publication;
        Name = "NewGameGeographicAdvantages"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new("#292d2c"), AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore });
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x2E1, returned, () => { }, returnOnly: true);
        chrome.ApplyDefaultHint("");
        var source = new UiSourceLayout(repository, 0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(source.Nodes.Single(node => node.Type == 2),
            source.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string advantage = messages.Records[0x2E1].Text ?? throw new InvalidDataException("Missing advantage caption.");
        Text(chrome.Canvas, "PreviewForce", publication.RulerName ?? publication.ForceDisplayName ?? text.Get("detail_unknown"),
            new(260, 105), 1400);
        string[] headings = [text.Get("detail_province"), advantage,
            text.Get("new_game_geo_owned_cities"), text.Get("new_game_geo_required_cities")];
        int[] widths = [360, 560, 240, 240];
        int x = 260;
        for (int column = 0; column < headings.Length; column++)
        { Text(chrome.Canvas, "Heading" + column, headings[column], new(x, 180), widths[column]); x += widths[column]; }
        var rows = publication.GeographicAdvantages ?? throw new InvalidDataException("Missing advantage publication.");
        var scroll = new ScrollContainer { Name = "GeographicAdvantagesScroll", Position = new(260, 235),
            Size = new(1400, 600), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        chrome.Canvas.AddChild(scroll);
        var body = new Control { Name = "Rows", CustomMinimumSize = new(1400, Math.Max(600, rows.Count * 60)),
            MouseFilter = MouseFilterEnum.Ignore };
        scroll.AddChild(body);
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var band = new ColorRect { Position = new(0, index * 60), Size = new(1400, 56),
                Color = new(index % 2 == 0 ? "#353b38" : "#303532"), MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(band);
            string[] values = [row.ProvinceName ?? text.Get("detail_unknown"), row.AdvantageName ?? text.Get("detail_unknown"),
                row.OwnedCities.ToString(CultureInfo.InvariantCulture), row.RequiredCities.ToString(CultureInfo.InvariantCulture)];
            int left = 0;
            for (int column = 0; column < values.Length; column++)
            { Text(body, $"Row{index}Column{column}", values[column], new(left, index * 60 + 10), widths[column]); left += widths[column]; }
        }
        SetMeta("source_information_pane", 0x27); SetMeta("force_id", publication.ForceId);

        void Text(Control parent, string name, string value, Vector2 position, int width)
        {
            var label = new UiSourceText { Name = name, Position = position, Size = new(width - 16, 36),
                MouseFilter = MouseFilterEnum.Ignore };
            parent.AddChild(label); label.Configure(chrome.Font, profile); label.Text = value;
        }
    }

    internal void SetBusy(bool value) => chrome?.ApplyReturn(!value);
    internal void Abandon() { chrome?.Abandon(); Hide(); }
}
