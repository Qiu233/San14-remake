/// <summary>
/// Source-specific GBI material code for DC52FD1C PS13 / DS141. Caller supplies
/// verified bindings and upstream coordinates. Sampler declarations are omitted
/// deliberately: filtering, addressing, scene selection and final light pass
/// are separate contracts. This block is not automatically activated on the map.
/// </summary>
public static class TerrainGbiShader
{
    // Shared by the Godot spatial shader and the native numerical GPU check.
    // NormalHeight.a is used for blending; only its RGB is a PS output input.
    public const string MathCode = """
        struct GbiMaterialSample {
            vec4 albedo_occlusion;
            vec4 normal_height;
            vec4 reflectance_roughness;
        };
        vec4 gbi_corner_weights(vec2 f) {
            return vec4((1.0-f.y)*(1.0-f.x), (1.0-f.y)*f.x, (1.0-f.x)*f.y, f.y*f.x);
        }
        vec2 gbi_corner_uv(vec2 uv, vec2 f, float count, vec2 corner) {
            return uv + (corner-f) / (count-1.0);
        }
        float gbi_quad_sum(float c00, float c10, float c01, float c11, vec4 w) {
            return ((c10*w.y + c00*w.x) + c01*w.z) + c11*w.w;
        }
        vec4 gbi_quad_sum4(vec4 c00, vec4 c10, vec4 c01, vec4 c11, vec4 w) {
            return ((c10*w.y + c00*w.x) + c01*w.z) + c11*w.w;
        }
        GbiMaterialSample gbi_height_blend(GbiMaterialSample a, GbiMaterialSample b, float beta, float sharpness_b) {
            float q_a = a.normal_height.a;
            float q_b = (beta-0.5)*2.0 + b.normal_height.a;
            float threshold = max(q_a,q_b) - (1.0-sharpness_b);
            float w_a = max(q_a-threshold,0.0);
            float w_b = max(q_b-threshold,0.0);
            float weight_sum = w_b+w_a;
            GbiMaterialSample result;
            result.albedo_occlusion = (b.albedo_occlusion*w_b+a.albedo_occlusion*w_a)/weight_sum;
            result.normal_height = (b.normal_height*w_b+a.normal_height*w_a)/weight_sum;
            result.reflectance_roughness = (b.reflectance_roughness*w_b+a.reflectance_roughness*w_a)/weight_sum;
            return result;
        }
        vec3 gbi_transform_normal(vec3 encoded_normal, vec3 source_normal, vec3 source_tangent, float handedness) {
            vec3 nt = normalize(encoded_normal*2.0-1.0);
            vec3 n = normalize(source_normal);
            vec3 t = normalize(source_tangent);
            return cross(t,n)*handedness*nt.x + t*nt.y + n*nt.z;
        }
        vec2 gbi_encode_oct(vec3 n) {
            vec3 q = n/(abs(n.x)+abs(n.y)+abs(n.z));
            vec2 xy = q.xy;
            if (q.z<0.0) {
                vec2 s = vec2(q.x>=0.0 ? 1.0 : -1.0, q.y>=0.0 ? 1.0 : -1.0);
                xy=(1.0-abs(q.yx))*s;
            }
            return xy*0.5+0.5;
        }
        vec4 gbi_reflectance_target(vec4 reflectance_roughness, vec4 specular_scale) {
            return reflectance_roughness*specular_scale*vec4(1.0,1.0,1.0,0.5);
        }
        float gbi_height_sum(vec4 heights, vec4 scales, vec4 biases, vec2 f) {
            vec4 h = heights*scales+biases;
            return gbi_quad_sum(h.x,h.y,h.z,h.w,gbi_corner_weights(f));
        }
        vec2 gbi_displacement_plane(vec3 p, vec3 n) {
            vec3 a = abs(n);
            if (a.x>=a.y && a.x>=a.z) { return p.yz; }
            if (a.y>=a.z) { return p.xz; }
            return p.xy;
        }
        vec3 gbi_displace(vec3 p, vec3 n, float height, float view_depth, vec3 distance_lod) {
            if (-view_depth < distance_lod.x) {
                float fade = clamp((view_depth*distance_lod.y+1.0)*distance_lod.z,0.0,1.0);
                return p+n*(fade*height);
            }
            return p;
        }
        """;

    // Required caller declarations:
    // sampler2D gbi_land_control;
    // sampler2DArray gbi_albedo_occlusion, gbi_normal_height, gbi_reflectance_roughness;
    // int gbi_layers[255]; float gbi_sharpness[255], gbi_scale_u[255], gbi_scale_v[255];
    // For the displacement helper also float gbi_tess_scale[255], gbi_tess_bias[255].
    // These are logical typed inputs, not the original packed CB0 offsets.
    public const string SamplingCode = """
        GbiMaterialSample gbi_sample_palette(int index, vec2 p, vec2 dx, vec2 dy) {
            vec2 scale = vec2(gbi_scale_u[index],gbi_scale_v[index]);
            vec3 uv_layer = vec3(p/scale,float(gbi_layers[index]));
            GbiMaterialSample result;
            result.albedo_occlusion = textureGrad(gbi_albedo_occlusion,uv_layer,dx/scale,dy/scale);
            result.normal_height = textureGrad(gbi_normal_height,uv_layer,dx/scale,dy/scale);
            result.reflectance_roughness = textureGrad(gbi_reflectance_roughness,uv_layer,dx/scale,dy/scale);
            return result;
        }
        bool gbi_center_discarded(float control_red) {
            int index = int(control_red*255.0);
            return gbi_scale_u[index]<=1.0;
        }
        GbiMaterialSample gbi_sample_corner(vec3 control, float beta, vec2 p, vec2 dx, vec2 dy) {
            int a = int(control.r*255.0);
            int b = int(control.g*255.0);
            if (b>=255) { return gbi_sample_palette(a,p,dx,dy); }
            if (control.b>0.99) { return gbi_sample_palette(b,p,dx,dy); }
            GbiMaterialSample sample_a = gbi_sample_palette(a,p,dx,dy);
            GbiMaterialSample sample_b = gbi_sample_palette(b,p,dx,dy);
            return gbi_height_blend(sample_a,sample_b,beta,gbi_sharpness[b]);
        }
        GbiMaterialSample gbi_sample_surface(vec2 control_uv, vec2 phase, float frequency, float count,
                vec2 p, vec2 dx, vec2 dy) {
            vec2 f = fract(phase*frequency);
            vec4 w = gbi_corner_weights(f);
            vec3 c00 = texture(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(0.0,0.0))).rgb;
            vec3 c10 = texture(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(1.0,0.0))).rgb;
            vec3 c01 = texture(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(0.0,1.0))).rgb;
            vec3 c11 = texture(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(1.0,1.0))).rgb;
            float beta = gbi_quad_sum(c00.b,c10.b,c01.b,c11.b,w);
            GbiMaterialSample v00 = gbi_sample_corner(c00,beta,p,dx,dy);
            GbiMaterialSample v10;
            if (all(equal(c00,c10))) { v10=v00; }
            else { v10=gbi_sample_corner(c10,beta,p,dx,dy); }
            GbiMaterialSample v01;
            if (all(equal(c00,c01))) { v01=v00; }
            else if (all(equal(c10,c01))) { v01=v10; }
            else { v01=gbi_sample_corner(c01,beta,p,dx,dy); }
            GbiMaterialSample v11;
            if (all(equal(c00,c11))) { v11=v00; }
            else if (all(equal(c10,c11))) { v11=v10; }
            else if (all(equal(c01,c11))) { v11=v01; }
            else { v11=gbi_sample_corner(c11,beta,p,dx,dy); }
            GbiMaterialSample result;
            result.albedo_occlusion = gbi_quad_sum4(v00.albedo_occlusion,v10.albedo_occlusion,v01.albedo_occlusion,v11.albedo_occlusion,w);
            result.normal_height = gbi_quad_sum4(v00.normal_height,v10.normal_height,v01.normal_height,v11.normal_height,w);
            result.reflectance_roughness = gbi_quad_sum4(v00.reflectance_roughness,v10.reflectance_roughness,v01.reflectance_roughness,v11.reflectance_roughness,w);
            return result;
        }
        float gbi_sample_displacement_height(vec2 control_uv, vec2 f, float count, vec3 p, vec3 n) {
            // DS141 SampleLevel, R only. No PS A/B blending here.
            vec4 control_r = vec4(
                textureLod(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(0.0,0.0)),0.0).r,
                textureLod(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(1.0,0.0)),0.0).r,
                textureLod(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(0.0,1.0)),0.0).r,
                textureLod(gbi_land_control,gbi_corner_uv(control_uv,f,count,vec2(1.0,1.0)),0.0).r);
            ivec4 index = ivec4(control_r*255.0);
            vec2 plane = gbi_displacement_plane(p,n);
            vec4 height;
            vec4 scale;
            vec4 bias;
            for (int i=0;i<4;i++) {
                int j=index[i];
                vec2 uv = plane/vec2(gbi_scale_u[j],gbi_scale_v[j]);
                height[i]=textureLod(gbi_normal_height,vec3(uv,float(gbi_layers[j])),0.0).a;
                scale[i]=gbi_tess_scale[j];
                bias[i]=gbi_tess_bias[j];
            }
            return gbi_height_sum(height,scale,bias,f);
        }
        """;
}
