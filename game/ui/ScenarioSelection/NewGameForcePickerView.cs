using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The startup full-force chooser consumes the page's existing roster.
/// This caller's selection mode accepts an ordinary row release directly.</summary>
internal partial class NewGameForcePickerView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Action<int?>? completed;
    private readonly List<UiSourceListRow> buttons = [];
    private bool abandoned, busy;
    internal NewGameForcePickerPreview Publication { get; private set; } = null!;
    internal IReadOnlyList<NewGameForcePreview> Rows { get; private set; } = [];
    internal int? SelectedForceId { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        NewGameForcePickerPreview publication, Action<int?> completed)
    {
        this.completed = completed; Publication = publication;
        Rows = publication.NativeSortedRows ?? publication.Rows;
        SelectedForceId = publication.InitialSelectedForceId;
        Name = "NewGameForcePicker"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new("#292d2c"), AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore });
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x36E, RequestBack, () => { }, returnOnly: true);
        chrome.ApplyDefaultHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2739));
        var source = new UiSourceLayout(repository, 0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(source.Nodes.Single(node => node.Type == 2),
            source.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing force-list caption.");
        string[] headings = [text.Get("force"), text.Get("detail_role_ruler"), Caption(0x21B),
            Caption(0x207), Caption(0x218), Caption(0x20C), Caption(0x20D), Caption(0x20E)];
        int[] widths = [150, 220, 180, 120, 120, 200, 210, 210];
        int left = 230;
        for (int column = 0; column < headings.Length; column++)
        { Label(chrome.Canvas, "Heading" + column, headings[column], new(left, 155), widths[column]); left += widths[column]; }
        var scroll = new ScrollContainer { Name = "ForcePickerScroll", Position = new(230, 210), Size = new(1420, 635),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        chrome.Canvas.AddChild(scroll);
        var body = new Control { Name = "ForcePickerRows", MouseFilter = MouseFilterEnum.Ignore };
        scroll.AddChild(body);
        float top = 0;
        for (int index = 0; index < Rows.Count; index++)
        {
            var row = Rows[index];
            var button = new UiSourceListRow { Name = "ForcePickerRow" + index, Position = new(0, top),
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release };
            body.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", 1410, false);
            button.Selected = row.ForceId == SelectedForceId;
            string Unknown(string? value) => value ?? text.Get("detail_unknown");
            string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
            string[] values = [Unknown(row.ForceDisplayName), Unknown(row.RulerName), Unknown(row.RankName),
                Number(row.CityCount), Number(row.ActiveOfficerCount), Number(row.Gold), Number(row.Food), Number(row.Troops)];
            int x = 0;
            for (int column = 0; column < values.Length; column++)
            { Label(button, "Column" + column, values[column], new(x + 8, 8), widths[column] - 8); x += widths[column]; }
            button.SetMeta("force_id", row.ForceId);
            button.Pressed += () =>
            {
                if (!ReadAdmission()) return;
                SelectedForceId = row.ForceId;
                this.completed?.Invoke(row.ForceId);
            };
            top += button.Size.Y;
        }
        body.CustomMinimumSize = new(1410, Math.Max(635, top));
        SetBusy(false);

        void Label(Control parent, string name, string value, Vector2 position, int width)
        {
            var label = new UiSourceText { Name = name, Position = position, Size = new(width - 12, 32),
                MouseFilter = MouseFilterEnum.Ignore };
            parent.AddChild(label); label.Configure(chrome.Font, profile); label.Text = value;
        }
    }

    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess();
    internal void RequestBack() { if (ReadAdmission()) completed?.Invoke(null); }
    internal void SetBusy(bool value)
    {
        busy = value; chrome?.ApplyReturn(!value);
    }
    internal void Abandon()
    {
        abandoned = true; completed = null;
        foreach (var button in buttons) { button.InputAdmission = null; button.Disabled = true; }
        chrome?.Abandon(); Hide();
    }
}
