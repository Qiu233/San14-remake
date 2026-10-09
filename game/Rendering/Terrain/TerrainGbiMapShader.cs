/// <summary>Original DC52FD1C PS18/52 detail and PS54 coarse surfaces before G-buffer packing and lighting.</summary>
public static class TerrainGbiMapShader
{
    // Compatibility lacks the explicit coarse derivative builtins. That backend
    // uses a named preview fallback, while RD uses the original coarse operation.
    public static string CreateSamplingCode(bool coarseDerivatives, bool extraAlbedo = false) => SamplingCode
        .Replace("GBI_DX", coarseDerivatives ? "dFdxCoarse" : "dFdx", StringComparison.Ordinal)
        .Replace("GBI_DY", coarseDerivatives ? "dFdyCoarse" : "dFdy", StringComparison.Ordinal)
        .Replace("/* GBI_EX_ALBEDO */", extraAlbedo ? "detail.albedo_occlusion.rgb*=texture(gbi_extra_albedo,uv).rgb;" : "", StringComparison.Ordinal);

    public const string SamplingCode = """
        uniform float gbi_cliff_threshold;
        uniform vec2 gbi_near_far;
        uniform vec3 gbi_eye;
        varying flat vec2 gbi_frequency_count;
        varying flat float gbi_coarse_patch;
        varying vec2 gbi_phase;
        varying vec2 gbi_control_uv;
        varying vec3 gbi_source_position;
        varying vec3 gbi_source_normal;

        vec3 gbi_map_plane_weights(vec3 n, float threshold) {
            vec3 a = abs(n);
            float m = max(max(a.z,a.y),a.x);
            if (threshold + 0.0001 >= m) {
                if (m == a.x) { return vec3(1.0,0.0,0.0); }
                if (m == a.y) { return vec3(0.0,1.0,0.0); }
                return vec3(0.0,0.0,1.0);
            }
            vec3 w = normalize(max(a-vec3(threshold),vec3(0.0)));
            return w / ((w.y+w.x)+w.z);
        }
        GbiMaterialSample gbi_map_add(GbiMaterialSample a, GbiMaterialSample b, float w) {
            GbiMaterialSample r;
            r.albedo_occlusion = a.albedo_occlusion + b.albedo_occlusion*w;
            r.normal_height = a.normal_height + b.normal_height*w;
            r.reflectance_roughness = a.reflectance_roughness + b.reflectance_roughness*w;
            return r;
        }
        GbiMaterialSample sample_original_gbi(vec3 position, vec3 n, vec2 uv, vec2 phase, vec3 eye) {
            // Original group2/PS54 directly samples the far surface. It has no
            // detail-array, triplanar or eye-distance blend before extra RGB.
            if (gbi_coarse_patch>0.5) {
                GbiMaterialSample detail = GbiMaterialSample(texture(gbi_far_albedo_occlusion,uv),
                    vec4(texture(gbi_far_normal_height,uv).rgb,0.0),
                    texture(gbi_far_reflectance_roughness,uv));
                /* GBI_EX_ALBEDO */
                return detail;
            }
            vec3 w = gbi_map_plane_weights(n,gbi_cliff_threshold);
            vec3 a=abs(n);
            bool dominant=gbi_cliff_threshold+0.0001>=max(max(a.z,a.y),a.x);
            // Original computes derivatives after per-fragment plane selection,
            // outside the branch. Adjacent fragments can select different planes.
            vec2 selected_plane=gbi_displacement_plane(position,n);
            vec2 selected_dx=GBI_DX(selected_plane);
            vec2 selected_dy=GBI_DY(selected_plane);
            vec3 dx=GBI_DX(position);
            vec3 dy=GBI_DY(position);
            GbiMaterialSample detail = GbiMaterialSample(vec4(0.0),vec4(0.0),vec4(0.0));
            if (dominant) {
                detail=gbi_sample_surface(uv,phase,gbi_frequency_count.x,gbi_frequency_count.y,
                    selected_plane,selected_dx,selected_dy);
            } else {
            if (w.x>0.0) {
                detail=gbi_map_add(detail,gbi_sample_surface(uv,phase,gbi_frequency_count.x,gbi_frequency_count.y,
                    position.yz,dx.yz,dy.yz),w.x);
            }
            if (w.y>0.0) {
                detail=gbi_map_add(detail,gbi_sample_surface(uv,phase,gbi_frequency_count.x,gbi_frequency_count.y,
                    position.xz,dx.xz,dy.xz),w.y);
            }
            if (w.z>0.0) {
                detail=gbi_map_add(detail,gbi_sample_surface(uv,phase,gbi_frequency_count.x,gbi_frequency_count.y,
                    position.xy,dx.xy,dy.xy),w.z);
            }
            }
            float distance_to_eye=length(eye-position);
            if (distance_to_eye>=gbi_near_far.x) {
                float far_weight=clamp((distance_to_eye-gbi_near_far.x)/(gbi_near_far.y-gbi_near_far.x),0.0,1.0);
                vec4 far_ao=texture(gbi_far_albedo_occlusion,uv);
                vec3 far_n=texture(gbi_far_normal_height,uv).rgb;
                vec4 far_rr=texture(gbi_far_reflectance_roughness,uv);
                detail.albedo_occlusion+=(far_ao-detail.albedo_occlusion)*far_weight;
                // Original PS18 only carries normal RGB after the corner height blend.
                detail.normal_height.rgb+=(far_n-detail.normal_height.rgb)*far_weight;
                detail.reflectance_roughness+=(far_rr-detail.reflectance_roughness)*far_weight;
            }
            /* GBI_EX_ALBEDO */
            return detail;
        }
        vec3 sample_original_terrain(vec2 source_xz) {
            return sample_original_gbi(gbi_source_position,gbi_source_normal,gbi_control_uv,gbi_phase,
                gbi_eye).albedo_occlusion.rgb;
        }
        """;
    public const string VertexInitializationCode = """
        gbi_phase=UV2;
        gbi_frequency_count=CUSTOM0.xy;
        gbi_coarse_patch=CUSTOM0.z;
        gbi_control_uv=UV;
        gbi_source_position=VERTEX;
        gbi_source_normal=NORMAL;
        """;
    public const string FragmentInitializationCode = """
        if (gbi_center_discarded(texture(gbi_land_control,gbi_control_uv).r)) { discard; }
        """;
}
