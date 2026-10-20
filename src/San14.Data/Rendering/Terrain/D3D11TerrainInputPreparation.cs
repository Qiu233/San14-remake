using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11TerrainInputBatch(int Group, int StartInstance, int InstanceCount, bool Coarse);

/// <summary>A selected initial submission supplied by the camera/terrain owner.
/// Null batches selects the ordinary near-only stream; explicit batches retain
/// the caller's order and near/coarse selection.</summary>
public sealed record D3D11TerrainInputFrame(int Width, int Height,
    IReadOnlyList<TerrainPatchInput> Patches, DeferredCameraInputs Camera,
    Matrix4x4 Projection,
    IReadOnlyList<D3D11TerrainInputBatch>? Batches = null);

public sealed record D3D11TerrainTextureInputSource(string Stage, uint Slot, string Filename,
    string ResourceUri, string DecodedSha256, int ImageIndex, byte PixelFormat, int LayerCount,
    int MipCount, uint DxgiFormat, G1TextureSamplerRecipe Sampler, bool InitialControl);

public sealed record D3D11TerrainPreparedInputs(int GridSize, int PatchCount, int IndexCount,
    TerrainGbufferDrawInputs DrawInputs);

/// <summary>First preparation of the stock ordinary Landscape input from the
/// user's installed resources. Generated DXBC, texels and streams are local
/// cache contents, not distributable project assets. Load this source owner
/// once and call WriteNativeInputs when preparing a cache; normal requests use
/// the already prepared native resources. Inherited raster,
/// later terrain setters and the other Main input packages belong to callers.</summary>
public sealed class D3D11TerrainInputPreparation
{
    private sealed record TextureInput(string Stage, uint Slot, string Filename,
        TerrainTextureBinding Binding, uint Format, G1TextureSamplerRecipe Sampler, bool InitialControl);

    private readonly LoadedRenderGraphTargets targets;
    private readonly TextureInput[] textures;
    private readonly TerrainShadowResources shadow;

    private D3D11TerrainInputPreparation(LoadedTerrainSceneResources source, TerrainGbufferSourceParameters scalar,
        TerrainGbufferMaterialDrawSource materialDraw, byte callFlags, LoadedRenderGraphTargets graphTargets,
        TerrainShadowResources shadowResources)
    {
        Source = source; Scalar = scalar; MaterialDraw = materialDraw; OrdinaryCallFlags = callFlags;
        shadow=shadowResources;
        var height = source.Height;
        var material = source.Material;
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
        TextureSources = Array.AsReadOnly(textures.Select(t => new D3D11TerrainTextureInputSource(
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

    public LoadedTerrainSceneResources Source { get; }
    public LoadedSceneResources Scene => Source.Scene;
    public LoadedTerrainResources Terrain => Source.Terrain;
    public LoadedTerrainParameterResources Parameters => Source.Parameters;
    public LoadedTerrainGbiMapResources Material => Source.Material;
    public LoadedTerrainInitialHeightResources Height => Source.Height;
    public TerrainGbufferSourceParameters Scalar { get; }
    public TerrainGbufferMaterialDrawSource MaterialDraw { get; }
    public byte OrdinaryCallFlags { get; }
    public LoadedRenderGraphTargets Targets => targets;
    public IReadOnlyList<D3D11TerrainTextureInputSource> TextureSources { get; }

    public static D3D11TerrainInputPreparation Load(GameResourceRepository repository,
        LoadedSceneResources scene, string locale)
        => Load(repository, TerrainSceneResources.Load(repository, scene), locale);

    /// <summary>Prepare native consumers of the scene's existing CPU sources.</summary>
    public static D3D11TerrainInputPreparation Load(GameResourceRepository repository,
        LoadedTerrainSceneResources source, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        var material = source.Material;
        var scalar = TerrainGbufferSourceParameters.CreateFresh(locale, material);
        var materialDraw = TerrainGbufferMaterialDrawSource.CreateFresh(locale, material);
        byte callFlags = MainSceneNode51Source.Load(repository, locale, 56).InitialCallFlags;
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var targets = RenderGraphTargetResources.Read(graph, 53, repository.LoadObjects("system", 0x93FB3280));
        return new(source, scalar, materialDraw, callFlags, targets,TerrainShadowResources.Load(repository,source.Parameters));
    }

    /// <summary>Select the initial grouped stream from the accepted camera.
    /// Both the first upload and subsequent Native requests use the ordinary
    /// terrain planner; the scene's preview prototype is not an initial View.</summary>
    public D3D11TerrainInputFrame PrepareInitialFrame(MapCameraConstructionPacket camera,
        int width, int height)
    {
        var tree = Terrain.SpatialTree;
        var eye = camera.Geometry.Eye;
        var plan = TerrainLodDrawPlanner.Build(tree, new(eye.X, eye.Y, eye.Z),
            TerrainParameterResources.ReadLodDistanceScale(Parameters),
            TerrainFrustumMath.Create(camera.SourceWorldToProjection, camera.Lens.ReverseDepth));
        var patches = plan.Rows.Select(row => TerrainPatchInputMath.Ordinary(tree, row.Patch.Node,
            Height.HeightBinding.Image.Width, Height.ControlBinding.Image.Width, row.Patch.EdgeFlags)).ToArray();
        var batches = new List<D3D11TerrainInputBatch>();
        for (int start = 0; start < plan.Rows.Count;)
        {
            int group = plan.Rows[start].Group;
            int end = start + 1;
            while (end < plan.Rows.Count && plan.Rows[end].Group == group) end++;
            batches.Add(new(group, start, end - start, group == 2));
            start = end;
        }
        return new(width, height, Array.AsReadOnly(patches), camera.SceneInputs,
            camera.SourceProjection, batches.AsReadOnly());
    }

    /// <summary>Writes only the base terrain input directory. The caller must
    /// supply the accepted initial camera and patches; this method does
    /// not read an exported receipt or select a fixed position/date/frame.</summary>
    public D3D11TerrainPreparedInputs WriteNativeInputs(string directory,
        D3D11TerrainInputFrame frame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(frame.Camera);
        ArgumentNullException.ThrowIfNull(frame.Patches);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Patches.Count == 0)
            throw new InvalidDataException("Initial terrain input requires positive viewport dimensions and selected patches.");
        int grid = checked((int)frame.Patches[0].GridSize);
        if (frame.Patches.Any(p => p.GridSize != grid))
            throw new InvalidDataException("Initial terrain patches must use the source common grid.");
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
        var input = DeferredSceneConstants.PrepareTerrainCamera(frame.Camera,
            new([], [], frame.Camera.SourceEye, MaterialDraw.PrepareSelectedOrdinaryF8(OrdinaryCallFlags).Specular,
                0, Height.HeightNormalInfo, Scalar.PrepareCliffInfo(), Scalar.PrepareNearFar(), 0));
        var shader = Material.Shader.Shader;
        int vertex = Program(Material.Combination, 1), pixel = Program(Material.Combination, 0);
        var nearVertex = Material.DetailProgram.VertexConstants.Pack(input).Bytes;
        var nearPixel = Material.DetailProgram.PixelConstants.Pack(input, Material.Palette).Bytes;
        int coarseVertex = Program(Material.CoarseCombination, 1), coarsePixel = Program(Material.CoarseCombination, 0);
        var coarseVertexBytes = Material.CoarseProgram.VertexConstants.Pack(input).Bytes;
        var coarsePixelBytes = Material.CoarseProgram.PixelConstants.Pack(input, Material.Palette).Bytes;
        ushort[] indices = TerrainPatchInputMath.TriangleIndices(grid);
        byte[] indexBytes = new byte[checked(indices.Length * 2)];
        for (int i = 0; i < indices.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(indexBytes.AsSpan(i * 2), indices[i]);

        Directory.CreateDirectory(directory);
        Write("terrain-vs.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, vertex).ToArray());
        Write("terrain-ps.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, pixel).ToArray());
        Write("near-vs.dxbc", shader.Container.GetProgramBytes(shader.Source.Bytes, vertex).ToArray());
        shadow.WriteNativeInputs(Path.Combine(directory,"shadow"));
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
        uint[] formats = targets.ColorTargets.Select(t => RenderTextureFormatMath.Resolve(t.Texture).TargetViewFormat).ToArray();
        var depth = RenderTextureFormatMath.Resolve(targets.DepthTarget!);
        File.WriteAllLines(Path.Combine(directory, "targets.txt"), targets.ColorTargets.Select(t =>
        {
            var f = RenderTextureFormatMath.Resolve(t.Texture);
            return $"{t.ObjectNameHash} {t.SurfaceId} {f.ResourceFormat} {f.ShaderViewFormat} {f.TargetViewFormat}";
        }).Append($"{targets.DepthObjectNameHash} {targets.DepthSurfaceId} {depth.ResourceFormat} {depth.ShaderViewFormat} {depth.TargetViewFormat}"));
        File.WriteAllText(Path.Combine(directory, "scene.txt"),
            $"{frame.Width} {frame.Height} {frame.Patches.Count} {indices.Length}\n" +
            string.Join(" ", formats) + $" {depth.TargetViewFormat}\n");
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
            scope = "installed-source stock ordinary terrain media; accepted camera/patch stream; raster inherited at draw; other Main packages required"
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
