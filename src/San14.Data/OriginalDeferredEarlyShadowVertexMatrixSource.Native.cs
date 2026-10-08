using System.Collections.ObjectModel;
using System.Numerics;

namespace San14.Data;

/// <summary>Imported once: the actual TC frontend matrices, v68 publication
/// and full17B070 product. No original process or graphics object is accessed.</summary>
public sealed class LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource
{
    internal LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource(string executableHash,
        string textHash, IReadOnlyDictionary<string,string> windows)
        => (ExecutableSha256,DecodedTextSha256,WindowSha256)=(executableHash,textHash,windows);
    public string Locale => "TC";
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public IReadOnlyDictionary<string,string> WindowSha256 { get; }
    public string SourceEvidence => $"SAN14CE_TC.exe@{ExecutableSha256};1050800-v68/104FBF0-current5D0;"+
        "97A580-frontend68-mW2P/frontend88-mL2P;976890/97C920/17B070-current-position-times-current-pass";
    public static LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource Load(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadEarlyShadowVertexMatrixNativeSource(repository.SourceDirectory);
    }

    public Matrix4x4 ComposeRigidLocalToProjection(ReadOnlySpan<float> actualFinalPositionShaderRows,
        Matrix4x4 actualPublishedShadow5D0)
    {
        if (actualFinalPositionShaderRows.Length != 12)
            throw new InvalidDataException("97C920 requires its complete three current position dot rows.");
        var s = actualFinalPositionShaderRows;
        var nativePosition = new Matrix4x4(s[0],s[4],s[8],0,s[1],s[5],s[9],0,
            s[2],s[6],s[10],0,s[3],s[7],s[11],1);
        return OriginalUnitWorldTransformMath.ComposeAffine17ADD0(nativePosition,actualPublishedShadow5D0);
    }

    /// <summary>Exact D28311 branch: WORLD shader rows are the first operand;
    /// each actual local palette triplet is the second. The result stays in
    /// shader dot-row order. Only actual active rows are returned.</summary>
    public IReadOnlyList<Vector4> ComposeModelWorldWithPalette(ReadOnlySpan<float> actualModelWorldShaderRows,
        IReadOnlyList<Vector4> actualLocalPaletteRows)
    {
        if (actualModelWorldShaderRows.Length != 12)
            throw new InvalidDataException("Skin model world transport requires its complete current affine dot rows.");
        ArgumentNullException.ThrowIfNull(actualLocalPaletteRows);
        if (actualLocalPaletteRows.Count is < 3 or > 384 || actualLocalPaletteRows.Count % 3 != 0)
            throw new InvalidDataException("Skin model world transport requires actual active palette triplets.");
        var w=actualModelWorldShaderRows;
        var world = new Matrix4x4(w[0],w[1],w[2],w[3],w[4],w[5],w[6],w[7],w[8],w[9],w[10],w[11],0,0,0,1);
        var rows = new Vector4[actualLocalPaletteRows.Count];
        for(int i=0;i<rows.Length;i+=3)
        {
            var a=actualLocalPaletteRows[i];var b=actualLocalPaletteRows[i+1];var c=actualLocalPaletteRows[i+2];
            var palette = new Matrix4x4(a.X,a.Y,a.Z,a.W,b.X,b.Y,b.Z,b.W,c.X,c.Y,c.Z,c.W,0,0,0,1);
            Matrix4x4 result = OriginalUnitWorldTransformMath.ComposeAffine17ADD0(world,palette);
            rows[i]=new(result.M11,result.M12,result.M13,result.M14);
            rows[i+1]=new(result.M21,result.M22,result.M23,result.M24);
            rows[i+2]=new(result.M31,result.M32,result.M33,result.M34);
        }
        return Array.AsReadOnly(rows);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource LoadEarlyShadowVertexMatrixNativeSource(string directory)
    {
        var profile=Profile("TC");byte[] exe=File.ReadAllBytes(Path.Combine(directory,"SAN14CE_TC.exe"));
        VerifyHash(exe,profile.ExeHash,"Early shadow vertex matrix executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int codeRva);
        VerifyHash(code,profile.TextHash,"Early shadow vertex matrix decoded text");
        var windows=new Dictionary<string,string>(StringComparer.Ordinal);
        void Window(string name,int rva,int count,string hash)
        {VerifyHash(code.AsSpan(rva-codeRva,count),hash,name);windows.Add(name,hash);}
        Window("CompleteFrontendConstructor",0x97A580,3680,"47B86856E6C7B2BE96AEE2061F32CC6BFF13CB22B30602EC13C1616721C1EF15");
        Window("CompleteShadowPassV68",0x1050800,1395,"531E681A4E95A257DE1A5F04129851204133EC2B2FAFAC3E14C4141497A406DA");
        Window("CompletePassProjectionCopy",0x104FBF0,85,"271B2EBC1D5433132D5C5A8D7640B9550E66F82CBE18296061A4B9949B8953EA");
        Window("CompleteGenericPrepareVC0",0xAC11A0,80,"66DA1E961C27B5AB54EAD71BDBC61E08E2FDAEB26ADCC2D89990EE8F594690D5");
        Window("CompleteGenericPrepare",0x1054140,144,"D6B1AB9FC200BBE7DD5F7D8DC94E7E2CD9C718B4E5F83BF677A0B73322A1BB73");
        Window("CompleteFrontendWriteDispatch",0x976890,345,"F06915890761AF48C1F5DC1433C1D04272E7905E40629EAFCAA5D319F243C2A8");
        Window("CompleteRigidProjectionWriter",0x97C920,441,"4F6DCE8220F28FD80D233BC51AEFBB6D8C49133EFD44BFC1501AA213BB350A01");
        Window("CompleteMatrixProduct",0x17B070,331,"E12AFDE597794034EA4B0D8AFA3EC24CD35616EC3272C41A5AB159875C3D52BB");
        Window("CompleteModelPalettePreparation",0xD27BC0,4198,"154A5E992FF9101452502F116C27512621E4AE05CC43F70BFC940D4E71AF919D");
        VerifyHash(image.Raw(0x14D2530,80),"323D37DA8F0328BA8677FCC2C75BE3FBAF165EC11CBABD6B43E9557BC34BF3E3","Frontend input matrix names");
        VerifyHash(image.Raw(0x14D26B0,704),"FC42A0E679922D1926C25DE3E309667934171A60624B94E6F1B918A0F86D34F2","Frontend output matrix names");
        VerifyHash(image.Raw(0x1343B98,4),"E00E5EB9444182F352323374EF4E08EBCB784725FDD4FD612D7730540B3E0C8C","Rigid source float one");
        VerifyHash(image.Raw(0x1343D30,16),"7AB8F6C26E4F9862C95A18C8E5C50403EEB64D8869FBBF9A7A6397D9A63B7B0E","Palette literal last row");
        return new(profile.ExeHash,profile.TextHash,new ReadOnlyDictionary<string,string>(windows));
    }
}
