using Godot;

/// <summary>Copies the preceding pixels in viewport coordinates while retaining
/// the original ordered copy/draw pair and its bounded copy region.</summary>
internal static class UiSourceBackBufferCopy
{
    internal static void Configure(CanvasItem owner, Rid copy, Rect2 rectangle,
        Transform2D? drawingTransform = null)
    {
        Transform2D parent = owner.GetGlobalTransformWithCanvas();
        Rect2 screen = (parent * (drawingTransform ?? Transform2D.Identity)) * rectangle;
        if (screen.HasArea())
        {
            // RD converts this rectangle to integer pixels. Round outwards
            // so a fractional edge cannot leave a drawn pixel uncopied.
            Vector2 start = screen.Position.Floor();
            screen = new Rect2(start, screen.End.Ceil() - start)
                .Intersection(owner.GetViewport().GetVisibleRect());
        }

        // Godot's RD Canvas renderer consumes the raw copy rectangle; its
        // culler also computes a transformed rectangle. Put this child copy
        // at viewport identity so both paths address the same actual pixels.
        // The draw child keeps its ordinary authored transform. Type6 prefix
        // viewports share this canvas and its identity viewport transform.
        RenderingServer.CanvasItemSetTransform(copy, parent.AffineInverse());
        // An empty enabled rectangle means full-screen copy in Godot.
        RenderingServer.CanvasItemSetCopyToBackbuffer(copy, screen.HasArea(), screen);
    }
}
