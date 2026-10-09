using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary> returns the actual attachment row at the current target
/// stack entry, not the texture wrapper. Row60 is's native output color
/// mode; wrapper60 itself is a different optional pointer. This helper admits
/// a target the caller actually inherited/bound and its resolved descriptor.</summary>
public static class OriginalMapPostEffect3InheritedTargetGammaSource
{
    public static OriginalMapPostEffect3DirectCallerOutputTarget Capture(
        OriginalRenderTextureRuntimeDescriptor actualInheritedTarget,
        OriginalMainMapContextCurrentPacket actualContext,string actualInheritedBindingEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualInheritedTarget);ArgumentNullException.ThrowIfNull(actualContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualInheritedBindingEvidence);
        actualContext.Source.Validate(actualContext);
        OriginalRenderTextureResolutionFrame.ValidateDeclaration(actualInheritedTarget.Declaration);
        var host=actualContext.Source.Host;
        if(actualContext.FrameNumber<=0||host.Locale is not ("TC" or "SC")||host.RuleVersion!=RenderRuleIdentity.Version)
            throw new NotSupportedException("Direct Apply gamma requires the actual same-locale pinned fresh Host Q owner.");
        uint mode=ResolveNativeAttachmentOutputMode(actualInheritedTarget.ColorSpace,actualInheritedTarget.OutputColorSpace);
        return new(actualContext.FrameNumber,actualInheritedTarget.OwnerIdentity,actualContext.Source.OwnedHostIdentity,
            mode==1,false,actualInheritedBindingEvidence+";"+actualInheritedTarget.SourceProvenance+";"+
            ";OutputColorSpace2-inherits-ColorSpace;:creator-output-mode"+
            "->/:attachment60=native-output-mode;-current-target-stack-row"+
            "-target60==1;QVT-v80=-false"+actualContext.SourceProvenance);
    }
    public static uint ResolveNativeAttachmentOutputMode(byte colorSpace,byte outputColorSpace)
    {
        if(colorSpace>1||outputColorSpace>2)
            throw new NotSupportedException("Native target color modes outside the verified RT declaration range need their own source route.");
        return outputColorSpace==2?colorSpace:outputColorSpace;
    }
}

/// <summary>Apply.. packs its source view offset in CPU1A3,
/// adds the same Q3F8 low31 bits used by the current Main execution and selects
/// from the same named E1 View collection. This receipt joins those real owners.</summary>
public sealed class OriginalMapPostEffect3ApplyViewSlotSource
{
    private OriginalMapPostEffect3ApplyViewSlotSource(int index,long frame,string context,string collection,string proof)
        =>(Index,FrameNumber,ContextOwnerIdentity,ViewCollectionOwnerIdentity,SourceEvidence)=(index,frame,context,collection,proof);
    public int Index{get;}
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string ViewCollectionOwnerIdentity{get;}
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3ApplyViewSlotSource CaptureSameCurrentView(
        LoadedRenderGraph actualApplyGraph,OriginalMainSceneNode51Execution actualMainExecution,
        OriginalMapSourceCreatedF103CurrentFrame actualWorldFrame)
    {
        ArgumentNullException.ThrowIfNull(actualApplyGraph);ArgumentNullException.ThrowIfNull(actualMainExecution);
        ArgumentNullException.ThrowIfNull(actualWorldFrame);
        var context=actualWorldFrame.Context;var invocation=actualMainExecution.Invocation;
        context.Source.Validate(context);
        actualMainExecution.RequireSelectedView(actualWorldFrame.FrameNumber,actualWorldFrame.Lighting.Binding);
        if(actualApplyGraph.Source.ResourceUri!="san14://rdb/system/024496EE"||
            actualApplyGraph.Document.Nodes.Count<=193)
            throw new InvalidDataException("Apply view selection requires the same original Main/Apply graph.");
        var apply=actualApplyGraph.Document.Nodes[193];
        var fields=apply.Call.Arguments.Where(p=>p.NameHash==0x9075EDA0&&p.DescriptorWord==0x01000001).ToArray();
        var collection=apply.Call.Arguments.Where(p=>p.NameHash==0x72DB7EBD&&p.DescriptorWord==0x05000001).ToArray();
        if(apply.Call.FactoryClassId!=OriginalMapFinalOutputResources.ApplyFactory||fields.Length!=1||fields[0].RawValue.Length!=1||
            collection.Length!=1||collection[0].RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(collection[0].RawValue)!=invocation.CurrentViewCollectionSourceNameHash||
            invocation.ContextPointer50Absent!=false||invocation.CurrentContextWord3F8 is not uint raw||
            invocation.CurrentContextOwnerIdentity!=context.Source.OwnedHostIdentity||context.FrameNumber!=actualWorldFrame.FrameNumber||
            actualWorldFrame.Common.SelectedViewOwnerIdentity!=invocation.SelectedView.ViewIdentity||
            invocation.SelectedViewSlotPresent!=true)
            throw new InvalidDataException("Apply and current Main must retain the same actual Q, E1 collection and nonnull View slot.");
        uint slot=unchecked((raw&0x7FFFFFFF)+(uint)(fields[0].RawValue[0]&0x7F));
        if(slot>=32||slot!=actualMainExecution.SelectedViewIndex)
            throw new NotSupportedException("Apply selects a different View than this actual World frame; resolve that collection slot before preparing its caller.");
        return new((int)slot,actualWorldFrame.FrameNumber,context.Source.OwnedHostIdentity,invocation.CurrentViewCollectionOwnerIdentity,
            actualMainExecution.SourceProvenance+";argument6-source-byte9075EDA0;-ctorEDX"+
            ":Apply1A3=source-low7;09/15/19/1C:(Apply1A3&7F)+(Q3F8&7FFFFFFF)"+
            "-current-E1-slot-nonnull;actual-node51-Q-and-view-collection-join");
    }

    public static OriginalMapPostEffect3OwnedDirectCaller PrepareFreshWorldCaller(
        LoadedRenderGraph actualApplyGraph,OriginalMainSceneNode51Execution actualMainExecution,
        OriginalMapSourceCreatedF103CurrentFrame actualWorldFrame,LoadedObjects selectedCommonTemplate,
        OriginalMapPostEffect3FreshGraphicsConfiguration actualGraphics,
        OriginalRenderTextureRuntimeDescriptor actualInheritedTarget,string actualInheritedTargetBindingEvidence,
        float actualRendererElapsedDelta,string actualClockOwnerIdentity)
    {
        var slot=CaptureSameCurrentView(actualApplyGraph,actualMainExecution,actualWorldFrame);
        var target=OriginalMapPostEffect3InheritedTargetGammaSource.Capture(actualInheritedTarget,
            actualWorldFrame.Context,actualInheritedTargetBindingEvidence);
        return OriginalMapPostEffect3OwnedDirectCaller.PrepareFreshWorldFrame(actualWorldFrame,selectedCommonTemplate,
            actualGraphics,target,actualRendererElapsedDelta,actualClockOwnerIdentity,slot.Index,slot.SourceEvidence);
    }
}
