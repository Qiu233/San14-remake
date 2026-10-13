using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The actual GSTK resource payload and its type13 parameter record.
/// This constructs CPU parameter values, not material or device readiness.</summary>
public sealed class ArmyMaterialTextureStageParameters
{
    private ArmyMaterialTextureStageParameters(LoadedRdbAsset source, byte[] declared,
        byte[] payload, byte[] parameters)
        => (Source, DeclaredFileBytes, Payload200, ParameterRecord204) =
            (source, declared, payload, parameters);

    public LoadedRdbAsset Source { get; }
    public ReadOnlyMemory<byte> DeclaredFileBytes { get; }
    public ReadOnlyMemory<byte> Payload200 { get; }
    public ReadOnlyMemory<byte> ParameterRecord204 { get; }
    public ushort EntryCount => BinaryPrimitives.ReadUInt16LittleEndian(Payload200.Span);

    public static ArmyMaterialTextureStageParameters Load(GameResourceRepository repository, uint resourceId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (resourceId == 0) throw new ArgumentOutOfRangeException(nameof(resourceId));
        return Read(repository.LoadRdbAsset("MaterialEditor", resourceId));
    }

    public static ArmyMaterialTextureStageParameters Read(LoadedRdbAsset source)
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] declared = source.Bytes.ToArray();
        if (declared.Length < 8 || !declared.AsSpan(0, 4).SequenceEqual("GSTK"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(declared.AsSpan(4, 4)) != 0)
            throw new NotSupportedException("Material texture-stage parameters require GSTK version zero.");

        byte[] payload = new byte[200];
        declared.AsSpan(8, Math.Min(200, declared.Length - 8)).CopyTo(payload);
        byte[] parameters = new byte[204];
        payload.CopyTo(parameters, 0);

        // The hash consumes the original zero-padded payload, before the
        // aligned record length replaces its ushort at byte six.
        int power = 1, hash = 0;
        foreach (byte value in payload)
        {
            if (value == 0) break;
            power = unchecked(power * 31);
            hash = unchecked(hash + unchecked((sbyte)value) * power);
        }
        BinaryPrimitives.WriteInt32LittleEndian(parameters.AsSpan(200, 4), hash);
        short count = BinaryPrimitives.ReadInt16LittleEndian(payload);
        short extent = unchecked((short)(count * 12 + 8));
        short aligned = unchecked((short)((extent + 7) >> 3));
        BinaryPrimitives.WriteInt16LittleEndian(parameters.AsSpan(6, 2), aligned);
        return new(source, declared, payload, parameters);
    }
}
