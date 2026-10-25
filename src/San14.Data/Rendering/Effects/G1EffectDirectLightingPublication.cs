using System.Buffers.Binary;

namespace San14.Data;

public static class G1EffectDirectLightingPublication
{
    /// <summary>Normal CLC resource programs plus same-frame grid and consumer
    /// values. Point counts are replaced by the native queue's actual population.</summary>
    public static byte[] EncodeCluster(G1EffectDirectLightingClusterPrepared value, uint indexFormat)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.IsReady || value.ConsumerInputs is not { } consumer)
            throw new InvalidOperationException("Current CLC preparation is incomplete.");
        using var stream = new MemoryStream(); stream.SetLength(128);
        void U(int at, uint number) => BinaryPrimitives.WriteUInt32LittleEndian(stream.GetBuffer().AsSpan(at), number);
        void F(int at, float number) => BinaryPrimitives.WriteSingleLittleEndian(stream.GetBuffer().AsSpan(at), number);
        void Append(int at, ReadOnlySpan<byte> bytes)
        {
            int offset = checked((int)((stream.Length + 15) & ~15L));
            stream.SetLength(checked(offset + bytes.Length)); bytes.CopyTo(stream.GetBuffer().AsSpan(offset));
            U(at, checked((uint)offset)); U(at + 4, checked((uint)bytes.Length));
        }
        U(0, 0x31434C43); U(4, 1);
        Append(8, value.Reset.Bytecode.Span); Append(16, value.Cull.Bytecode.Span);
        Append(24, value.ResetConstants.Span); Append(32, value.CullConstants.Span);
        U(40, value.ResetGroups.X); U(44, value.ResetGroups.Y); U(48, value.ResetGroups.Z);
        U(52, value.CullGroups.X); U(56, value.CullGroups.Y); U(60, value.CullGroups.Z);
        U(64, value.HeaderScalarCount); U(68, indexFormat);
        U(72, checked((uint)value.Reset.ProgramIndex)); U(76, checked((uint)value.Cull.ProgramIndex));
        F(80, consumer.DepthParameters.X); F(84, consumer.DepthParameters.Y);
        F(88, consumer.DepthParameters.Z); F(92, consumer.DepthParameters.W);
        U(96, checked((uint)consumer.HorizontalPatchCount)); U(100, checked((uint)consumer.PatchCount));
        U(104, consumer.Scaling.HasValue ? 1u : 0u); F(108, consumer.Scaling ?? 0);
        U(112, consumer.FadeDistance.HasValue ? 1u : 0u);
        F(116, consumer.FadeDistance?.X ?? 0); F(120, consumer.FadeDistance?.Y ?? 0);
        return stream.ToArray();
    }
}

public interface IG1EffectDirectLightingDevice
{
    void SetLightingCluster(G1EffectDirectLightingClusterPrepared frame, uint indexFormat);
}
