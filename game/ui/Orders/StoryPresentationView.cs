using Godot;
using San14.Data;

internal sealed record StoryActorVisual(int Slot, int OfficerId, IReadOnlyList<LoadedSprite> Layers);

/// <summary>One event scene. Accepted source steps update retained media;
/// only the current message owns input, and each of its pages must be drawn.</summary>
internal partial class StoryPresentationView : Control
{
    private UiAssets? assets;
    private Shader? shader;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Control canvas = null!, backgroundRoot = null!, actorRoot = null!, illustrationRoot = null!;
    private ColorRect blackout = null!;
    private readonly Dictionary<int, (int Officer, UiSourceStandingPortrait View)> actors = [];
    private Func<bool>? admitted;
    private DeploymentNarrationView? message;
    private Action? waitCompleted;
    private ulong waitUntil;
    private bool abandoned;
    internal bool IsWaitingForMessage => message?.IsAwaitingAdvance == true;
    internal DeploymentNarrationView? Message => message;

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted)
    {
        this.repository = repository; this.text = text; this.admitted = admitted;
        assets = new(repository); shader = UiSourceSprite.CreateShader();
        Name = "CampaignStoryScene"; ZIndex = 96; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "StorySourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop }; AddChild(canvas);
        backgroundRoot = Layer("StoryBackground"); actorRoot = Layer("StoryActors"); illustrationRoot = Layer("StoryIllustration");
        blackout = new() { Name = "StoryBlackout", Size = new(1920, 1080), Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 20 }; canvas.AddChild(blackout);
        Resized += Fit; Fit();
        Control Layer(string name)
        { var layer = new Control { Name = name, Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore }; canvas.AddChild(layer); return layer; }
    }

    internal void SetBackground(int identity)
    {
        Clear(backgroundRoot);
        backgroundRoot.Visible = identity > 0;
        backgroundRoot.SetMeta("source_identity", identity);
        if (identity <= 0) return;
        string suffix = identity.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        var layout = new UiSourceLayout(repository, 0xF6352F00);
        layout.Art(backgroundRoot, 1, layout.Rectangle(1), assets!, shader!,
            commonTexturePack: "texture_stl_event_bg_" + suffix, commonSpriteOverride: "stl_event_bg_" + suffix);
    }

    internal void SetIllustration(int identity)
    {
        Clear(illustrationRoot);
        illustrationRoot.SetMeta("source_identity", identity);
        illustrationRoot.Visible = identity != 0;
        if (identity == 0) return;
        // Every nonzero illustration request first retires visible actors.
        ClearActors();
        if (identity < 0) return; // Native lookup has no matching image, retaining its nonzero scene identity.
        string suffix = identity.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        var layout = new UiSourceLayout(repository, 0xC6CCCD9C);
        layout.Art(illustrationRoot, 1, layout.Rectangle(1), assets!, shader!,
            commonTexturePack: "texture_stl_event_" + suffix, commonSpriteOverride: "stl_event_" + suffix);
    }

    internal void ApplyActors(IReadOnlyList<StoryActorVisual> current, int? highlighted, Color darkColour)
    {
        var positions = new UiSourceLayout(repository, 0x86BA499F);
        int[] nodes = [2, 3, 4, 6, 5];
        foreach (int slot in actors.Keys.Where(slot => current.All(actor => actor.Slot != slot)).ToArray())
        { var previous = actors[slot].View; previous.Hide(); actorRoot.RemoveChild(previous); previous.QueueFree(); actors.Remove(slot); }
        foreach (var actor in current)
        {
            if ((uint)actor.Slot >= 5) throw new InvalidDataException("Story actor slot is outside the source scene.");
            if (!actors.TryGetValue(actor.Slot, out var previous) || previous.Officer != actor.OfficerId)
            {
                if (previous.View is { } old) { old.Hide(); actorRoot.RemoveChild(old); old.QueueFree(); }
                var portrait = new UiSourceStandingPortrait { Name = "StoryActor" + actor.Slot,
                    Position = positions.Rectangle(nodes[actor.Slot]).Position };
                actorRoot.AddChild(portrait); portrait.Configure(repository, assets!, shader!, actor.Layers);
                actors[actor.Slot] = (actor.OfficerId, portrait);
            }
            actors[actor.Slot].View.Modulate = highlighted == actor.Slot ? Colors.White : darkColour;
        }
        SetMeta("highlighted_slot", highlighted ?? -1);
    }

    internal void PresentMessage(string speaker, string expanded, Action consumed,
        Func<AutomaticMessageAdvanceMode> automaticAdvance)
    {
        RetireMessage();
        var view = new DeploymentNarrationView { Name = "StoryMessage", ZIndex = 10 }; message = view; canvas.AddChild(view);
        view.ConfigureStoryMessage(repository, text, speaker, expanded, () =>
        {
            if (!InputAdmitted() || message != view) return;
            RetireMessage(); consumed();
        }, automaticAdvance, InputAdmitted);
    }

    internal void BeginTransition(bool fadeIn, int milliseconds, Action completed)
    {
        if (!fadeIn) { ClearActors(); RetireMessage(); }
        blackout.Visible = !fadeIn;
        BeginWait(milliseconds, completed);
    }

    internal void BeginWait(int milliseconds, Action completed)
    {
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        waitCompleted = completed; waitUntil = Time.GetTicksMsec() + (ulong)milliseconds;
    }

    internal void RestoreBlackout(bool black) => blackout.Visible = black;
    internal void ClearActors()
    { foreach (var actor in actors.Values) { actor.View.Hide(); actorRoot.RemoveChild(actor.View); actor.View.QueueFree(); } actors.Clear(); }
    private void RetireMessage()
    { var previous = message; message = null; if (previous is null) return; previous.Abandon(); canvas.RemoveChild(previous); previous.QueueFree(); }
    private static void Clear(Control layer)
    { foreach (var child in layer.GetChildren()) { layer.RemoveChild(child); child.QueueFree(); } }
    private bool InputAdmitted() => !abandoned && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    public override void _Process(double delta)
    {
        if (!InputAdmitted() || waitCompleted is not { } complete || Time.GetTicksMsec() < waitUntil) return;
        waitCompleted = null; complete();
    }
    public override void _Input(InputEvent input)
    {
        if (InputAdmitted() && (input is InputEventKey { Keycode: Key.Escape } or InputEventMouseButton { ButtonIndex: MouseButton.Right }))
            GetViewport().SetInputAsHandled();
    }
    internal void Abandon()
    { abandoned = true; admitted = null; waitCompleted = null; RetireMessage(); Hide(); }
    private void Fit()
    { float factor = Math.Min(Size.X / 1920, Size.Y / 1080); canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2; }
    public override void _ExitTree()
    { admitted = null; waitCompleted = null; abandoned = true; assets?.Dispose(); shader?.Dispose(); }
}
