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
        var (row, named, file) = ResolveNamedFile(repository, name);
        if (file != expectedFile) throw new InvalidDataException($"Original map layout {name} source file differs.");
        var source = repository.LoadRdbAsset("ScreenLayout", file);
        string signature = Encoding.ASCII.GetString(source.Bytes, 0, 8);
        if (signature != expectedSignature) throw new InvalidDataException($"Original map layout {name} source signature differs.");
        var layout = LcskLayoutReader.Read(source.Bytes);
        return new(name, row, named, source, signature, layout, LcskNodeReader.Read(layout));
    }

    // A named consumer without a previously extracted file ID still resolves
    // through the same Common declaration. The normal layout reader owns its
    // supported binary format checks; fixed-file callers retain both checks above.
    internal static LoadedLayout LoadNamed(GameResourceRepository repository, string name)
    {
        var (_, _, file) = ResolveNamedFile(repository, name);
        return repository.LoadLayout("ScreenLayout", file);
    }

    private static (int Row, uint Named, uint File) ResolveNamedFile(
        GameResourceRepository repository, string name)
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
        return (row, named, file);
    }
}
