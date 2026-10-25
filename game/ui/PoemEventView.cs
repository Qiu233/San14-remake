using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The title poem event's real timed stages and resource curves.
/// It retains the parent detail while owning input, media and both footers.</summary>
internal partial class PoemEventView : Control
{
    private sealed class PlayingClip(LcskAnimationClip source, bool repeat)
    {
        internal readonly LcskAnimationClip Source = source;
        internal readonly bool Repeat = repeat;
        internal double Elapsed;
    }
    private PoemEventPublication publication = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, normalFooter = null!, information = null!, infoFooter = null!;
    private UiSourceText hint = null!;
    private ColorRect blackout = null!;
    private readonly List<PlayingClip> clips = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<Exception>? failed;
    private Func<Task>? appearanceSound;
    private bool appearanceSoundRequested;
    private bool abandoned;
    private int phase, inertia, pointerX, previousPointerX;
    private double phaseMilliseconds, fadeMilliseconds, fadeDuration;
    private float fadeStart, fadeTarget;
    private bool dragging, pointerReleased;

    internal void Configure(GameResourceRepository repository, PreviewText text, PoemEventPublication publication,
        Func<bool> admitted, Action returned, Action<Exception> failed, Func<Task> appearanceSound)
    {
        this.repository = repository; this.text = text; this.publication = publication;
        this.admitted = admitted; this.returned = returned; this.failed = failed;
        this.appearanceSound = appearanceSound;
        Name = "PoemEvent"; MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "PoemEventCanvas", Size = new(1920, 1080), ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas); BuildMedia(); BuildFooters(); BuildInformation();
        blackout = new() { Name = "PoemBlackout", Size = new(1920, 1080), Color = new(0, 0, 0, 0),
            MouseFilter = MouseFilterEnum.Ignore, ZIndex = 20 }; canvas.AddChild(blackout);
        normalFooter.Hide(); information.Hide(); infoFooter.Hide();
        SetMeta("item_id", publication.ItemId); SetMeta("source_layout", publication.Layout.Source.ResourceUri);
        Resized += Fit; Fit();
    }

    public override void _Process(double delta)
    {
        if (!ReadAdmission()) return;
        try { Advance(delta); }
        catch (Exception error) { SetProcess(false); failed?.Invoke(error); }
    }

    private void Advance(double delta)
    {
        phaseMilliseconds += delta * 1000;
        foreach (var playing in clips.ToArray())
        {
            playing.Elapsed += delta;
            double elapsed = playing.Repeat && playing.Source.DurationSeconds > 0
                ? playing.Elapsed % playing.Source.DurationSeconds : playing.Elapsed;
            Apply(playing.Source.Sample(playing.Source.FrameAfter(elapsed)));
            if (!playing.Repeat && elapsed >= playing.Source.DurationSeconds) clips.Remove(playing);
        }
        if (fadeDuration > 0)
        {
            fadeMilliseconds += delta * 1000;
            float t = (float)Math.Clamp(fadeMilliseconds / fadeDuration, 0, 1);
            blackout.Color = new(0, 0, 0, fadeStart + (fadeTarget - fadeStart) * t);
        }
        switch (phase)
        {
            case 0: FadeBlack(1, 500); Enter(1); break;
            case 1 when phaseMilliseconds >= 500:
                StartClip("loop", true); FadeBlack(0, 1500); Enter(2); break;
            case 2 when phaseMilliseconds >= 1500: Enter(3); break;
            case 3 when phaseMilliseconds >= 4000:
                StartClip("fade_out"); StartClip(publication.InitialAnimationName); normalFooter.Show(); Enter(4); break;
            case 4 when !IsPlaying(publication.InitialAnimationName): BeginReading(); break;
            case 4 when phaseMilliseconds >= 2900 && !appearanceSoundRequested:
                appearanceSoundRequested = true; _ = PlayAppearanceSoundAsync(); break;
            case 5 when !IsPlaying("txt_scrolling"): Enter(11); break;
            case 6 when phaseMilliseconds >= 500:
                clips.Clear(); StartClip("loop", true); information.Show(); infoFooter.Show();
                FadeBlack(0, 1500); Enter(7); break;
            case 7 when phaseMilliseconds >= 1500: Enter(8); break;
            case 9:
                information.Hide(); infoFooter.Hide(); normalFooter.Hide(); FadeBlack(1, 500); Enter(10); break;
            case 10 when phaseMilliseconds >= 500:
                var complete = returned; returned = null; complete?.Invoke(); return;
        }
        if (scrollAcquired && (phase is 5 or 11) && dragging)
        {
            if (IsPlaying("txt_scrolling")) { clips.Clear(); Enter(11); }
            int dx = pointerX - previousPointerX;
            SetScroll(Math.Clamp(dx + (int)poses[5].X, 0, publication.MaxTextScrollOffsetX));
            previousPointerX = pointerX;
            if (pointerReleased) { inertia = dx; dragging = false; pointerReleased = false; }
        }
        if (publication.MaxTextScrollOffsetX > 0 && scrollAcquired && inertia != 0)
        {
            float x = poses[5].X;
            if (inertia > 0 && x < publication.MaxTextScrollOffsetX || inertia < 0 && x > 0)
            {
                SetScroll(Math.Clamp((int)(x + inertia), 0, publication.MaxTextScrollOffsetX));
                inertia = inertia > 0 ? Math.Max(0, inertia - 10) : Math.Min(0, inertia + 10);
            }
        }
        RefreshMedia();
    }

    private void BeginReading()
    {
        hint.Text = Game(0x2984);
        if (publication.MaxTextScrollOffsetX > 0)
        { visuals[2].Hide(); StartClip("txt_scrolling"); scrollAcquired = true; Enter(5); }
        else Enter(11);
    }

    private void SkipAppearance()
    {
        if (phase != 4) return;
        var clip = publication.Animations.Single(entry => entry.Name == publication.InitialAnimationName);
        clips.RemoveAll(entry => entry.Source == clip); Apply(clip.Sample(clip.EndFrame));
        hint.Text = Game(0x2984);
        if (publication.MaxTextScrollOffsetX > 0)
        {
            visuals[2].Hide();
            var scroll = publication.Animations.Single(entry => entry.Name == "txt_scrolling");
            Apply(scroll.Sample(scroll.StartFrame)); scrollAcquired = true; Enter(5);
        }
        else Enter(11);
        RefreshMedia();
    }

    private void OpenInformation()
    {
        if (!ReadAdmission() || phase is 6 or 7 or 8 or 9 or 10 || phase < 4) return;
        normalFooter.Hide(); FadeBlack(1, 500); Enter(6);
    }

    internal void RequestBack()
    {
        if (!ReadAdmission()) return;
        if (phase == 4) SkipAppearance();
        else if (phase is 5 or 11) OpenInformation();
        else if (phase is 7 or 8) Enter(9);
    }

    public override void _Input(InputEvent input)
    {
        if (!ReadAdmission()) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed && phase == 4) SkipAppearance();
            if (scrollAcquired && (phase is 5 or 11))
            {
                pointerX = (int)(canvas.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position).X;
                if (mouse.Pressed)
                { dragging = true; pointerReleased = false; inertia = 0; previousPointerX = pointerX; }
                else if (dragging) pointerReleased = true;
            }
        }
        else if (input is InputEventMouseMotion motion && dragging && scrollAcquired && (phase is 5 or 11))
        {
            pointerX = (int)(canvas.GetGlobalTransformWithCanvas().AffineInverse() * motion.Position).X;
        }
    }

    private void Enter(int next) { phase = next; phaseMilliseconds = 0; SetMeta("source_phase", next); }
    private void FadeBlack(float target, int milliseconds)
    { fadeStart = blackout.Color.A; fadeTarget = target; fadeDuration = milliseconds; fadeMilliseconds = 0; }
    private void StartClip(string name, bool repeat = false)
    {
        var clip = publication.Animations.Single(entry => entry.Name == name);
        clips.RemoveAll(entry => entry.Source.Name == name);
        Apply(clip.Sample(clip.StartFrame)); clips.Add(new(clip, repeat));
    }
    private bool IsPlaying(string name) => clips.Any(entry => entry.Source.Name == name);
    private async Task PlayAppearanceSoundAsync()
    {
        try { if (appearanceSound is { } play) await play(); }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!abandoned) failed?.Invoke(error); }
    }
    private string Game(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Game)
        .Messages.Sections[id / 10000].Records[id % 10000].Literals.Single().Text;
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080); canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - canvas.Size * scale) / 2;
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; failed = null; appearanceSound = null;
        clips.Clear(); SetProcess(false); Hide();
    }
    public override void _ExitTree()
    { Abandon(); foreach (var image in imagePanes) image.Dispose(); assets?.Dispose(); font?.Dispose(); shader?.Dispose(); }
}
