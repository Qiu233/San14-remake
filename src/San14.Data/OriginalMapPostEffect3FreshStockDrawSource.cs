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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);
        VerifyHash(exe,native.ExecutableSha256,"Fresh stock draw executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Fresh stock draw decoded text");
        void Check(int rva,int length,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3FreshStockDrawSource.cs",rva,length,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,length),actual.Sha,"Fresh stock draw source window");
        }
        Check(0x1145E70,0x1C8,"E33E730AB6F099D838D2AD86D6422C9D96952998CC7793B37C2A56FA26A747CF");
        Check(0x11466F0,0x150,"F718AE367748E03F7087E278A308BC785C1DC237F89DCABA415C78F2274EAEC2");
        Check(0x1161C00,0x140,"7B5ED06ABB60CB3CC3119BCBADD4313FF8BF8C40E7767A10EC10B4D9F4C911D4");
        Check(0x959250,0xE2,"242CAF2EB0A0687BAE472F78084B081451AAD073E783D43539CD443F57E6E3A8");
        Check(0x95A7B0,0xA9,"901F808B3D58280C01505A08F77CD4254EC444DB96CA9F51E59B36F733FACE45");
        Check(0xEA9A80,0x1E9,"D863FB5F561232802E18A5C972D0F638E0055C42E881DEBAF74DE10FAC0093FE");
        Check(0x1162840,0x2E,"6AD849CDACD099151EBA8E624B2F5D2B547D7E062CEC6455F315AA751CCAB877");
        Check(0x1162610,0x49B,"1E900883635392A3B97079A562E50C5453607BE337A231263CA2AC75DE647AD8");
        Check(0x959740,0x10A,"4F16A556294058D1AA563EC3C5FB7D0C6CB74BC7383211705BAEE47C3966D872");
        Check(0xEAEB34,0x1F,"22A7E61E9ECFF1063D25E9F8A9ADAEDB96115E2EDE49940D7DD69C65E352EEBC");
        Check(0x1153120,0x1FA,"2121C0DCC0CBFCB9B1020E9AF8D28877F18100236EA7235B7BA5A9B569F2FC37");
        Check(0x958980,0x38,"657C5048C84A4A3894EEA8920BA9C6DD8247D86DDE0D375EFFDBFE5250827A92");
        Check(0x962320,0x57,"7A46D499F6D48EAF3202C3268481FD75815DF12A803D4453D5103F91B3C1A23C");
        Check(0x964ED0,0x67,"7EE93AD1EF77CF4E61C46AAF17E5B18F2DE5F0B54AB03DD7F4FA2B207D9D73EE");
        VerifyHash(image.Raw(OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,"OriginalMapPostEffect3FreshStockDrawSource.cs",0x14BEA98),36),"25BF804D6FA353948B97B878148D2477139E0B62EC14800F90BF6024D9A21851","Native primitive topology table");
        if(BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,"OriginalMapPostEffect3FreshStockDrawSource.cs",0x14BEA98)+4*4,4))!=5)
            throw new InvalidDataException("Native Ghost primitive4 no longer maps to TriangleStrip.");
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
