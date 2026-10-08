using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Explicit stored half4 direction/LOD values and float weights in the
/// original typed-buffer layout. This does not generate or guess native samples.</summary>
public sealed class SkyFilterSamplePacket
{
    public uint DestinationMip { get; }
    public int SampleCount { get; }
    public float WeightSum { get; }
    public ReadOnlyMemory<byte> DirectionBytes { get; }
    public ReadOnlyMemory<byte> WeightBytes { get; }

    public SkyFilterSamplePacket(uint destinationMip, ReadOnlySpan<byte> storedHalf4Directions,
        ReadOnlySpan<byte> storedFloatWeights, float explicitWeightSum)
    {
        if (destinationMip == 0 || storedHalf4Directions.Length == 0 || storedHalf4Directions.Length % 8 != 0 ||
            storedFloatWeights.Length % 4 != 0 || storedHalf4Directions.Length/8 != storedFloatWeights.Length/4 ||
            !float.IsFinite(explicitWeightSum) || explicitWeightSum <= 0)
            throw new InvalidDataException("Sky filter requires one explicit finite mip/sample buffer pair and positive weight sum.");
        for (int i=0;i<storedHalf4Directions.Length;i+=2)
            if ((BinaryPrimitives.ReadUInt16LittleEndian(storedHalf4Directions[i..]) & 0x7C00) == 0x7C00)
                throw new InvalidDataException("Sky filter stored direction/LOD contains a nonfinite half value.");
        for (int i=0;i<storedFloatWeights.Length;i+=4)
            if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(storedFloatWeights[i..])))
                throw new InvalidDataException("Sky filter stored weight contains a nonfinite float value.");
        DestinationMip=destinationMip; SampleCount=storedHalf4Directions.Length/8; WeightSum=explicitWeightSum;
        DirectionBytes=storedHalf4Directions.ToArray(); WeightBytes=storedFloatWeights.ToArray();
    }
}
