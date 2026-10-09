using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ModelGbufferMaterialSource(LoadedShaderContainer Shader,
    G1DrawMaterialBinding Draw, uint MaterialModeCount, uint RawMaterialFlags,
    Vector4 SourceSpecular, byte SourceAlphaReference, G1NamedParameter? MaterialIdParameter,
    int? MaterialId);

public sealed record ModelSpecularPreparation(bool WritesValue, Vector4? Value,
    uint NextCachedFlags);

/// <summary>Original material source fields and finite native preparation rules.
/// Backend alpha enable, forced reference, and previous cached flags/value remain
/// explicit inputs; absence of a source parameter never becomes an invented zero.</summary>
public static class ModelGbufferMaterialSourceReader
{
    public static ModelGbufferMaterialSource Read(LoadedShaderContainer shader, G1DrawMaterialBinding draw)
    {
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(draw);
        G2ShaderContainer container = G2ShaderContainerReader.Read(shader.Source.Bytes);
        // 96CB07 copies the complete source header to Module+30; 3000 performs
        // no header normalization. Module+84 therefore reads source+54.
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x54, 4));
        if (count > 1)
            throw new InvalidDataException($"{shader.Source.ResourceUri}: multiple material modes require an explicit original mode preparation input.");
        byte[] row = draw.MaterialRecord.RawBytes;
        if (row.Length != 64)
            throw new InvalidDataException("Original model material requires its complete 64-byte 10001 record.");
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(0x1C, 4));
        var specular = new Vector4(F32(row, 0x20), F32(row, 0x24), F32(row, 0x28), F32(row, 0x2C));
        G1NamedParameter[] parameters = draw.ParameterBlock.Parameters.Where(parameter => parameter.Name == "nMtrID").ToArray();
        if (parameters.Length > 1)
            throw new InvalidDataException($"Original draw {draw.Draw.Index} nMtrID source is ambiguous.");
        G1NamedParameter? materialId = parameters.SingleOrDefault();
        int? id = null;
        if (materialId is not null)
        {
            if (materialId.TypeCode != 5 || materialId.ElementCount != 1 || materialId.RawValueBytes.Length != 4)
                throw new InvalidDataException($"Original draw {draw.Draw.Index} nMtrID requires its explicit type5 int32 bits.");
            id = BinaryPrimitives.ReadInt32LittleEndian(materialId.RawValueBytes);
        }
        return new(shader, draw, count, flags, specular, row[0x3E], materialId, id);
    }

    /// <summary>9800D0 material-capability gate and 9801D0 specular cache behavior.
    /// A clear source bit can reset or retain depending on the previous flags.</summary>
    public static ModelSpecularPreparation PrepareSpecular(ModelGbufferMaterialSource source,
        uint explicitPreviousCachedFlags)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.MaterialModeCount == 0) return new(false, null, explicitPreviousCachedFlags);
        if (source.MaterialModeCount != 1)
            throw new InvalidDataException("Original model specular requires the supported single material mode.");
        if ((source.RawMaterialFlags & 0x100) != 0)
        {
            Vector4 value = source.SourceSpecular;
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || !float.IsFinite(value.W))
                throw new InvalidDataException("Original model enabled specular source has a non-finite value.");
            return new(true, value, source.RawMaterialFlags);
        }
        bool changed = ((source.RawMaterialFlags ^ explicitPreviousCachedFlags) & 0xFF00) != 0;
        return new(changed, changed ? new Vector4(0, 0, 0, 1) : null, source.RawMaterialFlags);
    }

    /// <summary>95E970's compare4 vATest. Every arithmetic step is float32;
    /// native disabled state is the literal bits FF7FFFFF.</summary>
    public static float PrepareAlphaTest(byte sourceReference, bool explicitBackendEnabled,
        byte? explicitForcedReference = null)
    {
        if (!explicitBackendEnabled) return BitConverter.UInt32BitsToSingle(0xFF7FFFFF);
        byte reference = explicitForcedReference ?? sourceReference;
        float bias = BitConverter.UInt32BitsToSingle(0x3DCCCCCD);
        float scale = BitConverter.UInt32BitsToSingle(0x3B808081);
        return Multiply(Add(reference, bias), scale);
    }

    private static float F32(byte[] source, int offset) => BinaryPrimitives.ReadSingleLittleEndian(source.AsSpan(offset, 4));
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float left, float right) => left + right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left, float right) => left * right;
}
