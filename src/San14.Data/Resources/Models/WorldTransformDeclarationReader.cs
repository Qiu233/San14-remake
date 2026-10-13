using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SceneTransformValues(Vector3 Position, Vector4? Quaternion, Vector3 Scale,
    DokProperty PositionProperty, DokProperty? QuaternionProperty, DokProperty ScaleProperty);

/// <summary>Imports position, raw XYZW and scale from an already parsed declaration.
/// A missing quaternion remains explicit; the constructor chooses its supported inheritance rule.</summary>
public static class WorldTransformDeclarationReader
{
    public static SceneTransformValues Read(LoadedObjects source, DokObject item)
    {
        RequireOwnedObject(source, item);
        return ReadFields(source, item);
    }

    internal static SceneTransformValues ReadDefinition(LoadedObjects source, DokObject definition)
    {
        RequireDefinition(source, definition);
        return ReadFields(source, definition);
    }

    private static SceneTransformValues ReadFields(LoadedObjects source, DokObject item)
    {
        DokProperty position = Property(item, 0x27D889E5, source, optional: false)!;
        DokProperty? rotation = Property(item, 0xF52EA910, source, optional: true);
        DokProperty scale = Property(item, 0xFEA9E4F8, source, optional: false)!;
        float[] p = Floats(position, 3, item, source), s = Floats(scale, 3, item, source);
        Vector4? q = null;
        if (rotation is not null)
        {
            float[] values = Floats(rotation, 4, item, source);
            q = new Vector4(values[0], values[1], values[2], values[3]);
        }
        return new(new(p[0], p[1], p[2]), q, new(s[0], s[1], s[2]), position, rotation, scale);
    }

    internal static void RequireDefinition(LoadedObjects source, DokObject definition)
    {
        RequireOwnedObject(source, definition);
        if (definition.Kind != DokObjectKind.Inline || definition.ClassId != ScenePlacementReader.ModelWorldPqClass)
            throw new InvalidDataException("WorldPQ constructor requires its inline Model::WorldPQ definition.");
    }

    internal static void RequireOwnedObject(LoadedObjects source, DokObject item)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(item);
        if (!source.Document.Objects.Any(row => ReferenceEquals(row, item)))
            throw new InvalidDataException("WorldPQ declaration does not belong to the supplied document.");
    }

    private static DokProperty? Property(DokObject item, uint hash, LoadedObjects source, bool optional)
    {
        DokProperty[] matches = item.Properties.Where(property => property.NameHash == hash).ToArray();
        if (matches.Length == 0 && optional) return null;
        if (matches.Length != 1) throw Error(source, item, hash, "requires a unique property");
        return matches[0];
    }

    private static float[] Floats(DokProperty property, int count, DokObject item, LoadedObjects source)
    {
        if (property.TypeCode != 8 || property.ElementCount != count || property.ElementSize != 4 ||
            property.RawValue.Length != count * 4)
            throw Error(source, item, property.NameHash, $"requires type 8 with {count} float32 components");
        var values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(property.RawValue.AsSpan(i * 4, 4));
            if (!float.IsFinite(values[i]))
                throw Error(source, item, property.NameHash, $"component {i} is not finite");
        }
        return values;
    }

    private static InvalidDataException Error(LoadedObjects source, DokObject item, uint hash, string detail) =>
        new($"{source.Source.ResourceUri} object {item.ObjectId:X8} property {hash:X8} {detail}.");
}
