namespace San14.Data;

/// <summary>Finite original Select/Bind/Release and stock After operations
/// for the admitted first World factories. The source reader establishes
/// the four-slot reset and actual fresh World state history. Imported or
/// previously untracked frontend state is outside this source profile.</summary>
public sealed class OriginalMapPostEffect3NativeFullscreenFrontendSource
{
    internal OriginalMapPostEffect3NativeFullscreenFrontendSource(
        OriginalMapPostEffect3FreshStockDrawSource fresh,string evidence)=>(Fresh,SourceEvidence)=(fresh,evidence);
    public OriginalMapPostEffect3FreshStockDrawSource Fresh { get; }
    public string SourceEvidence { get; }
    public string Locale=>Fresh.Native.Locale;
    public int NativeComputeMode=>0;
    public int NativeOrdinaryGraphicsMode=>2;
    public bool OrdinaryBindHasMatchingDepthTarget=>Fresh.FreshMatchingDepthTargetCount!=0;
    public bool OrdinaryBindDepthTest=>false;
    public uint OrdinaryBindNativeDepthCompare=>7;
    public bool ModuleDepthWrite=>Fresh.ModuleDepthWrite;
    public bool ModuleBlendEnabled=>Fresh.ModuleBlendEnabled;
    public byte ModuleColorWriteMask=>Fresh.ModuleColorWriteMask;
    public uint RetainedActualLateCullMode=>Fresh.RetainedLateNativeCullMode;
    public uint AdmittedFreshWorldNativeColorStateF4=>0x1100;
    public bool AdmittedFreshWorldPriorScissorEnabled=>false;
    public uint AdmittedFreshWorldMinimumDepthBits=>0;
    public uint AdmittedFreshWorldMaximumDepthBits=>0x3F800000;
    public bool ActualImmediateContextUsesWrapperRectangles=>true;
    public bool ModuleStockAlphaTestEnabled=>false;
    public uint ModuleDisabledStockAlphaTestWord=>0xFF7FFFFF;
    /// <summary>The supplied extent must be the real current native RT view,
    /// with its source-created full rectangle and retained writer history.
    /// is native int32 WHXY; this public record is numeric XYWH.</summary>
    public OriginalMapPostEffect3EyeNativeViewport CaptureSourceFullWrapperViewport(int actualWidth50,int actualHeight54)
    {
        if(actualWidth50<=0||actualHeight54<=0)throw new InvalidDataException("Actual current target dimensions are not positive.");
        return new(0,0,actualWidth50,actualHeight54,BitConverter.UInt32BitsToSingle(AdmittedFreshWorldMinimumDepthBits),
            BitConverter.UInt32BitsToSingle(AdmittedFreshWorldMaximumDepthBits));
    }

    public OriginalMapPostEffect3NativeFullscreenTargetCache ConstructAfterActualCompute104(
        OriginalMapPostEffect3AllocationPlan actualAllocation,int actualNextGraphicsPass)
    {
        ArgumentNullException.ThrowIfNull(actualAllocation);
        var state=OriginalMapPostEffect3PassStateReader.Read(actualAllocation.Source,Locale);
        if(state[104].AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.ComputeDispatch||
            actualNextGraphicsPass is <98 or >116||state[actualNextGraphicsPass].Factory.Index!=9||
            state[actualNextGraphicsPass].AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.SpecificFactoryGraphics||
            state[actualNextGraphicsPass].NativeFlags!=0||Fresh.FreshMatchingDepthTargetCount!=0)
            throw new NotSupportedException("Native fullscreen source reset needs the actual104 CS to factory9 ordinary graphics transition.");
        return new(this,actualAllocation,actualNextGraphicsPass);
    }
}

/// <summary>Source-owned target cache. Its initial null slots are produced
/// by.., then each counted Select setter and Before override
/// changes only its named slot. Clearing RT0 does not clear the other slots.</summary>
public sealed class OriginalMapPostEffect3NativeFullscreenTargetCache
{
    private readonly OriginalMapPostEffect3NativeFullscreenFrontendSource source;
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    private readonly OriginalMapPostEffect3Binding?[] slots=new OriginalMapPostEffect3Binding?[4];
    private readonly int firstPass;
    private bool selected;
    private int? boundPass;
    internal OriginalMapPostEffect3NativeFullscreenTargetCache(
        OriginalMapPostEffect3NativeFullscreenFrontendSource source,OriginalMapPostEffect3AllocationPlan allocation,int firstPass)
        =>(this.source,this.allocation,this.firstPass)=(source,allocation,firstPass);
    public string SourceEvidence=>source.SourceEvidence+";actualCS104-mode0->ordinarygraphics-mode2;"+
        "->/-four-target-slots-cleared;source-cache-maintained-per-Select-and-Before";
    public IReadOnlyList<OriginalMapPostEffect3Binding?> SelectAndApplyBefore(
        OriginalMapPostEffect3Composite2BeforeBindings before,OriginalMapPostEffect3TextureParameterState table)
    {
        before.RequireCurrent();
        if(boundPass is not null)throw new InvalidOperationException("Native cache needs the preceding actual Release/ResetPass before another Select.");
        if(!ReferenceEquals(before.Frame.Allocation,allocation)||!ReferenceEquals(table.Allocation,allocation)||
            before.IsCompute||!selected&&before.SourcePassIndex!=firstPass)
            throw new InvalidDataException("Native cache Select differs from this actual source allocation/mode transition.");
        var pass=OriginalMapPostEffect3PassStateReader.Read(allocation.Source,source.Locale)[before.SourcePassIndex];
        if(pass.NativeFlags!=0||pass.AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.SpecificFactoryGraphics)
            throw new NotSupportedException("Native cache cannot infer another execution-mode transition.");
        foreach(var row in allocation.Passes[before.SourcePassIndex].ActiveBindings.Where(b=>
            b.Role==OriginalMapPostEffect3BindingRole.RenderTargetWrite))
        {
            if(row.NativeViewKind!=3||row.Table!=OriginalMapPostEffect3BindingTable.TextureParameters||
                row.RegisterIndex is <0 or >3)
                throw new NotSupportedException("Native target cache requires its exact texture-kind3 setter.");
            // tests the actual wrapper. A known null pointer does not
            // execute1163800; it retains this slot's preceding cache value.
            if(table.ReadKnownNullablePointer(row.ParameterIndex) is not null)slots[row.RegisterIndex]=row;
        }
        if(before.ClearsNativeTarget0)slots[0]=null;
        selected=true;boundPass=before.SourcePassIndex;return Array.AsReadOnly(slots.ToArray());
    }
    public void ReleaseAndResetAfterCompleted(OriginalMapPostEffect3Composite2BeforeBindings before)
    {
        before.RequireCurrent();
        if(boundPass!=before.SourcePassIndex||!ReferenceEquals(before.Frame.Allocation,allocation))
            throw new InvalidOperationException("Native ResetPass differs from the actual preceding bound source command.");
        // clears all four slots even when the
        //following pass retains the same ordinary-graphics mode. FXAA has no
        //counted RT setter and therefore inherits the restored caller row.
        Array.Clear(slots);boundPass=null;
    }
}

public static partial class OriginalDeferredSamplerSources
{
    public static OriginalMapPostEffect3NativeFullscreenFrontendSource ReadNativeFullscreenFrontendSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        var fresh=ReadFreshPostEffect3StockDrawSource(native);

        return new(fresh,fresh.SourceEvidence+";compute-mode0/graphics-mode2;"+
            "-mode-diff-reset;-four-slot-reset;-counted-kind3->"+
            "-effective-cache-targets;-bpl0/fresh1C40zero-color-only-depthoff"+
            "-depthCompare1C44=7;->-release-actual-pushed-target-row"+
            "->-unconditional-ResetPass->-four-slots-clear"+
            "freshFrontend966F05-F4=1100/WORK-save->-restore"+
            "F103D712F0-zero-blend-bits-preserve;actual-Sky/class18/Terrain/callback-gates-required;"+
            "-scissorDisabled/Eye-and-Composite-save-restore;-min0/-max1/Filter-retains-original-packet"+
            "-reateDevice-immediate-context10/-GetType0->frontend28=1"+
            "-row60=&actualWrapper164;-fullXYWH/-int32-WHXY-getter/-inverse"+
            "-physical-RT-row-per-mip/-nativeXYWH-full-mip-rect"+
            "Eye1157F74;-RT0-activePhysical10+native-mip-selector;SRV-clones-remain-independent"+
            "-int32-XYWH->D3D-Viewport/-RSSetViewports"+
            "-alphaOff/opcode17->;-vATest2860=FF7FFFFF->-pixel-shadow"+
            "Lens-explicit-stock-After/-ScreenBegin/-quad/-restore"+
            "source-profile-F4_SCISSOR_SOURCE");
    }
}
