using Godot;

/// <summary>Owned remake presentation clock; no original process clock or
/// Core period time is substituted for an animation player.</summary>
public partial class OriginalRemakeArmyClock : Node
{
    public Func<bool>? IsRunning { get; set; }
    public Action<float>? Advance { get; set; }
    public override void _Process(double delta)
    {
        if (IsRunning?.Invoke() == true && double.IsFinite(delta) && delta >= 0)
            Advance?.Invoke((float)delta);
    }
}
