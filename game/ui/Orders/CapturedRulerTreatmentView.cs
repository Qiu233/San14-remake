using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Only an explicit source Execute/Release selection answers the
/// suspended operation. Closing or invalidating this view supplies no result.</summary>
internal partial class CapturedRulerTreatmentView : Control
{
    // All static resources are borrowed from the application owner.
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private UiSourceCaptionButton? execute, release;
    private Func<bool>? admitted;
    private Action<CapturedRulerDisposition>? replied;

    internal void Configure(GameResourceRepository repository, PreviewText text, OfficerDetailPublication officer,
        CapturedRulerTreatmentRequest request, Func<bool> admitted, Action<CapturedRulerDisposition> replied,
        AdministrationPageChrome.Resources resources)
    {
        this.admitted = admitted; this.replied = replied;
        Name = "CapturedRulerTreatment"; ZIndex = 96; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var detail = new OfficerDetailsView { Size = new(1920, 1080) }; canvas.AddChild(detail);
        detail.Configure(repository, officer, text, () => { }, admitted, showNavigation: false,resources:resources);
        assets = resources.Assets; font = resources.Font; shader = resources.SpriteShader;
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Data(int id) => data.Records[id].Text ?? throw new InvalidDataException("Missing captured ruler caption.");
        var title = new UiSourceHeader(); canvas.AddChild(title);
        title.Configure(repository, assets, shader, font, Data(0x4AD));
        var parent = new UiSourceLayout(repository, 0x088BCB80);
        var footer = new UiSourceLayout(repository, 0x18DB7286);
        Vector2 mount = parent.Rectangle(16).Position;
        footer.Art(canvas, 1, footer.Rectangle(1, mount), assets, shader);
        var normalFooter = new UiSourceLayout(repository, 0xD8913AB2);
        var narration = new UiSourceLayout(repository, 0xAB6ADAA7);
        var hintMount = normalFooter.Rectangle(19, new(0, 904)).Position;
        foreach (var node in narration.Nodes.Where(node => node.Type is 1 or 3))
            narration.Art(canvas, node.Index, narration.Rectangle(node.Index, hintMount), assets, shader);
        var hint = narration.Text(canvas, 4, hintMount, request.TextAtSource, font);
        hint.Name = "TreatmentPrompt";
        execute = Make(2, Data(0x3C), CapturedRulerDisposition.Execute);
        release = Make(4, Data(0x3D), CapturedRulerDisposition.Release);
        execute.Disabled = !request.CanExecute;
        string disabledHint = repository.LoadMessages(text.SourceLocale, MessageKind.Game).Messages.Sections[0x3248 / 10000]
            .Records[0x3248 % 10000].Literals.Single().Text;
        execute.MouseEntered += () => { if (!request.CanExecute && ReadAdmission()) hint.Text = disabledHint; };
        execute.MouseExited += () => { if (ReadAdmission()) hint.Text = request.TextAtSource; };
        SetMeta("source_layout", parent.Source.Source.ResourceUri);
        SetMeta("source_footer", footer.Source.Source.ResourceUri);
        SetMeta("officer_id", request.OfficerId);
        Resized += Fit; Fit();

        UiSourceCaptionButton Make(int node, string caption, CapturedRulerDisposition choice)
        {
            var rect = footer.Rectangle(node, mount);
            var button = new UiSourceCaptionButton { Name = choice.ToString(), Position = rect.Position,
                Size = rect.Size, InputAdmission = ReadAdmission };
            canvas.AddChild(button);
            button.ConfigureFooterText(repository, assets, shader, font, caption, () =>
            {
                if (!ReadAdmission() || choice == CapturedRulerDisposition.Execute && !request.CanExecute) return;
                var callback = this.replied; this.replied = null;
                execute!.Disabled = true; release!.Disabled = true;
                callback?.Invoke(choice);
            });
            return button;
        }
    }

    private bool ReadAdmission() => replied is not null && IsVisibleInTree() && CanProcess() &&
        !IsQueuedForDeletion() && admitted?.Invoke() == true;

    public override void _Input(InputEvent input)
    {
        if (ReadAdmission() && (input is InputEventKey { Keycode: Key.Escape } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right })) GetViewport().SetInputAsHandled();
    }

    internal void Abandon() { replied = null; admitted = null; Hide(); }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree() { replied = null; admitted = null; }
}
