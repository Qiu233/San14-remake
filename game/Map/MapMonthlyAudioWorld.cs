using Godot;

/// <summary>The map's audio listener follows the same published camera as the
/// native display, including when the preview Camera3D is not being rendered.</summary>
internal partial class MapMonthlyAudioWorld : Node3D
{
    internal Func<Transform3D?>? ReadCamera { get; init; }
    private readonly AudioListener3D _listener = new() { Name = "MapEffectListener" };

    public override void _Ready()
    {
        AddChild(_listener);
        UpdateListener();
        _listener.MakeCurrent();
    }

    public override void _Process(double delta) => UpdateListener();

    private void UpdateListener()
    {
        if (ReadCamera?.Invoke() is { } actual) _listener.GlobalTransform = actual;
    }
}
