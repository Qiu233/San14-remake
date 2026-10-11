using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OwnedWorldListDeclaration(uint ObjectId, int InitialCapacity, int GrowthStep);

/// <summary>Exact empty ListContainer declarations and FrameSync clear names.
/// This does not resolve a world actor, its CPU or any light payload.</summary>
public sealed class OwnedWorldListSource
{
    public const uint DefinitionsResourceId = 0x908AB6E2;
    private readonly OwnedWorldListDeclaration[] declarations;
    private readonly uint[] clearNames;

    private OwnedWorldListSource(OwnedWorldListDeclaration[] declarations, uint[] clearNames, LoadedRdbAsset source)
    {
        this.declarations = declarations;
        this.clearNames = clearNames;
        DefinitionResourceUri = source.ResourceUri;
        DefinitionSha256 = source.DecodedSha256;
    }

    public string DefinitionResourceUri { get; }
    public string DefinitionSha256 { get; }

    public IReadOnlyList<OwnedWorldListDeclaration> Declarations => Array.AsReadOnly(declarations);
    public IReadOnlyList<uint> FrameSyncClearNames => Array.AsReadOnly(clearNames);

    public static OwnedWorldListSource Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Read(repository.LoadObjects("system", DefinitionsResourceId),
            repository.LoadTaskGraph("system", DefaultView0Source.TaskGraphResourceId));
    }

    public static OwnedWorldListSource Read(LoadedRdbAsset definitions, LoadedRdbAsset graph)
    {
        return Read(new LoadedObjects(DokReader.Read(definitions.Bytes), definitions),
            new LoadedTaskGraph(TaskGraphReader.Read(graph.Bytes), graph));
    }

    public static OwnedWorldListSource Read(LoadedObjects definitions, LoadedTaskGraph graph)
    {
        if (definitions.Source.FileId != DefinitionsResourceId ||
            definitions.Source.ResourceUri != GameResourceId.Rdb("system", DefinitionsResourceId).ToString())
            throw new InvalidDataException("World list declarations require the supported definition library.");
        Verify(graph.Source, DefaultView0Source.TaskGraphResourceId, DefaultView0Source.TaskGraphResourceSha256);
        var dok = definitions.Document;
        uint[] names = [0x175E5FDB, 0x204AB825];
        var declarations = names.Select(name =>
        {
            var obj = dok.Objects.Single(o => o.ObjectId == name);
            if (obj.Kind != DokObjectKind.Inline || obj.ClassId != 0x2EB79141)
                throw new InvalidDataException("World list declaration is not the audited ListContainer class.");
            var capacity = Scalar(obj.Properties.Single(p => p.NameHash == 0xFC63FB31), 0xFC63FB31);
            var growth = Scalar(obj.Properties.Single(p => p.NameHash == 0x04E203A7), 0x04E203A7);
            var initial = obj.Properties.Single(p => p.NameHash == 0xFF26EE41);
            if (initial.NameHash != 0xFF26EE41 || initial.TypeCode != 5 || initial.ElementCount != 0 || initial.RawValue.Length != 0)
                throw new NotSupportedException("Nonempty declared ListContainer needs a resolved initial Data owner.");
            if (capacity == 0 || growth == 0 || capacity > int.MaxValue || growth > int.MaxValue)
                throw new NotSupportedException("World list managed allocation requires positive bounded capacity/growth.");
            return new OwnedWorldListDeclaration(name, (int)capacity, (int)growth);
        }).ToArray();
        var task = graph.Document.Nodes.Single(n => n.Index == 1);
        if (task.Call.FactoryClassId != 0xAB5B4109)
            throw new InvalidDataException("World list clear task is not the audited FrameSync node.");
        var clear = task.Call.Arguments.Single(a => a.NameHash == 0x94AFB4B5);
        if (clear.RawType != 5 || clear.RawElementCount != 8 || clear.RawValue.Length != 32)
            throw new InvalidDataException("FrameSync clear-name array shape differs from the original source.");
        uint[] clearNames = Enumerable.Range(0, 8).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(clear.RawValue.AsSpan(i * 4, 4))).ToArray();
        if (!clearNames.Contains(0x175E5FDBu) || clearNames.Contains(0x204AB825u))
            throw new InvalidDataException("FrameSync world-list clear roles differ from the audited source.");
        return new(declarations, clearNames, definitions.Source);
    }

    private static uint Scalar(DokProperty property, uint hash)
    {
        if (property.NameHash != hash || property.TypeCode != 5 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("World list allocation property shape differs from the source.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }

    private static void Verify(LoadedRdbAsset source, uint id, string hash)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.FileId != id || source.ResourceUri != GameResourceId.Rdb("system", id).ToString() ||
            !source.DecodedSha256.Equals(hash, StringComparison.OrdinalIgnoreCase) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != hash)
            throw new InvalidDataException("World list source URI, declared hash or decoded bytes differ from the pinned source.");
    }
}
