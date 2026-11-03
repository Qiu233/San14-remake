using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record StoryActorVisual(int Slot, int OfficerId, LoadedStandingPortraitSources Layers)
{
    internal bool? Dark { get; init; }
}

/// <summary>One event scene. Accepted source steps update retained media;
/// only the current message owns input, and each of its pages must be drawn.</summary>
internal partial class StoryPresentationView : Control
{
    private UiAssets? assets;
    private Shader? shader;
    private AdministrationPageChrome.Resources resources = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Control canvas = null!, backgroundRoot = null!, actorRoot = null!, illustrationRoot = null!, smallIllustrationRoot = null!, itemIllustrationRoot = null!, ownedEffectRoot = null!;
    private ColorRect blackout = null!;
    private Control playbackControls = null!;
    private TutorialStillView? tutorialIllustration;
    private readonly List<UiSourceFunctionButton> playbackButtons = [];
    private readonly Dictionary<int, (int Officer, string FirstSprite, ushort? Appearance, UiSourceStandingPortrait View)> actors = [];
    //Visibility never resets the five retained source-layout states.
    private readonly bool?[] portraitDarkFlags = new bool?[5];
    internal IReadOnlyList<bool?> PortraitDarkFlags => portraitDarkFlags;
    private Func<bool>? admitted;
    private DeploymentNarrationView? message;
    private Action? waitCompleted;
    private Func<bool>? waitReady;
    private bool waitDrawn;
    private ulong waitUntil;
    private uint? sourceWaitUntil;
    private bool waitRequiresDraw;
    private bool abandoned;
    internal Control OwnedEffectOutput => ownedEffectRoot;
    internal bool IsWaitingForMessage => message?.IsAwaitingAdvance == true;
    internal DeploymentNarrationView? Message => message;
    internal Func<InputEvent, bool>? KeyboardInput { get; set; }
    internal Func<int, bool>? MessageModeRequested { get; set; }
    internal Func<bool>? ReadAccelerated { get; set; }
    internal Action? OpenLog { get; set; }

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        AdministrationPageChrome.Resources resources)
    {
        this.repository = repository; this.text = text; this.admitted = admitted;
        this.resources = resources; assets = resources.Assets; shader = resources.SpriteShader;
        Name = "CampaignStoryScene"; ZIndex = 96; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "StorySourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop }; AddChild(canvas);
        canvas.Draw += ObserveDraw;
        backgroundRoot = Layer("StoryBackground"); actorRoot = Layer("StoryActors"); illustrationRoot = Layer("StoryIllustration");
        smallIllustrationRoot = Layer("StorySmallIllustration");
        itemIllustrationRoot = Layer("StoryItemIllustration");
        ownedEffectRoot = Layer("StoryOwnedEffects");
        blackout = new() { Name = "StoryBlackout", Size = new(1920, 1080), Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 20 }; canvas.AddChild(blackout);
        BuildPlaybackControls();
        Resized += Fit; Fit();
        Control Layer(string name)
        { var layer = new Control { Name = name, Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore }; canvas.AddChild(layer); return layer; }
    }

    private void BuildPlaybackControls()
    {
        var root = assets!.SourceLayout(0xD8B39FC6);
        var source = assets!.SourceLayout(0xE6F9D1D6);
        playbackControls = new Control { Name = "StoryPlaybackControls", Position = root.Rectangle(6).Position,
            Size = source.Rectangle(0).Size, ZIndex = 15, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        canvas.AddChild(playbackControls);
        var strings = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        int[] nodes = [5, 3, 1], tooltips = [0x607, 0x606, 0x5B5];
        string[] icons = ["000_cmn_icon_advance_spd2", "000_cmn_icon_advance_skip", "000_cmn_icon_log"];
        for (int index = 0; index < nodes.Length; index++)
        {
            int action = index;
            var button = new UiSourceFunctionButton { Position = source.Rectangle(nodes[index]).Position,
                TooltipText = strings[tooltips[index]].Text };
            playbackControls.AddChild(button);
            playbackButtons.Add(button);
            button.Configure(repository, assets!, shader!, icons[index], () =>
            {
                if (!InputAdmitted()) return;
                if (action == 2) OpenLog?.Invoke();
                else MessageModeRequested?.Invoke(action == 0 ? 24 : 23);
            });
        }
    }

    internal void SetBackground(int identity, int resourceKind = 15)
    {
        Clear(backgroundRoot);
        backgroundRoot.Visible = identity > 0;
        backgroundRoot.SetMeta("source_identity", identity);
        if (identity <= 0) return;
        string family = resourceKind switch { 14 => "stl_event_d_", 15 => "stl_event_bg_",
            _ => throw new InvalidDataException("story.background.resource_kind") };
        string suffix = identity.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        var layout = assets!.SourceLayout(0xF6352F00);
        layout.Art(backgroundRoot, 1, layout.Rectangle(1), assets!, shader!,
            commonTexturePack: "texture_" + family + suffix, commonSpriteOverride: family + suffix);
        if (resourceKind == 14) return;
        var blur = assets!.SourceLayout(0xAEF1609E);
        Vector2 mount = layout.Rectangle(3).Position;
        blur.Blur(backgroundRoot, 1, blur.Rectangle(1, mount), weightInput: 100);
        blur.Blur(backgroundRoot, 2, blur.Rectangle(2, mount), weightInput: 100);
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
        var layout = assets!.SourceLayout(0xC6CCCD9C);
        layout.Art(illustrationRoot, 1, layout.Rectangle(1), assets!, shader!,
            commonTexturePack: "texture_stl_event_" + suffix, commonSpriteOverride: "stl_event_" + suffix);
    }

    internal void SetSmallIllustration(int identity)
    {
        Clear(smallIllustrationRoot);
        smallIllustrationRoot.SetMeta("source_identity", identity);
        smallIllustrationRoot.Visible = identity > 0;
        if (identity <= 0) return;
        ClearActors();
        string suffix = identity.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        var layout = assets!.SourceLayout(0xC6CCCD9C);
        layout.Art(smallIllustrationRoot, 8, layout.Rectangle(8), assets!, shader!,
            commonTexturePack: "texture_stl_event_komono_" + suffix, commonSpriteOverride: "stl_event_komono_" + suffix);
    }

    //Image0 is a real item picture. Only null names the source hide branch.
    internal void SetItemIllustration(int? identity, bool suppressed = false)
    {
        //843570 hides the five live portraits before the manager gate in the
        //item-layer mutator; suppressing the image must not skip those hides.
        if (identity.HasValue) ClearActors();
        if (suppressed) return;
        Clear(itemIllustrationRoot);
        itemIllustrationRoot.Visible = identity.HasValue;
        itemIllustrationRoot.SetMeta("source_identity", identity ?? -1);
        if (identity is not int image) return;
        if (image < 0) throw new InvalidDataException("story.item_illustration.identity");
        var route = CollectionImageRoute.ForItem(image);
        var layout = assets!.SourceLayout(0xC6CCCD9C);
        layout.Art(itemIllustrationRoot, 12, layout.Rectangle(12), assets!, shader!,
            commonTexturePack: route.TexturePack, commonSpriteOverride: route.SpriteName);
    }

    internal void HideMessage() => RetireMessage();

    internal void SetTutorialIllustration(int identity)
    {
        if (identity != 0) ClearActors();
        if (tutorialIllustration is null)
        {
            if (identity == 0) return;
            tutorialIllustration = new TutorialStillView { Name = "StoryTutorialIllustration" };
            canvas.AddChild(tutorialIllustration);
            tutorialIllustration.Configure(repository, text);
        }
        tutorialIllustration.SetIllustration(identity);
    }

    internal void ApplyActors(IReadOnlyList<StoryActorVisual> current, int? highlighted,
        IReadOnlyList<bool?>? darkFlags = null, bool immediateDark = false)
    {
        var previousDark = portraitDarkFlags.ToArray();
        if (darkFlags is not null)
        {
            if (darkFlags.Count != portraitDarkFlags.Length)
                throw new InvalidDataException("story.presentation.dark_slot_count");
            for (int slot = 0; slot < portraitDarkFlags.Length; slot++)
                if (darkFlags[slot] is bool dark) portraitDarkFlags[slot] = dark;
        }
        var positions = assets!.SourceLayout(0x86BA499F);
        int[] nodes = [2, 3, 4, 6, 5];
        foreach (int slot in actors.Keys.Where(slot => current.All(actor => actor.Slot != slot)).ToArray())
        { var previous = actors[slot].View; previous.Hide(); actorRoot.RemoveChild(previous); previous.QueueFree(); actors.Remove(slot); }
        foreach (var actor in current)
        {
            if ((uint)actor.Slot >= 5) throw new InvalidDataException("Story actor slot is outside the source scene.");
            if(actor.Layers.Count==0)throw new InvalidDataException("Story actor has no resolved standing portrait.");
            string firstSprite=actor.Layers[0].Definition.Name;
            ushort? appearance=actor.Layers.CurrentAppearanceIndex;
            if (!actors.TryGetValue(actor.Slot, out var previous) || previous.Officer != actor.OfficerId ||
                previous.FirstSprite!=firstSprite || previous.Appearance!=appearance)
            {
                if (previous.View is { } old) { old.Hide(); actorRoot.RemoveChild(old); old.QueueFree(); }
                var portrait = new UiSourceStandingPortrait { Name = "StoryActor" + actor.Slot,
                    Position = positions.Rectangle(nodes[actor.Slot]).Position };
                actorRoot.AddChild(portrait); portrait.Configure(repository, assets!, shader!, actor.Layers);
                if (!immediateDark && previousDark[actor.Slot] is bool retainedDark)
                    portrait.SetStoryDark(retainedDark, immediate: true);
                actors[actor.Slot] = (actor.OfficerId, firstSprite, appearance, portrait);
            }
            if (actor.Dark is bool dark) portraitDarkFlags[actor.Slot] = dark;
            //A replacement artwork uses the retained slot state. A missing
            //legacy value does not infer brightness from speaker visibility.
            if (portraitDarkFlags[actor.Slot] is bool currentDark)
                actors[actor.Slot].View.SetStoryDark(currentDark, immediateDark);
        }
        SetMeta("highlighted_slot", highlighted ?? -1);
    }

    internal void PresentMessage(string speaker, string expanded, Action consumed,
        Func<AutomaticMessageAdvanceMode> automaticAdvance, bool singleWait = false)
    {
        RetireMessage();
        playbackControls.Show();
        var view = new DeploymentNarrationView { Name = "StoryMessage", ZIndex = 10, KeyboardInput = HandleMessageInput,
            MessageModeRequested = MessageModeRequested, ReadStoryAccelerated = ReadAccelerated,
            MouseInputAdmission = input => !PlaybackControlOwnsPointer(input.Position) }; message = view; canvas.AddChild(view);
        // GUI picking follows sibling order independently of drawing ZIndex.
        // Keep the real function buttons above the full-window message input.
        canvas.MoveChild(playbackControls, -1);
        view.ConfigureStoryMessage(repository, text, speaker, expanded, () =>
        {
            if (!InputAdmitted() || message != view) return;
            consumed();
        }, automaticAdvance, InputAdmitted, singleWait, resources);
    }

    internal void RestoreMessage(string speaker, string expanded, bool singleWait = false)
    {
        PresentMessage(speaker, expanded, () => { }, () => AutomaticMessageAdvanceMode.Manual, singleWait);
        message!.RetainStoryPage();
    }

    internal void BeginTransition(bool fadeIn, int milliseconds, Action completed)
    {
        if (!fadeIn) { ClearActors(); RetireMessage(); }
        blackout.Visible = !fadeIn;
        BeginWait(milliseconds, completed);
    }

    internal void RetireAttachments()
    {
        ClearActors(); RetireMessage();
        foreach (var layer in new[] { backgroundRoot, illustrationRoot, smallIllustrationRoot,
            itemIllustrationRoot, ownedEffectRoot }) Clear(layer);
        tutorialIllustration?.Hide();
    }

    //Ordinary CEventPlaybackState changes black alpha before retiring actors,
    //and reveals the map only after its audio/camera return has completed.
    internal void BeginNormalExitFade(bool reveal, int milliseconds, Action completed)
    {
        blackout.Visible = true;
        blackout.Color = new Color(0, 0, 0, reveal ? 1 : 0);
        var fade = CreateTween();
        fade.TweenProperty(blackout, "color", new Color(0, 0, 0, reveal ? 0 : 1), milliseconds / 1000.0);
        BeginWait(milliseconds, () =>
        {
            if (reveal) blackout.Hide();
            completed();
        }, () => !fade.IsRunning());
    }

    internal void RetireNormalTalk()
    {
        ClearActors(); RetireMessage();
    }

    internal void BeginReturnFade(int milliseconds, Action completed)
    {
        blackout.Visible = true; blackout.Color = new Color(0, 0, 0, 0);
        var fade = CreateTween();
        fade.TweenProperty(blackout, "color", Colors.Black, milliseconds / 1000.0);
        BeginWait(milliseconds, completed, () => !fade.IsRunning());
    }

    internal void BeginWait(int milliseconds, Action completed, Func<bool>? ready = null)
    {
        ResetInputWait();
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        waitCompleted = completed; waitReady = ready; waitDrawn = false; waitRequiresDraw = true;
        sourceWaitUntil = null;
        waitUntil = Time.GetTicksMsec() + (ulong)milliseconds; canvas.QueueRedraw();
    }

    internal void BeginSourceWait(int milliseconds, Action completed)
    {
        ResetInputWait();
        // CTimeWait stores a signed duration in an unsigned low32 deadline.
        // It observes the real QPC clock, including the original
        // wrap comparison; there is no frame count or duration clamp.
        waitCompleted = completed; waitReady = null; waitRequiresDraw = false;
        sourceWaitUntil = unchecked(SourceWaitMilliseconds() + (uint)milliseconds);
    }

    private static uint SourceWaitMilliseconds() => unchecked((uint)(
        unchecked(System.Diagnostics.Stopwatch.GetTimestamp() * 1000L) / System.Diagnostics.Stopwatch.Frequency));

    internal void RestoreBlackout(bool black) => blackout.Visible = black;
    internal void ClearActors()
    { foreach (var actor in actors.Values) { actor.View.Hide(); actorRoot.RemoveChild(actor.View); actor.View.QueueFree(); } actors.Clear(); }
    internal void HideCharacters(IReadOnlyList<int> characters)
    {
        if (characters.Count == 0) { ClearActors(); return; }
        foreach (int slot in actors.Where(pair => characters.Contains(pair.Value.Officer)).Select(pair => pair.Key).ToArray())
        {
            var previous = actors[slot].View;
            previous.Hide(); actorRoot.RemoveChild(previous); previous.QueueFree(); actors.Remove(slot);
        }
    }
    private void RetireMessage()
    {
        playbackControls?.Hide();
        var previous = message; message = null; if (previous is null) return;
        previous.Abandon(); canvas.RemoveChild(previous); previous.QueueFree();
    }
    private static void Clear(Control layer)
    { foreach (var child in layer.GetChildren()) { layer.RemoveChild(child); child.QueueFree(); } }
    private bool InputAdmitted() => !abandoned && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private bool PlaybackControlOwnsPointer(Vector2 viewportPosition) => playbackControls.IsVisibleInTree() &&
        playbackButtons.Any(button => !button.Disabled && button._HasPoint(
            button.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition));
    private bool HandleMessageInput(InputEvent input)
    {
        if (input is InputEventKey { Keycode: Key.Escape } or InputEventMouseButton { ButtonIndex: MouseButton.Right })
        {
            if (input is InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: false })
                MessageModeRequested?.Invoke(23);
            return true;
        }
        return KeyboardInput?.Invoke(input) ?? false;
    }
    public override void _Process(double delta)
    {
        if (ProcessInputWait()) return;
        if (!InputAdmitted() || waitRequiresDraw && !waitDrawn || waitCompleted is not { } complete ||
            (sourceWaitUntil is uint deadline ? SourceWaitMilliseconds() < deadline : Time.GetTicksMsec() < waitUntil) ||
            waitReady?.Invoke() == false) return;
        waitCompleted = null; waitReady = null; sourceWaitUntil = null; complete();
    }
    private void ObserveDraw() => waitDrawn = true;
    public override void _Input(InputEvent input)
    {
        if (HandleInputWait(input)) return;
        if (InputAdmitted() && (input is InputEventKey { Keycode: Key.Escape } or InputEventMouseButton { ButtonIndex: MouseButton.Right }))
        {
            HandleMessageInput(input);
            GetViewport().SetInputAsHandled();
        }
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; waitCompleted = null; waitReady = null; sourceWaitUntil = null;
        KeyboardInput = null; InputWaitKeyboard = null; ResetInputWait(); RetireMessage(); Hide();
    }
    private void Fit()
    { float factor = Math.Min(Size.X / 1920, Size.Y / 1080); canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2; }
    public override void _ExitTree()
    {
        admitted = null; waitCompleted = null; abandoned = true;
        if (GodotObject.IsInstanceValid(canvas)) canvas.Draw -= ObserveDraw;
        assets = null; shader = null;
    }
}
