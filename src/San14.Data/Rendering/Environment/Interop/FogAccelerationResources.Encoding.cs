using System.Buffers.Binary;

namespace San14.Data;

public sealed partial class FogAccelerationResources
{
    /// <summary>Encode imported media and the actual initial frame at the Native ABI boundary.</summary>
    public void WriteNativeInputs(FogAccelerationInitialFrame frame, string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "target.txt"), FormattableString.Invariant(
            $"{Formats.ResourceFormat} {Formats.ShaderViewFormat} {Formats.TargetViewFormat} {Target.Width} {Target.Height}\n"));
        File.WriteAllText(Path.Combine(directory, "settings.txt"), FormattableString.Invariant(
            $"{RendererFogMode} {Profile} {(FogEnabled ? 1 : 0)}\n"));
        File.WriteAllLines(Path.Combine(directory, "passes.txt"), ["pass0", "pass1"]);
        for (int pass = 0; pass < programs.Length; pass++)
        {
            string output = Path.Combine(directory, "pass" + pass); Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "vs.dxbc"),
                Shader.Container.GetProgramBytes(Shader.Source.Bytes, programs[pass].Vertex).ToArray());
            File.WriteAllBytes(Path.Combine(output, "ps.dxbc"),
                Shader.Container.GetProgramBytes(Shader.Source.Bytes, programs[pass].Pixel).ToArray());
            File.WriteAllText(Path.Combine(output, "geometry.txt"), pass == 0 ? "1 1\n" : "500 1\n");
            File.WriteAllLines(Path.Combine(output, "cbuffers.txt"), pass == 0
                ? ["PS 0 ps-b0.bin"] : new[] { "VS 0 vs-b0.bin", "PS 0 ps-b0.bin" });
            File.WriteAllBytes(Path.Combine(output, "ps-b0.bin"), (pass == 0 ? frame.FirstPixel : frame.Pixel).ToArray());
            if (pass == 1) File.WriteAllBytes(Path.Combine(output, "vs-b0.bin"), frame.Vertex.ToArray());
        }
        // Native D3D11_BLEND_DESC: alpha-to-coverage and independent blending are false.
        // The reduction publishes ONE/ONE/MIN and red-only writes to all eight slots.
        byte[] minimum = new byte[264];
        for (int slot = 0; slot < 8; slot++)
        {
            uint[] fields = [1, 2, 2, 4, 2, 2, 4, 1];
            for (int word = 0; word < fields.Length; word++)
                BinaryPrimitives.WriteUInt32LittleEndian(minimum.AsSpan(8 + slot * 32 + word * 4, 4), fields[word]);
        }
        File.WriteAllBytes(Path.Combine(directory, "pass1", "blend-desc.bin"), minimum);
    }
}
