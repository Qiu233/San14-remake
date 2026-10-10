using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Resolve the graph's named material resource and pack its entries.
/// Row zero is reserved. The CSV source supplies the remaining rows unchanged.</summary>
internal static class HdrMaterialTable
{
    private const uint MaterialObjectDefinitions = 0xD956E4A2;
    private const uint MaterialObjectClass = 0xF6F3AFBD;
    private const uint MaterialResourceProperty = 0x6143C32F;

    public static byte[] Import(GameResourceRepository repository, uint objectId)
    {
        if (objectId == 0) return [];
        var objects = repository.LoadObjects("KIDSSystemResource", MaterialObjectDefinitions);
        var material = objects.Document.Objects.Single(value => value.ObjectId == objectId);
        if (material.ClassId != MaterialObjectClass)
            throw new InvalidDataException("HDR material reference must identify a material table object.");
        var reference = material.Properties.Single(value => value.NameHash == MaterialResourceProperty);
        if (reference.TypeCode != 5 || reference.RawValue.Length != sizeof(uint))
            throw new InvalidDataException("HDR material table requires a resource reference.");
        uint resource = BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue);
        var entries = MapMaterialCacheCodeSources.ReadSourceRecords(repository.LoadRdbAsset("RRPreview", resource));
        var table = new byte[checked((entries.Count + 1) * sizeof(uint))];
        for (int row = 0; row < entries.Count; row++)
            BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan((row + 1) * sizeof(uint)), entries[row].PackedWord);
        return table;
    }
}
