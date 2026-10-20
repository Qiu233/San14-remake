using Godot;

/// <summary>Blurs the paused map beneath a modal detail page. The screen copy
/// is taken before the page's content, so its text stays sharp.</summary>
internal partial class MapDialogBackdrop : ColorRect
{
    private Shader? _shader;
    private ShaderMaterial? _material;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _shader = new Shader { Code = """
            shader_type canvas_item;
            uniform sampler2D map_image : hint_screen_texture, repeat_disable, filter_linear_mipmap;
            void fragment() {
                vec3 map = textureLod(map_image, SCREEN_UV, 4.0).rgb;
                COLOR = vec4(map * 0.82, 1.0);
            }
            """ };
        _material = new ShaderMaterial { Shader = _shader };
        Material = _material;
    }

    public override void _ExitTree()
    {
        Material = null;
        _material?.Dispose();
        _shader?.Dispose();
    }
}
