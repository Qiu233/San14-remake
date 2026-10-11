using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

public sealed record D3D11MainSkyTextureInput(uint Slot, string Name, SkyTextureSource Texture);

/// <summary>Static device media for one genuinely selected Main Sky program and
/// texture generation. Constants, draw admission and inherited pipeline state
/// remain with their actual current owners.</summary>
public sealed class D3D11MainSkyInputPreparation
{
    private readonly IReadOnlyDictionary<string, byte[]> files;
    private D3D11MainSkyInputPreparation(SkyMainProgramSelection program,
        Dictionary<string, byte[]> files, D3D11MainSkyTextureInput[] textures)
    { Program = program; this.files = files; Textures = Array.AsReadOnly(textures); }

    public SkyMainProgramSelection Program { get; }
    public IReadOnlyList<D3D11MainSkyTextureInput> Textures { get; }

    /// <summary>Borrow the same atmosphere images and logical sampling snapshot
    /// used by the current preparation. A null sampling slot is preserved as null,
    /// and no package reference is reinterpreted as the state of a retained binding.</summary>
    public static D3D11MainSkyInputPreparation Load(SkyMainProgramSelection program,
        SkyAppliedCloudTextureSet atmosphere, Sky2TextureBindingSnapshot sampling)
    {
        sampling.RequireCurrent();
        if (program.VertexDefinitions.Bindings.Any(value => value.InputType != 0))
            throw new NotSupportedException("Main Sky quad vertex resources need another actual producer.");
        var bindings = program.PixelDefinitions.Bindings;
        if (bindings.Any(value => value.InputType is not (0 or 2 or 3) || value.BindCount != 1))
            throw new NotSupportedException("Main Sky has an unsupported resource binding.");
        var images = bindings.Where(value => value.InputType == 2).OrderBy(value => value.BindPoint).ToArray();
        var samplers = bindings.Where(value => value.InputType == 3).ToArray();
        if (images.Select(value => value.BindPoint).Distinct().Count() != images.Length ||
            samplers.Length != images.Length || samplers.Select(value => value.BindPoint).Distinct().Count() != samplers.Length)
            throw new NotSupportedException("Main Sky requires one source sampler for each sampled texture slot.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["main-sky-vs.dxbc"] = program.CopyVertexBytes(),
            ["main-sky-ps.dxbc"] = program.CopyPixelBytes(),
        };
        byte[] presence = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(presence, 0x31424D53); // SMB1: installed sampling presence.
        BinaryPrimitives.WriteUInt32LittleEndian(presence.AsSpan(4),
            (sampling.Presence.Earth ? 1u : 0u) | (sampling.Presence.Moon ? 2u : 0u) |
            (sampling.Presence.Milky ? 4u : 0u));
        files.Add("sampling-presence.bin", presence);
        float[] positions = [-1, 1, -1, -1, 1, 1, 1, -1];
        byte[] quad = new byte[positions.Length * 4];
        for (int index = 0; index < positions.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(quad.AsSpan(index * 4, 4), positions[index]);
        files.Add("quad-position-xy.bin", quad);
        var rows = new List<string>();
        var inputs = new List<D3D11MainSkyTextureInput>();
        foreach (var image in images)
        {
            var sampler = samplers.SingleOrDefault(value => value.BindPoint == image.BindPoint);
            if (sampler is null || sampler.Dimension != 0 || sampler.Name.Text != "__smp" + image.Name.Text || image.ReturnType != 5)
                throw new NotSupportedException("Main Sky texture and retained sampler roles do not match.");
            (string Name, uint Dimension, SkyTextureSource? Texture) role = image.BindPoint switch
            {
                0 => ("g_sStableMap", 8, atmosphere.StaInputs[0]),
                1 => ("g_sTtableMap", 4, atmosphere.StaInputs[1]),
                2 => ("g_sAmbientSkyLightMap", 4, atmosphere.StaInputs[2]),
                3 => ("g_sEarthSurfaceMap", 4, sampling.GetTexture(SkySamplingRole.Earth)?.Texture),
                4 => ("g_sMilkyWayMap", 4, sampling.GetTexture(SkySamplingRole.MilkyWay)?.Texture),
                5 => ("g_sMoonSurfaceMap", 4, sampling.GetTexture(SkySamplingRole.Moon)?.Texture),
                _ => throw new NotSupportedException("Main Sky has no producer for the selected texture slot."),
            };
            if (image.Name.Text != role.Name || image.Dimension != role.Dimension ||
                (role.Texture is not null && role.Texture.SourceDimension != role.Dimension))
                throw new NotSupportedException("Main Sky texture name or dimension differs from its actual source role.");
            if (role.Texture is null) continue;
            string prefix = $"main-sky-slot{image.BindPoint}";
            rows.Add(SkyTextureInputEncoding.Add(files, "", "main-sky", image.BindPoint, prefix, role.Texture));
            inputs.Add(new(image.BindPoint, role.Name, role.Texture));
        }
        files.Add("main-sky-textures.tsv", Encoding.UTF8.GetBytes(string.Join("\n", rows) + (rows.Count == 0 ? "" : "\n")));
        return new(program, files, inputs.ToArray());
    }

    /// <summary>Write only the immutable prepared generation. No resource reads,
    /// program reselection or numerical material preparation happens here.</summary>
    public void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in files) File.WriteAllBytes(Path.Combine(directory, file.Key), file.Value);
        Program.MaterialLayout.WriteNativeInputs(directory);
    }
}
