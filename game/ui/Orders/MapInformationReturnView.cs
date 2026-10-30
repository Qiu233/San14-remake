using Godot;
using San14.Data;

/// <summary>The ordinary CMapInfoState exposes the resident map with its
/// single style-1 return control. It does not manufacture an entity page.</summary>
internal partial class MapInformationReturnView : Control
{
    private Control canvas = null!;
    private UiAssets? assets;
    private Shader? shader;
    private Action? returned;
    private Func<bool>? admitted;

    internal void Configure(GameResourceRepository repository, string locale, Func<bool> admitted, Action returned)
    {
        this.admitted = admitted; this.returned = returned;
        Name = "MapInformation"; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "MapInformationCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        assets = new(repository); shader = UiSourceSprite.CreateShader();
        var source = new UiSourceLayout(repository, 0xD8913AB2);
        var button = new UiSourceRoundButton { Name = "ReturnFromMapInformation",
            Position = source.Rectangle(1, new(0, 904)).Position,
            InputAdmission = () => this.admitted?.Invoke() == true && IsVisibleInTree() };
        canvas.AddChild(button);
        button.ConfigureScenarioReturn(repository, assets, shader, locale, RequestBack);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_camera_input_mode", 4);
        Resized += Fit; Fit();
    }

    internal void RequestBack()
    {
        if (admitted?.Invoke() == true) returned?.Invoke();
    }
    internal void Retire() { returned = null; admitted = null; Hide(); }
    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        returned = null; admitted = null;
        assets?.Dispose(); assets = null; shader?.Dispose(); shader = null;
    }
}
