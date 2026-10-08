using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Recovered95A6C0 stock integer fullscreen quad. Current scale/color
/// are explicit; no original-process context or constructor value is inferred.</summary>
public sealed class OriginalDeferredAuxiliaryStockQuad
{
    public ReadOnlyMemory<byte> VertexBytes { get; }
    public ReadOnlyMemory<byte> IndexBytes { get; }
    public ReadOnlyMemory<byte> VertexConstantBytes { get; }
    public string CurrentContextProvenance { get; }
    public Vector2I Extent { get; }

    private OriginalDeferredAuxiliaryStockQuad(byte[] vertices,byte[] indices,byte[] constants,string provenance,Vector2I extent)
        =>(VertexBytes,IndexBytes,VertexConstantBytes,CurrentContextProvenance,Extent)=(vertices,indices,constants,provenance,extent);

    public static OriginalDeferredAuxiliaryStockQuad FromCurrentViewport(Vector2I extent,Vector2 scale,Color colorScale,string provenance)
    {
        if(extent.X<=0||extent.Y<=0||string.IsNullOrWhiteSpace(provenance)||
            new[]{scale.X,scale.Y,colorScale.R,colorScale.G,colorScale.B,colorScale.A}.Any(v=>!float.IsFinite(v)))
            throw new InvalidDataException("Stock source quad needs its finite current viewport, scale/color and owned context.");
        byte[] vertices=new byte[96];
        float w=extent.X,h=extent.Y;
        float[][] rows=[[0,0,0,0,0],[0,h,0,0,1],[w,h,0,1,1],[w,0,0,1,0]];
        for(int row=0;row<4;row++)
        {
            for(int c=0;c<3;c++)BinaryPrimitives.WriteSingleLittleEndian(vertices.AsSpan(row*24+c*4,4),rows[row][c]);
            BinaryPrimitives.WriteUInt32LittleEndian(vertices.AsSpan(row*24+12,4),uint.MaxValue);
            for(int c=0;c<2;c++)BinaryPrimitives.WriteSingleLittleEndian(vertices.AsSpan(row*24+16+c*4,4),rows[row][c+3]);
        }
        byte[] indices=new byte[12];ushort[] order=[0,1,2,0,2,3];
        for(int i=0;i<order.Length;i++)BinaryPrimitives.WriteUInt16LittleEndian(indices.AsSpan(i*2,2),order[i]);
        byte[] constants=OriginalDeferredAuxiliaryDepthCopySource.BuildStockVertexConstants(extent.X,extent.Y,
            scale.X,scale.Y,new System.Numerics.Vector4(colorScale.R,colorScale.G,colorScale.B,colorScale.A));
        return new(vertices,indices,constants,provenance,extent);
    }
}
