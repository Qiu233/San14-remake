using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Complete source opcode4B emitted by95B570. The native958210
/// decoder passes its four corners, UVs, packed colors and depth to962CC0.
/// A complete caller record is required; text layout and atlas placement are
/// distinct producers and no centered quad or replacement UV is inferred.</summary>
public sealed class OriginalArmyFlagGlyphQuad
{
    private readonly byte[] command;
    public string CallerSource { get; }
    public ReadOnlyMemory<byte> Command88=>command;
    private OriginalArmyFlagGlyphQuad(byte[] bytes,string callerSource)=>(command,CallerSource)=(bytes,callerSource);

    public static OriginalArmyFlagGlyphQuad ReadComplete(ReadOnlySpan<byte> complete88,string callerSource)
    {
        if(complete88.Length!=88||BinaryPrimitives.ReadUInt32LittleEndian(complete88)!=0x4B||string.IsNullOrWhiteSpace(callerSource))
            throw new InvalidDataException("Army flag glyph requires the complete source opcode4B and caller provenance.");
        foreach(int offset in Enumerable.Range(0,16).Select(i=>4+i*4).Append(84))
            if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(complete88.Slice(offset,4))))
                throw new InvalidDataException("Army flag source glyph corners/UV/depth must be finite.");
        return new(complete88.ToArray(),callerSource);
    }

    public byte[] CopyNativeVertexBytes()
    {
        byte[] vertices=new byte[96];
        int[] nativeOrder=[0,2,3,1];
        for(int i=0;i<4;i++)
        {
            int source=nativeOrder[i];
            command.AsSpan(4+source*8,8).CopyTo(vertices.AsSpan(i*24,8));
            command.AsSpan(84,4).CopyTo(vertices.AsSpan(i*24+8,4));
            command.AsSpan(68+source*4,4).CopyTo(vertices.AsSpan(i*24+12,4));
            command.AsSpan(36+source*8,8).CopyTo(vertices.AsSpan(i*24+16,8));
        }
        return vertices;
    }

    public static byte[] CopyNativeIndices()=>[0,0,1,0,2,0,0,0,2,0,3,0];
}
