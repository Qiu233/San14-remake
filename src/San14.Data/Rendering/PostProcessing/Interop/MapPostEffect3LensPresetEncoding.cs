using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Portable built-in Lens Split ABI. The pinned parameter owner later
/// relocates the two pointer slots; compatibility padding stays backend zero.</summary>
internal static class MapPostEffect3LensPresetEncoding
{
    internal const int HeaderByteCount = 0x90;
    internal const int LensGroupCount = 4;
    internal const int LensGroupStride = 16;
    internal const int SimpleGhostCount = 16;
    internal const int SimpleGhostStride = 28;
    internal const int LensGroupOffset = HeaderByteCount;
    internal const int SimpleGhostOffset = LensGroupOffset + LensGroupCount * LensGroupStride;
    internal const int PayloadByteCount = SimpleGhostOffset + SimpleGhostCount * SimpleGhostStride;

    internal static byte[] Encode(MapPostEffect3LensPresetSettings settings)
    {
        if (settings.LensGroups.Count != LensGroupCount || settings.SimpleGhosts.Count != SimpleGhostCount)
            throw new NotSupportedException("The built-in lens Split requires four lens groups and sixteen simple ghosts.");

        byte[] bytes = new byte[PayloadByteCount];
        var optics = settings.Optics;
        var flare = settings.Flare;
        // The native written word at 00 and pair at 28/2C are fixed zero.
        // Their purpose is not inferred. The portable pointer slots 60/70 are
        // relocation placeholders, distinct from the ten unwritten padding bytes.
        Put(0x04, optics.ImageDiagonal);
        Put(0x08, optics.ImageHeight);
        PutPair(0x0C, optics.InfiniteFocalLengthRange);
        PutInt(0x14, optics.BladeCount);
        PutPair(0x18, optics.FStopRange);
        PutPair(0x20, optics.FStopShapeRange);
        PutPair(0x30, optics.ExposureDialRange);
        PutPair(0x38, optics.ShutterSpeedRange);
        Put(0x40, optics.FocusingPower);
        Put(0x44, optics.ExposureValueRange);
        Put(0x48, optics.MaximumImageFieldOfViewScale);
        Put(0x4C, optics.InfiniteImageCircleRate);
        Put(0x50, optics.InfiniteVignettingStartRate);
        bytes[0x54] = flare.Suppressed ? (byte)1 : (byte)0;
        PutInt(0x58, settings.LensGroups.Count);
        PutInt(0x68, flare.FocusMovingGroup);
        PutInt(0x6C, settings.SimpleGhosts.Count);
        PutInt(0x78, flare.HaloReferenceGhostIndex);
        PutPair(0x7C, flare.LensAberration);
        PutPair(0x84, optics.AdaptedTime);
        bytes[0x8C] = flare.ForceExposureValueRangeToFloat16Precision ? (byte)1 : (byte)0;

        for (int i = 0; i < settings.LensGroups.Count; i++)
        {
            var group = settings.LensGroups[i];
            int at = LensGroupOffset + i * LensGroupStride;
            Put(at, group.WideSpaceInitial);
            Put(at + 4, group.TeleSpaceInitial);
            Put(at + 8, group.DistanceFormulaA);
            Put(at + 12, group.DistanceFormulaB);
        }
        for (int i = 0; i < settings.SimpleGhosts.Count; i++)
        {
            var ghost = settings.SimpleGhosts[i];
            int at = SimpleGhostOffset + i * SimpleGhostStride;
            PutInt(at, ghost.GroupIndex);
            Put(at + 4, ghost.PositionScale);
            Put(at + 8, ghost.ExtendOffset);
            Put(at + 12, ghost.ReflectionRate);
            Put(at + 16, ghost.CoatingColor.X);
            Put(at + 20, ghost.CoatingColor.Y);
            Put(at + 24, ghost.CoatingColor.Z);
        }
        return bytes;

        void Put(int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at, 4), value);
        void PutInt(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at, 4), value);
        void PutPair(int at, Vector2 value) { Put(at, value.X); Put(at + 4, value.Y); }
    }
}
