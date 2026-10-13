using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The two type16 providers of node68, independent of node119/163.</summary>
public enum DeferredEarlyIndirectProvider { Primary, NoSubShadow }

public sealed record DeferredEarlyIndirectResourceDeclaration(string Name, uint PixelRegister,
    uint Dimension, uint ReturnType, short? PrimaryEncoded, short? SecondarySamplerEncoded);
public sealed record DeferredEarlyIndirectSamplerDeclaration(string Name, uint PixelRegister,
    uint BindCount, uint Flags);

/// <summary>An imported program row, not a current selection or a device object.</summary>
public sealed class DeferredEarlyIndirectProgram
{
    internal DeferredEarlyIndirectProgram(LoadedDeferredEarlyIndirectSource owner,
        DeferredEarlyIndirectProvider kind, NamedModelShader provider,
        LoadedShaderContainer shader, G1ShaderResourceBindings resources, DxbcResourceDefinitions definitions,
        IReadOnlyList<DeferredEarlyIndirectResourceDeclaration> textures)
    {
        (Owner, ProviderKind, Provider, Shader, Resources, PixelDefinitions, Textures) =
            (owner, kind, provider, shader, resources, definitions, textures);
        Samplers = Array.AsReadOnly(definitions.Bindings.Where(b => b.InputType == 3)
            .Select(b => new DeferredEarlyIndirectSamplerDeclaration(b.Name.Text, b.BindPoint, b.BindCount, b.Flags)).ToArray());
    }
    public LoadedDeferredEarlyIndirectSource Owner { get; }
    public DeferredEarlyIndirectProvider ProviderKind { get; }
    public NamedModelShader Provider { get; }
    public LoadedShaderContainer Shader { get; }
    public G1ShaderResourceBindings Resources { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public IReadOnlyList<DeferredEarlyIndirectResourceDeclaration> Textures { get; }
    /// <summary>Actual original declarations. Complete current wrapper keys
    /// and their plugin/caller writes must be supplied by the actual owner.</summary>
    public IReadOnlyList<DeferredEarlyIndirectSamplerDeclaration> Samplers { get; }
    public int CombinationIndex => Resources.Combination.Index;
    public int VertexProgramIndex => Resources.Combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null).Entry!.ProgramIndex;
    public int PixelProgramIndex => Resources.Combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null).Entry!.ProgramIndex;
}

/// <summary>Node68's loaded graph, providers and complete program directory.
/// It never supplies current native feature/variant keys, shader readiness or
/// GPU contents. The full current selectors remain separate inputs.</summary>
public sealed class LoadedDeferredEarlyIndirectSource
{
    private readonly Dictionary<DeferredEarlyIndirectProvider, G1ShaderBindingTables> tables = new();
    private readonly Dictionary<DeferredEarlyIndirectProvider, IReadOnlyList<G1ShaderFeatureRow>> featureRows = new();
    private readonly Dictionary<(DeferredEarlyIndirectProvider, int), DeferredEarlyIndirectProgram> programs = new();
    internal LoadedDeferredEarlyIndirectSource(LoadedRenderGraph graph, NamedModelShader primary,
        NamedModelShader noSubShadow, LoadedShaderContainer primaryShader, LoadedShaderContainer noSubShader)
    {
        Graph = graph; Primary = primary; NoSubShadow = noSubShadow;
        Add(DeferredEarlyIndirectProvider.Primary, primary, primaryShader, 3);
        Add(DeferredEarlyIndirectProvider.NoSubShadow, noSubShadow, noSubShader, 4);
        void Add(DeferredEarlyIndirectProvider kind, NamedModelShader provider,
            LoadedShaderContainer shader, uint features)
        {
            var bindings = shader.ReadG1Bindings();
            if (bindings.PassCount != 1 || !bindings.RawDimensionCounts.SequenceEqual(new uint[] { features, 0, 0, 0, 0, 0, 2 }) ||
                bindings.Variants.RecordWidth != 96 || bindings.Variants.Records.Count != 2)
                throw new InvalidDataException("Node68 G1 feature/variant directory differs from its actual provider.");
            tables.Add(kind, bindings);
            featureRows.Add(kind, Array.AsReadOnly(bindings.Features.Select(f => f.Feature).ToArray()));
            foreach (var row in bindings.Combinations.Rows)
            {
                var resources = shader.ReadG1ResourceBindings(row.Index);
                var stages = row.Stages.Where(s => s.Entry is not null).ToArray();
                if (stages.Length != 2 || stages.Count(s => s.StageCode == 1) != 1 || stages.Count(s => s.StageCode == 0) != 1)
                    throw new InvalidDataException("Node68 source program is not its actual VS/PS pair.");
                var definitions = shader.ReadResourceDefinitions(stages.Single(s => s.StageCode == 0).Entry!.ProgramIndex);
                var textures = definitions.Bindings.Where(b => b.InputType == 2).Select(binding =>
                {
                    var mapping = resources.Entries.SingleOrDefault(m => m.Name == binding.Name.Text);
                    if (mapping is not null && (mapping.SlotIndex != binding.BindPoint || binding.BindCount != 1))
                        throw new InvalidDataException("Node68 actual RDEF texture and G1 encoded setter differ.");
                    return new DeferredEarlyIndirectResourceDeclaration(binding.Name.Text, binding.BindPoint,
                        binding.Dimension, binding.ReturnType, mapping?.PrimaryEncoded, mapping?.SecondarySamplerEncoded);
                }).ToArray();
                programs.Add((kind, row.Index), new(this, kind, provider, shader, resources, definitions, Array.AsReadOnly(textures)));
            }
        }
    }
    public LoadedRenderGraph Graph { get; }
    public CompiledGraphNode Node => Graph.Document.Nodes[68];
    public NamedModelShader Primary { get; }
    public NamedModelShader NoSubShadow { get; }
    public uint LightGroupId => 8;
    public uint ColorWriteMask => 8;
    public uint DepthObjectId => 0xE13DDB4F;
    public uint LitBufferProviderId => 0xEA36C27D;
    public byte SourceProcessMode => 0;
    public IReadOnlyList<DeferredEarlyIndirectProgram> Programs =>
        Array.AsReadOnly(programs.Values.OrderBy(p => p.ProviderKind).ThenBy(p => p.CombinationIndex).ToArray());
    public string SourceEvidence => $"{Graph.Source.ResourceUri}@{Graph.Source.DecodedSha256};node68/000130CB/93FFBE0F;" +
        "CF60AA7C=0;own-primary3E4CCFB5/noSubCDC8F8D5;EAF120-mode1/EBCD70;no-selection-or-GPU-completion";

    /// <summary>980850→96DE40 uses the actual D76780 twelve-word packet.
    /// A row match alone does not select a native interface or certify a draw.</summary>
    public G1ShaderFeatureSelection SelectCurrentFeature(DeferredEarlyIndirectProvider provider,
        ReadOnlySpan<uint> actualD76780Words) => G1ShaderFeatureMatcher.Select(actualD76780Words, featureRows[provider]);

    /// <summary>Resolve explicit source selector outputs through the original
    /// seven-dimension table. This consumes the native selector outputs;
    /// it does not invent a feature or variant0 from provider presence.</summary>
    public DeferredEarlyIndirectProgram ResolveProgram(DeferredEarlyIndirectProvider provider,
        uint actualFeatureRow, uint actualVariantRow)
    {
        Span<uint> dimensions = stackalloc uint[7]; dimensions.Clear();
        dimensions[0] = actualFeatureRow; dimensions[6] = actualVariantRow;
        int index = tables[provider].ResolveCombination(dimensions, 0).Index;
        return programs[(provider, index)];
    }

    /// <summary>Actual2939 v188/A52890 compares all96B, unlike the separate
    /// generic renderer's predicate. Both keys must have been produced for
    /// this shader instance; NoSub presence is not a feature/variant producer.</summary>
    public DeferredEarlyIndirectProgram ResolveCurrentProgram(DeferredEarlyIndirectProvider provider,
        ReadOnlySpan<uint> actualD76780Words, ReadOnlySpan<byte> actualCurrentVariant96)
    {
        var feature = SelectCurrentFeature(provider, actualD76780Words);
        if (feature.RowIndex < 0) throw new InvalidDataException("Actual node68 feature has no original source row.");
        var variant = tables[provider].Variants.Match(actualCurrentVariant96) ??
            throw new InvalidDataException("Actual node68 variant has no original source row; native miss/retention remains required.");
        return ResolveProgram(provider, checked((uint)feature.RowIndex), checked((uint)variant.Index));
    }

    /// <summary>The actual full96B rows are metadata for the native callback,
    /// not a producer of the same instance's current96B selector cache.</summary>
    public ReadOnlyMemory<byte> ReadSourceVariant(DeferredEarlyIndirectProvider provider, int row)
    {
        var variants = tables[provider].Variants.Records;
        if ((uint)row >= variants.Count) throw new InvalidDataException("Node68 source variant row is out of range.");
        return variants[row].RawBytes.ToArray();
    }
    internal G1ShaderFeatureRow ReadFeatureRow(DeferredEarlyIndirectProvider provider, int row)
        => featureRows[provider][row];
    internal G1ShaderVariantRecord? MatchVariant(DeferredEarlyIndirectProvider provider, ReadOnlySpan<byte> current)
        => tables[provider].Variants.Match(current);
    internal uint AllowedVariantWordValues(DeferredEarlyIndirectProvider provider, int word)
    {
        uint result = 0;
        foreach (var row in tables[provider].Variants.Records)
            result |= 1u << (int)(BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes.Span.Slice(word * 4, 4)) & 31);
        return result;
    }
    public void RequireProgram(DeferredEarlyIndirectProgram program)
    {
        if (program is null || !ReferenceEquals(program.Owner, this) ||
            !programs.TryGetValue((program.ProviderKind, program.CombinationIndex), out var found) || !ReferenceEquals(program, found))
            throw new InvalidDataException("Node68 program belongs to another imported directory.");
    }
}

public static class DeferredEarlyIndirectSource
{
    public static LoadedDeferredEarlyIndirectSource Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        if (graph.Source.ResourceUri != "san14://rdb/system/024496EE")
            throw new InvalidDataException("Early68 graph resource reference differs.");
        graph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        var node = graph.Document.Nodes[68];
        uint Word(uint name)
        {
            var a = node.Call.Arguments.Single(a => a.NameHash == name);
            if (a.DescriptorWord != 0x05000001 || a.RawValue.Length != 4)
                throw new InvalidDataException("Node68 declared field differs from its typed source.");
            return BinaryPrimitives.ReadUInt32LittleEndian(a.RawValue);
        }
        var mode = node.Call.Arguments.Single(a => a.NameHash == 0xCF60AA7C);
        if (node.Call.FactoryClassId != 0x93FFBE0F || node.Call.LookupKey != 0x000130CB ||
            !node.Call.ConnectedNodeIndices.SequenceEqual(new uint[] { 69 }) ||
            Word(0xFA5A06A8) != 8 || Word(0x59BD6FC4) != 8 || Word(0xB7CCDD1F) != 0xE13DDB4F ||
            Word(0x61A17857) != 0xEA36C27D || Word(0xFF9A2626) != 0xEA36C27D ||
            mode.DescriptorWord != 0x01000001 || !mode.RawValue.AsSpan().SequenceEqual(new byte[] { 0 }))
            throw new InvalidDataException("Early68 source instance differs from its original group8/depth/Lit/mode0 recipe.");
        var objects = repository.LoadObjects("system", 0x718CCB8B); Verify(objects.Source, objects.Source.DecodedSha256);
        objects = objects with { Document = DokReader.Read(objects.Source.Bytes) };
        NamedModelShader Read(uint name, uint objectId, uint fileId)
        {
            if (Word(name) != objectId) throw new InvalidDataException("Node68 provider declaration differs.");
            var provider = ModelShaderResources.ReadDeclaredShader(objects, objectId);
            if (provider.Object.ClassId != 0x2939B4B1 || provider.ShaderFileHash != fileId || provider.DependencyObjectHashes.Count != 0)
                throw new InvalidDataException("Node68 needs its own generic type16 provider, not a dependency renderer.");
            return provider;
        }
        var primary = Read(0x7046CA70, 0x3E4CCFB5, 0x1667D3A0);
        var noSub = Read(0x3C2655F2, 0xCDC8F8D5, 0x1FD07CC0);
        LoadedShaderContainer Shader(NamedModelShader provider, string hash)
        {
            var shader = repository.LoadShaderContainer("system", provider.ShaderFileHash); Verify(shader.Source, hash);
            return shader;
        }
        return new(graph, primary, noSub,
            Shader(primary, "BB33C50FACECA62E45B020A78FA37B521D2A0AAD58E0D85829283DCEAB6961EE"),
            Shader(noSub, "CDFBC3C722B35EE0DFF1ECEE163AA68D8E802D5A0EC867DE20545E8364C15DC1"));
    }
    private static void Verify(LoadedRdbAsset asset, string expected)
    {
        if (asset.OverrideSource is not null || asset.DecodedSha256 != expected || Convert.ToHexString(SHA256.HashData(asset.Bytes)) != expected)
            throw new InvalidDataException("Early68 asset differs at its load/import boundary.");
    }
}
