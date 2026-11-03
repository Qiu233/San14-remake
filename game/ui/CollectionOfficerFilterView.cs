using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The collection's three independent admission groups. Draft changes
/// belong to this dialog and are published only by its confirm command.</summary>
internal partial class CollectionOfficerFilterView : Control
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<IReadOnlyList<bool>>? confirmed;
    private readonly bool[] groups = new bool[3];
    private readonly List<UiSourceCommandButton> choices = [];
    private bool abandoned;
    private bool ownsResources;

    internal void Configure(GameResourceRepository repository, PreviewText text, IReadOnlyList<bool> initial,
        Func<bool> admitted, Action returned, Action<IReadOnlyList<bool>> confirmed,
        AdministrationPageChrome.Resources? resources = null)
    {
        if (initial.Count != 3) throw new ArgumentException("Collection filter requires three groups.", nameof(initial));
        this.admitted = admitted; this.returned = returned; this.confirmed = confirmed;
        for (int index = 0; index < groups.Length; index++) groups[index] = initial[index];
        Name = "CollectionOfficerFilter"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ownsResources = resources is null;
        assets = resources?.Assets ?? new(repository);
        font = resources?.Font ?? new(repository, text.SourceLocale);
        shader = resources?.SpriteShader ?? UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var source = assets.SourceLayout(0x2714E51B);
        Vector2 mount = (canvas.Size - source.Rectangle(0).Size) / 2;
        foreach (var node in source.Nodes.Where(node => node.Index < 7))
        {
            if (node.Type is 1 or 3) source.Art(canvas, node.Index, source.Rectangle(node.Index, mount), assets, shader);
            else if (node.Type == 6) source.Blur(canvas, node.Index, source.Rectangle(node.Index, mount));
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Collection filter caption is missing.");
        source.Text(canvas, 19, mount, Caption(0x70), font);
        int[] captions = [0x71, 0x72, 0x65];
        for (int index = 0; index < groups.Length; index++)
        {
            int group = index; var box = source.Rectangle(7 + index * 4, mount);
            var button = new UiSourceCommandButton { Name = "CollectionOfficerGroup" + group,
                Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission, Selected = groups[index] };
            canvas.AddChild(button); choices.Add(button);
            button.Configure(repository, assets, shader, font, Caption(captions[index]),
                () => { if (ReadAdmission()) { groups[group] = !groups[group]; button.Selected = groups[group]; } });
        }
        var footer = assets.SourceLayout(0xD8913AB2);
        AddFooter(4, "CollectionOfficerFilterCancel", "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        AddFooter(5, "CollectionOfficerFilterConfirm", "000_cmn_txt_decision_kettei",
            () => { if (ReadAdmission()) this.confirmed?.Invoke(groups.ToArray()); }, UiAudioCue.ButtonSelect);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Resized += Fit; Fit();

        void AddFooter(int node, string name, string caption, Action action, UiAudioCue cue)
        {
            var button = new UiSourceRoundButton { Name = name,
                Position = footer.Rectangle(node, new(0, 904)).Position, InputAdmission = ReadAdmission };
            canvas.AddChild(button); button.Configure(repository, assets, shader, text.SourceLocale, caption, action, cue);
        }
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; confirmed = null;
        foreach (var choice in choices) choice.Abandon();
        Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        Abandon();
        if (ownsResources) { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
    }
}
