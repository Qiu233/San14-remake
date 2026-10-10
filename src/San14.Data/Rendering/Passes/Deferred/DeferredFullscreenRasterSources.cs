using San14.Data.Formats;

namespace San14.Data;

public enum DeferredFullscreenRasterSourceKind { F103, Copy97, Filter137 }
public enum DeferredFullscreenRasterExitKind
{
    SourceShaderCleanupAndStencilRestore,
    RestorePriorCullRetainDisabledBlend,
    RestorePriorCullAndBlend
}

/// <summary>An exact draw-time source recipe. It is neither a successful GPU
/// receipt nor a claim about the current frontend after the complete graph.</summary>
public sealed class DeferredFullscreenRasterDefinition
{
    internal DeferredFullscreenRasterDefinition(DeferredFullscreenRasterSourceKind kind,
        int node, DeferredFullscreenRasterExitKind exit, string provenance)
    { Kind=kind; NodeIndex=node; Exit=exit; SourceProvenance=provenance; }
    public DeferredFullscreenRasterSourceKind Kind { get; }
    public int NodeIndex { get; }
    /// <summary>Native D3D Cull NONE. The Game adapter maps this to its Disabled enum.</summary>
    public uint NativeCullMode => 1;
    public bool EnableBlend => false;
    public DeferredFullscreenRasterExitKind Exit { get; }
    public string SourceProvenance { get; }
}

/// <summary>Finite actual node166 F103, node97 depth/scene copy and node137
/// filter state writers. Additional/imported shader hooks and mutable current
/// state owners require a separate recipe. No state is inferred for139/140
/// no-op geometry, other source factories, or a source graph's final state.</summary>
public sealed class DeferredFullscreenRasterSources
{
    private readonly DeferredFullscreenRasterDefinition f103,copy,filter;
    private bool retained=true;
    private DeferredFullscreenRasterSources(string locale,string ruleVersion)
    {
        Locale=locale; RuleVersion=ruleVersion;
        string prefix=$"fullscreen-source/{locale}/{ruleVersion};024496EE;owned-hooks;";
        f103=new(DeferredFullscreenRasterSourceKind.F103,166,
            DeferredFullscreenRasterExitKind.SourceShaderCleanupAndStencilRestore,
            prefix+"F103-EADC55-D712F0-blendOff;EAEB34-Cull1-opcode1D;"+
            "class868-DBE30/138F810/14C3610-C0/v48-source;9973A0-cleanup;not-whole-graph-exit-state");
        copy=new(DeferredFullscreenRasterSourceKind.Copy97,97,
            DeferredFullscreenRasterExitKind.RestorePriorCullRetainDisabledBlend,
            prefix+"FD1176C6-CPU-E9E6F0;959250-Cull1;9999C0-slot0-blendOff;95A7B0-restores-prior-Cull");
        filter=new(DeferredFullscreenRasterSourceKind.Filter137,137,
            DeferredFullscreenRasterExitKind.RestorePriorCullAndBlend,
            prefix+"C41F73B1-CPU-E9ECB0;GFM1733400-v60-1055F60-Cull1/blendOff;"+
            "v70-1056800-restores-prior-Cull/blend");
    }
    public string Locale { get; }
    public string RuleVersion { get; }

    public static DeferredFullscreenRasterSources ReadSource(GameResourceRepository repository,
        string locale, LoadedRenderGraph actualGraph)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualGraph);
        if(locale is not("SC" or "TC"))throw new InvalidDataException("Fullscreen raster requires exact SC or TC.");
        Exact(actualGraph.Source,"san14://rdb/system/024496EE");
        var graph=actualGraph with{Document=RenderGraphReader.Read(actualGraph.Source.Bytes)};
        if(graph.Document.Nodes[166].Call.FactoryClassId!=0xF103DB00 ||
            graph.Document.Nodes[97].Call.FactoryClassId!=0xFD1176C6 ||
            graph.Document.Nodes[137].Call.FactoryClassId!=DeferredAuxiliaryPixelSourceRecipes.FactoryClassId)
            throw new InvalidDataException("Fullscreen raster source nodes differ from the actual166/97/137 factories.");
        var objects=repository.LoadObjects("system",0x718CCB8B);
        Exact(objects.Source,DeferredAuxiliaryPixelSourceRecipes.ShaderObjectsResourceUri);
        objects=objects with{Document=DokReader.Read(objects.Source.Bytes)};
        var deferred=repository.LoadShaderContainer("system",0x9D8E811F);
        Exact(deferred.Source,"san14://rdb/system/9D8E811F");
        var reference=RenderGraphShaderReferences.Read(graph,objects).References.Single(r=>r.Node.Index==166);
        if(reference.ShaderFileHash!=deferred.Source.FileId || reference.ShaderObject.ClassId!=0x8687085E)
            throw new InvalidDataException("F103 raster requires its actual class868 source shader owner.");
        var specular=repository.LoadShaderContainer("system",DeferredAuxiliaryPixelSourceRecipes.FilterShaderFileId);
        var depth=repository.LoadShaderContainer("system",DeferredAuxiliaryPixelSourceRecipes.DepthFilterShaderFileId);
        Exact(specular.Source,"san14://rdb/system/EC36DC74");
        Exact(depth.Source,"san14://rdb/system/98327BB0");
        foreach(var (id,file) in new[]{(0xB208D0BAu,0xEC36DC74u),(0x5929A0F6u,0x98327BB0u)})
        {
            var provider=objects.Document.Objects.Single(o=>o.ObjectId==id);
            var field=provider.Properties.Single(p=>p.NameHash==0x193025DF);
            if(provider.ClassId!=0x01818D62 || field.TypeCode!=5 || field.ElementCount!=1 || field.RawValue.Length!=4 ||
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue)!=file)
                throw new InvalidDataException("Node137 raster requires its actual filter provider/kernel family.");
        }
        return new(locale,RenderRuleIdentity.Version);
    }

    public DeferredFullscreenRasterDefinition PrepareF103SourceDefinition()
    { RequireRetained();return f103; }
    public DeferredFullscreenRasterDefinition PrepareCopy97SourceDefinition()
    { RequireRetained();return copy; }
    public DeferredFullscreenRasterDefinition PrepareFilter137SourceDefinition()
    { RequireRetained();return filter; }

    /// <summary>Optional current-key validation for the exact node166 plan.
    /// Actual execution, readiness and exit-state receipts stay with the Game owner.</summary>
    public DeferredFullscreenRasterDefinition PrepareF103(DeferredF103SelectedSource current)
    {
        ArgumentNullException.ThrowIfNull(current);RequireRetained();
        if(current.SourcePlan.Node.Index!=166 || current.SourcePlan.Shader.Source.ResourceUri!="san14://rdb/system/9D8E811F" ||
            current.SourcePlan.Graph.Source.ResourceUri!="san14://rdb/system/024496EE")
            throw new InvalidDataException("Current F103 source key is foreign to this raster owner.");
        return f103;
    }
    public void MarkImportedEditedOrExtendedHooks(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);retained=false; }
    private void RequireRetained()
    {
        if(!retained)
            throw new InvalidDataException("Fullscreen raster source bytes/hooks were replaced, imported or edited.");
    }
    private static void Exact(LoadedRdbAsset source,string uri)
    {
        if(source.ResourceUri!=uri)
            throw new InvalidDataException("Fullscreen raster source resource reference differs.");
    }
}
