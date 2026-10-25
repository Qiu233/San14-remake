using Godot;
using San14.Data;
using San14.Client.Gameplay;

/// <summary>The self-recommendation decision keeps the inspected officer
/// visible above the original footer. Only an explicit reply resolves it.</summary>
internal partial class SelfRecommendationView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private UiSourceRoundButton? accept, refuse;
    private Func<bool>? admitted;
    private Action<bool>? replied;

    internal void Configure(GameResourceRepository repository, PreviewText text, OfficerDetailPublication officer,
        string expanded, Func<bool> admitted, Action<bool> replied)
    {
        this.admitted = admitted; this.replied = replied;
        Name = "SelfRecommendation"; ZIndex = 96; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        // CEmployFreePersonDlg hides the ordinary title and old footer,
        // including its page buttons. Its own footer supplies the reply.
        var detail = new OfficerDetailsView { Size = new(1920, 1080) };
        canvas.AddChild(detail);
        detail.Configure(repository, officer, text, () => { }, admitted, showNavigation: false);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 mount = new(0, 904);
        var narration = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 narrationMount = footer.Rectangle(19, mount).Position;
        foreach (var node in narration.Nodes.Where(node => node.Type is 1 or 3))
            narration.Art(canvas, node.Index, narration.Rectangle(node.Index, narrationMount), assets, shader);
        narration.Text(canvas, 4, narrationMount, expanded, font);
        refuse = Reply(false, 4); accept = Reply(true, 5);
        SetMeta("source_footer", footer.Source.Source.ResourceUri);
        SetMeta("message_global_id", 0x5DC6);
        Resized += Fit; Fit();

        UiSourceRoundButton Reply(bool accepted, int node)
        {
            var button = new UiSourceRoundButton
            {
                Name = accepted ? "AcceptRecommendation" : "RefuseRecommendation",
                Position = footer.Rectangle(node, mount).Position,
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release
            };
            canvas.AddChild(button);
            button.ConfigureSelfRecommendationReply(repository, assets, shader, text.SourceLocale, accepted, () =>
            {
                if (!ReadAdmission()) return;
                var callback = this.replied;
                this.replied = null;
                if (accept is not null) accept.Disabled = true;
                if (refuse is not null) refuse.Disabled = true;
                callback?.Invoke(accepted);
            });
            return button;
        }
    }

    private bool ReadAdmission() => replied is not null && IsVisibleInTree() && CanProcess() &&
        !IsQueuedForDeletion() && admitted?.Invoke() == true;

    public override void _Input(InputEvent input)
    {
        // 831860 consumes the source cancel command without answering the
        // offer. Back cannot become an implicit refusal or close this owner.
        if (ReadAdmission() && (input is InputEventKey { Keycode: Key.Escape } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right }))
            GetViewport().SetInputAsHandled();
    }

    internal void Abandon()
    {
        replied = null; admitted = null;
        if (accept is not null) accept.Disabled = true;
        if (refuse is not null) refuse.Disabled = true;
        Hide();
    }

    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree()
    {
        replied = null; admitted = null;
        font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}
