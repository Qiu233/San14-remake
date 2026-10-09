using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Unmodified E01606F4 source values. This declaration does not identify
/// a selected LightGroup or the postmodifier values of an original frame.</summary>
public sealed record DeferredAmbientSourceParameters(float DiffuseFade, float SpecularFade,
    float SpecularScale, Vector3 SpecularColorScale, float DiffuseScale, Vector3 DiffuseColorScale);

public sealed record LoadedDeferredAmbientSource(uint ObjectId,
    DeferredAmbientSourceParameters Parameters, LoadedObjects SourceDocument);

/// <summary>Reads the original Directional source's six ambient modifier properties.
/// CBFAB0 copies them into built-in H = L+1B0, at H+150..174. External modifiers,
/// subsequent setters and CameraScratch+3CC remain explicit caller inputs.</summary>
public static class DeferredAmbientSource
{
    public const uint SourceClassId = 0xE01606F4;

    public static LoadedDeferredAmbientSource Load(GameResourceRepository repository,
        string archive, uint fileId, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedObjects document = repository.LoadObjects(archive, fileId);
        DokObject[] matches = document.Document.Objects.Where(item => item.ObjectId == objectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Ambient source {document.Source.ResourceUri}: object {objectId:X8} is missing or ambiguous.");
        return new(objectId, Read(matches[0]), document);
    }

    public static DeferredAmbientSourceParameters Read(DokObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Kind != DokObjectKind.Inline || source.ClassId != SourceClassId)
            throw new InvalidDataException($"Ambient source {source.ObjectId:X8} requires inline class {SourceClassId:X8}; inherited/reference sources are unresolved.");
        return new(Scalar(0x4F49EF33, "IDLFade"), Scalar(0x4F4A2782, "ISLFade"),
            Scalar(0xE3EB705E, "ISLScalerOfAmbientSpecularMap"),
            Color(0x952A6114, "ISLScalerOfAmbientSpecularMapColorRGB"),
            Scalar(0xAAD35562, "IDLScalerOfAmbientDiffuseMap"),
            Color(0x663875EC, "IDLScalerOfAmbientDiffuseMapColorRGB"));

        float Scalar(uint hash, string name) => Values(hash, name, 1)[0];
        Vector3 Color(uint hash, string name)
        {
            float[] values = Values(hash, name, 3);
            return new(values[0], values[1], values[2]);
        }
        float[] Values(uint hash, string name, int count)
        {
            DokProperty[] properties = source.Properties.Where(item => item.NameHash == hash).ToArray();
            if (properties.Length != 1 || properties[0].TypeCode != 8 ||
                properties[0].ElementCount != count || properties[0].ElementSize != 4 ||
                properties[0].RawValue.Length != count * 4)
                throw new InvalidDataException($"Ambient source {source.ObjectId:X8} requires one float{count} {name}/{hash:X8} property.");
            float[] values = new float[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = BinaryPrimitives.ReadSingleLittleEndian(properties[0].RawValue.AsSpan(i * 4, 4));
                if (!float.IsFinite(values[i]))
                    throw new InvalidDataException($"Ambient source {source.ObjectId:X8} contains nonfinite {name}[{i}].");
            }
            return values;
        }
    }

    /// <summary>Original D74820 writes, only when this unmodified built-in source
    /// modifier has explicitly been selected. The camera rate is not a light
    /// property and is never taken from LightMapDOFade or a source default.</summary>
    public static DeferredAmbientInputs Prepare(DeferredAmbientSourceParameters source,
        float explicitCameraMapOcclusionRate)
    {
        ArgumentNullException.ThrowIfNull(source);
        float[] consumed = [source.DiffuseFade, source.SpecularFade, source.SpecularScale,
            source.SpecularColorScale.X, source.SpecularColorScale.Y, source.SpecularColorScale.Z,
            source.DiffuseScale, source.DiffuseColorScale.X, source.DiffuseColorScale.Y,
            source.DiffuseColorScale.Z, explicitCameraMapOcclusionRate];
        if (consumed.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Ambient preparation requires finite explicit source fields and CameraScratch+3CC.");
        var result = new DeferredAmbientInputs(explicitCameraMapOcclusionRate,
            new(Multiply(source.DiffuseScale, source.DiffuseColorScale.X),
                Multiply(source.DiffuseScale, source.DiffuseColorScale.Y),
                Multiply(source.DiffuseScale, source.DiffuseColorScale.Z), source.DiffuseFade),
            new(Multiply(source.SpecularScale, source.SpecularColorScale.X),
                Multiply(source.SpecularScale, source.SpecularColorScale.Y),
                Multiply(source.SpecularScale, source.SpecularColorScale.Z), source.SpecularFade));
        if (!Finite(result.DiffuseScale) || !Finite(result.SpecularScale))
            throw new InvalidDataException("Ambient source multiplication produced a nonfinite shader value.");
        return result;
    }

    private static bool Finite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right) => left * right;
}
