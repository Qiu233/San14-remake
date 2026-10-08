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
    public uint NativeSelectRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3NativeFullscreenFrontendSource.cs",0x1161D40));
    public uint NativeFourSlotResetRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3NativeFullscreenFrontendSource.cs",0x11635A7));
    public uint NativeBindRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3NativeFullscreenFrontendSource.cs",0x1162610));
    public uint NativeReleaseRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3NativeFullscreenFrontendSource.cs",0x1163170));
    public uint NativeResetPassRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3NativeFullscreenFrontendSource.cs",0x1163080));
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
    ///963D80 is native int32 WHXY; this public record is numeric XYWH.</summary>
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
/// by11635A7..11635E4, then each counted Select setter and Before override
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
        "1161E21/1161E2C->1163460/11635A7-four-target-slots-cleared;source-cache-maintained-per-Select-and-Before";
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
            //1162093 tests the actual wrapper. A known null pointer does not
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
        //114CDBA ->1163089 ->1163460 clears all four slots even when the
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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Native fullscreen executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Native fullscreen decoded text");
        VerifyAdmittedFreshFullscreenProfile(text,start,native.Locale);
        void Check(int rva,int size,string sha)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3NativeFullscreenFrontendSource.cs",rva,size,sha);
            VerifyHash(text.AsSpan(actual.Rva-start,size),actual.Sha,"Native fullscreen operation");
        }
        Check(0x1161D40,1773,"57B3E131B5FEAA2CC5DFC7318AD910B987B26478F475619E5ADB679F020B770A");
        Check(0x1163460,426,"26428584698B46F67D86E7816F3EC7897A27BA3284E80895B037F7538B8A17A7");
        Check(0x1163800,208,"23BE6A18FBE9C36F5873CFDE127B27AE9818139563C02C969F0FC2DD6A4BED5C");
        Check(0x11630A0,201,"202C5FD0FAF339B76763C64E431AA453948C84ACBBF822DB13CDF4031314E3D4");
        Check(0x1162610,1195,"0F382035B15FA1F73D25ECB7E99E5C778B48C8C68C21B4D20FB4B90DD0458AAB");
        Check(0x1163170,751,"521950BFFD2C49366ACAF604363A74BDB91126F20BC422F31370E925794C5DA3");
        Check(0x1163080,28,"98DED7630F7B1079FB54BAD0024AC6EF05F24ADC41481D36BEF7135CED74AC80");
        Check(0x95AB00,183,"EE9A7E36C0B6E8F5C5FA2C22AD9AF347745279124423DB90DD73451ECE48FB26");
        Check(0x95A370,0x12E,"87C8929F3B33304874C693AC9C197B74AFAA96DC1A71E8B96379CE8903F35E60");
        Check(0x958340,0x22,"A35E5AE85AFD35D3888798DE6DA289ECD5AD2A864EC74C7C5CD144EABDE35319");
        Check(0x9625E0,0x7E,"BA9F434AB7D1ADC28B05A9491451325BA585140074EA1E2D1FF9DF8CDCC832A4");
        Check(0x95E970,0x17,"2E068329069ADB92586E0F46FB3CA3DFE85B948CBC90898E359ECE9CD192E7EF");
        Check(0x966CF0,0xB4,"B40062D4870A19114D89ABB5CA10B85743014617CC127B3925B6C4E8B99789AD");
        return new(fresh,fresh.SourceEvidence+";TC1161DD(E)-compute-mode0/1161DF7-graphics-mode2;"+
            "1161E21-mode-diff-reset;11635A7-four-slot-reset;1162088-counted-kind3->1163800;"+
            "11630A0-effective-cache-targets;11626A4-bpl0/fresh1C40zero-color-only-depthoff;"+
            "1162A52-depthCompare1C44=7;1163422/1163435->95AB00-release-actual-pushed-target-row;"+
            "114CDBA->1163080-unconditional-ResetPass->1163460-four-slots-clear;"+
            "freshFrontend966F05-F4=1100/WORK1198D08-save->11991F5-restore;"+
            "F103D712F0-zero-blend-bits-preserve;actual-Sky/class18/Terrain/callback-gates-required;"+
            "966EBA-scissorDisabled/Eye-and-Composite-save-restore;966F9C-min0/96702E-max1/Filter95C070-retains-original-packet;"+
            "955F8E-D3D11CreateDevice-immediate-context10/965821-GetType0->frontend28=1;"+
            "9665A0-row60=&actualWrapper164;95D440-fullXYWH/963D80-int32-WHXY-getter/95C070-inverse;"+
            "94EA60-physical-RT-row-per-mip/95D440-nativeXYWH-full-mip-rect;"+
            "Eye1157F74/1157FC3/1157FF6-RT0-activePhysical10+native-mip-selector;SRV-clones-remain-independent;"+
            "968000-int32-XYWH->D3D-Viewport/96822E-RSSetViewports;"+
            "1161C00-alphaOff/opcode17->958340/9625E0/95E970-vATest2860=FF7FFFFF->966CF0-pixel-shadow;"+
            "Lens114BEA0-explicit-stock-After/959250-ScreenBegin/95A370-quad/95A7B0-restore;"+
            "source-profile-F4_SCISSOR_SOURCE@7CFF04FED12D131BA116635B380B9988A4167348AB77DE4E944B4CD7463121AE");
    }
}
