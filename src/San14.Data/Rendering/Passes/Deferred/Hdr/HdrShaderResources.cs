using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A graph declaration and its resource program bank. Current View
/// population, renderer queries and stage flags are selection inputs, not
/// properties inferred from a cached picture or shader declaration.</summary>
public sealed class HdrShaderDeclaration
{
    internal HdrShaderDeclaration(ClusteredDeferredShaderReference reference,
        LoadedShaderContainer shader)
    {
        Reference = reference;
        Shader = shader;
        View = ByteArgument(ViewArgument);
        LightGroup = WordArgument(LightGroupArgument);
        OceanRendering = ByteArgument(OceanArgument) != 0;
        MaterialObjectId = WordArgument(MaterialObjectArgument);
        Programs = HdrProgramBank.Import(shader, OceanRendering);
    }

    private const uint ViewArgument = 0x9075EDA0;
    private const uint LightGroupArgument = 0x59BD6FC4;
    private const uint OceanArgument = 0xE8F3DC36;
    private const uint MaterialObjectArgument = 0x9826997D;
    public ClusteredDeferredShaderReference Reference { get; }
    public LoadedShaderContainer Shader { get; }
    public int NodeIndex => Reference.Node.Index;
    public byte View { get; }
    public uint LightGroup { get; }
    public bool OceanRendering { get; }
    public uint MaterialObjectId { get; }
    public HdrProgramBank Programs { get; }

    private TaskGraphArgument Argument(uint name) =>
        Reference.Node.Call.Arguments.Single(argument => argument.NameHash == name);
    private byte ByteArgument(uint name) => Argument(name).RawValue.Single();
    private uint WordArgument(uint name) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Argument(name).RawValue);
}

/// <summary>The normal map HDR declarations. Graph references select the
/// shader files; source programs and referenced material tables are imported
/// once for this loaded scene. No executable or experiment receipt is used.</summary>
public sealed class HdrShaderResources
{
    private const uint MapRenderGraph = 0x024496EE;
    private const uint ShaderObjects = 0x718CCB8B;
    public const int MainLightingNode = 166;
    private readonly IReadOnlyDictionary<int, HdrShaderDeclaration> declarations;
    private readonly IReadOnlyDictionary<uint, byte[]> materialTables;

    private HdrShaderResources(IReadOnlyDictionary<int, HdrShaderDeclaration> declarations,
        IReadOnlyDictionary<uint, byte[]> materialTables)
    {
        this.declarations = declarations;
        this.materialTables = materialTables;
    }

    public IEnumerable<HdrShaderDeclaration> Declarations => declarations.Values;
    public ReadOnlyMemory<byte> MaterialTable(int node) => materialTables[declarations[node].MaterialObjectId];
    public HdrShaderDeclaration this[int node] => declarations[node];

    public static HdrShaderResources Load(GameResourceRepository repository)
    {
        var graph = repository.LoadRenderGraph("system", MapRenderGraph);
        var objects = repository.LoadObjects("system", ShaderObjects);
        var references = RenderGraphShaderReferences.Read(graph, objects);
        // These are graph node identities, rather than executable addresses.
        int[] normalHdrNodes = [77, 78, 79, 80, 123, MainLightingNode];
        var declarations = new Dictionary<int, HdrShaderDeclaration>();
        foreach (int node in normalHdrNodes)
        {
            var reference = references.References.Single(value => value.Node.Index == node);
            declarations.Add(node, new(reference,
                repository.LoadShaderContainer("system", reference.ShaderFileHash)));
        }
        var materials = declarations.Values.Select(declaration => declaration.MaterialObjectId).Distinct()
            .ToDictionary(id => id, id => HdrMaterialTable.Import(repository, id));
        return new(new System.Collections.ObjectModel.ReadOnlyDictionary<int, HdrShaderDeclaration>(declarations),
            materials);
    }

    public void WritePreparedProgram(string directory, HdrShaderDeclaration declaration, HdrPreparedProgram program)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "vs.dxbc"), program.VertexBytes.ToArray());
        File.WriteAllBytes(Path.Combine(directory, "ps.dxbc"), program.PixelBytes.ToArray());
        string materialPath = Path.Combine(directory, "material-table.bin");
        var materialTable = MaterialTable(declaration.NodeIndex);
        bool usesMaterialTable = program.PixelDefinitions.Bindings.Any(binding =>
            binding.InputType == 2 && binding.Name.Text == "tMaterialTable");
        if (usesMaterialTable && materialTable.Length != 0) File.WriteAllBytes(materialPath, materialTable.ToArray());
        else File.Delete(materialPath);
    }
}
