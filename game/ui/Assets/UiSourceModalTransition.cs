using Godot;
using San14.Data;

/// <summary>The Config state's three real registered foreground groups.
/// Resource clocks, exact curves and target roots stay with this page.</summary>
internal partial class UiSourceModalTransition : Node
{
    private UiSourceLayout source = null!;
    private UiSourceCommonBlur backdrop = null!;
    private CanvasItem body = null!;
    private Node2D header = null!, footer = null!;
    private IReadOnlyDictionary<string, LcskAnimationClip> clips = null!;
    private (LcskAnimationClip Clip, float Frame)[]? playing;
    private TaskCompletionSource<bool>? completion;
    internal bool AutomaticAnimationClock { get; set; } = true;
    internal bool IsPlaying => playing is not null;

    internal void Configure(UiSourceLayout layout, UiSourceCommonBlur common,
        CanvasItem bodyRoot, Node2D headerRoot, Node2D footerRoot)
    {
        source = layout; backdrop = common; body = bodyRoot; header = headerRoot; footer = footerRoot;
        clips = LcskAnimationReader.Read(layout.Source.Layout, new HashSet<string>
        { "ani_dlg_slide_fade_in", "ani_dlg_slide_fade_out", "ani_header_in", "ani_header_out", "ani_footer_in", "ani_footer_out" })
            .ToDictionary(clip => clip.Name);
    }

    internal async Task<bool> Play(bool opening)
    {
        completion?.TrySetResult(false);
        string suffix = opening ? "_in" : "_out";
        playing = new[] { "ani_dlg_slide_fade", "ani_header", "ani_footer" }
            .Select(name => clips[name + suffix]).Select(clip => (clip, clip.StartFrame)).ToArray();
        completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var foreground = completion.Task;
        ApplyFrames();
        var background = backdrop.PlayTransition(opening);
        var results = await Task.WhenAll(foreground, background);
        return results.All(value => value);
    }

    public override void _Process(double delta) { if (AutomaticAnimationClock) Advance(delta); }
    internal void Advance(double delta)
    {
        if (playing is not { } current) return;
        float advance = (float)(Math.Max(0, delta) / LcskAnimationClip.SecondsPerFrame) * 1.5f;
        for (int i = 0; i < current.Length; i++)
            current[i].Frame = Math.Min(current[i].Clip.EndFrame, current[i].Frame + advance);
        ApplyFrames();
        if (current.All(value => value.Frame >= value.Clip.EndFrame))
        { playing = null; completion?.TrySetResult(true); completion = null; }
    }

    private void ApplyFrames()
    {
        foreach (var (clip, frame) in playing!)
        {
            int node = clip.Tracks[0].NodeId;
            CanvasItem target = node switch { 1 => body, 4 => header, 6 => footer,
                _ => throw new InvalidDataException("Unregistered Config animation group.") };
            var authored = source.Nodes[node].HeaderValues;
            float x = authored[0], y = authored[1]; bool moved = false;
            foreach (var track in clip.Tracks)
            {
                if (track.NodeId != node) throw new InvalidDataException("Unexpected Config foreground animation owner.");
                double sampled = track.Curve.Sample(frame);
                float value = (float)sampled;
                switch (track.Channel)
                {
                    case 0: x = value; moved = true; break;
                    case 1: y = value; moved = true; break;
                    case 2: if (value != 0) throw new InvalidDataException("Unsupported Config animation depth."); break;
                    case 10:
                        byte alpha = (byte)(int)Math.Clamp(sampled * 255, 0, 255);
                        target.Modulate = new(1, 1, 1, alpha / 255f); break;
                    default: throw new InvalidDataException("Unsupported Config foreground channel.");
                }
            }
            if (moved)
            {
                var absolute = source.Rectangle(node).Position + new Vector2(x - authored[0], -(y - authored[1]));
                // Footer children were constructed at their real904 mount;
                // its source-follow root receives absolute coordinates.
                var sourcePosition = new Vector2((int)absolute.X, (int)absolute.Y);
                ((Node2D)target).Position = sourcePosition - (node == 6 ? new Vector2(0, 904) : Vector2.Zero);
            }
        }
    }

    public override void _ExitTree()
    { playing = null; completion?.TrySetResult(false); completion = null; }
}
