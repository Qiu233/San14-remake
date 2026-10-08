using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedOriginalSkyAmbientResources(LoadedObjects Resource, DokObject Object,
    IReadOnlyList<OriginalCubeRenderTextureDefinition> SpecularTargets,
    OriginalCubeRenderTextureDefinition DiffuseTarget, IReadOnlyList<OriginalRenderTextureDefinition> PatternTargets,
    LoadedObjects ShaderObjects, DokObject ShaderObject, DokProperty ShaderReference, LoadedShaderContainer Shader);

/// <summary>Follows one explicit SkyAmbient source into its ordered targets and
/// MakeSkyAmbient shader. Does not choose a view clone, cube, phase or shader combination.</summary>
public static class OriginalSkyAmbientResources
{
    public const uint ClassId = 0x24A7AF0F;
    public const uint ShaderObjectClassId = 0x896435F1;

    public static LoadedOriginalSkyAmbientResources Load(OriginalGameRepository repository,
        LoadedObjects resource, uint objectNameHash, LoadedObjects shaderObjects, LoadedObjects targetObjects)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(shaderObjects); ArgumentNullException.ThrowIfNull(targetObjects);
        DokObject Object(LoadedObjects document, uint key, uint classId)
        {
            DokObject[] rows = document.Document.Objects.Where(o => o.ObjectId == key).ToArray();
            if (key == 0 || rows.Length != 1 || rows[0].Kind != DokObjectKind.Inline || rows[0].ClassId != classId)
                throw new InvalidDataException($"{document.Source.ResourceUri}: object {key:X8} requires one inline {classId:X8} source.");
            return rows[0];
        }
        DokProperty Field(DokObject obj, uint hash, int count)
        {
            DokProperty[] rows = obj.Properties.Where(p => p.NameHash == hash).ToArray();
            if (rows.Length != 1 || rows[0].TypeCode != 5 || rows[0].ElementCount != count || rows[0].RawValue.Length != count*4)
                throw new InvalidDataException($"SkyAmbient object {obj.ObjectId:X8}: property {hash:X8} requires {count} explicit type5 value(s).");
            return rows[0];
        }
        uint[] Keys(DokObject obj, uint hash, int count)
        {
            DokProperty field = Field(obj, hash, count);
            var keys = new uint[count];
            for (int i = 0; i < count; i++) keys[i] = BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(i*4,4));
            if (keys.Any(k => k == 0)) throw new InvalidDataException("SkyAmbient source has an unresolved zero target/shader reference.");
            return keys;
        }
        DokObject ambient = Object(resource, objectNameHash, ClassId);
        var specular = Keys(ambient, 0x94B0DC72, 4).Select(k => OriginalCubeRenderTextureResources.Read(targetObjects,k)).ToArray();
        var diffuse = OriginalCubeRenderTextureResources.Read(targetObjects, Keys(ambient, 0x5F5A9EDD, 1)[0]);
        var patterns = Keys(ambient, 0x4686371B, 4).Select(k => OriginalRenderTextureResources.Read(targetObjects,k)).ToArray();
        if (patterns.Any(t => t.Kind != OriginalRenderTextureKind.Texture2D))
            throw new InvalidDataException("SkyAmbient PatternMap requires the original 2D target class.");
        uint shaderKey = Keys(ambient, 0x134F0FA2, 1)[0];
        DokObject provider = Object(shaderObjects, shaderKey, ShaderObjectClassId);
        DokProperty reference = Field(provider, 0x193025DF, 1);
        uint file = BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue);
        if (file == 0) throw new InvalidDataException("SkyAmbient MakeSkyAmbient shader file reference is zero.");
        LoadedShaderContainer shader = repository.LoadShaderContainer("system", file);
        return new(resource, ambient, Array.AsReadOnly(specular), diffuse, Array.AsReadOnly(patterns),
            shaderObjects, provider, reference, shader);
    }
}
