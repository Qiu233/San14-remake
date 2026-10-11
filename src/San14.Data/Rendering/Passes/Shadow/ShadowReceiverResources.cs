using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ShadowReceiverNodeResources(int NodeIndex, byte ProcessMode,
    uint PrimaryProvider, uint NoSubShadowProvider, uint LightGroup, uint ColorWriteMask,
    uint DepthObject, uint LightSourceObject, uint OverrideLightSourceObject);

/// <summary>The graph's receiver declarations and immutable shader banks.
/// Current lights, parameter state, program selection and device objects belong
/// to the receiver operation rather than these imported resources.</summary>
public sealed class ShadowReceiverResources
{
    private readonly IReadOnlyDictionary<uint, LoadedShaderContainer> shaders;

    private ShadowReceiverResources(ShadowReceiverNodeResources[] nodes,
        Dictionary<uint, LoadedShaderContainer> shaders)
    {
        Nodes = Array.AsReadOnly(nodes);
        this.shaders = new ReadOnlyDictionary<uint, LoadedShaderContainer>(shaders);
    }

    public IReadOnlyList<ShadowReceiverNodeResources> Nodes { get; }
    public IReadOnlyDictionary<uint, LoadedShaderContainer> Providers => shaders;

    public static ShadowReceiverResources Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Import(repository.LoadRenderGraph("system", 0x024496EE),
            repository.LoadObjects("system", 0x718CCB8B),
            file => repository.LoadShaderContainer("system", file));
    }

    /// <summary>Imports the supplied parsed documents once and permits normal
    /// repository overrides. Resource references determine each shader.</summary>
    public static ShadowReceiverResources Import(LoadedRenderGraph graph,
        LoadedObjects declarations, Func<uint, LoadedShaderContainer> loadShader)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(loadShader);
        var shaders = new Dictionary<uint, LoadedShaderContainer>();
        var nodes = new[] { ReadNode(68), ReadNode(119) };
        return new(nodes, shaders);

        ShadowReceiverNodeResources ReadNode(int index)
        {
            var node = graph.Document.Nodes[index];
            if (node.Call.FactoryClassId != 0x93FFBE0F)
                throw new NotSupportedException("The map receiver requires its declared receiver operation.");
            uint Word(uint name)
            {
                var field = node.Call.Arguments.Single(value => value.NameHash == name);
                if (field.DescriptorWord != 0x05000001 || field.RawValue.Length != 4)
                    throw new InvalidDataException("Receiver graph field requires one unsigned resource word.");
                return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
            }
            var process = node.Call.Arguments.Single(value => value.NameHash == 0xCF60AA7C);
            if (process.DescriptorWord != 0x01000001 || process.RawValue.Length != 1 ||
                process.RawValue[0] is not (0 or 0x50))
                throw new NotSupportedException("Receiver process mode needs its own current operation.");
            uint primary = Word(0x7046CA70), noSub = Word(0x3C2655F2);
            Add(primary); Add(noSub);
            return new(index, process.RawValue[0], primary, noSub, Word(0xFA5A06A8),
                Word(0x59BD6FC4), Word(0xB7CCDD1F), Word(0x61A17857), Word(0xFF9A2626));
        }
        void Add(uint id)
        {
            if (id == 0) throw new NotSupportedException("Receiver provider is absent from the graph declaration.");
            if (shaders.ContainsKey(id)) return;
            var provider = ModelShaderResources.ReadDeclaredShader(declarations, id);
            if (provider.Object.ClassId != 0x2939B4B1 || provider.DependencyObjectHashes.Count != 0)
                throw new NotSupportedException("Receiver provider needs its own declared shader interface.");
            shaders.Add(id, loadShader(provider.ShaderFileHash));
        }
    }

    public void WriteNativeInputs(string inputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        string root = Path.Combine(inputRoot, "normal-pattern-receiver-input");
        Directory.CreateDirectory(root);
        File.WriteAllLines(Path.Combine(root, "nodes.tsv"), Nodes.Select(node => FormattableString.Invariant(
            $"{node.NodeIndex} {node.ProcessMode} {node.PrimaryProvider} {node.NoSubShadowProvider} {node.LightGroup} {node.ColorWriteMask} {node.DepthObject} {node.LightSourceObject} {node.OverrideLightSourceObject}")));
        foreach (var provider in shaders)
            WriteBank(provider.Value, Path.Combine(root, "program-bank", provider.Key.ToString(CultureInfo.InvariantCulture)));
    }

    private static void WriteBank(LoadedShaderContainer shader, string directory)
    {
        var bindings = shader.ReadG1Bindings();
        if (bindings.PassCount != 1 || bindings.Variants.RecordWidth != 96 ||
            bindings.Features.Count == 0 || bindings.Variants.Records.Count == 0)
            throw new NotSupportedException("Receiver shader requires its single-pass feature and full variant bank.");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "features.bin"), bindings.Features.SelectMany(row => row.Feature.RawBytes).ToArray());
        File.WriteAllBytes(Path.Combine(directory, "variants.bin"), bindings.Variants.Records.SelectMany(row => row.RawBytes).ToArray());
        var definitions = new Dictionary<int, DxbcResourceDefinitions>();
        var recipes = new Dictionary<(int Vertex, int Pixel, bool OtherStages), int>();
        var programs = new HashSet<int>();
        var rows = new List<string>();
        for (int feature = 0; feature < bindings.Features.Count; feature++)
            for (int variant = 0; variant < bindings.Variants.Records.Count; variant++)
            {
                var combination = bindings.ResolveCombination([(uint)feature, 0, 0, 0, 0, 0, (uint)variant], 0);
                foreach (var stage in combination.Stages.Where(stage => stage.Entry is not null))
                {
                    int program = stage.Entry!.ProgramIndex;
                    if (!programs.Add(program)) continue;
                    string target = Path.Combine(directory, "programs", program.ToString(CultureInfo.InvariantCulture) + ".dxbc");
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, shader.Container.GetProgramBytes(shader.Source.Bytes, program).ToArray());
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
                        recipe = recipes.Count; recipes.Add(pair, recipe);
                        string path = Path.Combine(directory, "recipes", recipe.ToString(CultureInfo.InvariantCulture));
                        Directory.CreateDirectory(path);
                        try
                        {
                            if (others) throw new NotSupportedException("Receiver graphics stages need another material producer.");
                            RequireFullscreenInterface(shader.ReadSignatures(vertex), shader.ReadSignatures(pixel));
                            var layout = ShadowReceiverMaterialLayout.Import(Definitions(vertex), Definitions(pixel));
                            File.WriteAllBytes(Path.Combine(path, "layout.bin"), layout.EncodeNativeLayout());
                            File.Delete(Path.Combine(path, "unsupported.txt"));
                        }
                        catch (NotSupportedException error)
                        {
                            File.WriteAllText(Path.Combine(path, "unsupported.txt"), error.Message + "\n", Encoding.UTF8);
                            File.Delete(Path.Combine(path, "layout.bin"));
                        }
                    }
                }
                rows.Add(FormattableString.Invariant($"{feature} {variant} {combination.Index} {vertex} {pixel} {recipe}"));
            }
        File.WriteAllText(Path.Combine(directory, "bank.txt"), FormattableString.Invariant(
            $"{bindings.Features.Count} {bindings.Variants.Records.Count} {rows.Count} {bindings.Variants.RecordWidth}\n"));
        File.WriteAllLines(Path.Combine(directory, "programs.txt"), rows);

        DxbcResourceDefinitions Definitions(int program)
        {
            if (!definitions.TryGetValue(program, out var value))
            { value = shader.ReadResourceDefinitions(program); definitions.Add(program, value); }
            return value;
        }
    }

    private static void RequireFullscreenInterface(DxbcProgramSignatures vertex, DxbcProgramSignatures pixel)
    {
        var inputs = vertex.Parts.Where(part => part.Kind == DxbcSignatureKind.Input).SelectMany(part => part.Parameters);
        if (inputs.Any(input => !input.SemanticName.Text.Equals("SV_VertexID", StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Receiver needs a vertex input beyond the fullscreen vertex ID.");
        var outputs = pixel.Parts.Where(part => part.Kind == DxbcSignatureKind.Output).SelectMany(part => part.Parameters).ToArray();
        if (outputs.Length != 1 || !outputs[0].SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) ||
            outputs[0].SemanticIndex != 0 || outputs[0].Register != 0)
            throw new NotSupportedException("Receiver requires another output attachment producer.");
    }
}
