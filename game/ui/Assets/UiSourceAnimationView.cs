using Godot;
using San14.Data;

/// <summary>Authored pane hierarchy and curves for the map's name/number
/// layouts. Content and clip selection stay with each actual view owner.</summary>
internal partial class UiSourceAnimationView : Control
{
    protected UiSourceLayout AnimationLayout = null!;
    protected readonly Dictionary<int, Node2D> AnimationPanes = [];
    protected readonly Dictionary<int, byte> AnimationPaneAlphas = [];
    protected readonly HashSet<int> HiddenAnimationPanes = [];
    private readonly Dictionary<int, Transform2D> transforms = [];
    private readonly Dictionary<int, byte> authoredAlphas = [];
    private IReadOnlyDictionary<string, LcskAnimationClip> clips = null!;
    private LcskAnimationClip? playing;
    private double elapsed;
    internal bool IsPlaying => playing is not null;
    internal bool AutomaticAnimationClock { get; set; } = true;

    protected void ConfigureAnimation(UiSourceLayout source, IReadOnlySet<string> names, IEnumerable<int> nodes)
        => ConfigureAnimation(source, LcskAnimationReader.Read(source.Source.Layout, names)
            .ToDictionary(clip => clip.Name), nodes);

    protected void ConfigureAnimation(UiSourceLayout source,
        IReadOnlyDictionary<string, LcskAnimationClip> preparedClips, IEnumerable<int> nodes)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnimationLayout = source; Size = source.Rectangle(0).Size;
        clips = preparedClips;
        foreach (int index in nodes)
        {
            var node = source.Nodes[index]; var transform = source.NodeTransform(index);
            var pane = new Node2D { Name = node.Name };
            if (node.ParentIndex is int parent && AnimationPanes.TryGetValue(parent, out var parentPane))
            {
                transform = source.NodeTransform(parent).AffineInverse() * transform;
                parentPane.AddChild(pane);
            }
            else AddChild(pane);
            pane.Transform = transform;
            AnimationPanes.Add(index, pane); transforms.Add(index, transform);
            byte alpha = AuthoredPaneAlpha(index);
            authoredAlphas.Add(index, alpha); AnimationPaneAlphas.Add(index, alpha);
        }
        Hide();
    }

    protected virtual byte AuthoredPaneAlpha(int index) => 255;

    protected void PlayAnimation(string name)
    {
        playing = clips[name]; elapsed = 0;
        foreach (var (index, pane) in AnimationPanes)
            pane.Visible = playing.NodeIds.Contains(index) && !HiddenAnimationPanes.Contains(index);
        Show(); ApplyFrame(playing, playing.StartFrame);
    }

    public override void _Process(double delta)
    {
        if (AutomaticAnimationClock) AdvanceAnimation(delta);
    }

    internal void AdvanceAnimation(double delta)
    {
        if (playing is not { } clip) return;
        elapsed += delta; ApplyFrame(clip, clip.FrameAfter(elapsed));
        if (elapsed >= clip.DurationSeconds) { playing = null; Hide(); }
    }

    internal void StopAnimation()
    {
        if (playing is { } clip) ApplyFrame(clip, clip.EndFrame);
        playing = null; Hide();
    }

    /// <summary>A media-clock owner samples a source clip directly. Holding its
    /// last sample is separate from the automatic name/number retirement path.</summary>
    protected void ApplyAnimationAt(string name, double seconds, bool repeat = false, bool reveal = true)
    {
        var clip = clips[name];
        foreach (var (index, pane) in AnimationPanes)
            pane.Visible = clip.NodeIds.Contains(index) && !HiddenAnimationPanes.Contains(index);
        if (repeat && clip.DurationSeconds > 0) seconds %= clip.DurationSeconds;
        if (reveal) Show();
        ApplyFrame(clip, clip.FrameAfter(Math.Max(0, seconds)));
    }

    private void ApplyFrame(LcskAnimationClip clip, float frame)
    {
        foreach (var (index, pane) in AnimationPanes)
        {
            var node = AnimationLayout.Nodes[index]; var f = node.HeaderValues;
            float x = f[0], y = f[1], scaleX = f[7], scaleY = f[8];
            byte alpha = authoredAlphas[index];
            foreach (var track in clip.Tracks)
            {
                if (track.NodeId != index) continue;
                float value = (float)track.Curve.Sample(frame);
                switch (track.Channel)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: break;
                    case 6: scaleX = value; break;
                    case 7: scaleY = value; break;
                    case 10: alpha = (byte)(int)Math.Clamp(value * 255, 0, 255); break;
                    default: throw new InvalidDataException("map-action.animation_channel");
                }
            }
            var original = transforms[index]; var current = original;
            current.X *= scaleX / f[7]; current.Y *= scaleY / f[8];
            byte anchor = (byte)node.Value48;
            var pivot = new Vector2((anchor >> 4) * .5f * node.Value44, (anchor & 15) * .5f * node.Value46);
            current.Origin += original.BasisXform(pivot) - current.BasisXform(pivot) + new Vector2(x - f[0], -(y - f[1]));
            AnimationPaneAlphas[index] = alpha;
            pane.Transform = current; pane.Modulate = new(1, 1, 1, alpha / 255f);
        }
    }
}
