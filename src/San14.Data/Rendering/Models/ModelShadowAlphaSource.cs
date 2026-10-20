using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The model loader's independent shadow reference. It is not the
/// ordinary material alpha-test reference and is not the shadow constructor seed.</summary>
public static class ModelShadowAlphaSource
{
    public static int ReadLoaderOverride(LoadedRdbAsset source)
    {
        ReadOnlySpan<byte> data = source.LoaderMetadata;
        // IDRK's fixed header is 56 bytes. The repository retains from +48;
        // the first word is opaque, and +52 is the parameter count.
        if (data.IsEmpty) return -1;
        if (data.Length < 8) throw Invalid(source, "truncated loader metadata");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (count > (data.Length - 8) / 12) throw Invalid(source, "invalid loader parameter count");
        int values = 8 + (int)count * 12;
        int result = -1;
        bool found = false;
        for (int index = 0; index < count; index++)
        {
            int descriptor = 8 + index * 12;
            uint type = BinaryPrimitives.ReadUInt32LittleEndian(data[descriptor..]);
            uint elements = BinaryPrimitives.ReadUInt32LittleEndian(data[(descriptor + 4)..]);
            uint name = BinaryPrimitives.ReadUInt32LittleEndian(data[(descriptor + 8)..]);
            // The actual model requests use tightly packed signed/unsigned
            // 32-bit options. Other shapes require their own verified reader.
            if (type is not (4 or 5)) throw Invalid(source, $"unsupported loader parameter type {type}");
            if (elements > (data.Length - values) / 4) throw Invalid(source, "truncated loader parameter value");
            if (name == 0xD3C00659) // ShadowCasterAlphaThreshold, F54870 ordinal 2.
            {
                if (found || type != 4 || elements != 1) throw Invalid(source, "invalid shadow alpha override");
                result = BinaryPrimitives.ReadInt32LittleEndian(data[values..]);
                found = true;
            }
            values += (int)elements * 4;
        }
        if (values != data.Length) throw Invalid(source, "unparsed loader parameter bytes");
        return result;
    }

    /// <summary>F5AB90 installs runtime material +40; D27BC0/D6E810 normalize
    /// it into sbias.w. A negative loader option preserves source material +3E.</summary>
    public static float PrepareReference(byte materialReference, int loaderOverride)
    {
        byte reference = loaderOverride < 0 ? materialReference : (byte)Math.Clamp(loaderOverride, 0, 255);
        return reference / 255f;
    }

    private static InvalidDataException Invalid(LoadedRdbAsset source, string reason) =>
        new($"Model {source.ResourceUri}: {reason}.");
}
