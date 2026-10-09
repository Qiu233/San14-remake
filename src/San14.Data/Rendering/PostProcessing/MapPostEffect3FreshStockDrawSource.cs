using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Finite source state evaluator for a newly created SC/TC module,
/// actual last F103, node1 Exposure and node193 Begin. It never reads a process
/// pointer or purports to cover a reused context or arbitrary graph hooks.</summary>
public sealed class MapPostEffect3FreshStockDrawSource
{
    internal MapPostEffect3FreshStockDrawSource(LoadedDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    // Constructor writes qword1C44=7: the high dword1C48 is +0.0f.
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

public static partial class RenderSamplerSourceAdapters
{
    public static MapPostEffect3FreshStockDrawSource ReadFreshPostEffect3StockDrawSource(
        LoadedDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Fresh stock draw state currently has verified SC/TC source only.");

        return new(native,native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";qword1C44=7->float1C48=+0"+
            "actual-last-F103-cullNONE-retained;node1-;-only-PSslot0"+
            "-depthWrite1D8off;-moduleBegin-blendOff-RGBA-alphaTestOff"+
            "-depth-cache-count1C40zero;-bpl0/->-color-only-target-full-viewport"+
            "->-ScreenBegin-depth1BFoff-cullE8NONE-clip1F0zero;-bpl0-depthoff-retained"+
            "-opcode40031->;->[4]=D3D5-TriangleStrip"+
            "-ScreenEnd-restores-depth-cull-clip;depthless-color-target-no-applicable-stencil"+
            "owned-first-source-context-state-evaluation-not-original-process-observation");
    }
}
