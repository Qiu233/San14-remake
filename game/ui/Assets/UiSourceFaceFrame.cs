using Godot;
using San14.Data;

/// <summary>The kind-12 face frame owns a normal nine-part window. Its source
/// size setter changes the root and window dimensions without scaling margins.</summary>
internal partial class UiSourceFaceFrame : Control
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new((int)Size.X, (int)Size.Y);
        // The source frame constructor subtracts one from its caller's X.
        Position -= Vector2.Right;
        var layout = MapMarkerLayout.LoadNamed(repository, "cmn_face_frame");
        var source = new UiSourceLayout(layout, new Dictionary<int, UiSourceNodeGeometry>
        {
            [0] = new(Size: Size), [1] = new(Size: Size)
        });
        source.Art(this, 1, source.Rectangle(1), assets, shader);
        SetMeta("source_layout", layout.Source.ResourceUri);
    }
}
