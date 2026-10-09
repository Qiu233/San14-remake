using System.Numerics;

namespace San14.Data;

/// <summary>The project's TC frontend matrix composition rules.
/// No original executable, process or graphics object is accessed.</summary>
public sealed class LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource
{
    internal LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource(string ruleVersion)
        => RuleVersion=ruleVersion;
    public string Locale => "TC";
    public string RuleVersion { get; }
    public string SourceEvidence => $"{RuleVersion};1050800-v68/104FBF0-current5D0;"+
        "97A580-frontend68-mW2P/frontend88-mL2P;976890/97C920/17B070-current-position-times-current-pass";
    public static LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource Load(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadEarlyShadowVertexMatrixNativeSource(string.Empty);
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
        return new(RenderRuleIdentity.For("shadow-vertex-matrix","TC"));
    }
}
