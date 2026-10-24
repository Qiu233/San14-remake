using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The selected existing armies retain their own rows. Opening one
/// member does not change the map selection or recreate the list publication.</summary>
internal partial class ArmyDetailsListView : DetailsView
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private readonly List<UiSourceListRow> buttons = [];
    private Action<UnitSourceDetails>? inspect;
    private bool closed;
    internal UnitSourceDetailsList Publication { get; private set; } = null!;
    internal int? SelectedUnitId { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        UnitSourceDetailsList publication, Action<UnitSourceDetails> inspect, Action close)
    {
        Publication = publication; this.inspect = inspect;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new Control { Name = "UnitDetailsListCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var header = new UiSourceHeader { Name = "UnitDetailsListTitle" }; canvas.AddChild(header);
        ConfigureSourcePage("UnitSourceDetailsList", header, close);
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => data.Records[id].Text ?? throw new InvalidDataException("Missing army-list caption.");
        header.Configure(repository, assets, shader, font, Caption(0x462));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(canvas, 3, shell.Rectangle(3, new(191, 147)), assets, shader);
        // Selected row order and source counters are usable now; the original
        // list's complete column/sort profile remains a separate visual task.
        int[] captions = [0x211, 0x1FD, 0x22B, 0x20E, 0x226, 0x225, 0x20C, 0x20D];
        int[] widths = [240, 180, 180, 150, 140, 140, 170, 170];
        var narration = new UiSourceLayout(repository, 0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(narration.Nodes.Single(node => node.Type == 2),
            narration.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        float left = 230;
        for (int column = 0; column < captions.Length; column++)
        { Cell(canvas, "Heading" + column, Caption(captions[column]), new(left, 166), widths[column]); left += widths[column]; }
        var scroll = new ScrollContainer { Name = "UnitDetailsScroll", Position = new(230, 220), Size = new(1450, 610),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        canvas.AddChild(scroll);
        var body = new Control { Name = "UnitDetailsRows", MouseFilter = MouseFilterEnum.Ignore };
        scroll.AddChild(body);
        float top = 0;
        for (int index = 0; index < publication.Rows.Count; index++)
        {
            var row = publication.Rows[index];
            var button = new UiSourceListRow { Name = "UnitDetailsRow" + index, Position = new(0, top),
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release };
            body.AddChild(button); buttons.Add(button);
            button.Configure(repository, assets, shader, font, "", 1430, false);
            button.Disabled = row.Exists != true;
            button.SetMeta("unit_id", row.UnitId);
            string Unknown(string? value) => value ?? text.Get("detail_unknown");
            string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
            string Cargo(int? value) => row.Cargo.IsApplicable == false ? "" : Number(value);
            string[] values = [Unknown(row.Identity?.CommanderName), Unknown(row.Identity?.ForceName),
                Unknown(row.Formation?.Selected?.Name), Number(row.Resources.ReadyTroops), Number(row.Resources.WoundedTroops),
                Number(row.Resources.CurrentMorale), Cargo(row.Cargo.Gold), Cargo(row.Cargo.Food)];
            float x = 0;
            for (int column = 0; column < values.Length; column++)
            { Cell(button, "Column" + column, values[column], new(x + 8, 8), widths[column] - 8); x += widths[column]; }
            button.Pressed += () =>
            {
                if (!ReadAdmission() || row.Exists != true) return;
                SelectedUnitId = row.UnitId;
                for (int i = 0; i < buttons.Count; i++) buttons[i].Selected = publication.Rows[i].UnitId == row.UnitId;
                this.inspect?.Invoke(row);
            };
            top += button.Size.Y;
        }
        body.CustomMinimumSize = new(1430, Math.Max(610, top));
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CloseUnitDetailsList", Position = footer.Rectangle(1, new(0, 904)).Position,
            InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestClose, UiAudioCue.Cancel);
        Resized += Fit; Fit();

        void Cell(Control owner, string name, string value, Vector2 position, int width)
        {
            var label = new UiSourceText { Name = name, Position = position, Size = new(width - 12, 32),
                MouseFilter = MouseFilterEnum.Ignore };
            owner.AddChild(label); label.Configure(font, profile); label.Text = value;
        }
    }

    private bool ReadAdmission() => !closed && IsVisibleInTree() && CanProcess() && !IsQueuedForDeletion();
    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        closed = true; inspect = null;
        foreach (var button in buttons) { button.InputAdmission = null; button.Disabled = true; }
        font?.Dispose(); assets?.Dispose(); shader?.Dispose(); base._ExitTree();
    }
}
