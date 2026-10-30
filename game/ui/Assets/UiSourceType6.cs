using Godot;

/// <summary>A real source draw boundary, kept in its original Control tree.
/// Its Main-owned compositor supplies the preceding RGBA image; it never
/// advances input, simulation, audio or the native map renderer.</summary>
internal partial class UiSourceType6 : Control
{
    internal UiSourceType6Parameters Parameters { get; private set; } = null!;
    internal event Action? Changed;
    internal event Action<UiSourceType6>? WeightChanged;

    internal void Configure(UiSourceType6Parameters parameters)
    {
        if (Parameters is null) VisibilityChanged += Invalidate;
        Parameters = parameters;
        Modulate = new Color(1, 1, 1, parameters.AuthoredAlpha);
        MouseFilter = MouseFilterEnum.Ignore;
        SetNotifyTransform(true);
        Invalidate();
    }
    // Authored Type6 curves animate weight, not opacity. Keep the two
    // retained captures and notify only their uniforms, never rebuild the tree.
    internal void SetWeightInput(float weight)
    {
        if (!float.IsFinite(weight) || weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
        if (Parameters.WeightInput == weight) return;
        Parameters = Parameters with { WeightInput = weight };
        WeightChanged?.Invoke(this);
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
