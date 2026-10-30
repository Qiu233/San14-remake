namespace San14.Data;

public sealed partial class GroundDetailTemplateResources
{
    public void WriteNativeInputs(string gpuTemplatesDirectory)
    {
        Directory.CreateDirectory(gpuTemplatesDirectory);
        foreach (var (relative, bytes) in files)
        {
            string target = Path.Combine(gpuTemplatesDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);
        }
        File.WriteAllLines(Path.Combine(gpuTemplatesDirectory, "models.txt"), modelDirectories);
        File.WriteAllLines(Path.Combine(gpuTemplatesDirectory, "draw-producers.txt"), producerRows);
        File.WriteAllLines(Path.Combine(gpuTemplatesDirectory, "template-lods.txt"),
            Metadata.SelectMany(t => t.LodDraws.SelectMany((draws, lod) => draws.Select(draw =>
                FormattableString.Invariant($"{t.Index} {t.NameHash:X8} {lod} {draw} template-{t.Index}-{t.NameHash:X8}/draw-{draw}")))));
        using var output = new BinaryWriter(File.Create(Path.Combine(gpuTemplatesDirectory, "template-world.bin")));
        output.Write(0x31574447u); // GDW1: ordered source World declarations.
        output.Write((uint)Metadata.Count);
        foreach (var template in Metadata)
        {
            output.Write((uint)template.Index);
            output.Write(template.NameHash);
            output.Write(template.World.ConstructorFlags44);
            output.Write(template.World.DeclaredExclusion78);
            output.Write(template.World.CasterFlags10C);
        }
    }

    private static void WriteTextures(GameResourceRepository repository, D3D11ModelResourceWriter writer,
        Dictionary<string, byte[]> media, string directory, MapStaticModelAsset asset, int draw,
        NamedModelShader provider, LoadedShaderContainer shader, int combination, bool tree)
    {
        if (!tree)
        {
            writer.WriteStaticTextures(directory, asset.TextureResources, draw, provider, shader, combination);
            return;
        }
        writer.WriteTree2Textures(directory, asset.TextureResources, draw, provider, shader, combination);
    }
}
