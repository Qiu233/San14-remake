using Godot;

/// <summary>The application's four immutable UI composition programs. Live
/// passes own their materials, targets and uniforms, and borrow these programs
/// until the Main canvas host retires its final graph.</summary>
internal sealed class UiSourceType6Shaders : IDisposable
{
    private readonly List<Shader> programs = [];
    private bool disposed;
    internal Shader Copy { get; }
    internal Shader Horizontal { get; }
    internal Shader Vertical { get; }
    internal Shader Output { get; }

    internal UiSourceType6Shaders()
    {
        try
        {
            Copy = Create(UiSourceType6Pass.CopyCode, "Type6EncodedRgbaCopy");
            Horizontal = Create(UiSourceType6Pass.KernelCode + UiSourceType6Pass.HorizontalCode, "Type6HorizontalRgb");
            Vertical = Create(UiSourceType6Pass.KernelCode + UiSourceType6Pass.VerticalCode, "Type6VerticalFinalBlend");
            Output = Create("""
                shader_type canvas_item;
                render_mode unshaded, blend_disabled;
                uniform sampler2D image : filter_nearest, repeat_disable;
                void fragment() { COLOR = texture(image, UV); }
                """, "UiPrefixEncodedReplacement");
        }
        catch { Dispose(); throw; }
    }

    private Shader Create(string code, string name)
    {
        var shader = new Shader();
        try
        {
            shader.Code = code; shader.ResourceName = name;
            programs.Add(shader); return shader;
        }
        catch { shader.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var shader in programs) shader.Dispose();
        programs.Clear();
    }
}
