using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

/// <summary>Immutable Cloud and shared Sky image media. A candidate can replace
/// these inputs without constructing targets or changing retained surface scale.</summary>
public sealed class D3D11CloudImageResources
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<string> mainSamplerRows;

    private D3D11CloudImageResources(SkyAppliedCloudTextureSet textures,
        Sky2TextureBindingSnapshot sampling)
    {
        sampling.RequireCurrent();
        Textures = textures;
        EarthTexture = sampling.GetTexture(SkySamplingRole.Earth)?.Texture;
        byte[] presence = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(presence, 0x31424D53); // SMB1: installed shared Sky sampling.
        BinaryPrimitives.WriteUInt32LittleEndian(presence.AsSpan(4),
            (sampling.Presence.Earth ? 1u : 0u) | (sampling.Presence.Moon ? 2u : 0u) |
            (sampling.Presence.Milky ? 4u : 0u));
        files.Add("ambient-input/sampling-presence.bin", presence);

        const string main = "main-cloud-input/", ambient = "ambient-input/";
        var mainRows = new List<string>();
        var cloudRows = new List<string>();
        var samplers = new List<string>();
        foreach (var input in textures.CloudInputs)
        {
            string prefix = $"cloud-slot{input.SourceBindPoint}";
            cloudRows.Add(Texture(ambient, "clouds", input.SourceBindPoint, prefix, input.Texture));
            if (input.SourceBindPoint < 6)
            {
                mainRows.Add(Texture(main, "pass1", input.SourceBindPoint, prefix, input.Texture));
                samplers.Add($"pass1 {input.SourceBindPoint} {prefix}-sampler.bin {prefix}-sampler.bin");
            }
        }
        Text(main + "pass1-textures.tsv", string.Join("\n", mainRows) + "\n");
        Text(ambient + "cloud-textures.tsv", string.Join("\n", cloudRows) + "\n");
        Text(ambient + "sky-textures.tsv", EarthTexture is null ? "" :
            Texture(ambient, "sky-current", 0, "sky-earth", EarthTexture) + "\n");
        mainSamplerRows = samplers.AsReadOnly();
    }

    public SkyAppliedCloudTextureSet Textures { get; }
    public SkyTextureSource? EarthTexture { get; }
    internal IReadOnlyList<string> MainSamplerRows => mainSamplerRows;

    public static D3D11CloudImageResources Prepare(SkyAppliedCloudTextureSet textures,
        Sky2TextureBindingSnapshot sampling)
    {
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(sampling);
        return new(textures, sampling);
    }

    /// <summary>Compatibility for callers that have not installed a replacement.
    /// Current Earth is the retained Main sampling value, not a package key.</summary>
    public void RequireCompatible(SkyAppliedCloudTextureSet textures, SkyTextureSource? currentEarth)
    {
        if ((!ReferenceEquals(Textures, textures) && !Textures.SamePhysical(textures)) ||
            !SameTexture(EarthTexture, currentEarth))
            throw new NotSupportedException("Changed Native Cloud or Main Sky images require a physical resource replacement.");
    }

    /// <summary>Write only image media. The surface owner assembles samplers.txt
    /// with its retained history and composite routes through WriteImageInputs.</summary>
    public void WriteNativeInputs(string inputRoot)
    {
        foreach (var file in files)
        {
            string path = Path.Combine(inputRoot, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Value);
        }
    }
    private static bool SameTexture(SkyTextureSource? first, SkyTextureSource? second) =>
        ReferenceEquals(first, second) || first is not null && second is not null &&
        first.Identity == second.Identity && first.SamplerDescriptorSha256 == second.SamplerDescriptorSha256;
    private void Text(string path, string value) => files.Add(path, Encoding.UTF8.GetBytes(value));
    private string Texture(string directory, string program, uint slot, string prefix, SkyTextureSource texture) =>
        SkyTextureInputEncoding.Add(files, directory, program, slot, prefix, texture);
}
