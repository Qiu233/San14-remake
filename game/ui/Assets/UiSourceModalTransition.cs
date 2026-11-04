using Godot;
using San14.Data;

/// <summary>Source curves for a page's explicitly registered foregrounds.
/// The page retains its roots and decides whether it owns the background.</summary>
internal partial class UiSourceModalTransition : Node
{
    private UiSourceLayout source = null!;
    private UiSourceCommonBlur backdrop = null!;
    private CanvasItem body = null!;
    private Node2D header = null!, footer = null!;
    private IReadOnlyDictionary<string, LcskAnimationClip> clips = null!;
    private (LcskAnimationClip Clip, float Frame)[]? playing;
    private TaskCompletionSource<bool>? completion;
    private IReadOnlyDictionary<int, (Node2D Root, Vector2 ContentMount)>? groups;
    private bool advancingBackdrop;
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

    internal void Configure(UiSourceLayout layout, UiSourceCommonBlur? common,
        IReadOnlyDictionary<int, (Node2D Root, Vector2 ContentMount)> registered)
    {
        source = layout; backdrop = common!; groups = registered;
        if (registered.Count == 0 || registered.Keys.Any(id => id is not (1 or 4 or 5 or 6 or 7)))
            throw new InvalidDataException("Unsupported source foreground registration.");
        var names = registered.Keys.SelectMany(id => new[] { ClipName(id) + "_in", ClipName(id) + "_out" }).ToHashSet();
        clips = LcskAnimationReader.Read(layout.Source.Layout, names).ToDictionary(clip => clip.Name);
        foreach (var (id, _) in registered)
            foreach (string suffix in new[] { "_in", "_out" })
                if (clips[ClipName(id) + suffix].Tracks.Any(track => track.NodeId != id ||
                    track.Channel is not (0 or 1 or 2 or 6 or 7 or 10)))
                    throw new InvalidDataException("Unsupported source foreground track.");
    }

    private static string ClipName(int group) => group switch
    {
        1 => "ani_dlg_slide_fade", 4 => "ani_header", 5 => "ani_header_info",
        6 => "ani_footer", 7 => "ani_narration",
        _ => throw new InvalidDataException("Unregistered foreground group.")
    };

    // Rebinding a retained page's table/guide adopts its already-entered
    // source geometry. This does not start a clock or complete a navigation.
    internal void ApplyShownState()
    {
        foreach (int node in groups!.Keys)
        {
            var clip = clips[ClipName(node) + "_in"];
            ApplyRegisteredFrame(node, clip, clip.EndFrame);
        }
    }

    // A rejected application callback retains the current page. Restore its
    // known drawn endpoint without completing another application operation.
    internal void RestoreShownState()
    {
        playing = null; completion?.TrySetResult(false); completion = null;
        ApplyShownState();
        backdrop?.RestoreShownState();
    }

    internal async Task<bool> Play(bool opening, bool includeBackground = true)
    {
        completion?.TrySetResult(false);
        string suffix = opening ? "_in" : "_out";
        playing = (groups is null ? new[] { "ani_dlg_slide_fade", "ani_header", "ani_footer" } : groups.Keys.Select(ClipName))
            .Select(name => clips[name + suffix]).Select(clip => (clip, clip.StartFrame)).ToArray();
        completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var foreground = completion.Task;
        ApplyFrames();
        // A borrowed background stays with its parent. When this owner is
        // input-disabled, its player still advances the owned blur clock.
        var backgroundOwner = includeBackground ? backdrop : null;
        bool priorClock = backgroundOwner?.AutomaticAnimationClock ?? false;
        if (backgroundOwner is not null) { backgroundOwner.AutomaticAnimationClock = false; advancingBackdrop = true; }
        try
        {
            var background = backgroundOwner?.PlayTransition(opening) ?? Task.FromResult(true);
            var results = await Task.WhenAll(foreground, background);
            return results.All(value => value);
        }
        finally
        {
            advancingBackdrop = false;
            if (backgroundOwner is not null && GodotObject.IsInstanceValid(backgroundOwner)) backgroundOwner.AutomaticAnimationClock = priorClock;
        }
    }

    public override void _Process(double delta) { if (AutomaticAnimationClock) Advance(delta); }
    internal void Advance(double delta)
    {
        if (advancingBackdrop) backdrop.AdvanceTransition(delta);
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
            if (groups is not null) { ApplyRegisteredFrame(node, clip, frame); continue; }
            CanvasItem target = node switch { 1 => body, 4 => header, 6 => footer,
                _ => throw new InvalidDataException("Unregistered Config animation group.") };
            var authored = source.Nodes[node].HeaderValues;
            float x = authored[0], y = authored[1]; bool moved = false;
            foreach (var track in clip.Tracks)
            {
                if (track.NodeId != node) throw new InvalidDataException("Unexpected Config foreground animation owner.");
                // AC7DA0 does not invoke the property writer for an empty
                // curve. Its independent sampler's default is not a write.
                if (track.Curve.Keys.Count == 0) continue;
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

    private void ApplyRegisteredFrame(int node, LcskAnimationClip clip, float frame)
    {
        var (target, mount) = groups![node];
        var authored = source.Nodes[node];
        float x = authored.HeaderValues[0], y = authored.HeaderValues[1];
        float sx = authored.HeaderValues[7], sy = authored.HeaderValues[8];
        bool followsTransform = false;
        foreach (var track in clip.Tracks)
        {
            if (track.Curve.Keys.Count == 0) continue;
            double sampled = track.Curve.Sample(frame);
            switch (track.Channel)
            {
                case 0: x = (float)sampled; followsTransform = true; break;
                case 1: y = (float)sampled; followsTransform = true; break;
                case 2: if (sampled != 0) throw new InvalidDataException("Unsupported foreground depth."); break;
                case 6: sx = (float)sampled; followsTransform = true; break;
                case 7: sy = (float)sampled; followsTransform = true; break;
                case 10:
                    byte alpha = (byte)(int)Math.Clamp(sampled * 255, 0, 255);
                    target.Modulate = new(1, 1, 1, alpha / 255f); break;
            }
        }
        // These registered source nodes have center anchors and an identity
        // parent. The original truncates the scaled half-size before its
        // final integer coordinate getter. Body group1 writes only alpha.
        if (followsTransform)
        {
            Vector2 position = new((int)(960 + x - (int)(authored.Value44 * sx / 2)),
                (int)(540 - y - (int)(authored.Value46 * sy / 2)));
            target.Scale = new(sx, sy);
            target.Position = position - target.Scale * mount;
        }
    }

    public override void _ExitTree()
    { playing = null; completion?.TrySetResult(false); completion = null; }
}
