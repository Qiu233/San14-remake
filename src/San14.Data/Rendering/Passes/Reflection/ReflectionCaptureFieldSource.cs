using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The source field's three unsigned dimensions, without a computed
/// probe population or an allocation-time size restriction.</summary>
public readonly record struct ReflectionCaptureFieldDimensions(uint X, uint Y, uint Z);

/// <summary>One immutable normal probe-field declaration. Source identity and
/// authored values do not attest a current Scene instance, capture, or GPU readiness.</summary>
public sealed record ReflectionCaptureFieldDefinition(
    uint ObjectId, uint SourceFileId, string SourceResourceUri, string SourceDecodedSha256,
    Vector3 WorldPosition, Quaternion WorldQuaternion,
    ReflectionCaptureFieldDimensions FieldDimension, float CellSize,
    uint TileSize, uint GBufSize, float ViewRange, uint GroupMask,
    uint MaxPrimitiveNum, uint ProbeFieldShadowMapSize)
{
    public string SourceProvenance =>
        $"{SourceResourceUri}@{SourceDecodedSha256}#object/{ObjectId:X8};class/{ReflectionCaptureFieldSource.ClassId:X8}";
}

/// <summary>Imports class E6379BA2's ten typed fields at the normal resource
/// boundary. Missing properties retain the actual factory's initial values;
/// malformed present properties fail rather than turning into default values.</summary>
public static class ReflectionCaptureFieldSource
{
    public const uint ClassId = 0xE6379BA2;

    public static ReflectionCaptureFieldDefinition Read(LoadedObjects resources, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var matches = resources.Document.Objects.Where(value => value.ObjectId == objectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Reflection capture field requires one object {objectId:X8} in its source library.");
        return ReadDefinition(resources, matches[0]);
    }

    /// <summary>Returns actual matching declarations in the supplied resource's
    /// object order. An empty result describes this resource only; it never
    /// supplies an empty current Scene provider registry.</summary>
    public static IReadOnlyList<ReflectionCaptureFieldDefinition> Find(LoadedObjects resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var result = new List<ReflectionCaptureFieldDefinition>();
        var identities = new HashSet<uint>();
        foreach (var definition in resources.Document.Objects)
        {
            if (definition.ClassId != ClassId) continue;
            if (!identities.Add(definition.ObjectId))
                throw new InvalidDataException($"Reflection capture field identity {definition.ObjectId:X8} is duplicated in one resource.");
            result.Add(ReadDefinition(resources, definition));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static ReflectionCaptureFieldDefinition ReadDefinition(LoadedObjects resources, DokObject definition)
    {
        if (definition.Kind != DokObjectKind.Inline || definition.ClassId != ClassId || definition.Reference is not null)
            throw new InvalidDataException("Reflection capture field requires its inline E6379BA2 definition.");

        DokProperty? Field(uint hash, uint type, int count)
        {
            var fields = definition.Properties.Where(value => value.NameHash == hash).ToArray();
            if (fields.Length == 0) return null;
            if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != count ||
                fields[0].ElementSize != 4 || fields[0].RawValue.Length != checked(count * 4))
                throw new InvalidDataException($"Reflection capture field {definition.ObjectId:X8} has an invalid typed property {hash:X8}.");
            return fields[0];
        }

        static float Float(DokProperty property, int index) =>
            BinaryPrimitives.ReadSingleLittleEndian(property.RawValue.AsSpan(checked(index * 4), 4));
        static uint Word(DokProperty property, int index) =>
            BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(checked(index * 4), 4));
        float Scalar(uint hash, float initial) => Field(hash, 8, 1) is { } value ? Float(value, 0) : initial;
        uint Unsigned(uint hash, uint initial) => Field(hash, 5, 1) is { } value ? Word(value, 0) : initial;

        var position = Field(0x27D889E5, 8, 3);
        var quaternion = Field(0xF52EA910, 8, 4);
        var dimensions = Field(0xA9A7E56C, 5, 3);
        // Keep the source quaternion and IEEE values unchanged. The importer
        // does not run a setter, normalize a pose, multiply dimensions, or
        // infer positive allocation limits that the declaration loader lacks.
        return new(definition.ObjectId, resources.Source.FileId,
            resources.Source.ResourceUri, resources.Source.DecodedSha256,
            position is null ? Vector3.Zero : new(Float(position, 0), Float(position, 1), Float(position, 2)),
            quaternion is null ? Quaternion.Identity : new(Float(quaternion, 0), Float(quaternion, 1), Float(quaternion, 2), Float(quaternion, 3)),
            dimensions is null ? new(0, 0, 0) : new(Word(dimensions, 0), Word(dimensions, 1), Word(dimensions, 2)),
            Scalar(0x46843CA3, 0), Unsigned(0x4621A92F, 0), Unsigned(0x4633440D, 0),
            Scalar(0x4929D408, 0), Unsigned(0xC0C5A5B5, 0), Unsigned(0xDB26951D, 0),
            Unsigned(0x4A4A850D, 0x800));
    }
}
