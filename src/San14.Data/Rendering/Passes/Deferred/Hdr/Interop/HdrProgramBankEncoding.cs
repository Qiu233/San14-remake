namespace San14.Data;

public sealed record HdrProgramMediaRecipe(int Index, HdrShaderProgram Program, string Directory);

/// <summary>Complete resource program tables. Current View and phase selection
/// never enter this export; recipes retain every resource combination.</summary>
public static class HdrProgramBankEncoding
{
    public static IReadOnlyList<HdrProgramMediaRecipe> WriteNativeInputs(
        HdrShaderDeclaration declaration, string directory)
    {
        Directory.CreateDirectory(directory);
        string programsDirectory=Path.Combine(directory,"programs");
        Directory.CreateDirectory(programsDirectory);
        var bank=declaration.Programs;
        var recipes=new List<HdrProgramMediaRecipe>();
        var indices=new Dictionary<(int Vertex,int Pixel,uint Bindings),int>();
        var rows=new List<(HdrShaderProgram Program,int Recipe)>();
        foreach(var program in bank.Programs)
        {
            var key=(program.VertexProgramIndex,program.PixelProgramIndex,program.ResourceTableIdentity);
            if(!indices.TryGetValue(key,out int recipe))
            {
                recipe=recipes.Count;
                indices.Add(key,recipe);
                recipes.Add(new(recipe,program,Path.Combine(directory,"recipes",recipe.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            }
            rows.Add((program,recipe));
        }
        foreach(var program in bank.Programs.DistinctBy(program=>program.VertexProgramIndex))
            File.WriteAllBytes(Path.Combine(programsDirectory,program.VertexProgramIndex+".dxbc"),program.VertexBytes.ToArray());
        foreach(var program in bank.Programs.DistinctBy(program=>program.PixelProgramIndex))
            File.WriteAllBytes(Path.Combine(programsDirectory,program.PixelProgramIndex+".dxbc"),program.PixelBytes.ToArray());
        using var stream=File.Create(Path.Combine(directory,"bank.bin"));
        using var writer=new BinaryWriter(stream);
        writer.Write(0x31504248u); // HBP1: HDR program bank.
        writer.Write(declaration.NodeIndex);writer.Write((uint)declaration.View);writer.Write(declaration.LightGroup);
        var plugins=bank.Plugins;
        writer.Write((plugins.Ocean?1u:0u)|(plugins.Refraction?2u:0u)|
            (plugins.ThinSpecularSource?4u:0u)|(plugins.VolumetricFog?8u:0u)|(plugins.Caustics?16u:0u));
        writer.Write(bank.ConstructorColorFlags);
        writer.Write(bank.FeatureDeclarations.Length);writer.Write(bank.VariantDeclarations.Length);writer.Write(rows.Count);
        foreach(var feature in bank.FeatureDeclarations)writer.Write(HdrShaderCodec.WriteFeature(feature));
        foreach(var variant in bank.VariantDeclarations)writer.Write(HdrShaderCodec.WriteVariant(variant));
        foreach(var row in rows)
        {
            writer.Write(row.Program.CombinationIndex);writer.Write(row.Program.VertexProgramIndex);
            writer.Write(row.Program.PixelProgramIndex);writer.Write(row.Recipe);
        }
        return recipes.AsReadOnly();
    }
}
