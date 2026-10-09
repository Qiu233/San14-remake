using System.Buffers.Binary;

namespace San14.Data;

public sealed record MapSourceCreatedCameraLightDataPacket(byte[] Bytes, bool ContextApplied);

/// <summary>CBC770's actual Game default HDRI mode2 path, when the prepared
/// Camera comes from the resolved absent-table producer. That producer writes
/// Camera3B8=1, so the conditional ambient scale/reclassification tail skips.</summary>
public static class MapSourceCreatedCameraLightData
{
    public static MapSourceCreatedCameraLightDataPacket ApplyPreparedWithoutTransmittanceTable(
        ReadOnlySpan<byte> explicitCompleteLightData, ReadOnlySpan<byte> explicitPreparedCameraPrefix,
        uint explicitContextFlags3E4)
        => ApplySourceMode2PreparedCamera(explicitCompleteLightData,explicitPreparedCameraPrefix,explicitContextFlags3E4);

    /// <summary>Actual CBC770 mode2 + prepared Camera3B8=1 continuation.
    /// Both the source absent-table and bounded owned present-season RGB
    /// producers write3B8=1. Mode2 skips all RGB/quaternion/Fog copies; only
    /// F1E=2 is written and the ambient scaling/reclassification tail skips.</summary>
    public static MapSourceCreatedCameraLightDataPacket ApplySourceMode2PreparedCamera(
        ReadOnlySpan<byte> explicitCompleteLightData, ReadOnlySpan<byte> explicitPreparedCameraPrefix,
        uint explicitContextFlags3E4)
    {
        if (explicitCompleteLightData.Length != CameraDirectionalLightData.LightDataBytes)
            throw new InvalidDataException("Source-created Camera lighting requires the complete explicit10C0 LightData.");
        if ((explicitContextFlags3E4 & 3) != 1)
            return new(explicitCompleteLightData.ToArray(), false);
        if (explicitPreparedCameraPrefix.Length != PreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Source-created Camera lighting requires the complete prepared3C0 Camera prefix.");
        if (explicitCompleteLightData[0x10B0] != 2)
            throw new NotSupportedException("The constructed Game default HDRI path requires its unchanged source lighting mode2.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(explicitPreparedCameraPrefix.Slice(0x3B8, 4)) != 0x3F800000)
            throw new NotSupportedException("Source-created mode2 requires the actual prepared Camera3B8=1; the ambient scaling/reclassification tail needs its separate producer.");
        // CBC82B writes this byte before the mode6 compare. Mode2 then jumps
        // directly to CBCB8E, skipping quaternion/RGB/Fog copies. Its3B8=1
        // comparison skips the two ambient products and CBA530 callback.
        byte[] result = explicitCompleteLightData.ToArray();
        result[0xF1E] = 2;
        return new(result, true);
    }
}
