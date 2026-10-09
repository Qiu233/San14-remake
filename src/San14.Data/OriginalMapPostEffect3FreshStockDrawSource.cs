using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Finite source state evaluator for a newly created SC/TC module,
/// actual last F103, node1 Exposure and node193 Begin. It never reads a process
/// pointer or purports to cover a reused context or arbitrary graph hooks.</summary>
public sealed class OriginalMapPostEffect3FreshStockDrawSource
{
    internal OriginalMapPostEffect3FreshStockDrawSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    // Constructor1145F16 writes qword1C44=7: the high dword1C48 is +0.0f.
    public float FreshContext1C48=>0f;
    public uint StockNativeCullMode=>1;
    public uint RetainedLateNativeCullMode=>1;
    public bool StockDepthTest=>false;
    public bool ModuleDepthWrite=>false;
    public bool ModuleBlendEnabled=>false;
    public byte ModuleColorWriteMask=>15;
    public uint StockClipPlaneMask=>0;
    public uint FreshMatchingDepthTargetCount=>0;
    public uint GhostNativePrimitiveCode=>4;
    public uint GhostD3DPrimitiveTopology=>5;
    public bool ExposureWritesPixelSampler7=>false;
}

public static partial class OriginalDeferredSamplerSources
{
    public static OriginalMapPostEffect3FreshStockDrawSource ReadFreshPostEffect3StockDrawSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Fresh stock draw state currently has verified SC/TC source only.");

        return new(native,native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+";TC:1145F16-qword1C44=7->float1C48=+0;"+
            "actual-last-F103-EAEB34-cullNONE-retained;node1-EA9A80..EA9C68-only-PSslot0;"+
            "11467F7-depthWrite1D8off;1161C00-moduleBegin-blendOff-RGBA-alphaTestOff;"+
            "1145F0F-depth-cache-count1C40zero;11626A4-bpl0/1162714->959740-color-only-target-full-viewport;"+
            "1162862->959250-ScreenBegin-depth1BFoff-cullE8NONE-clip1F0zero;1162A1F-bpl0-depthoff-retained;"+
            "1153244-opcode40031->958980/962320/964ED0->14BEA98[4]=D3D5-TriangleStrip;"+
            "95A7B0-ScreenEnd-restores-depth-cull-clip;depthless-color-target-no-applicable-stencil;"+
            "owned-first-source-context-state-evaluation-not-original-process-observation");
    }
}
