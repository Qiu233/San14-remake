/// <summary>Original VS0 clip/TEXCOORD values with the D3D-to-Godot Vulkan
/// positive-height viewport adaptation. Only a non-indexed three-vertex draw,
/// firstVertex=0 is supported. The original fragment program is not rewritten.</summary>
public static class DeferredFullscreenShader
{
    public const string VertexSource = """
        #version 450
        layout(location=1) out vec4 source_clip;
        void main() {
            vec2 p;
            if (gl_VertexIndex == 0) p=vec2(-1.0,1.0);
            else if (gl_VertexIndex == 1) p=vec2(-1.0,-3.0);
            else p=vec2(3.0,1.0);
            source_clip=vec4(p,0.0,1.0);
            gl_Position=vec4(source_clip.x,-source_clip.y,source_clip.zw);
        }
        """;
}
