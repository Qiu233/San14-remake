using Godot;
using San14.Core;
using San14.Data;

/// <summary>
/// Visual ownership and area overlay for the playable map. Cells are current
/// simulation state. Source CPU colors can be supplied; final compositing and
/// line widths remain preview choices.
/// </summary>
public sealed class PlayableTerrainOverlay : IDisposable
{
    public ShaderMaterial Material { get; }
    public ImageTexture OwnershipTexture { get; }
    public ImageTexture AreaTexture { get; }
    public ImageTexture TintAlphaTexture { get; }
    private readonly Shader _shader;
    private bool _disposed;

    private PlayableTerrainOverlay(ShaderMaterial material, ImageTexture ownershipTexture,
        ImageTexture areaTexture, ImageTexture tintAlphaTexture, Shader shader)
        => (Material, OwnershipTexture, AreaTexture, TintAlphaTexture, _shader) =
            (material, ownershipTexture, areaTexture, tintAlphaTexture, shader);

    public static PlayableTerrainOverlay Build(ITerrainSurface source,
        MapWorldProjection projection, GameState world,
        IReadOnlyDictionary<int, Color>? sourceForceColors = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(world);
        MapWorldProjectionPolicy policy = projection.Policy;
        if (world.MapWidth != policy.Width || world.MapHeight != policy.Height ||
            !world.MapSha256.Equals(policy.MapSha256, StringComparison.OrdinalIgnoreCase) ||
            source.ProjectionSource.ResourceUri != policy.FieldResourceUri ||
            !source.ProjectionSource.DecodedSha256.Equals(policy.FieldDecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Playable territory overlay and source map projection differ.");
        int count = checked(policy.Width * policy.Height);
        if (world.Territory.Count != count)
            throw new InvalidDataException("Playable territory overlay requires one record per source hex.");

        ImageTexture? texture = null;
        ImageTexture? areaTexture = null;
        ImageTexture? tintAlphaTexture = null;
        Shader? shader = null;
        ShaderMaterial? material = null;
        try
        {
            using Image pixels = Image.CreateEmpty(policy.Width, policy.Height, false, Image.Format.Rgba8);
            using Image areas = Image.CreateEmpty(policy.Width, policy.Height, false, Image.Format.Rgba8);
            using Image tintAlphas = Image.CreateEmpty(policy.Width, policy.Height, false, Image.Format.Rgba8);
            for (int id = 0; id < count; id++)
            {
                if (!world.Territory.TryGetValue(id, out TerritoryState? cell) || cell.Id != id)
                    throw new InvalidDataException($"Playable territory overlay is missing source hex {id}.");
                if (cell.AreaId is int areaId)
                {
                    if (!world.Areas.ContainsKey(areaId) || areaId is < 0 or > 65534)
                        throw new InvalidDataException($"Playable territory hex {id} has an unsupported area ID {areaId}.");
                    int code = areaId + 1;
                    areas.SetPixel(id % policy.Width, id / policy.Width,
                        new Color((code & 255) / 255f, (code >> 8) / 255f, 0, 1));
                }
                if (cell.AreaId is null || cell.ForceId is null) continue;
                int forceId = cell.ForceId.Value;
                if (!world.Forces.ContainsKey(forceId) || forceId is < 0 or > 253)
                    throw new InvalidDataException($"Playable territory hex {id} has an unsupported force ID {forceId}.");
                Color color = sourceForceColors is not null &&
                    sourceForceColors.TryGetValue(forceId, out Color resolved)
                    ? resolved : PlayableMapVisualPalette.Force(forceId) with { A = 0.30f };
                tintAlphas.SetPixel(id % policy.Width, id / policy.Width,
                    new Color(color.A, 0, 0, 1));
                color.A = (forceId + 1) / 255f;
                pixels.SetPixel(id % policy.Width, id / policy.Width, color);
            }
            texture = ImageTexture.CreateFromImage(pixels)
                ?? throw new InvalidDataException("Could not upload current territory cells.");
            areaTexture = ImageTexture.CreateFromImage(areas)
                ?? throw new InvalidDataException("Could not upload current area cells.");
            tintAlphaTexture = ImageTexture.CreateFromImage(tintAlphas)
                ?? throw new InvalidDataException("Could not upload current force tint alpha cells.");
            shader = new Shader { ResourceName = "PlayableTerritoryPreviewShader",
                Code = ShaderHeader + source.SamplingCode + OverlayShaderCode
                    .Replace("/* SURFACE_VERTEX */", source.VertexInitializationCode, StringComparison.Ordinal)
                    .Replace("/* SURFACE_FRAGMENT */", source.FragmentInitializationCode, StringComparison.Ordinal) };
            material = new ShaderMaterial { Shader = shader, ResourceName = "PlayableTerritoryPreview" };
            source.BindSamplingParameters(material);
            material.SetShaderParameter("ownership", texture);
            material.SetShaderParameter("areas", areaTexture);
            material.SetShaderParameter("tint_alphas", tintAlphaTexture);
            material.SetShaderParameter("map_size", new Vector2I(policy.Width, policy.Height));
            material.SetShaderParameter("projection_origin", new Vector2((float)policy.OriginX, (float)policy.OriginZ));
            material.SetShaderParameter("projection_steps", new Vector2((float)policy.ColumnStep, (float)policy.RowStep));
            material.SetShaderParameter("even_column_row_offset", (float)policy.EvenColumnRowOffset);
            material.SetMeta("map_sha256", policy.MapSha256);
            material.SetMeta("projection_policy_id", policy.Id);
            material.SetMeta("source_formula_id", policy.SourceFormulaId ?? "");
            material.SetMeta("field_resource_uri", policy.FieldResourceUri);
            material.SetMeta("field_decoded_sha256", policy.FieldDecodedSha256);
            material.SetMeta("source_texture_resource_uri", source.ProjectionSource.ResourceUri);
            material.SetMeta("original_world_mapping_verified", false);
            material.SetMeta("original_materials_resolved", false);
            material.SetMeta("source_force_color_count", sourceForceColors?.Count ?? 0);
            material.SetMeta("original_overlay_composite_verified", false);
            material.SetMeta("overlay_convention", "current territory and areas; source-grid nearest-center Voronoi; per-force CPU tint alpha if supplied; provisional shader composite and line widths");
            return new(material, texture, areaTexture, tintAlphaTexture, shader);
        }
        catch
        {
            material?.Dispose(); shader?.Dispose(); texture?.Dispose(); areaTexture?.Dispose(); tintAlphaTexture?.Dispose();
            throw;
        }
    }

    private const string ShaderHeader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled;
        """;
    private const string OverlayShaderCode = """
        uniform sampler2D ownership : filter_nearest, repeat_disable;
        uniform sampler2D areas : filter_nearest, repeat_disable;
        uniform sampler2D tint_alphas : filter_nearest, repeat_disable;
        uniform ivec2 map_size;
        uniform vec2 projection_origin;
        uniform vec2 projection_steps;
        uniform float even_column_row_offset;
        varying vec2 source_xz;

        void vertex() { source_xz = VERTEX.xz; /* SURFACE_VERTEX */ }

        vec4 owner_at(ivec2 cell) {
            if (cell.x < 0 || cell.y < 0 || cell.x >= map_size.x || cell.y >= map_size.y)
                return vec4(0.0);
            return texelFetch(ownership, cell, 0);
        }

        int area_at(ivec2 cell) {
            if (cell.x < 0 || cell.y < 0 || cell.x >= map_size.x || cell.y >= map_size.y)
                return 0;
            vec2 encoded = texelFetch(areas, cell, 0).rg;
            return int(round(encoded.x * 255.0)) +
                256 * int(round(encoded.y * 255.0));
        }

        float tint_alpha_at(ivec2 cell) {
            if (cell.x < 0 || cell.y < 0 || cell.x >= map_size.x || cell.y >= map_size.y)
                return 0.0;
            return texelFetch(tint_alphas, cell, 0).r;
        }

        void fragment() {
            /* SURFACE_FRAGMENT */
            vec3 base = sample_original_terrain(source_xz);
            float column_position = (source_xz.x - projection_origin.x) / projection_steps.x;
            int center_column = int(floor(column_position + 0.5));
            float best_distance = 1e30;
            float second_distance = 1e30;
            ivec2 best_cell = ivec2(-1);
            ivec2 second_cell = ivec2(-1);
            // Nearest-center cells form the fitted hex Voronoi grid used by map picking.
            for (int dc = -1; dc <= 1; dc++) {
                int column = center_column + dc;
                float offset = (column % 2 == 0) ? even_column_row_offset : 0.0;
                float row_position = (source_xz.y - projection_origin.y) / projection_steps.y - offset;
                int center_row = int(floor(row_position + 0.5));
                for (int dr = -1; dr <= 1; dr++) {
                    int row = center_row + dr;
                    vec2 delta = source_xz - (projection_origin +
                        vec2(float(column) * projection_steps.x,
                             (float(row) + offset) * projection_steps.y));
                    float distance = length(delta);
                    if (distance < best_distance) {
                        second_distance = best_distance;
                        second_cell = best_cell;
                        best_distance = distance;
                        best_cell = ivec2(column, row);
                    } else if (distance < second_distance) {
                        second_distance = distance;
                        second_cell = ivec2(column, row);
                    }
                }
            }
            vec4 owner = owner_at(best_cell);
            vec4 adjacent = owner_at(second_cell);
            float tint_alpha = tint_alpha_at(best_cell);
            if (owner.a > 0.001) base = mix(base, owner.rgb, tint_alpha);
            float separation = second_distance - best_distance;
            float pixel_width = max(fwidth(separation), 0.001);
            bool force_edge = abs(owner.a - adjacent.a) > 0.001 &&
                (owner.a > 0.001 || adjacent.a > 0.001);
            int area = area_at(best_cell);
            int adjacent_area = area_at(second_cell);
            bool area_edge = area != adjacent_area && (area != 0 || adjacent_area != 0);
            float width = force_edge ? max(16.0, pixel_width * 1.4) :
                area_edge ? max(10.0, pixel_width * 1.2) : max(5.0, pixel_width * 0.85);
            float edge = 1.0 - smoothstep(0.0, width, separation);
            if (force_edge) {
                vec3 edge_color = owner.a > 0.001 ? owner.rgb : adjacent.rgb;
                base = mix(base, edge_color, edge * 0.82);
            } else if (area_edge) {
                base = mix(base, vec3(0.90, 0.86, 0.72), edge * 0.75);
            } else {
                float hex_pixels = projection_steps.x / max(fwidth(source_xz.x), 0.001);
                float grid_visibility = smoothstep(8.0, 24.0, hex_pixels);
                base = mix(base, vec3(0.80, 0.79, 0.68), edge * 0.23 * grid_visibility);
            }
            ALBEDO = base;
        }
        """;

    public void Dispose()
    {
        if (_disposed) return;
        Material.Dispose(); _shader.Dispose(); OwnershipTexture.Dispose(); AreaTexture.Dispose(); TintAlphaTexture.Dispose();
        _disposed = true;
    }
}
