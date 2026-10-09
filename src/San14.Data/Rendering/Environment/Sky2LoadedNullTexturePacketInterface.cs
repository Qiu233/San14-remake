using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Fixed null-texture interface imported with an actual compiled
/// Sky2 program. It stores no current Camera/light values or device readiness.</summary>
public sealed class Sky2LoadedNullTexturePacketInterface
{
    private readonly Sky2SourceNull2D[] proofs;
    private readonly DxbcResourceDefinitions definitions;
    private readonly int packetLength, presenceOffset;
    private readonly int[] presenceLanes;
    private readonly bool finiteMoon;

    internal Sky2LoadedNullTexturePacketInterface(
        IReadOnlyList<Sky2SourceNull2D> proofs, DxbcResourceDefinitions definitions,
        int packetLength, int presenceOffset, int[] presenceLanes, bool finiteMoon)
    {
        this.proofs = proofs.ToArray(); this.definitions = definitions;
        this.packetLength = packetLength; this.presenceOffset = presenceOffset;
        this.presenceLanes = presenceLanes; this.finiteMoon = finiteMoon;
    }

    public void Validate(IReadOnlyList<Sky2SourceNull2D> actualProgramProofs,
        DxbcResourceDefinitions actualDefinitions, ReadOnlySpan<byte> completePixelPacket)
    {
        ArgumentNullException.ThrowIfNull(actualProgramProofs);
        if (!ReferenceEquals(actualDefinitions, definitions) ||
            actualProgramProofs.Count != proofs.Length || completePixelPacket.Length != packetLength)
            throw new InvalidDataException("Current Sky2 packet must use this loaded program interface and complete CB.");
        for (int index = 0; index < proofs.Length; index++)
        {
            if (!ReferenceEquals(actualProgramProofs[index], proofs[index]))
                throw new InvalidDataException("Current Sky2 packet requires the null proofs bound by this program.");
            if (BinaryPrimitives.ReadSingleLittleEndian(completePixelPacket.Slice(
                    checked(presenceOffset + presenceLanes[index] * 4), 4)) != 0)
                throw new InvalidDataException("Current Sky2 packet has a nonzero source-null texture presence flag.");
        }
        if (finiteMoon)
            for (int lane = 0; lane < 4; lane++)
                if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(
                        completePixelPacket.Slice(160 + lane * 4, 4))))
                    throw new InvalidDataException("Finite Moon PS5/11/6/14/15 requires finite current MoonRGB lanes.");
    }
}
