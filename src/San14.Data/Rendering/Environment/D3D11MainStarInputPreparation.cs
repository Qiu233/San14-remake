using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Immutable media for both Main Star passes and exposure variants.
/// Generated geometry, camera values and animation belong to current runtime.</summary>
public sealed class D3D11MainStarInputPreparation
{
    private readonly Dictionary<string, byte[]> files;
    private readonly SkyStarProgramSelection[][] programs;
    private D3D11MainStarInputPreparation(SkyStarResources resources,
        Dictionary<string, byte[]> files, SkyStarProgramSelection[][] programs)
        => (Resources, this.files, this.programs) = (resources, files, programs);
    public SkyStarResources Resources { get; }

    public static D3D11MainStarInputPreparation Load(SkyStarResources resources)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["controller-points.bin"] = resources.Geometry?.VertexBytes.ToArray() ?? [],
        };
        float[] table = SkyStarTwinkle.CreateTable();
        byte[] tableBytes = new byte[table.Length * 4];
        for (int index = 0; index < table.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(tableBytes.AsSpan(index * 4), table[index]);
        files.Add("twinkle-table.bin", tableBytes);
        var programs = new SkyStarProgramSelection[2][];
        for (int variant = 0; variant < programs.Length; variant++)
        {
            programs[variant] = [resources.Select(0, variant != 0), resources.Select(1, variant != 0)];
            for (int pass = 0; pass < programs[variant].Length; pass++)
            {
                var program = programs[variant][pass];
                RequireResources(program.VertexDefinitions, 1, variant != 0);
                RequireResources(program.GeometryDefinitions, 2, variant != 0);
                RequireResources(program.PixelDefinitions, 0, variant != 0);
                foreach ((uint stage, string name) in new (uint, string)[] { (1, "vs"), (2, "gs"), (0, "ps") })
                    files.Add($"variant{variant}/stage{pass}-{name}.dxbc", program.CopyProgramBytes(stage));
            }
        }
        string textureRow = resources.StarTexture is { } texture ?
            SkyTextureInputEncoding.Add(files, "", "main-star", 0, "star-slot0", texture.Texture) + "\n" : "";
        files.Add("star-textures.tsv", Encoding.UTF8.GetBytes(textureRow));
        return new(resources, files, programs);
    }

    public void WriteNativeInputs(string starsDirectory)
    {
        Directory.CreateDirectory(starsDirectory);
        foreach (var file in files)
        {
            string path = Path.Combine(starsDirectory, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Value);
        }
        for (int variant = 0; variant < programs.Length; variant++)
            SkyStarMaterialLayout.WriteNativeInputs(Path.Combine(starsDirectory, $"variant{variant}"), programs[variant]);
    }

    private static void RequireResources(DxbcResourceDefinitions definitions, uint stage, bool exposure)
    {
        foreach (var binding in definitions.Bindings)
        {
            if (binding.InputType == 0) continue;
            bool expected = binding.BindCount == 1 && binding.InputType is 2 or 3;
            string texture = stage switch { 0 => "sStar", 1 when exposure => "sExposureScale", _ => "" };
            uint slot = stage == 0 ? 0u : 8u;
            if (!expected || texture.Length == 0 || binding.BindPoint != slot ||
                binding.Name.Text != (binding.InputType == 2 ? texture : "__smp" + texture) ||
                binding.Dimension != (binding.InputType == 2 ? 4u : 0u))
                throw new NotSupportedException("Main Star shader resources need another actual source role.");
        }
    }
}
