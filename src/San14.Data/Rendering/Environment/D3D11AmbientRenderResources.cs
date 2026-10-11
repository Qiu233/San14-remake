using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Immutable device inputs for the selected specular environment.
/// Target declarations and selected programs are imported once. Generation,
/// interpolation and current lighting remain with the runtime owner.</summary>
public sealed class D3D11AmbientRenderResources
{
    private sealed record Target(uint Role, uint Key, int Width, int Height, byte Levels,
        RenderTextureFormats Formats, uint DepthFormat = 0);

    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly D3D11MainSkyInputPreparation sky;
    private readonly SkyAmbientFilterSelection filter;

    private D3D11AmbientRenderResources(GameResourceRepository repository,
        D3D11AmbientWorkConstants work, string locale)
    {
        sky = work.SkyMedia;
        filter = work.Filter;
        var cubes = work.Ambient.SpecularTargets;
        if (cubes.Count != 4 || cubes.Select(value => value.Object.ObjectId).Distinct().Count() != 4)
            throw new NotSupportedException("Specular environment needs four distinct output declarations for its generation and interpolation consumers.");
        var first = cubes[0];
        if (first.LevelNum != 7)
            throw new NotSupportedException("The retained environment update mechanism supports its seven-level, fixed-work schedule.");
        foreach (var cube in cubes)
        {
            CubeRenderTextureResources.ResolveSurface(cube, 0);
            if (cube.Width != first.Width || cube.LevelNum != first.LevelNum)
                throw new NotSupportedException("Environment history and interpolation require compatible output extents and levels.");
        }
        var targets = cubes.Select((cube, index) => CubeTarget((uint)index, cube.Object.ObjectId, cube)).ToList();
        // These are fresh constructor-owned cubes. Only extent, levels and
        // pixel format are borrowed; authored output color modes are not.
        var working = first with { ColorSpace = 0, OutputColorSpace = 0 };
        targets.Add(CubeTarget(4, 0, working));
        targets.Add(CubeTarget(5, 0, working));
        targets.Add(new(6, 0, first.Width, first.Width, 1, new(39, 41, 0), 40));
        targets.Add(new(7, 0, first.Width, first.Width, first.LevelNum, new(9, 10, 10)));
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream);
            writer.Write(0x31525341u); // ASR1: ambient specular resource declarations.
            writer.Write(filter.SampleCount); writer.Write(work.SkyEnabled ? 1u : 0u); writer.Write((uint)targets.Count);
            foreach (var target in targets)
            {
                writer.Write(target.Role); writer.Write(target.Key);
                writer.Write((uint)target.Width); writer.Write((uint)target.Height); writer.Write((uint)target.Levels);
                writer.Write(target.Formats.ResourceFormat); writer.Write(target.Formats.ShaderViewFormat);
                writer.Write(target.Formats.TargetViewFormat); writer.Write(target.DepthFormat); writer.Write(1u);
            }
            files.Add("targets.bin", stream.ToArray());
        }
        var samplers = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        for (uint role = 0; role < 6; role++)
        {
            var cube = role < 4 ? cubes[(int)role] : first;
            byte[] key = RenderSamplerSourceAdapters.AmbientSpecularKey(
                RenderSamplerSourceAdapters.CubeTextureKey(samplers, cube.LevelNum));
            if (role == 4)
            {
                // The working cube's copy/filter caller installs linear
                // minification, magnification and mip filtering at MinLOD zero.
                ushort low = BinaryPrimitives.ReadUInt16LittleEndian(key);
                BinaryPrimitives.WriteUInt16LittleEndian(key, (ushort)((low & 0x003F) | 0x9400));
            }
            files.Add($"cube{role}-sampler-desc.bin", RenderSamplerSourceAdapters.DecodeKey(samplers, key).EncodeD3D11Descriptor());
        }
        files.Add("blend-cameras.bin", Enumerable.Range(0, 6)
            .SelectMany(face => SkyCubeBlendFaceMath.EncodeNativeFaceMatrix((uint)face)).ToArray());
        var logTables = NativeLogTables.Load(repository.SourceDirectory, locale);
        for (uint mip = 1; mip < first.LevelNum; mip++)
        {
            var generated = SkyFilterSampleMath.GenerateMip(filter.SampleCount, checked((ushort)first.Width),
                first.LevelNum, mip, logTables, NativeLogDispatch.AvxFma, filter.SampleCount);
            files.Add($"filter-mip{mip}-t17-half4.bin", generated.Packet.DirectionBytes.ToArray());
            files.Add($"filter-mip{mip}-t18-float.bin", generated.Packet.WeightBytes.ToArray());
            byte[] weightSum = new byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(weightSum, generated.Packet.WeightSum);
            files.Add($"filter-mip{mip}-weight-sum.bin", weightSum);
        }
        for (int face = 0; face < work.SkyFaces.Count; face++)
        {
            byte[] material = new byte[SkyMainMaterialEncoding.ByteLength];
            SkyMainMaterialEncoding.Write(work.SkyFaces[face], material);
            files.Add($"sky/face{face}.bin", material);
        }
    }

    public static D3D11AmbientRenderResources Load(GameResourceRepository repository,
        D3D11AmbientWorkConstants work, string locale) => new(repository, work, locale);

    public uint SampleCount => filter.SampleCount;

    /// <summary>A new sample request needs matching programs and sample buffers.
    /// A current numeric frame cannot silently reuse an incompatible generation.</summary>
    public void RequireCurrent(MapSourceCreatedSeasonLightingFrame present)
    {
        uint requested = BinaryPrimitives.ReadUInt32LittleEndian(present.PreparedCameraPrefix.Span.Slice(0x3A4, 4));
        if (requested != filter.SampleCount)
            throw new NotSupportedException("Changed environment sample count requires resource generation replacement.");
    }

    public void WriteNativeInputs(string inputRoot)
    {
        string directory = Path.Combine(inputRoot, "ambient-input");
        Directory.CreateDirectory(directory);
        foreach (var file in files)
        {
            string path = Path.Combine(directory, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Value);
        }
        sky.WriteNativeInputs(Path.Combine(directory, "sky"));
        filter.WriteNativeInputs(directory);
    }

    private static Target CubeTarget(uint role, uint key, CubeRenderTextureDefinition cube)
    {
        var formats = RenderTextureFormatMath.Resolve(cube.PixelFormat, cube.ColorSpace, cube.OutputColorSpace);
        if (formats.TargetViewFormat is 20 or 40)
            throw new NotSupportedException("Specular cubes require color render-target views.");
        return new(role, key, cube.Width, cube.Width, cube.LevelNum, formats);
    }
}
