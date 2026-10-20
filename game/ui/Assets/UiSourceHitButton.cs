using Godot;

/// <summary>BaseButton input for source artwork supplied by child controls.</summary>
internal partial class UiSourceHitButton : TextureButton
{
    public override bool _HasPoint(Vector2 point) => new Rect2(Vector2.Zero, Size).HasPoint(point);
}
