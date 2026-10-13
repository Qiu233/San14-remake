using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Declared material texture inputs. Names remain lookup keys until
/// the resource context captures their actual dependencies; they are not ready images.</summary>
public sealed class ModelMaterialTextureDeclaration
{
    public uint RegularGroup { get; }
    public uint RenderTargetGroup { get; }
    public uint AlternateGroup { get; }
    public uint BlendState { get; }
    public uint StageTableResource { get; }
    public ReadOnlyMemory<byte> Filename { get; }

    private ModelMaterialTextureDeclaration(uint regular, uint target, uint alternate,
        uint blend, uint stage, byte[] filename)
        => (RegularGroup, RenderTargetGroup, AlternateGroup, BlendState, StageTableResource, Filename) =
            (regular, target, alternate, blend, stage, filename);

    public static ModelMaterialTextureDeclaration Read(DokObject material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (material.Kind != DokObjectKind.Inline || material.ClassId != 0xA8F14404)
            throw new NotSupportedException("Material texture inputs require the inline material declaration.");
        uint Reference(uint name)
        {
            var property = material.Properties.SingleOrDefault(field => field.NameHash == name);
            if (property is null || property.TypeCode != 5 || property.ElementCount != 1 || property.RawValue.Length != 4)
                throw new InvalidDataException($"Material texture reference {name:X8} is not a resource identifier.");
            return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        }
        var filename = material.Properties.SingleOrDefault(field => field.NameHash == 0x8FD1DA96);
        if (filename is null || filename.TypeCode != 1 || filename.RawValue.Length != filename.ElementCount)
            throw new InvalidDataException("Material texture-stage filename is not its declared byte string.");
        return new(Reference(0xF92C5190), Reference(0x87814C77), Reference(0x80C25565),
            Reference(0xC17A66DF), Reference(0x0A3D837B), filename.RawValue.ToArray());
    }
}
