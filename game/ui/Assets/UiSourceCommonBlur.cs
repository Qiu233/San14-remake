using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original medium CCmnBlur background, before modal content.
/// The page owns its visibility and lifetime; the existing Type6 host owns
/// both ordered captures. This control does not change map or command state.</summary>
internal partial class UiSourceCommonBlur : Control
{
    private Control canvas = null!;
    private readonly Dictionary<int, UiSourceType6> panes = [];
    private IReadOnlyDictionary<string, LcskAnimationClip>? clips;
    private LcskAnimationClip? playing;
    private float frame;
    private TaskCompletionSource<bool>? completion;
    internal bool AutomaticAnimationClock { get; set; } = true;

    internal void Configure(GameResourceRepository repository, UiSourceLayout? preparedSource = null)
    {
        Name = "CommonModalBlur";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080),
            MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        var source = preparedSource ?? new UiSourceLayout(repository, 0xAEF1609E);
        clips = LcskAnimationReader.Read(source.Source.Layout, new HashSet<string> { "blur_medium_in", "blur_medium_out" }).ToDictionary(clip => clip.Name);
        // Both real panes use the static endpoint of blur_medium_in.
        panes.Add(1, source.Blur(canvas, 1, source.Rectangle(1), weightInput: 100));
        panes.Add(2, source.Blur(canvas, 2, source.Rectangle(2), weightInput: 100));
        Resized += Fit;
        Fit();
    }

    internal Task<bool> PlayTransition(bool opening)
    {
        completion?.TrySetResult(false);
        playing = clips![opening ? "blur_medium_in" : "blur_medium_out"];
        frame = playing.StartFrame;
        completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ApplyFrame(playing); return completion.Task;
    }

    internal void RestoreShownState()
    {
        playing = null; completion?.TrySetResult(false); completion = null;
        var shown = clips!["blur_medium_in"];
        frame = shown.EndFrame;
        ApplyFrame(shown);
    }

    internal void AdvanceTransition(double delta)
    {
        if (playing is not { } clip) return;
        frame = Math.Min(clip.EndFrame, frame + (float)(Math.Max(delta, 0) / LcskAnimationClip.SecondsPerFrame) * 2f);
        ApplyFrame(clip);
        if (frame >= clip.EndFrame) { playing = null; completion?.TrySetResult(true); completion = null; }
    }
    public override void _Process(double delta) { if (AutomaticAnimationClock) AdvanceTransition(delta); }
    private void ApplyFrame(LcskAnimationClip clip)
    {
        foreach (var track in clip.Tracks)
        {
            if (track.Channel != 57 || !panes.TryGetValue(track.NodeId, out var pane))
                throw new InvalidDataException("Unsupported common modal blur track.");
            if (track.Curve.Keys.Count == 0) continue;
            pane.SetWeightInput((float)track.Curve.Sample(frame));
        }
    }
    public override void _ExitTree() { playing = null; completion?.TrySetResult(false); completion = null; }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
}
