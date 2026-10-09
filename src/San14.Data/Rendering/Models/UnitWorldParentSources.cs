using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual declared native controller inputs. RDOK's definition
/// reference is retained separately; it does not become a transform parent.</summary>
public sealed record UnitWorldParentSourceInputs(uint DefinitionObjectId,
    IReadOnlyList<uint> StaticDopeSheetObjectNameHashes, uint PoseScripCodeObjectNameHash,
    IReadOnlyList<uint> ChildObjectNameHashes, IReadOnlyList<uint> ChildObjectOidNameHashes,
    string EvidenceUri, string EvidenceSha256)
{
    public bool HasDeclaredControllerInputs => StaticDopeSheetObjectNameHashes.Count != 0 ||
        PoseScripCodeObjectNameHash != 0 || ChildObjectNameHashes.Count != 0 || ChildObjectOidNameHashes.Count != 0;
}

/// <summary>Source-only inspection of the four actual native properties. The
/// tables and pose script can drive later operations; they are not parent edges.
/// Their absence does not assert that an external later bind never happened.</summary>
public static class UnitWorldParentSources
{
    public const uint StaticDopeSheetObjectNameHash = 0xAA117B97;
    public const uint PoseScripCodeObjectNameHash = 0x5F698B03;
    public const uint ChildObjectNameHashArray = 0xE4706CC5;
    public const uint ChildObjectOidNameHashArray = 0x55460C2F;

    public static UnitWorldParentSourceInputs ReadDefinition(LoadedObjects catalog, DokObject definition)
    {
        // This validates the actual inline Model::WorldPQ identity and complete
        // source hash. Reparse the four additional properties from those bytes.
        _ = UnitWorldTransform.ConstructDefinition(catalog, definition);
        DokObject actual = DokReader.Read(catalog.Source.Bytes).Objects.Single(row =>
            row.Index == definition.Index && row.Offset == definition.Offset);
        uint[] dopes = Words(actual, StaticDopeSheetObjectNameHash);
        uint[] pose = Words(actual, PoseScripCodeObjectNameHash);
        if (pose.Length != 1) throw new InvalidDataException("Native PoseScripCodeObjectNameHash requires its explicit scalar.");
        return new(actual.ObjectId, Array.AsReadOnly(dopes), pose[0],
            Array.AsReadOnly(Words(actual, ChildObjectNameHashArray)),
            Array.AsReadOnly(Words(actual, ChildObjectOidNameHashArray)),
            catalog.Source.ResourceUri, catalog.Source.DecodedSha256);
    }

    /// <summary>Actual known scene constructor checkpoint, gated by the explicit
    /// empty controller fields of the referenced definition. Later parent state
    /// must be supplied through OriginalUnitWorldParentReference.</summary>
    public static UnitWorldParentState ConstructScenePrefix(ScenePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var inputs = ReadDefinition(placement.Source.DefinitionDocument, placement.Source.Definition);
        if (inputs.HasDeclaredControllerInputs)
            throw new InvalidDataException("Scene WorldPQ declares native controller inputs; their actual operations/current context are required.");
        if (placement.Source.Object.Properties.Any(row => row.NameHash is StaticDopeSheetObjectNameHash or
            PoseScripCodeObjectNameHash or ChildObjectNameHashArray or ChildObjectOidNameHashArray))
            throw new InvalidDataException("Scene instance controller overrides require their actual native materialization context.");
        return UnitWorldParent.FromConstructor(UnitWorldTransform.ConstructScenePlacement(placement));
    }

    private static uint[] Words(DokObject definition, uint hash)
    {
        DokProperty[] fields = definition.Properties.Where(row => row.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementSize != 4 ||
            fields[0].RawValue.Length != fields[0].ElementCount * 4L)
            throw new InvalidDataException($"Native WorldPQ source property {hash:X8} requires its explicit type5 array/scalar.");
        var result = new uint[fields[0].ElementCount];
        for (int i = 0; i < result.Length; i++) result[i] = BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue.AsSpan(i * 4, 4));
        return result;
    }
}
