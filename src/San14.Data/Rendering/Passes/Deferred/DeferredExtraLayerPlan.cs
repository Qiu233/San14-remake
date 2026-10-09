using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedDeferredExtraLayerPlan(LoadedDeferredTexturePlan Deferred,
    RenderTextureDefinition Rs,RenderTextureDefinition Rr,LoadedRenderGraphTargets SdfTarget,
    CompiledGraphNode ReflectionNode,CompiledGraphNode SdfNode);

public enum DeferredExtraAction { Preserve,ClearReflectionSlice,FillSdfRedGreen,RequiresActiveProducer }
public sealed record DeferredExtraLayerStep(uint TextureObjectId,uint Slice,DeferredExtraAction Action,string Reason,
    uint ColorWriteMask,Vector4? SourceColor);
public sealed record DeferredReflectionDecision(IReadOnlyList<DeferredExtraLayerStep> Steps,bool RequiresActiveProducer,string Reason,
    bool RequiresTargetDescriptorFallback=false);
public sealed record DeferredSdfDecision(DeferredExtraLayerStep Step,uint NextState40);

/// <summary>Caller-proved source conditions. These are neither guessed current
/// values nor initialization defaults. Slice binding outcomes are consumed only
/// when the exact original fallback branch is selected.</summary>
public sealed record DeferredReflectionState(bool RendererValidFrame,uint ViewIndex,bool ViewPresent,uint Renderer3E4,
    bool PreparedShaderReady,bool View1C80Present,bool ProviderReady,bool FeatureEnabled,bool RawSliceDescriptorsPresent,
    IReadOnlyList<bool>? SliceBindingSucceeded);
public sealed record DeferredSdfState(bool RendererValidFrame,uint ViewIndex,bool ViewPresent,bool RequiredBuffersResolved,
    uint CpuProcess30,bool ProviderD61120Active,uint View158C,uint CpuState40,bool CurrentTargetHelper959250);

public static class DeferredExtraLayerPlan
{
    public const uint RsObjectId=0x521C7DED,RrObjectId=0x85C125A6;
    public const uint SdfOnceMask=0xC0000000;

    /// <summary>Exact current source node66/162/164 declarations. Modified
    /// graphs need a separate source contract rather than silently reusing these
    /// ordinal-dependent rules. Texture overrides retain their own source bytes.</summary>
    public static LoadedDeferredExtraLayerPlan Read(LoadedDeferredTexturePlan deferred)
    {
        ArgumentNullException.ThrowIfNull(deferred);
        if(deferred.Graph.Source.FileId!=0x024496EE || deferred.Graph.Source.DecodedSha256!=
            "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D" || deferred.Node.Index!=166)
            throw new InvalidDataException("Extra deferred layers require the exact source RenderGraph and node166 contract.");
        RenderTextureDefinition T(string role,uint key,int layers,uint format)
        {
            var matches=deferred.Inputs.Where(i=>i.Role==role && i.Texture.Object.ObjectId==key).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Original extra layer texture is absent or ambiguous: "+role);
            var texture=matches[0].Texture;
            if(texture.Kind!=RenderTextureKind.Texture2DArray || texture.ArrayDepth!=layers ||
                RenderTextureFormatMath.Resolve(texture).TargetViewFormat!=format)
                throw new InvalidDataException("Original extra layer texture differs in array shape or target format: "+role);
            for(uint i=0;i<layers;i++)_ = RenderSurfaceMath.Resolve(texture,i);
            return texture;
        }
        var rs=T("RS",RsObjectId,5,87);var rr=T("RR",RrObjectId,3,26);
        var target=RenderGraphTargetResources.Read(deferred.Graph,162,deferred.TextureObjects);
        if(target.ColorTargets.Count!=1 || target.ColorTargets[0].ObjectNameHash!=RsObjectId ||
            target.ColorTargets[0].SurfaceId!=3 || target.Flag!=18)
            throw new InvalidDataException("Original SDF inheritance requires node162 RS surface3.");
        var reflection=deferred.Graph.Document.Nodes[66];var sdf=deferred.Graph.Document.Nodes[164];
        if(reflection.Call.FactoryClassId!=0x88416CDB || sdf.Call.FactoryClassId!=0xEBFE38E8 ||
            U(reflection,0x1BA2A872)!=RrObjectId || U(sdf,0x43E79658)!=RsObjectId ||
            B(sdf,0x69E3D5B7)!=1 || B(sdf,0xE98D44D5)!=2 || U(sdf,0xFA5A06A8)!=3)
            throw new InvalidDataException("Original ReflectionProbe/ViewSDF target and process declarations differ.");
        return new(deferred,rs,rr,target,reflection,sdf);
    }

    public static DeferredReflectionDecision Reflection(DeferredReflectionState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        DeferredReflectionDecision Preserve(string why)=>new(Array.AsReadOnly(Enumerable.Range(0,3)
            .Select(i=>new DeferredExtraLayerStep(RrObjectId,(uint)i,DeferredExtraAction.Preserve,why,0,null)).ToArray()),false,why);
        if(!source.RendererValidFrame || source.ViewIndex>=32 || !source.ViewPresent)return Preserve("original-view-or-frame-skipped");
        if((source.Renderer3E4&3)!=1)return Preserve("original-reflection-renderer-mode-skipped");
        if(!source.PreparedShaderReady)return Preserve("original-prepared-reflection-shader-unavailable");
        if(!source.View1C80Present)return Preserve("original-view1C80-absent-no-fallback");
        if(source.ProviderReady && source.FeatureEnabled)
            return new(Array.AsReadOnly(Enumerable.Range(0,3).Select(i=>new DeferredExtraLayerStep(RrObjectId,(uint)i,
                DeferredExtraAction.RequiresActiveProducer,"original-active-reflection-producer-required",0,null)).ToArray()),true,
                "original-active-reflection-producer-required");
        if(!source.RawSliceDescriptorsPresent)
            return new(Array.Empty<DeferredExtraLayerStep>(),false,
                "original-null-target-inherits-current-attachment-descriptor",true);
        if(source.SliceBindingSucceeded is null || source.SliceBindingSucceeded.Count!=3)
            throw new InvalidDataException("Original reflection fallback requires explicit outcomes for all three slice binds.");
        var rows=new DeferredExtraLayerStep[3];
        for(int i=0;i<3;i++)rows[i]=source.SliceBindingSucceeded[i] ?
            new(RrObjectId,(uint)i,DeferredExtraAction.ClearReflectionSlice,"original-fallback-bound-packedColor0",15,Vector4.Zero) :
            new(RrObjectId,(uint)i,DeferredExtraAction.Preserve,"original-reflection-slice-binding-failed",0,null);
        return new(Array.AsReadOnly(rows),false,"original-conditional-reflection-fallback");
    }

    public static DeferredSdfDecision Sdf(DeferredSdfState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        DeferredSdfDecision Preserve(string why,uint? next=null)=>new(new(RsObjectId,3,
            DeferredExtraAction.Preserve,why,0,null),next??source.CpuState40);
        if(!source.RendererValidFrame || source.ViewIndex>=32 || !source.ViewPresent || !source.RequiredBuffersResolved)
            return Preserve("original-sdf-frame-view-or-input-skipped");
        if((source.CpuProcess30&255)!=1)return Preserve("original-sdf-process-not1");
        if(source.ProviderD61120Active && source.View158C>=((source.CpuProcess30>>8)&3))
            return new(new(RsObjectId,3,DeferredExtraAction.RequiresActiveProducer,"original-active-sdf-producer-required",0,null),source.CpuState40);
        if((source.CpuState40&SdfOnceMask)==SdfOnceMask)return Preserve("original-sdf-fallback-once-already-published");
        uint next=source.CpuState40|SdfOnceMask;
        if(!source.CurrentTargetHelper959250)return Preserve("original-sdf-current-target-helper-false-no-pixels",next);
        return new(new(RsObjectId,3,DeferredExtraAction.FillSdfRedGreen,
            "original-first-fallback-full-rectangle",3,new Vector4(0,0,1,1)),next);
    }

    /// <summary>Source 959740 inherits attachment slot0 when the original raw
    /// reflection slice table is absent. Caller supplies the actual current
    /// descriptor and all three source bind results. No RR identity is assumed.</summary>
    public static DeferredReflectionDecision ReflectionInheritedTarget(DeferredReflectionState source,
        uint? explicitCurrentTargetObjectId,uint explicitCurrentTargetSlice,IReadOnlyList<bool>? threeSourceBindResults)
    {
        var decision=Reflection(source);
        if(!decision.RequiresTargetDescriptorFallback)return decision;
        if(explicitCurrentTargetObjectId is null || threeSourceBindResults is null || threeSourceBindResults.Count!=3)
            throw new InvalidDataException("Original inherited reflection fallback requires current slot0 target identity and all three bind results.");
        var rows=new DeferredExtraLayerStep[3];
        for(int i=0;i<3;i++)rows[i]=threeSourceBindResults[i] ?
            new(explicitCurrentTargetObjectId.Value,explicitCurrentTargetSlice,DeferredExtraAction.ClearReflectionSlice,
                "original-null-target-current-slot0-bind-succeeded",15,Vector4.Zero) :
            new(explicitCurrentTargetObjectId.Value,explicitCurrentTargetSlice,DeferredExtraAction.Preserve,
                "original-null-target-current-slot0-bind-failed",0,null);
        return new(Array.AsReadOnly(rows),false,"original-null-target-current-slot0-inherited");
    }
    private static TaskGraphArgument Arg(CompiledGraphNode node,uint name,uint descriptor,int bytes)
    {
        var rows=node.Call.Arguments.Where(a=>a.NameHash==name && a.DescriptorWord==descriptor).ToArray();
        if(rows.Length!=1||rows[0].RawValue.Length!=bytes)throw new InvalidDataException("Missing explicit extra-layer graph field: "+name.ToString("X8"));
        return rows[0];
    }
    private static uint U(CompiledGraphNode node,uint name)=>BinaryPrimitives.ReadUInt32LittleEndian(Arg(node,name,0x05000001,4).RawValue);
    private static byte B(CompiledGraphNode node,uint name)=>Arg(node,name,0x01000001,1).RawValue[0];
}
