using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalDeferredAuxiliaryPixelStage(uint StageCode, int ProgramIndex,
    string ProgramSha256, DxbcResourceDefinitions Definitions, DxbcProgramSignatures Signatures);
public sealed record OriginalDeferredAuxiliaryPixelPass(uint PassIndex, G2ShaderCombination Combination,
    G1ShaderResourceBindings Resources, IReadOnlyList<OriginalDeferredAuxiliaryPixelStage> Stages);

public sealed class OriginalDeferredAuxiliaryShaderSource
{
    public LoadedObjects SourceObjects { get; }
    public DokObject Object { get; }
    public DokProperty ShaderReference { get; }
    public LoadedShaderContainer Shader { get; }
    internal OriginalDeferredAuxiliaryShaderSource(LoadedObjects objects, DokObject provider,
        DokProperty reference, LoadedShaderContainer shader)
        => (SourceObjects,Object,ShaderReference,Shader)=(objects,provider,reference,shader);

    /// <summary>Read an actual pass with all seven explicitly resolved current
    /// dimensions. This method does not infer the current native shader selector.</summary>
    public OriginalDeferredAuxiliaryPixelPass ReadPass(uint passIndex, IReadOnlyList<uint> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        RequireOriginalShaderBytes();
        var row=Shader.ReadG1Bindings().ResolveCombination(dimensions.ToArray(),passIndex);
        var resources=Shader.ReadG1ResourceBindings(row.Index);
        var stages=row.Stages.Where(s=>s.Entry is not null).Select(s=>new OriginalDeferredAuxiliaryPixelStage(
            s.StageCode,s.Entry!.ProgramIndex,
            Convert.ToHexString(SHA256.HashData(Shader.Container.GetProgramBytes(Shader.Source.Bytes,s.Entry.ProgramIndex))),
            Shader.ReadResourceDefinitions(s.Entry.ProgramIndex),Shader.ReadSignatures(s.Entry.ProgramIndex))).ToArray();
        return new(passIndex,row,resources,Array.AsReadOnly(stages));
    }

    /// <summary>1056A70 compares the filter type, and compares size only for
    /// Gaussian type1. GGX retains a former size without changing its variant.</summary>
    public OriginalDeferredAuxiliaryPixelPass ResolveFilterPass(uint filterType,uint gaussianFeatureSize,uint passIndex)
    {
        RequireOriginalShaderBytes();
        var tables=Shader.ReadG1Bindings();
        if(tables.Variants.RecordWidth!=8 || tables.RawDimensionCounts.Take(6).Any(v=>v>1))
            throw new InvalidDataException("Auxiliary source filter dimensions differ from their native condition contract.");
        var variant=tables.Variants.Records.FirstOrDefault(v=>
            BinaryPrimitives.ReadUInt32LittleEndian(v.RawBytes)==filterType &&
            (filterType!=1 || BinaryPrimitives.ReadUInt32LittleEndian(v.RawBytes.AsSpan(4))==gaussianFeatureSize));
        if(variant is null)throw new InvalidDataException("Auxiliary filter condition has no verified original shader variant.");
        return ReadPass(passIndex,new uint[]{0,0,0,0,0,0,(uint)variant.Index});
    }
    private void RequireOriginalShaderBytes()
    {
        string expected=Shader.Source.FileId switch
        {
            OriginalDeferredAuxiliaryPixelSourceRecipes.FilterShaderFileId=>OriginalDeferredAuxiliaryPixelSourceRecipes.FilterShaderSha256,
            OriginalDeferredAuxiliaryPixelSourceRecipes.DepthFilterShaderFileId=>OriginalDeferredAuxiliaryPixelSourceRecipes.DepthFilterShaderSha256,
            _=>throw new InvalidDataException("Auxiliary shader left its finite original provider set.")
        };
        if(Shader.Source.OverrideSource is not null || Shader.Source.DecodedSha256!=expected ||
            Convert.ToHexString(SHA256.HashData(Shader.Source.Bytes))!=expected)
            throw new InvalidDataException("Auxiliary shader source changed after its original recipe was parsed.");
    }
}

/// <summary>Actual node137 shader and texture owners. Its execution object is
/// distinct from the factory's optional fog method. Pixel writes require the
/// current View gate, source filter mode, source constants and completed inputs.</summary>
public sealed class LoadedOriginalDeferredAuxiliaryPixelSourceRecipes
{
    public LoadedRenderGraph Graph { get; }
    public CompiledGraphNode Node { get; }
    public byte ViewId { get; }
    public OriginalDeferredAuxiliaryShaderSource FilterShader { get; }
    public OriginalDeferredAuxiliaryShaderSource DepthFilterShader { get; }
    public OriginalRenderTextureDefinition MainDepth { get; }
    public IReadOnlyList<OriginalDeferredAuxiliaryTargetSource> Targets { get; }
    public IReadOnlyList<OriginalDeferredAuxiliaryTargetSource> Intermediates { get; }
    public OriginalDeferredAuxiliaryTargetSource BlurredDepth => Targets.Single(t=>t.Texture.Object.ObjectId==0x18C86C69);
    public OriginalDeferredAuxiliaryTargetSource DepthIntermediate => Intermediates.Single(t=>t.Texture.Object.ObjectId==0xD50F0811);
    internal LoadedOriginalDeferredAuxiliaryPixelSourceRecipes(LoadedRenderGraph graph,CompiledGraphNode node,
        byte viewId,OriginalDeferredAuxiliaryShaderSource filter,OriginalDeferredAuxiliaryShaderSource depth,
        OriginalRenderTextureDefinition mainDepth,IReadOnlyList<OriginalDeferredAuxiliaryTargetSource> targets,
        IReadOnlyList<OriginalDeferredAuxiliaryTargetSource> intermediates)
        => (Graph,Node,ViewId,FilterShader,DepthFilterShader,MainDepth,Targets,Intermediates)=
            (graph,node,viewId,filter,depth,mainDepth,targets,intermediates);
    public OriginalDeferredAuxiliaryTargetSource SelectSpecular(OriginalDeferredTransparencyImageSelection selection)
        => Targets.Single(t=>t.Texture.Object.ObjectId==(selection.SourceIndex==0?0x45A0F6C7u:0x93637E41u));
    public OriginalDeferredAuxiliaryTargetSource SelectSpecularIntermediate(OriginalDeferredTransparencyImageSelection selection)
        => Intermediates.Single(t=>t.Texture.Object.ObjectId==(selection.SourceIndex==0?0xDD50021Fu:0x13D2F0E5u));
}

public static class OriginalDeferredAuxiliaryPixelSourceRecipes
{
    public const string GraphResourceUri="san14://rdb/system/024496EE";
    public const string GraphSha256="B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    public const string ShaderObjectsResourceUri="san14://rdb/system/718CCB8B";
    public const string ShaderObjectsSha256="67115E16E1B21065B4F04E25109F8182AB027B7DBD38B98979CB02BCFD1291B9";
    public const string FilterShaderSha256="B8CC0B5F37CF7DDFBC7664C8C30AEAC901E3F6DBBB470069DE0AD43DD8EC7CB2";
    public const string DepthFilterShaderSha256="82A24C78A84F840CA11849E692C86586C834C9E6E36F039D7BBA80D6CA3A9970";
    public const uint FactoryClassId=0xC41F73B1, FilterObjectId=0xB208D0BA,
        DepthFilterObjectId=0x5929A0F6, FilterShaderFileId=0xEC36DC74, DepthFilterShaderFileId=0x98327BB0;

    /// <summary>C9AAE0:nonnegative current LData+EDC overrides the depth filter.
    /// A negative override reads current CommonView+98; explicitly absent
    /// CommonView returns literal1. Null here means absence, not missing data.</summary>
    public static int ReadDepthFilterType(ReadOnlySpan<byte> completeCurrentLightData,int? explicitCurrentCommonView98)
    {
        if(completeCurrentLightData.Length!=OriginalMainLightConstruction.LightDataBytes)
            throw new InvalidDataException("Depth filter selection requires complete current10C0 LData.");
        int current=BinaryPrimitives.ReadInt32LittleEndian(completeCurrentLightData.Slice(0xEDC,4));
        return current>=0?current:explicitCurrentCommonView98??1;
    }

    public static LoadedOriginalDeferredAuxiliaryPixelSourceRecipes Read(LoadedRenderGraph graph,
        LoadedObjects shaderObjects,LoadedObjects textureObjects,LoadedShaderContainer filterShader,
        LoadedShaderContainer depthFilterShader,int nativeFrameWidth3E8,int nativeFrameHeight3EC)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(shaderObjects);
        ArgumentNullException.ThrowIfNull(textureObjects);ArgumentNullException.ThrowIfNull(filterShader);
        ArgumentNullException.ThrowIfNull(depthFilterShader);
        Exact(graph.Source,GraphResourceUri,GraphSha256);
        Exact(shaderObjects.Source,ShaderObjectsResourceUri,ShaderObjectsSha256);
        var canonicalGraph=new LoadedRenderGraph(CompiledGraphReader.Read(graph.Source.Bytes,OriginalCompiledGraphKind.RenderGraph),graph.Source);
        var canonicalObjects=new LoadedObjects(DokReader.Read(shaderObjects.Source.Bytes),shaderObjects.Source);
        var node=canonicalGraph.Document.Nodes[137];
        if(node.Call.FactoryClassId!=FactoryClassId || node.Call.Arguments.Count!=10)
            throw new InvalidDataException("Auxiliary filter node137 source factory or argument shape differs.");
        TaskGraphArgument Arg(uint hash,uint descriptor,int length)
        {
            var args=node.Call.Arguments.Where(a=>a.NameHash==hash && a.DescriptorWord==descriptor).ToArray();
            if(args.Length!=1 || args[0].RawValue.Length!=length)
                throw new InvalidDataException("Auxiliary filter requires an explicit typed source field "+hash.ToString("X8"));
            return args[0];
        }
        uint U(uint name)=>BinaryPrimitives.ReadUInt32LittleEndian(Arg(name,0x05000001,4).RawValue);
        byte view=Arg(0x9075EDA0,0x01000001,1).RawValue[0];
        (uint Field,uint Id)[] fields=[(0x29DD7D66,FilterObjectId),(0x877B56E1,DepthFilterObjectId),
            (0x1F5E2225,0x45A0F6C7),(0xAC84A635,0x93637E41),(0x30C9C178,0xDD50021F),
            (0xB6114288,0x13D2F0E5),(0x56AB46F0,0xE13DDB4F),(0x4DAA4058,0x18C86C69),(0xC0FB7D85,0xD50F0811)];
        if(view!=0 || fields.Any(f=>U(f.Field)!=f.Id))
            throw new InvalidDataException("Auxiliary filter source View, shader or target identity differs.");
        var filter=Shader(canonicalObjects,FilterObjectId,FilterShaderFileId,FilterShaderSha256,filterShader);
        var depth=Shader(canonicalObjects,DepthFilterObjectId,DepthFilterShaderFileId,DepthFilterShaderSha256,depthFilterShader);
        var targets=OriginalDeferredAuxiliaryTargetSources.Read(textureObjects,nativeFrameWidth3E8,nativeFrameHeight3EC);
        var intermediates=OriginalDeferredAuxiliaryTargetSources.ReadIntermediates(textureObjects,nativeFrameWidth3E8,nativeFrameHeight3EC);
        var canonicalTextures=new LoadedObjects(DokReader.Read(textureObjects.Source.Bytes),textureObjects.Source);
        return new(canonicalGraph,node,view,filter,depth,OriginalRenderTextureResources.Read(canonicalTextures,0xE13DDB4F),targets,intermediates);
    }

    private static OriginalDeferredAuxiliaryShaderSource Shader(LoadedObjects objects,uint objectId,uint fileId,string hash,LoadedShaderContainer shader)
    {
        var providers=objects.Document.Objects.Where(o=>o.ObjectId==objectId).ToArray();
        if(providers.Length!=1 || providers[0].Kind!=DokObjectKind.Inline || providers[0].ClassId!=0x01818D62)
            throw new InvalidDataException("Auxiliary source filter requires its exact inline shader provider.");
        var refs=providers[0].Properties.Where(p=>p.NameHash==0x193025DF).ToArray();
        if(refs.Length!=1 || refs[0].TypeCode!=5 || refs[0].ElementCount!=1 || refs[0].RawValue.Length!=4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(refs[0].RawValue)!=fileId || shader.Source.FileId!=fileId ||
            shader.Source.OverrideSource is not null || shader.Source.ResourceUri!=$"san14://rdb/system/{fileId:X8}" ||
            shader.Source.DecodedSha256!=hash || Convert.ToHexString(SHA256.HashData(shader.Source.Bytes))!=hash)
            throw new InvalidDataException("Auxiliary shader bytes or G1S reference differ from the actual source provider.");
        var canonicalShader=new LoadedShaderContainer(G2ShaderContainerReader.Read(shader.Source.Bytes),shader.Source);
        _=canonicalShader.ReadG1Bindings();
        return new(objects,providers[0],refs[0],canonicalShader);
    }
    private static void Exact(LoadedRdbAsset source,string uri,string hash)
    {
        if(source.OverrideSource is not null || source.ResourceUri!=uri || source.DecodedSha256!=hash ||
            Convert.ToHexString(SHA256.HashData(source.Bytes))!=hash)
            throw new InvalidDataException("Auxiliary recipe requires its complete original source bytes: "+uri);
    }
}
