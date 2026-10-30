using Godot;

/// <summary>Rail-local source draws whose destination-alpha state is reconstructed
/// from the same mask and base pair. Other UI sprite users keep their own shader.</summary>
internal static class MapCommandRailShaders
{
    internal static Shader Cursor() => Create(false);
    internal static Shader Focus(bool submenu = false) => Create(true, submenu);

    private static Shader Create(bool clipped, bool submenu = false) => new()
    {
        ResourceName = clipped ? "MainCommandFocusBlend" : "MainCommandCursorBlend",
        Code = "shader_type canvas_item;\nrender_mode unshaded, blend_disabled;\n" +
            "const bool clipped_focus = " + (clipped ? "true" : "false") + ";\n" +
            "const bool submenu_focus = " + (submenu ? "true" : "false") + ";\n" + """
            uniform sampler2D screen_texture : hint_screen_texture, filter_nearest, repeat_disable;
            uniform sampler2D first_image : filter_linear, repeat_disable;
            uniform sampler2D mask_image : filter_linear, repeat_disable;
            uniform sampler2D second_mask_image : filter_linear, repeat_disable;
            uniform vec4 first_uv = vec4(0.0,0.0,1.0,1.0);
            uniform vec4 mask_uv = vec4(0.0,0.0,1.0,1.0);
            uniform vec4 second_mask_uv;
            uniform vec4 mask_rectangle;
            uniform vec4 second_mask_rectangle;
            uniform bool has_second_mask = false;
            uniform vec2 draw_size;
            uniform int blend_kind = 0;
            uniform vec3 material_color_scale = vec3(1.0);
            varying vec4 sprite_tint;
            varying vec2 local_position;
            void vertex() { sprite_tint = COLOR; local_position = VERTEX; }
            vec3 decode_srgb(vec3 v) {
                return mix(pow((v+0.055)/1.055,vec3(2.4)),v/12.92,lessThanEqual(v,vec3(0.04045)));
            }
            vec3 encode_srgb(vec3 v) {
                return mix(1.055*pow(max(v,vec3(0.0)),vec3(1.0/2.4))-0.055,
                    v*12.92,lessThanEqual(v,vec3(0.0031308)));
            }
            float alpha8(float a) { return floor(clamp(a,0.0,1.0)*255.0+0.5)/255.0; }
            void fragment() {
                vec4 sample_color = texture(TEXTURE,UV);
                vec4 src = sample_color*sprite_tint;
                src.rgb *= material_color_scale;
                vec4 dst = texture(screen_texture,SCREEN_UV);
                vec3 rgb = decode_srgb(dst.rgb);
                float alpha;
                if (clipped_focus) {
                    vec2 p = (local_position-mask_rectangle.xy)/mask_rectangle.zw;
                    float coverage = alpha8(texture(mask_image,mask_uv.xy+p*mask_uv.zw).a);
                    if (has_second_mask) {
                        vec2 q = (local_position-second_mask_rectangle.xy)/second_mask_rectangle.zw;
                        coverage = alpha8(coverage*texture(second_mask_image,second_mask_uv.xy+q*second_mask_uv.zw).a);
                    }
                    float base_alpha = texture(first_image,first_uv.xy+local_position/draw_size*first_uv.zw).a;
                    float prepared = alpha8(base_alpha*coverage+coverage*(1.0-coverage));
                    // 00918000 then 00098800 have left this destination alpha.
                    float destination_alpha = alpha8(base_alpha*prepared);
                    // Actual focus layer 00018800: RGB DEST_ALPHA/ONE, A DEST_ALPHA/ZERO.
                    rgb += src.rgb*destination_alpha;
                    // Submenu 00114800 instead uses SRC_ALPHA/ONE for alpha.
                    alpha = submenu_focus ? src.a*src.a+destination_alpha : src.a*destination_alpha;
                } else {
                    // Actual cursor 00118400: RGB SRC_ALPHA/ONE, A DEST_ALPHA/ONE.
                    rgb += src.rgb*src.a;
                    alpha = src.a*dst.a+dst.a;
                }
                COLOR = vec4(encode_srgb(rgb),alpha);
            }
            """
    };
}
