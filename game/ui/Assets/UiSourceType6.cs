using Godot;

/// <summary>A real source draw boundary, kept in its original Control tree.
/// Its Main-owned compositor supplies the preceding RGBA image; it never
/// advances input, simulation, audio or the native map renderer.</summary>
internal partial class UiSourceType6 : Control
{
    internal UiSourceType6Parameters Parameters { get; private set; } = null!;
    internal event Action? Changed;

    internal void Configure(UiSourceType6Parameters parameters)
    {
        Parameters = parameters;
        Modulate = new Color(1, 1, 1, parameters.AuthoredAlpha);
        MouseFilter = MouseFilterEnum.Ignore;
        SetNotifyTransform(true);
        VisibilityChanged += Invalidate;
    }
    private void Invalidate() => Changed?.Invoke();
    public override void _Notification(int what)
    {
        if (what == NotificationTransformChanged || what == NotificationResized) Invalidate();
    }
    internal UiSourceType6Geometry Geometry(Vector2I contextSize)
    {
        Transform2D screen = GetGlobalTransformWithCanvas();
        Vector2[] corners = [Vector2.Zero, new(Size.X, 0), new(0, Size.Y), Size];
        // This renderer owns a top-left Canvas and its texture UV orientation.
        return UiSourceType6Geometry.FromScreenCorners(corners.Select(p => screen * p).ToArray(), contextSize, false);
    }
}
