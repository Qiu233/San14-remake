using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The caller's terrain raster state. The terrain source does not
/// invent the preceding model's inherited state or a current View.</summary>
public sealed record OriginalD3D11TerrainInputRaster(uint CullMode, bool FrontCounterClockwise,
    uint DepthCompare, uint ColorMasks, bool StencilEnabled, uint StencilReference, float DepthClear);

public sealed record OriginalD3D11TerrainInputBatch(int Group, int StartInstance, int InstanceCount, bool Coarse);

/// <summary>A selected initial submission supplied by the camera/terrain owner.
/// Null batches selects the ordinary near-only stream; explicit batches retain
/// the caller's order and near/coarse selection.</summary>
public sealed record OriginalD3D11TerrainInputFrame(int Width, int Height,
    IReadOnlyList<TerrainPatchInput> Patches, OriginalDeferredCameraInputs Camera,
    Matrix4x4 Projection, OriginalD3D11TerrainInputRaster Raster,
    IReadOnlyList<OriginalD3D11TerrainInputBatch>? Batches = null);

public sealed record OriginalD3D11TerrainTextureInputSource(string Stage, uint Slot, string Filename,
    string ResourceUri, string DecodedSha256, int ImageIndex, byte PixelFormat, int LayerCount,
    int MipCount, uint DxgiFormat, G1TextureSamplerRecipe Sampler, bool InitialControl);

public sealed record OriginalD3D11TerrainPreparedInputs(int GridSize, int PatchCount, int IndexCount,
    TerrainGbufferDrawInputs DrawInputs);

/// <summary>First preparation of the stock ordinary Landscape input from the
/// user's installed resources. Generated DXBC, texels and streams are local
/// cache contents, not distributable project assets. Load this source owner
/// once and call WriteNativeInputs when preparing a cache; normal requests use
/// the already prepared native resources. Camera selection, inherited raster,
/// later terrain setters and the other Main input packages belong to callers.</summary>
public sealed class OriginalD3D11TerrainInputPreparation
{
    private sealed record TextureInput(string Stage, uint Slot, string Filename,
        TerrainTextureBinding Binding, uint Format, G1TextureSamplerRecipe Sampler, bool InitialControl);

    private readonly LoadedRenderGraphTargets targets;
    private readonly TextureInput[] textures;

    private OriginalD3D11TerrainInputPreparation(LoadedSceneResources scene, LoadedTerrainResources terrain,
        LoadedTerrainParameterResources parameters, LoadedTerrainGbiMapResources material,
        LoadedTerrainInitialHeightResources height, OriginalTerrainGbufferSourceParameters scalar,
        OriginalTerrainGbufferMaterialDrawSource materialDraw, byte callFlags, LoadedRenderGraphTargets graphTargets)
    {
        Scene = scene; Terrain = terrain; Parameters = parameters; Material = material;
        Height = height; Scalar = scalar; MaterialDraw = materialDraw; OrdinaryCallFlags = callFlags;
        targets = graphTargets;
        var rows = new List<TextureInput>();
        TerrainTextureBinding fieldHeight = height.Field.Bindings.Single(b => b.Slot == 0);
        var heightSampler = G1TextureSamplerReader.ReadRetainedSource(fieldHeight.Image);
        Add("VS", 8, fieldHeight);
        rows.Add(new("PS", 3, "ps-t3.tex", material.InitialControl.Binding, 28, heightSampler, true));
        Add("PS", 4, material.Field.Bindings.Single(b => b.Slot == 2));
        Add("PS", 5, material.Field.Bindings.Single(b => b.Slot == 3));
        Add("PS", 6, material.Field.Bindings.Single(b => b.Slot == 4));
        Add("PS", 7, material.Root.Bindings.Single(b => b.Slot == 0));
        Add("PS", 8, material.Root.Bindings.Single(b => b.Slot == 1));
        Add("PS", 9, material.Root.Bindings.Single(b => b.Slot == 2));
        if (material.Auxiliary is not null) Add("PS", 10, material.Auxiliary.Bindings.Single(b => b.Slot == 0));
        textures = rows.ToArray();
        TextureSources = Array.AsReadOnly(textures.Select(t => new OriginalD3D11TerrainTextureInputSource(
            t.Stage, t.Slot, t.Filename, t.Binding.Resource.Source.ResourceUri,
            t.Binding.Resource.Source.DecodedSha256, t.Binding.Image.Index, t.Binding.Image.PixelFormat,
            t.InitialControl ? 1 : t.Binding.Image.LayerCount, t.InitialControl ? 1 : t.Binding.Image.MipCount,
            t.Format, t.Sampler, t.InitialControl)).ToArray());

        void Add(string stage, uint slot, TerrainTextureBinding binding)
        {
            G1TextureImage image = binding.Image;
            bool srgb = image.RawHeader.Length >= 20 && (image.RawHeader[19] & 1) != 0;
            uint format = image.PixelFormat switch
            {
                0x00 => srgb ? 29u : 28u, 0x01 => srgb ? 91u : 87u, 0x02 when !srgb => 41u,
                0x59 => srgb ? 72u : 71u, 0x5B => srgb ? 78u : 77u,
                _ => throw new NotSupportedException($"Actual terrain texture format{image.PixelFormat:X2}")
            };
            rows.Add(new(stage, slot, $"{stage.ToLowerInvariant()}-t{slot}.tex", binding, format,
                G1TextureSamplerReader.ReadRetainedSource(image), false));
        }
    }

    public LoadedSceneResources Scene { get; }
    public LoadedTerrainResources Terrain { get; }
    public LoadedTerrainParameterResources Parameters { get; }
    public LoadedTerrainGbiMapResources Material { get; }
    public LoadedTerrainInitialHeightResources Height { get; }
    public OriginalTerrainGbufferSourceParameters Scalar { get; }
    public OriginalTerrainGbufferMaterialDrawSource MaterialDraw { get; }
    public byte OrdinaryCallFlags { get; }
    public LoadedRenderGraphTargets Targets => targets;
    public IReadOnlyList<OriginalD3D11TerrainTextureInputSource> TextureSources { get; }

    public static OriginalD3D11TerrainInputPreparation Load(OriginalGameRepository repository,
        LoadedSceneResources scene, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene);
        var terrain = OriginalTerrainResources.Load(repository, "KIDSSystemResource", scene.TerrainBundle.Source.FileId);
        var parameters = OriginalTerrainParameterResources.Read(scene);
        var material = OriginalTerrainGbiMapResources.LoadStockDefault(repository, terrain, parameters, locale);
        var height = OriginalTerrainInitialHeightResources.Load(repository, terrain, parameters);
        var scalar = OriginalTerrainGbufferSourceParameters.ReadSourceDefaults(repository, locale, material);
        var materialDraw = OriginalTerrainGbufferMaterialDrawSource.Load(repository, locale, material);
        byte callFlags = OriginalMainSceneNode51Source.Load(repository, locale, 56).InitialCallFlags;
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var targets = OriginalRenderGraphTargetResources.Read(graph, 53, repository.LoadObjects("system", 0x93FB3280));
        return new(scene, terrain, parameters, material, height, scalar, materialDraw, callFlags, targets);
    }

    /// <summary>Writes only the base terrain input directory. The caller must
    /// supply the accepted initial camera, patches and raster; this method does
    /// not read an exported receipt or select a fixed position/date/frame.</summary>
    public OriginalD3D11TerrainPreparedInputs WriteNativeInputs(string directory,
        OriginalD3D11TerrainInputFrame frame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(frame.Camera); ArgumentNullException.ThrowIfNull(frame.Raster);
        ArgumentNullException.ThrowIfNull(frame.Patches);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Patches.Count == 0)
            throw new InvalidDataException("Initial terrain input requires positive viewport dimensions and selected patches.");
        int grid = checked((int)frame.Patches[0].GridSize);
        if (frame.Patches.Any(p => p.GridSize != grid))
            throw new InvalidDataException("Initial terrain patches must use the source common grid.");
        var raster = frame.Raster;
        if (raster.CullMode is < 1 or > 3 || raster.DepthCompare is < 1 or > 8 ||
            raster.ColorMasks > 0xFFFFF || raster.StencilReference > 255 ||
            !float.IsFinite(raster.DepthClear) || raster.DepthClear is < 0 or > 1)
            throw new InvalidDataException("Initial terrain raster fields cannot be encoded by the native loader.");
        if (frame.Batches is { } batches)
        {
            int next = 0;
            foreach (var batch in batches)
            {
                if (batch.Group is < 0 or > 2 || batch.StartInstance != next || batch.InstanceCount <= 0 ||
                    batch.Coarse != (batch.Group == 2))
                    throw new InvalidDataException("Initial terrain batches must cover the selected ordered instance stream.");
                next = checked(next + batch.InstanceCount);
            }
            if (next != frame.Patches.Count)
                throw new InvalidDataException("Initial terrain batches differ from the selected patch count.");
        }
        var input = OriginalDeferredSceneConstants.PrepareTerrainCamera(frame.Camera,
            new([], [], frame.Camera.SourceEye, MaterialDraw.PrepareSelectedOrdinaryF8(OrdinaryCallFlags).Specular,
                0, Height.HeightNormalInfo, Scalar.PrepareCliffInfo(), Scalar.PrepareNearFar(), 0));
        var shader = Material.Shader.Shader;
        int vertex = Program(Material.Combination, 1), pixel = Program(Material.Combination, 0);
        var nearVertex = TerrainGbufferConstants.Vertex(shader.ReadResourceDefinitions(vertex), input).Bytes;
        var nearPixel = TerrainGbufferConstants.Pixel(shader.ReadResourceDefinitions(pixel), input, Material.Palette).Bytes;
        int coarseVertex = Program(Material.CoarseCombination, 1), coarsePixel = Program(Material.CoarseCombination, 0);
        var coarseVertexBytes = TerrainGbufferConstants.Vertex(shader.ReadResourceDefinitions(coarseVertex), input).Bytes;
        var coarsePixelBytes = TerrainGbufferConstants.Pixel(shader.ReadResourceDefinitions(coarsePixel), input, Material.Palette).Bytes;
        ushort[] indices = TerrainPatchInputMath.TriangleIndices(grid);
        byte[] indexBytes = new byte[checked(indices.Length * 2)];
        for (int i = 0; i < indices.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(indexBytes.AsSpan(i * 2), indices[i]);

        Directory.CreateDirectory(directory);
        Write("terrain-vs.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, vertex).ToArray());
        Write("terrain-ps.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, pixel).ToArray());
        Write("near-vs.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, vertex).ToArray());
        Write("near-ps.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, pixel).ToArray());
        Write("coarse-vs.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, coarseVertex).ToArray());
        Write("coarse-ps.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, coarsePixel).ToArray());
        Write("vs-b0.bin", nearVertex); Write("ps-b0.bin", nearPixel);
        Write("near-vs-b0.bin", nearVertex); Write("near-ps-b0.bin", nearPixel);
        Write("coarse-vs-b0.bin", coarseVertexBytes); Write("coarse-ps-b0.bin", coarsePixelBytes);
        Write("positions.bin", TerrainPatchInputMath.EncodePositionStream(grid));
        Write("instances.bin", frame.Patches.SelectMany(p => p.EncodeInstance()).ToArray());
        Write("indices.bin", indexBytes);
        Write("camera-view.bin", MatrixBytes(frame.Camera.SourceWorldToView));
        Write("camera-projection.bin", MatrixBytes(frame.Projection));
        Write("camera-world-to-projection.bin", MatrixBytes(frame.Camera.SourceWorldToProjection));
        foreach (var texture in textures) WriteTexture(directory, texture);
        File.WriteAllLines(Path.Combine(directory, "textures.txt"), textures.Select(t => $"{t.Stage} {t.Slot} {t.Filename}"));
        File.WriteAllLines(Path.Combine(directory, "samplers.txt"), textures.Select(t => SamplerRow(t.Stage, t.Slot, t.Sampler)));
        uint[] formats = targets.ColorTargets.Select(t => OriginalRenderTextureFormatMath.Resolve(t.Texture).TargetViewFormat).ToArray();
        var depth = OriginalRenderTextureFormatMath.Resolve(targets.DepthTarget!);
        File.WriteAllLines(Path.Combine(directory, "targets.txt"), targets.ColorTargets.Select(t =>
        {
            var f = OriginalRenderTextureFormatMath.Resolve(t.Texture);
            return $"{t.ObjectNameHash} {t.SurfaceId} {f.ResourceFormat} {f.ShaderViewFormat} {f.TargetViewFormat}";
        }).Append($"{targets.DepthObjectNameHash} {targets.DepthSurfaceId} {depth.ResourceFormat} {depth.ShaderViewFormat} {depth.TargetViewFormat}"));
        string rasterWords = $"{raster.CullMode} {(raster.FrontCounterClockwise ? 1 : 0)} {raster.DepthCompare} " +
            $"{raster.ColorMasks} {(raster.StencilEnabled ? 1 : 0)} {raster.StencilReference}";
        File.WriteAllText(Path.Combine(directory, "scene.txt"),
            $"{frame.Width} {frame.Height} {frame.Patches.Count} {indices.Length}\n" +
            string.Join(" ", formats) + $" {depth.TargetViewFormat}\n{rasterWords}\n");
        File.WriteAllText(Path.Combine(directory, "terrain-raster.txt"),
            rasterWords + " " + raster.DepthClear.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\n");
        if (frame.Batches is not null)
        {
            File.WriteAllLines(Path.Combine(directory, "batches.txt"), frame.Batches.Select(b =>
                $"{b.Group} {b.StartInstance} {b.InstanceCount} {(b.Coarse ? 1 : 0)}"));
            File.WriteAllText(Path.Combine(directory, "geometry.txt"),
                $"{frame.Width} {frame.Height} {frame.Patches.Count} {indices.Length}\n");
        }
        File.WriteAllText(Path.Combine(directory, "source.json"), JsonSerializer.Serialize(new
        {
            sourceScene = Scene.Scene.Source.ResourceUri, sceneSha256 = Scene.Scene.Source.DecodedSha256,
            sourceShader = shader.Source.ResourceUri, shaderSha256 = shader.Source.DecodedSha256,
            locale = Scalar.Locale, width = frame.Width, height = frame.Height, gridSize = grid,
            scope = "installed-source stock ordinary terrain input; caller supplies current camera/patch/raster; other Main packages required"
        }, new JsonSerializerOptions { WriteIndented = true }));
        return new(grid, frame.Patches.Count, indices.Length, input);

        void Write(string name, byte[] bytes) => File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }

    private void WriteTexture(string directory, TextureInput texture)
    {
        G1TextureImage image = texture.Binding.Image;
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, texture.Filename)));
        if (texture.InitialControl)
        {
            var map = Material.InitialControl.SourceMap;
            Header(map.Width, map.Height, 1, 1, 28);
            Subresource(map.Width * 4, Material.InitialControl.RgbaPixels);
            return;
        }
        Header(image.Width, image.Height, image.LayerCount, image.MipCount, texture.Format);
        for (int layer = 0; layer < image.LayerCount; layer++)
            for (int mip = 0; mip < image.MipCount; mip++)
            {
                var m = image.Mips[mip];
                int pitch = image.PixelFormat is 0x59 or 0x5B
                    ? Math.Max(1, (m.Width + 3) / 4) * (image.PixelFormat == 0x59 ? 8 : 16) : m.Width * 4;
                Subresource(pitch, image.GetEncodedMip(texture.Binding.Resource.Source.Bytes, mip, layer));
            }
        void Header(int width, int height, int layers, int mips, uint format)
        {
            foreach (uint word in new[] { (uint)width, (uint)height, (uint)layers, (uint)mips, format }) writer.Write(word);
        }
        void Subresource(int pitch, ReadOnlySpan<byte> bytes)
        {
            writer.Write((uint)pitch); writer.Write((uint)bytes.Length); writer.Write((uint)bytes.Length); writer.Write(bytes);
        }
    }

    private static int Program(G2ShaderCombination combination, int stage) =>
        combination.Stages.Single(s => s.StageCode == stage).Entry!.ProgramIndex;
    private static string SamplerRow(string stage, uint slot, G1TextureSamplerRecipe r) =>
        FormattableString.Invariant($"{stage} {slot} {r.Filter} {r.AddressU} {r.AddressV} {r.AddressW} {BitConverter.UInt32BitsToSingle(r.MipLodBiasBits):R} {r.MaxAnisotropy} {r.ComparisonFunction} {BitConverter.UInt32BitsToSingle(r.MinimumLodBits):R} {BitConverter.UInt32BitsToSingle(r.MaximumLodBits):R}");
    private static byte[] MatrixBytes(Matrix4x4 m)
    {
        float[] words = [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
            m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
        byte[] bytes = new byte[64];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), words[i]);
        return bytes;
    }
}
