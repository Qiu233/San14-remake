using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact already-packed native matrices; no matrix convention,
/// default values, source projection or activity is inferred here.</summary>
public sealed record OriginalDeferredIndirectConstantInputs(ReadOnlyMemory<byte> WorldToPointMatrix,
    float Pattern,ReadOnlyMemory<byte> ProjectionToWorldMatrix,float DiffuseOcclusionFade);

public static class OriginalDeferredIndirectConstants
{
    public static byte[] ApplyNoSubShadow(DxbcResourceDefinitions source,ReadOnlySpan<byte> explicitCompleteTemplate,
        OriginalDeferredIndirectConstantInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(inputs);
        var buffers=source.Buffers.Where(b=>b.Name.Text=="$Globals").ToArray();
        if(buffers.Length!=1||buffers[0].ByteSize!=160||explicitCompleteTemplate.Length!=160||
            inputs.WorldToPointMatrix.Length!=64||inputs.ProjectionToWorldMatrix.Length!=64||
            !float.IsFinite(inputs.Pattern)||!float.IsFinite(inputs.DiffuseOcclusionFade))
            throw new InvalidDataException("Indirect no-subshadow constants require exact source CB/matrix lengths and finite scalar inputs.");
        var cb=buffers[0];
        foreach(var expected in new[]{(Name:"mW2Pt",Offset:0,Bytes:64),(Name:"fPtn",Offset:64,Bytes:4),
            (Name:"mP2W",Offset:80,Bytes:64),(Name:"fDOFade",Offset:144,Bytes:4)})
        {
            var actual=cb.Variables.Where(v=>v.Name.Text==expected.Name).ToArray();
            if(actual.Length!=1||actual[0].StartOffset!=expected.Offset||actual[0].ByteSize!=expected.Bytes)
                throw new InvalidDataException("Indirect no-subshadow named source field differs: "+expected.Name);
        }
        var result=explicitCompleteTemplate.ToArray();
        inputs.WorldToPointMatrix.Span.CopyTo(result.AsSpan(0,64));
        BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(64,4),inputs.Pattern);
        inputs.ProjectionToWorldMatrix.Span.CopyTo(result.AsSpan(80,64));
        BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(144,4),inputs.DiffuseOcclusionFade);
        return result;
    }
}
