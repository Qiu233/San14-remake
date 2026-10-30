using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G2ShaderTextureBaseRow(int CombinationIndex, int MetadataOffset,
    byte[] RawMetadataHeader, ushort MetadataEntryCount, ushort ExtraTextureCount, uint TextureBase);
public sealed record G2ShaderTextureBases(int ModeValueOffset, uint ModeValue,
    int DescriptorOffset, uint DescriptorFlags, uint DescriptorTextureCount,
    byte[] RawDescriptor, IReadOnlyList<G2ShaderTextureBaseRow> Rows);

/// <summary>
/// The verified CE renderer's zero-mode source texture-base calculation.
/// Keeps per-combination metadata; it does not choose an active row or sampler.
/// Other native constructor modes are explicitly unsupported.
/// </summary>
public static class G2ShaderTextureBaseReader
{
    public static G2ShaderTextureBases Read(ReadOnlySpan<byte> source)
        => ReadCore(source,0,0,true);

    /// <summary>Actual selected feature and texture-descriptor rows. The old
    /// single-row constructor contract remains the default Read overload.</summary>
    public static G2ShaderTextureBases ReadSelectedRows(ReadOnlySpan<byte> source,
        uint actualFeatureRow,uint actualTextureDescriptorRow)
        => ReadCore(source,actualFeatureRow,actualTextureDescriptorRow,false);

    private static G2ShaderTextureBases ReadCore(ReadOnlySpan<byte> source,uint featureRow,uint textureRow,bool single)
    {
        G2ShaderContainer container = G2ShaderContainerReader.Read(source);
        G2ShaderCombinations combinations = G2ShaderCombinationReader.Read(source);
        uint featureCount=U32(source,0x48),textureCount=U32(source,0x58);
        if (single && (featureCount != 1 || textureCount != 1))
            throw Error("constructor requires one entry in both source descriptor tables");
        if(featureRow>=featureCount||textureRow>=textureCount)
            throw Error("selected source descriptor row exceeds its declaration");
        uint table = U32(source,0x78), descriptor = U32(source,0x88);
        if (table > int.MaxValue - 0x20 || descriptor > int.MaxValue)
            throw Error("source descriptor offset exceeds supported addressing");
        int modeOffset = checked((int)table + checked((int)featureRow*48) + 0x20);
        int descriptorOffset = checked((int)descriptor + checked((int)textureRow*40));
        InMetadata(container,modeOffset,4);
        InMetadata(container,descriptorOffset,16);
        uint mode = U32(source,modeOffset);
        if (mode != 0) throw Error($"native constructor mode {mode} is unresolved");
        uint flags = U32(source,descriptorOffset), count = U32(source,descriptorOffset+4);
        uint baseValue = unchecked(((flags >> 5) & 1) + count);
        var rows = new List<G2ShaderTextureBaseRow>(combinations.Rows.Count);
        foreach (G2ShaderCombination combination in combinations.Rows)
        {
            if (combination.RawValue4 > int.MaxValue)
                throw Error($"combination {combination.Index} metadata offset exceeds supported addressing");
            int at = (int)combination.RawValue4;
            InMetadata(container,at,4);
            ushort entries = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(at,2));
            ushort extra = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(at+2,2));
            rows.Add(new(combination.Index,at,source.Slice(at,4).ToArray(),entries,extra,
                unchecked(baseValue+extra)));
        }
        return new(modeOffset,mode,descriptorOffset,flags,count,
            source.Slice(descriptorOffset,16).ToArray(),rows.AsReadOnly());
    }
    private static void InMetadata(G2ShaderContainer container,int offset,int length)
    {
        if (offset < container.MetadataRange.Offset || (long)offset+length >
            (long)container.MetadataRange.Offset+container.MetadataRange.Length)
            throw Error("source descriptor or row metadata lies outside the original metadata range");
    }
    private static uint U32(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at,4));
    private static InvalidDataException Error(string detail)=>new($"S2G source texture base: {detail}.");
}
