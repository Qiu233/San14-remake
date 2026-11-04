using Godot;
using San14.Data;

/// <summary>The original cursor's RGB curve, owned by the current hovered row.</summary>
internal partial class MapCommandRailCursor : Control
{
    private CanvasItem artwork = null!;
    private LcskAnimationClip clip = null!;
    private double elapsed;

    internal void Configure(CanvasItem art, LcskAnimationClip animation)
    {
        artwork = art; clip = animation; MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
    }

    internal void Begin()
    {
        elapsed = 0; Show(); Apply(clip.StartFrame); SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) { SetProcess(false); return; }
        elapsed += delta; Apply(clip.FrameAfter(elapsed));
        if (elapsed >= clip.DurationSeconds) SetProcess(false);
    }

    private void Apply(float frame)
    {
        Color tint = Colors.White;
        foreach (var track in clip.Tracks)
        {
            if (track.NodeId != 2 || track.Curve.Keys.Count == 0) continue;
            float value = (byte)(int)Math.Clamp(track.Curve.Sample(frame) * 255d, 0d, 255d) / 255f;
            switch (track.Channel)
            {
                case 16: tint.R = value; break;
                case 17: tint.G = value; break;
                case 18: tint.B = value; break;
                case 19: tint.A = value; break;
                default: throw new InvalidDataException("main_command.cursor.animation_channel");
            }
        }
        artwork.Modulate = tint;
    }
}
