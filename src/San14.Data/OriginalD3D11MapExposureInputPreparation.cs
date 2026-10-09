using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Resource Common settings and the project's stock rectangle inputs
/// for the independent map Exposure owner. Current Main and View fields belong
/// to the caller; shaders are written by ProjectShaderInputPreparation.</summary>
public sealed class OriginalD3D11MapExposureInputPreparation
{
    private OriginalD3D11MapExposureInputPreparation(LoadedOriginalMapExposureSource source,
        OriginalMapExposureViewAux viewAux) => (Source, ViewAux) = (source, viewAux);

    public LoadedOriginalMapExposureSource Source { get; }
    public OriginalMapExposureViewAux ViewAux { get; }

    public static OriginalD3D11MapExposureInputPreparation Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = OriginalMapExposureSource.Load(repository, locale);
        var viewAux = OriginalMapExposureViewAux.ReadTemplate(
            repository.LoadObjects("KIDSSystemResource", 0x6D011726), 0x899F101B);
        if ((viewAux.Flags8 & ((1u << 22) | (1u << 23))) != 0 || !viewAux.CustomPixelShaderAbsent)
            throw new NotSupportedException("Map Exposure initialization requires the ordinary stock working-view mode.");
        return new(source, viewAux);
    }

    /// <summary>Writes this owner's initial native inputs. Main+1510 is the
    /// completed current tail, and View+19B8 is the caller's selected mask.</summary>
    public void WriteNativeInputs(string directory, uint currentMainSpecial1510, int currentViewMask19B8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        using (var profile = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin"))))
        {
            profile.Write(ViewAux.Flags8);
            profile.Write(ViewAux.ToneType3A4);
            profile.Write(currentMainSpecial1510);
            profile.Write(ViewAux.CustomPixelShaderAbsent ? 1u : 0u);
            profile.Write(currentViewMask19B8);
        }
        File.WriteAllBytes(Path.Combine(directory, "sampler-desc.bin"), Source.InputSampler.EncodeD3D11Descriptor());
        WriteFloats("vs-b0.bin", [1, -2, -1, 1, 1, 1, 1, 1]);
        WriteFloats("ps-b0.bin", [-float.MaxValue, 0, 0, 0]);
        File.WriteAllText(Path.Combine(directory, "layout.txt"),
            "POSITION 0 6 0 0 0 0\nCOLOR 0 28 0 12 0 0\nTEXCOORD 0 16 0 16 0 0\n");
        File.WriteAllText(Path.Combine(directory, "geometry.txt"), "4 5 24\n");
        File.WriteAllText(Path.Combine(directory, "target.txt"), "1 2 2 2 1\n");
        File.WriteAllBytes(Path.Combine(directory, "full-quad.bin"), CreateQuad(false));
        File.WriteAllBytes(Path.Combine(directory, "half-quad.bin"), CreateQuad(true));

        void WriteFloats(string name, ReadOnlySpan<float> values)
        {
            byte[] bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), values[i]);
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
        }
    }

    private static byte[] CreateQuad(bool half)
    {
        float left = half ? 1 : 0, u = half ? .5f : 0;
        float[][] rows = [[left, 0, 0, u, 0], [2, 0, 0, 1, 0], [left, 1, 0, u, 1], [2, 1, 0, 1, 1]];
        byte[] bytes = new byte[4 * 24];
        for (int i = 0; i < rows.Length; i++)
        {
            for (int c = 0; c < 3; c++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 24 + c * 4, 4), rows[i][c]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 24 + 12, 4), uint.MaxValue);
            for (int c = 0; c < 2; c++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 24 + 16 + c * 4, 4), rows[i][c + 3]);
        }
        return bytes;
    }
}
