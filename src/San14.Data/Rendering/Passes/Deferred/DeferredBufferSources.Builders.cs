using System.Buffers.Binary;

namespace San14.Data;

public sealed record DeferredClusterCell(IReadOnlyList<ushort> PointIndices,
    IReadOnlyList<ushort> SpotIndices);

public static partial class DeferredBufferSources
{
    /// <summary>Copy already prepared source texels without changing format,
    /// float bits or allocation tails. Every texel must have its source extent.</summary>
    public static DeferredPreparedBuffer FromPreparedTexels(uint sourceBindPoint,
        uint explicitNativeViewFormat,IReadOnlyList<ReadOnlyMemory<byte>> exactSourceTexels,
        string sourceResourceUri)
    {
        ArgumentNullException.ThrowIfNull(exactSourceTexels);
        int size=TexelByteSize(explicitNativeViewFormat);
        if(exactSourceTexels.Count==0 || exactSourceTexels.Any(t=>t.Length!=size) ||
            string.IsNullOrWhiteSpace(sourceResourceUri))
            throw Error("prepared source texel copy requires complete nonempty rows and provenance");
        byte[] bytes=new byte[checked(exactSourceTexels.Count*size)];
        for(int i=0;i<exactSourceTexels.Count;i++)exactSourceTexels[i].Span.CopyTo(bytes.AsSpan(i*size,size));
        return new(sourceBindPoint,explicitNativeViewFormat,bytes,sourceResourceUri);
    }

    /// <summary>Pack an explicitly supplied populated source membership list
    /// into the PS1 four-word cell layout. This does not calculate memberships.
    /// Whole-source empty branches require their original null/retain decision.</summary>
    public static DeferredPreparedBuffer CreateClusterIndices(
        IReadOnlyList<DeferredClusterCell> explicitSourceCells,uint explicitNativeViewFormat,
        string sourceResourceUri)
    {
        ArgumentNullException.ThrowIfNull(explicitSourceCells);
        if(explicitSourceCells.Count==0 || explicitSourceCells.Any(c=>c is null || c.PointIndices is null ||
            c.SpotIndices is null || c.PointIndices.Count>ushort.MaxValue || c.SpotIndices.Count>ushort.MaxValue))
            throw Error("cluster membership requires complete source cells with ushort counts");
        int references=0;
        foreach(var cell in explicitSourceCells)references=checked(references+cell.PointIndices.Count+cell.SpotIndices.Count);
        if(references==0)throw Error("empty cluster membership does not prove a bindable source buffer");
        int header=checked(explicitSourceCells.Count*4);
        byte[] bytes=IndexBytes(explicitNativeViewFormat,checked(header+references),sourceResourceUri);
        int offset=header;
        for(int cell=0;cell<explicitSourceCells.Count;cell++)
        {
            var source=explicitSourceCells[cell];
            PutIndex(bytes,explicitNativeViewFormat,cell*4,(uint)offset&ushort.MaxValue);
            PutIndex(bytes,explicitNativeViewFormat,cell*4+1,(uint)offset>>16);
            PutIndex(bytes,explicitNativeViewFormat,cell*4+2,(uint)source.PointIndices.Count);
            PutIndex(bytes,explicitNativeViewFormat,cell*4+3,(uint)source.SpotIndices.Count);
            foreach(ushort index in source.PointIndices)PutIndex(bytes,explicitNativeViewFormat,offset++,index);
            foreach(ushort index in source.SpotIndices)PutIndex(bytes,explicitNativeViewFormat,offset++,index);
        }
        return new(16,explicitNativeViewFormat,bytes,sourceResourceUri);
    }

    /// <summary>Pack explicitly supplied populated local ambient memberships
    /// into the PS1 (offset,count) cell layout. Transform preparation and scene
    /// probe selection remain the source producer's responsibility.</summary>
    public static DeferredPreparedBuffer CreateLocalAmbientIndices(
        IReadOnlyList<IReadOnlyList<uint>> explicitSourceCells,uint explicitNativeViewFormat,
        string sourceResourceUri)
    {
        ArgumentNullException.ThrowIfNull(explicitSourceCells);
        if(explicitSourceCells.Count==0 || explicitSourceCells.Any(c=>c is null))
            throw Error("local ambient membership requires complete source cells");
        int references=0;
        foreach(var cell in explicitSourceCells)references=checked(references+cell.Count);
        if(references==0)throw Error("empty local ambient membership does not prove a bindable source buffer");
        int header=checked(explicitSourceCells.Count*2);
        byte[] bytes=IndexBytes(explicitNativeViewFormat,checked(header+references),sourceResourceUri);
        int offset=header;
        for(int cell=0;cell<explicitSourceCells.Count;cell++)
        {
            var source=explicitSourceCells[cell];
            PutIndex(bytes,explicitNativeViewFormat,cell*2,(uint)offset);
            PutIndex(bytes,explicitNativeViewFormat,cell*2+1,(uint)source.Count);
            foreach(uint index in source)PutIndex(bytes,explicitNativeViewFormat,offset++,index);
        }
        return new(24,explicitNativeViewFormat,bytes,sourceResourceUri);
    }

    private static byte[] IndexBytes(uint format,int count,string uri)
    {
        if(format is not(42 or 57) || string.IsNullOrWhiteSpace(uri))
            throw Error("index builder requires an explicit scalar uint source view and provenance");
        return new byte[checked(count*TexelByteSize(format))];
    }
    private static void PutIndex(byte[] bytes,uint format,int texel,uint value)
    {
        if(format==57)
        {
            if(value>ushort.MaxValue)throw Error("source R16_UINT texel value exceeds its original extent");
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(checked(texel*2),2),(ushort)value);
        }
        else BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(checked(texel*4),4),value);
    }
}
