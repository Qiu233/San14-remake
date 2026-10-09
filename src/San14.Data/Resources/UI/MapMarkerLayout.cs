using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapMarkerLayout(string Name, int CommonRow,
    uint NamedObjectId, LoadedRdbAsset Source, string SourceSignature,
    LcskDocument Layout, IReadOnlyList<LcskNodeRecord> Nodes)
{
    internal static MapMarkerLayout Load(GameResourceRepository repository,
        string name, uint expectedFile, string expectedSignature)
    {
        var common = repository.LoadObjects("KIDSSystemResource", 0x6D011726);
        var owner = common.Document.Objects.Single(o => o.ObjectId == 0x61AD16CF && o.ClassId == 0xDAC911D7);
        var keys = owner.Properties.Single(p => p.NameHash == 0x10FA95C2).RawValue;
        var names = owner.Properties.Single(p => p.NameHash == 0xB45B5E75).RawValue;
        uint key = ResourceNameHash.Compute(name);
        int row = -1;
        for (int i = 0; i < keys.Length / 4; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(keys.AsSpan(i * 4)) == key) { row = i; break; }
        if (row < 0) throw new InvalidDataException($"Original map layout name {name} is missing.");
        uint named = BinaryPrimitives.ReadUInt32LittleEndian(names.AsSpan(row * 4));
        var definitions = repository.LoadObjects("KIDSSystemResource", 0x208BE13A);
        var declaration = definitions.Document.Objects.Single(o => o.ObjectId == named);
        uint file = BinaryPrimitives.ReadUInt32LittleEndian(declaration.Properties.Single(p => p.NameHash == 0x15BE61AD).RawValue);
        if (file != expectedFile) throw new InvalidDataException($"Original map layout {name} source file differs.");
        var source = repository.LoadRdbAsset("ScreenLayout", file);
        string signature = Encoding.ASCII.GetString(source.Bytes, 0, 8);
        if (signature != expectedSignature) throw new InvalidDataException($"Original map layout {name} source signature differs.");
        byte[] readable = source.Bytes.ToArray();
        // These audited map children have the same block/node/part structure.
        // Keep the general LCSK reader's supported versions unchanged.
        if (signature is "LCSK6500" or "LCSK7500" || signature == "LCSK4500" && file == 0xAFC125D8)
            "LCSK8500"u8.CopyTo(readable);
        var layout = LcskLayoutReader.Read(readable);
        return new(name, row, named, source, signature, layout, LcskNodeReader.Read(layout));
    }
}
