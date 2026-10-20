using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record LcskPolygonVertex(float X, float Y, float Z,
    SpriteUv FirstUv, SpriteUv SecondUv, SpriteUv ThirdUv, uint Color);

public sealed record LcskPolygonMesh(IReadOnlyList<LcskPolygonVertex> Vertices,
    IReadOnlyList<int> Indices, ushort ReferenceCode);

/// <summary>Reads the type-8 indexed mesh from its declared pools. The retained
/// reference code does not identify the graphics primitive topology.</summary>
public static class LcskPolygonNodeReader
{
    public static LcskPolygonMesh Read(LcskDocument layout, LcskNodeRecord node)
    {
        if(node.Type!=8 || node.RawBytes.Length!=228)
            throw new ArgumentException("An indexed UI polygon requires a type-8 node.",nameof(node));
        var block=layout.Blocks.Single(b=>b.Tag==1);
        ReadOnlySpan<byte> raw=node.RawBytes;
        ushort vertexEntry=BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(128,2));
        ushort indexEntry=BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(130,2));
        var vertexPool=block.Pools.Single(p=>p.Type==11);
        var indexPool=block.Pools.Single(p=>p.Type==12);
        if(vertexEntry>=vertexPool.Entries.Count || indexEntry>=indexPool.Entries.Count)
            throw new InvalidDataException("UI polygon references a missing mesh pool entry.");
        ReadOnlySpan<byte> vertices=vertexPool.Entries[vertexEntry].Data;
        ReadOnlySpan<byte> indices=indexPool.Entries[indexEntry].Data;
        uint vertexCount=BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(132,4));
        uint indexCount=BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(136,4));
        if(vertexCount==0 || indexCount==0 || vertices.Length!=(long)vertexCount*40 || indices.Length!=(long)indexCount*4)
            throw new InvalidDataException("UI polygon declared mesh counts differ from their pool payloads.");
        var result=new LcskPolygonVertex[checked((int)vertexCount)];
        for(int i=0;i<result.Length;i++)
        {
            ReadOnlySpan<byte> vertex=vertices.Slice(i*40,40);
            float[] values=new float[9];
            for(int component=0;component<values.Length;component++)
            {
                values[component]=BinaryPrimitives.ReadSingleLittleEndian(vertex.Slice(component*4,4));
                if(!float.IsFinite(values[component])) throw new InvalidDataException("UI polygon vertex contains a nonfinite value.");
            }
            result[i]=new(values[0],values[1],values[2],new(values[3],values[4]),
                new(values[5],values[6]),new(values[7],values[8]),BinaryPrimitives.ReadUInt32LittleEndian(vertex.Slice(36,4)));
        }
        var order=new int[checked((int)indexCount)];
        for(int i=0;i<order.Length;i++)
        {
            uint index=BinaryPrimitives.ReadUInt32LittleEndian(indices.Slice(i*4,4));
            if(index>=vertexCount) throw new InvalidDataException("UI polygon index exceeds its declared vertices.");
            order[i]=(int)index;
        }
        return new(result,order,BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(140,2)));
    }
}
