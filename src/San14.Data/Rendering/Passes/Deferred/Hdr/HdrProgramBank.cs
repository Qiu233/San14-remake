using System.Collections.Frozen;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A resource program pair. It carries no variant, population or draw claim.</summary>
public sealed class HdrPreparedProgram
{
    private readonly byte[] vertex, pixel;
    internal HdrPreparedProgram(int vertexIndex,int pixelIndex,byte[] vertex,byte[] pixel,
        DxbcResourceDefinitions definitions)
    {
        VertexProgramIndex=vertexIndex; PixelProgramIndex=pixelIndex;
        this.vertex=vertex;this.pixel=pixel;PixelDefinitions=definitions;
        VertexSha256=Convert.ToHexString(SHA256.HashData(vertex));
        PixelSha256=Convert.ToHexString(SHA256.HashData(pixel));
    }
    public int VertexProgramIndex {get;}
    public int PixelProgramIndex {get;}
    public ReadOnlyMemory<byte> VertexBytes=>vertex;
    public ReadOnlyMemory<byte> PixelBytes=>pixel;
    public DxbcResourceDefinitions PixelDefinitions {get;}
    public string VertexSha256 {get;}
    public string PixelSha256 {get;}
    internal bool Matches(ReadOnlySpan<byte> vs,ReadOnlySpan<byte> ps)
        =>vs.SequenceEqual(vertex)&&ps.SequenceEqual(pixel);
}

public sealed class HdrShaderProgram
{
    private readonly G2ShaderCombination combination;
    private readonly G1ShaderResourceBindings bindings;
    internal HdrShaderProgram(G2ShaderCombination combination,HdrPreparedProgram prepared,
        G1ShaderResourceBindings bindings)
    {this.combination=combination;Prepared=prepared;this.bindings=bindings;}
    public int CombinationIndex=>combination.Index;
    public HdrPreparedProgram Prepared {get;}
    public int VertexProgramIndex=>Prepared.VertexProgramIndex;
    public int PixelProgramIndex=>Prepared.PixelProgramIndex;
    public ReadOnlyMemory<byte> VertexBytes=>Prepared.VertexBytes;
    public ReadOnlyMemory<byte> PixelBytes=>Prepared.PixelBytes;
    public DxbcResourceDefinitions PixelDefinitions=>Prepared.PixelDefinitions;
    public G1ShaderResourceBindings ResourceBindings=>bindings with
    {
        Combination=CompatibilityCombination,
        Entries=Array.AsReadOnly(bindings.Entries.Select(entry=>entry with {RawBytes=entry.RawBytes.ToArray()}).ToArray())
    };
    internal G2ShaderCombination CompatibilityCombination=>HdrShaderCodec.CopyCombination(combination);
}

public sealed record HdrProgramSelection(HdrFeatureKey Feature,HdrVariantKey Variant,
    G1ShaderFeatureSelection FeatureMatch,int VariantIndex,HdrShaderProgram Program);

/// <summary>One immutable import of a resource's HDR programs and selection
/// rules. Current frame inputs are values; the bank retains no Camera, View,
/// lighting owner, provenance receipt or loader-owned resource byte buffer.</summary>
public sealed class HdrProgramBank
{
    private readonly HdrFeatureKey[] features;
    private readonly HdrVariantKey[] variants;
    private readonly int[] variantOffsets;
    private readonly FrozenDictionary<HdrVariantKey,int> variantIndices;
    private readonly FrozenDictionary<HdrVariantField,uint> capabilities;
    private readonly HdrShaderPlugins plugins;
    private readonly HdrPreparedProgram[] preparedPrograms;
    private HdrProgramBank(HdrFeatureKey[] features,HdrVariantKey[] variants,int[] offsets,
        HdrShaderPlugins plugins,HdrShaderProgram[] programs,HdrPreparedProgram[] prepared,
        DxbcResourceDefinitions canonical)
    {
        this.features=features;this.variants=variants;variantOffsets=offsets;this.plugins=plugins;
        Programs=Array.AsReadOnly(programs);preparedPrograms=prepared;CanonicalPixelDefinitions=canonical;
        var indices=new Dictionary<HdrVariantKey,int>();
        for(int i=0;i<variants.Length;i++) indices.TryAdd(variants[i],i);
        variantIndices=indices.ToFrozenDictionary();
        capabilities=Enum.GetValues<HdrVariantField>().ToFrozenDictionary(field=>field,
            field=>variants.Aggregate(0u,(mask,key)=>mask|(1u<<(int)(HdrShaderCodec.ReadField(key,field)&31))));
    }
    public IReadOnlyList<HdrShaderProgram> Programs {get;}
    public HdrVariantKey InitialVariant=>variants[0];
    internal DxbcResourceDefinitions CanonicalPixelDefinitions {get;}

    public static HdrProgramBank Import(LoadedShaderContainer shader,bool oceanRendering=false)
    {
        // Capture the input resource once. Its mutable loader carrier is not a
        // current selection owner and cannot edit this imported generation.
        byte[] bytes=shader.Source.Bytes.ToArray();
        var owned=new LoadedShaderContainer(G2ShaderContainerReader.Read(bytes),shader.Source with {Bytes=bytes});
        var tables=owned.ReadG1Bindings();
        if(tables.PassCount!=1 || tables.RawDimensionCounts.Skip(1).Take(5).Any(count=>count>1))
            throw new InvalidDataException("HDR bank requires a single-pass feature/variant program grid.");
        var configs=G1ShaderConfigurationReader.Read(bytes,owned.Container);
        var thin=configs.Records.SingleOrDefault(config=>config.Name=="ThinSurfaceBSDF");
        var plugins=new HdrShaderPlugins(oceanRendering,configs.Records.Any(config=>config.Name=="Refraction"),
            thin?.Find(0xFEB72A6D)?.RawValue==1,configs.Records.Any(config=>config.Name=="VolumetricFog"));
        var prepared=new Dictionary<(int Vertex,int Pixel),HdrPreparedProgram>();
        var bindingTables=new Dictionary<uint,G1ShaderResourceBindings>();
        var programs=new List<HdrShaderProgram>();
        foreach(var combination in tables.Combinations.Rows)
        {
            int vertex=combination.Stages.Single(stage=>stage.StageCode==1&&stage.Entry is not null).Entry!.ProgramIndex;
            int pixel=combination.Stages.Single(stage=>stage.StageCode==0&&stage.Entry is not null).Entry!.ProgramIndex;
            if(!prepared.TryGetValue((vertex,pixel),out var pair))
            {
                pair=new(vertex,pixel,owned.Container.GetProgramBytes(bytes,vertex).ToArray(),
                    owned.Container.GetProgramBytes(bytes,pixel).ToArray(),HdrShaderCodec.FreezeDefinitions(owned.ReadResourceDefinitions(pixel)));
                prepared.Add((vertex,pixel),pair);
            }
            if(!bindingTables.TryGetValue(combination.RawValue4,out var binding))
            {
                binding=owned.ReadG1ResourceBindings(combination.Index);
                bindingTables.Add(combination.RawValue4,binding);
            }
            programs.Add(new(combination,pair,binding with {Combination=combination}));
        }
        return new(tables.Features.Select(row=>HdrShaderCodec.ReadFeature(row.Feature.RawBytes)).ToArray(),
            tables.Variants.Records.Select(row=>HdrShaderCodec.ReadVariant(row.RawBytes)).ToArray(),
            tables.Variants.Records.Select(row=>row.SourceOffset).ToArray(),plugins,programs.ToArray(),prepared.Values.ToArray(),
            HdrShaderCodec.FreezeDefinitions(owned.ReadResourceDefinitions(1)));
    }

    public HdrProgramSelection Select(HdrFeatureKey feature,HdrSelectionSettings settings)
    {
        var key=HdrShaderSelectionRules.PrepareVariant(variants[0],capabilities,plugins,settings);
        var match=HdrShaderSelectionRules.SelectFeature(feature,features);
        if(match.RowIndex<0)throw new InvalidDataException("HDR feature has no resource program match.");
        if(!variantIndices.TryGetValue(key,out int variant))
            throw new InvalidDataException("HDR requested variant has no resource program match.");
        return new(feature,key,match,variant,Programs[match.RowIndex*variants.Length+variant]);
    }

    public HdrOceanSelection SelectOcean(HdrFeatureKey feature,HdrSelectionSettings settings,HdrOceanRecord record)
    {
        if(!plugins.Ocean || settings.ColorMode!=HdrColorMode.Ocean || !record.IsAdmitted)
            throw new InvalidDataException("HDR Ocean selection requires an admitted current Ocean record.");
        return new(Select(feature,settings),record.StencilReference);
    }

    /// <summary>Resolve existing cached shader bytes to a resource program pair.
    /// This deliberately does not infer a current variant or resource binding.</summary>
    public HdrPreparedProgram ResolvePreparedProgram(ReadOnlySpan<byte> vertex,ReadOnlySpan<byte> pixel)
    {
        HdrPreparedProgram? found=null;
        foreach(var pair in preparedPrograms)
        {
            if(!pair.Matches(vertex,pixel))continue;
            if(found is not null)throw new InvalidDataException("Cached HDR bytecode has ambiguous resource program indices.");
            found=pair;
        }
        return found??throw new InvalidDataException("Cached HDR bytecode is not in the loaded resource program bank.");
    }

    internal G1ShaderVariantRecord CompatibilityVariant(HdrProgramSelection selection)
        =>new(selection.VariantIndex,variantOffsets[selection.VariantIndex],HdrShaderCodec.WriteVariant(selection.Variant));
}
