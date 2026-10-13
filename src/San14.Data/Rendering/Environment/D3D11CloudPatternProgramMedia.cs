using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual Cloud pass-five program bank and imported Pattern
/// material layouts. Selection belongs to the current Native renderer state.</summary>
public sealed class D3D11CloudPatternProgramMedia
{
    private readonly IReadOnlyDictionary<string, byte[]> files;

    private D3D11CloudPatternProgramMedia(SkyCloudProgramResources resources, Dictionary<string, byte[]> files)
    { Resources = resources; this.files = files; }

    public SkyCloudProgramResources Resources { get; }

    public static D3D11CloudPatternProgramMedia Prepare(SkyCloudProgramResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var bindings = resources.Bindings;
        if (bindings.PassCount <= 5)
            throw new NotSupportedException("The Cloud shader has no Pattern pass five.");
        var shader = resources.Shader;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var definitions = new Dictionary<int, DxbcResourceDefinitions>();
        var signatures = new Dictionary<int, DxbcProgramSignatures>();
        var recipes = new Dictionary<(int Vertex, int Pixel, bool OtherStages), int>();
        var rows = new List<string>();
        files.Add("features.bin", bindings.Features.SelectMany(row => row.Feature.RawBytes.ToArray()).ToArray());
        files.Add("variants.bin", bindings.Variants.Records.SelectMany(row => row.RawBytes.ToArray()).ToArray());
        for (int feature = 0; feature < bindings.Features.Count; feature++)
            for (int variant = 0; variant < bindings.Variants.Records.Count; variant++)
            {
                var combination = bindings.ResolveCombination([(uint)feature, 0, 0, 0, 0, 0, (uint)variant], 5);
                foreach (var stage in combination.Stages.Where(stage => stage.Entry is not null))
                {
                    int program = stage.Entry!.ProgramIndex;
                    string filename = "programs/" + program.ToString(CultureInfo.InvariantCulture) + ".dxbc";
                    if (!files.ContainsKey(filename))
                        files.Add(filename, shader.Container.GetProgramBytes(shader.Source.Bytes, program).ToArray());
                }
                int vertex = combination.Stages.Single(stage => stage.StageCode == 1).Entry?.ProgramIndex ?? -1;
                int pixel = combination.Stages.Single(stage => stage.StageCode == 0).Entry?.ProgramIndex ?? -1;
                int recipe = -1;
                if (vertex >= 0 && pixel >= 0)
                {
                    bool others = combination.Stages.Any(stage => stage.Entry is not null && stage.StageCode is not (0 or 1));
                    var pair = (vertex, pixel, others);
                    if (!recipes.TryGetValue(pair, out recipe))
                    {
                        recipe = recipes.Count;
                        recipes.Add(pair, recipe);
                        string directory = "recipes/" + recipe.ToString(CultureInfo.InvariantCulture) + "/";
                        try
                        {
                            if (others) throw new NotSupportedException("Pattern requires vertex and pixel stages without another graphics stage.");
                            RequireFullscreenInterface(Signatures(vertex), Signatures(pixel));
                            var layout = SkyCloudPatternMaterialLayout.Import(Definitions(vertex), Definitions(pixel));
                            files.Add(directory + "layout.bin", layout.EncodeNativeLayout());
                        }
                        catch (NotSupportedException error)
                        {
                            // A complete bank can contain unselected interfaces
                            // for which this operation has no material producer.
                            files.Add(directory + "unsupported.txt", Encoding.UTF8.GetBytes(error.Message + "\n"));
                        }
                    }
                }
                rows.Add(FormattableString.Invariant($"{feature} {variant} {combination.Index} {vertex} {pixel} {recipe}"));
            }
        files.Add("bank.txt", Encoding.UTF8.GetBytes(FormattableString.Invariant(
            $"{bindings.Features.Count} {bindings.Variants.Records.Count} {rows.Count}\n")));
        files.Add("programs.txt", Encoding.UTF8.GetBytes(string.Join("\n", rows) + "\n"));
        return new(resources, files);

        DxbcResourceDefinitions Definitions(int program)
        {
            if (!definitions.TryGetValue(program, out var value))
            { value = shader.ReadResourceDefinitions(program); definitions.Add(program, value); }
            return value;
        }
        DxbcProgramSignatures Signatures(int program)
        {
            if (!signatures.TryGetValue(program, out var value))
            { value = shader.ReadSignatures(program); signatures.Add(program, value); }
            return value;
        }
    }

    private static void RequireFullscreenInterface(DxbcProgramSignatures vertex, DxbcProgramSignatures pixel)
    {
        var inputs = vertex.Parts.Where(part => part.Kind == DxbcSignatureKind.Input)
            .SelectMany(part => part.Parameters);
        if (inputs.Any(input => !input.SemanticName.Text.Equals("SV_VertexID", StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Pattern vertex inputs require a producer beyond the fullscreen vertex ID.");
        var outputs = pixel.Parts.Where(part => part.Kind == DxbcSignatureKind.Output)
            .SelectMany(part => part.Parameters).ToArray();
        if (outputs.Length != 1 ||
            !outputs[0].SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) ||
            outputs[0].SemanticIndex != 0 || outputs[0].Register != 0)
            throw new NotSupportedException("Pattern pixel output requires a target beyond its single color attachment.");
    }

    public void WriteNativeInputs(string inputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        string root = Path.Combine(inputRoot, "pattern-input", "program-bank");
        Directory.CreateDirectory(root);
        foreach (var file in files)
        {
            string path = Path.Combine(root, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // A replacement import may change a recipe's support status.
            // Do not let an old status file override its new actual layout.
            string? obsolete = file.Key.EndsWith("/layout.bin", StringComparison.Ordinal) ? "unsupported.txt" :
                file.Key.EndsWith("/unsupported.txt", StringComparison.Ordinal) ? "layout.bin" : null;
            if (obsolete is not null)
            {
                string old = Path.Combine(Path.GetDirectoryName(path)!, obsolete);
                if (File.Exists(old)) File.Delete(old);
            }
            File.WriteAllBytes(path, file.Value);
        }
    }
}
