using Godot;

/// <summary>Ordinary map mouse input after the enclosing canonical Canvas
/// transform. Both source rectangles include their four endpoints. The
/// controller supplies the integer parent and tester rectangles together.</summary>
internal partial class SceneryMapLabelHitArea : TextureButton
{
    private Rect2I own, tester;
    private bool hasGeometry;
    internal Func<Vector2I?>? ReadCanonicalPointer { private get; set; }

    internal void SetCanonicalGeometry(Rect2I ownRectangle, Rect2I testerRectangle)
    {
        own = ownRectangle;
        tester = testerRectangle;
        hasGeometry = true;
    }

    public override bool _HasPoint(Vector2 point)
    {
        // Source8734B0 reads one global canonical integer mouse owner.
        // Reconstructing it from each inverse-transformed child point can
        // change an integer edge after Canvas scaling and cancellation.
        return ReadCanonicalPointer?.Invoke() is Vector2I cursor && ContainsCanonical(cursor);
    }

    internal bool ContainsCanonical(Vector2I point)
        => hasGeometry && Contains(own,point.X,point.Y) && Contains(tester,point.X,point.Y);

    private static bool Contains(Rect2I rectangle, int x, int y)
        => x >= rectangle.Position.X && y >= rectangle.Position.Y &&
            x <= rectangle.End.X && y <= rectangle.End.Y;
}
