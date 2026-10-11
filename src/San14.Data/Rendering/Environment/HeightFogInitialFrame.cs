using System.Buffers.Binary;
using System.Globalization;

namespace San14.Data;

/// <summary>Initial fog media from the actual imported program bank and current
/// settings. Later admission and program replacement belong to the native owner.</summary>
public sealed record HeightFogInitialFrame(HeightFogProgramResources Resources,
    HeightFogSettings Settings, IReadOnlyList<D3D11AtmosphereInitialFogConstants> Passes,
    int Width, int Height, RenderTextureFormats TargetFormat, byte[] Sampler)
{
    public void WriteNativeInputs(string directory)
    {
        if (Width <= 0 || Height <= 0 || Sampler.Length != 52)
            throw new InvalidDataException("Initial height fog needs positive target dimensions and its actual sampler descriptor.");
        Directory.CreateDirectory(directory);
        Resources.WriteNativeInputs(directory);
        File.WriteAllBytes(Path.Combine(directory, "initial-height.bin"), HeightFogSettingsEncoding.WriteNative(Settings));
        File.WriteAllBytes(Path.Combine(directory, "mask-sampler.bin"), Sampler);
        WriteWhiteMask(directory);

        byte[] vertices = FullscreenVertices();
        var manifest = new List<string>();
        foreach (var pass in Passes)
        {
            string path = Path.Combine(directory, pass.Name);
            Directory.CreateDirectory(path);
            bool background = pass.Mask == 0;
            manifest.Add(pass.Name + (pass.Program is null ? " 0" : " 1"));
            Text(path, "population-gate.txt", FormattableString.Invariant($"125 {pass.Mask} {(background ? 1 : 0)}"));
            Text(path, "stencil.txt", FormattableString.Invariant($"1 3 {pass.Mask} {(background ? 255 : 15)} 0"));
            Text(path, "viewport.txt", FormattableString.Invariant($"0 0 {Width} {Height} 0 1"));
            Text(path, "target.txt", FormattableString.Invariant(
                $"current-hdr {TargetFormat.ResourceFormat} {TargetFormat.ShaderViewFormat} {TargetFormat.TargetViewFormat} {Width} {Height}"));
            Text(path, "geometry.txt", "36 6 4");
            Text(path, "layout.txt", "POSITION 0 6 0 0");
            File.WriteAllBytes(Path.Combine(path, "fullscreen-vb.bin"), vertices);

            // An unadmitted local record has no initial light or selected
            // program. The first actual admission installs its real choice.
            if (pass.Program is not { } program)
            {
                File.Delete(Path.Combine(path, "selection.txt"));
                File.Delete(Path.Combine(path, "cbuffers.txt"));
                File.Delete(Path.Combine(path, "phase-velocities.txt"));
                continue;
            }

            var key = program.Key;
            Text(path, "selection.txt", FormattableString.Invariant(
                $"{key.Feature} {(key.Background ? 1 : 0)} {key.FirstType} {key.SecondType} {key.ThirdType}"));
            var constants = new List<string>();
            WriteStage(path, "VS", program.VertexLayout, pass.VertexBytes, constants);
            WriteStage(path, "PS", program.PixelLayout, pass.PixelBytes, constants);
            File.WriteAllLines(Path.Combine(path, "cbuffers.txt"), constants);
            File.WriteAllLines(Path.Combine(path, "phase-velocities.txt"), pass.Phases.Select(phase =>
                phase.Offset.ToString(CultureInfo.InvariantCulture) + " " + phase.Velocity.ToString("R", CultureInfo.InvariantCulture)));
        }
        File.WriteAllLines(Path.Combine(directory, "passes.txt"), manifest);
    }

    private static void WriteStage(string directory, string stage, HeightFogStageLayout layout,
        byte[] bytes, List<string> bindings)
    {
        if (bytes.Length != layout.ByteSize)
            throw new InvalidDataException("Initial height fog constants differ from their selected stage layout.");
        if (layout.Slot < 0) return;
        string file = stage.ToLowerInvariant() + "-b" + layout.Slot.ToString(CultureInfo.InvariantCulture) + ".bin";
        File.WriteAllBytes(Path.Combine(directory, file), bytes);
        bindings.Add(stage + " " + layout.Slot.ToString(CultureInfo.InvariantCulture) + " " + file);
    }

    private static void WriteWhiteMask(string directory)
    {
        // The source's successful zero-reference initialization creates one
        // opaque white pixel with a typeless resource and an UNORM view.
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "height-fog-mask.tex")));
        foreach (uint word in new uint[] { 1, 1, 1, 1, 90, 4, 4, 4 }) writer.Write(word);
        writer.Write(uint.MaxValue);
        Text(directory, "mask-view-format.txt", "87");
    }

    private static byte[] FullscreenVertices()
    {
        // The width-two, height-two rectangle has two ordinary triangles.
        float[][] positions = [[-1, 1, 0, 0], [-1, -1, 0, 1], [1, -1, 1, 1],
            [1, 1, 1, 0], [-1, 1, 0, 0], [1, -1, 1, 1]];
        byte[] bytes = new byte[6 * 36];
        for (int index = 0; index < positions.Length; index++)
        {
            Span<byte> vertex = bytes.AsSpan(index * 36, 36);
            BinaryPrimitives.WriteSingleLittleEndian(vertex, positions[index][0]);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[4..], positions[index][1]);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[20..], 1);
            BinaryPrimitives.WriteUInt32LittleEndian(vertex[24..], uint.MaxValue);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[28..], positions[index][2]);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[32..], positions[index][3]);
        }
        return bytes;
    }

    private static void Text(string directory, string name, string text)
        => File.WriteAllText(Path.Combine(directory, name), text + "\n");
}
