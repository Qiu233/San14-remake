using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalDeferredIndirectAction { Preserve, Draw, RequiresOtherRenderer }
public sealed record OriginalDeferredIndirectSource(LoadedOriginalDeferredExtraLayerPlan Extra,
    CompiledGraphNode Node, OriginalNamedModelShader Primary, OriginalNamedModelShader NoSubShadow,
    uint ColorWriteMask, uint LightGroupId, uint DepthObjectId);
public sealed record OriginalDeferredIndirectState(uint RendererFrameFlags,uint ViewIndex,bool ViewPresent,
    uint RendererMode,bool LitBufferPresent,bool View1Bd0Present,bool View1Bd0PayloadPresent,
    bool View1Bd0FirstPresent,uint View1Bfc,bool SelectedShaderReady,uint View19Ac,bool View1Bf0Present);
public sealed record OriginalDeferredIndirectDecision(OriginalDeferredIndirectAction Action,
    OriginalNamedModelShader? Shader,string Reason,uint NextRendererFrameFlags);
public sealed record OriginalDeferredIndirectProgramSource(OriginalDeferredIndirectSource Pass,
    OriginalNamedModelShader Provider,LoadedShaderContainer Shader,G1ShaderResourceBindings Resources,
    DxbcResourceDefinitions PixelDefinitions,int CombinationIndex);

/// <summary>Finite node163 source selection. Current native readiness, View flags,
/// variant and complete resources must be supplied by the caller.</summary>
public static class OriginalDeferredIndirectPlan
{
    public const uint FactoryClass=0x93FFBE0F,ShaderClass=0x2939B4B1;
    public static OriginalDeferredIndirectSource Read(LoadedOriginalDeferredExtraLayerPlan extra,
        LoadedObjects shaderObjects)
    {
        ArgumentNullException.ThrowIfNull(extra);ArgumentNullException.ThrowIfNull(shaderObjects);
        if(Convert.ToHexString(SHA256.HashData(shaderObjects.Source.Bytes))!=shaderObjects.Source.DecodedSha256)
            throw new InvalidDataException("Indirect shader object source hash differs from its selected bytes.");
        shaderObjects=new(DokReader.Read(shaderObjects.Source.Bytes),shaderObjects.Source);
        var n=extra.Deferred.Graph.Document.Nodes[163];
        uint Word(uint hash){var a=n.Call.Arguments.Where(a=>a.NameHash==hash&&a.DescriptorWord==0x05000001).ToArray();
            if(a.Length!=1||a[0].RawValue.Length!=4)throw new InvalidDataException("Missing typed indirect graph field.");
            return BinaryPrimitives.ReadUInt32LittleEndian(a[0].RawValue);}
        if(n.Call.FactoryClassId!=FactoryClass || Word(0xFA5A06A8)!=8 || Word(0x59BD6FC4)!=8 ||
            Word(0xB7CCDD1F)!=0xE13DDB4F)
            throw new InvalidDataException("Original indirect node163 mask/group/depth contract differs.");
        OriginalNamedModelShader Provider(uint property)
        {
            var value=OriginalModelShaderResources.ReadDeclaredShader(shaderObjects,Word(property));
            if(value.Object.ClassId!=ShaderClass||value.DependencyObjectHashes.Count!=0)
                throw new InvalidDataException("Indirect provider requires the exact 2939B4B1 own-interface source.");
            return value;
        }
        return new(extra,n,Provider(0x7046CA70),Provider(0x3C2655F2),8,8,0xE13DDB4F);
    }

    public static OriginalDeferredIndirectDecision Select(OriginalDeferredIndirectSource source,
        OriginalDeferredIndirectState state)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(state);
        if((state.RendererFrameFlags&0x80000000)!=0||state.ViewIndex>=32||!state.ViewPresent)
            return new(OriginalDeferredIndirectAction.Preserve,null,"original-indirect-invalid-frame-or-view",state.RendererFrameFlags);
        if((state.RendererMode&3)!=1)
            return new(OriginalDeferredIndirectAction.RequiresOtherRenderer,null,"original-other-renderer-indirect-chain-required",state.RendererFrameFlags);
        bool primary=state.LitBufferPresent&&state.View1Bd0Present&&state.View1Bd0PayloadPresent&&
            state.View1Bd0FirstPresent&&(state.View1Bfc&15)!=0;
        if(!state.SelectedShaderReady)
        {
            bool pending=(state.RendererFrameFlags&0x2000)!=0||state.View1Bf0Present;
            uint next=pending?state.RendererFrameFlags|0x2000:state.RendererFrameFlags&~0x2000u;
            return new(OriginalDeferredIndirectAction.Preserve,null,"original-selected-indirect-interface-unavailable",next);
        }
        if((state.View19Ac&0x9FFFFFFF)==0)
            return new(OriginalDeferredIndirectAction.Preserve,null,"original-indirect-empty-view-mask",state.RendererFrameFlags);
        return new(OriginalDeferredIndirectAction.Draw,primary?source.Primary:source.NoSubShadow,
            primary?"original-indirect-subshadow-provider":"original-indirect-no-subshadow-provider",state.RendererFrameFlags);
    }

    public static OriginalDeferredIndirectProgramSource ReadProgram(OriginalDeferredIndirectSource pass,
        OriginalNamedModelShader explicitSelectedProvider,LoadedShaderContainer shader,int explicitCombination)
    {
        ArgumentNullException.ThrowIfNull(pass);ArgumentNullException.ThrowIfNull(explicitSelectedProvider);ArgumentNullException.ThrowIfNull(shader);
        var selected=new[]{pass.Primary,pass.NoSubShadow}.SingleOrDefault(p=>p==explicitSelectedProvider);
        if(selected is null||shader.Source.FileId!=selected.ShaderFileHash||
            Convert.ToHexString(SHA256.HashData(shader.Source.Bytes))!=shader.Source.DecodedSha256)
            throw new InvalidDataException("Indirect program differs from the selected source provider.");
        shader=new(G2ShaderContainerReader.Read(shader.Source.Bytes),shader.Source);
        var resources=shader.ReadG1ResourceBindings(explicitCombination);
        var stages=resources.Combination.Stages.Where(s=>s.Entry is not null).ToArray();
        if(stages.Length!=2||stages.Count(s=>s.StageCode==1)!=1||stages.Count(s=>s.StageCode==0)!=1)
            throw new InvalidDataException("Indirect pass requires one original VS/PS pair.");
        var ps=stages.Single(s=>s.StageCode==0);
        return new(pass,selected,shader,resources,shader.ReadResourceDefinitions(ps.Entry!.ProgramIndex),explicitCombination);
    }

    /// <summary>Resolve the source G1 mixed-radix feature dimensions and pass.
    /// None of these indices are inferred from shader names or physical programs.</summary>
    public static OriginalDeferredIndirectProgramSource ReadSelectedProgram(OriginalDeferredIndirectSource pass,
        OriginalNamedModelShader explicitSelectedProvider,LoadedShaderContainer shader,
        ReadOnlySpan<uint> explicitDimensions,uint explicitPass)
    {
        ArgumentNullException.ThrowIfNull(shader);
        var row=shader.ReadG1Bindings().ResolveCombination(explicitDimensions,explicitPass);
        return ReadProgram(pass,explicitSelectedProvider,shader,row.Index);
    }
}
