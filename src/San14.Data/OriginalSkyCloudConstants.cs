using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalSkyCloudConstantPacket(byte[] VertexBytes, byte[] PixelBytes,
    bool CloudIntegrationEnabled, uint MaximumRaySamples);

/// <summary>Retains two complete explicit native pass5 packets. Source values
/// are supplied by the caller; only consumed float words are checked, retaining
/// float3/tail padding unchanged. Work limits reject unsafe input rather than
/// changing the original shader's counts or constants.</summary>
public static class OriginalSkyCloudConstants
{
    public static OriginalSkyCloudConstantPacket Read(DxbcResourceDefinitions vertex,
        DxbcResourceDefinitions pixel, ReadOnlySpan<byte> vertexBytes,
        ReadOnlySpan<byte> pixelBytes, uint explicitMaximumRaySamples)
    {
        Validate(vertex, vertexBytes, 160, ["ksgClipPlane", "g_mtP2W", "g_vCameraDirVectors", "g_vProjFarNear"]);
        Validate(pixel, pixelBytes, 192, ["vEye", "g_vBaseTexOffset", "g_vBaseTexScale",
            "g_vDetailTexOffset", "g_vDetailTexScale", "g_vDistortTexOffset", "g_vDistortTexScale",
            "g_vRaySampleNum", "g_vEarthRadius", "g_vCloudParams0", "g_vCloudParams1", "g_vDistortScale", "g_vErodeScale"]);
        return ReadValidated(pixel, vertexBytes, pixelBytes, explicitMaximumRaySamples);
    }

    public static OriginalSkyCloudConstantPacket ReadColor(DxbcResourceDefinitions vertex,
        DxbcResourceDefinitions pixel, ReadOnlySpan<byte> vertexBytes,
        ReadOnlySpan<byte> pixelBytes, uint explicitMaximumRaySamples)
    {
        Validate(vertex, vertexBytes, 160, ["ksgClipPlane", "g_mtP2W", "g_vCameraDirVectors", "g_vProjFarNear"]);
        if(pixel.Buffers.Count==1&&pixel.Buffers[0].ByteSize==512)
            Validate(pixel,pixelBytes,512,["litDir","vEye","fog","Scat","g_vBaseTexOffset","g_vBaseTexScale",
                "g_vDetailTexOffset","g_vDetailTexScale","g_vDistortTexOffset","g_vDistortTexScale",
                "g_vCirrusTexOffset","g_vCirrusTexScale","g_vCirrusDistortTexOffset","g_vCirrusDistortTexScale",
                "g_vRaySampleNum","g_vEarthRadius","g_vCloudParams0","g_vCloudParams1","g_vCloudParams2",
                "g_vDistortScale","g_vCirrusParams","g_vCirrusDistortScale","g_vErodeScale","g_vHgParams0","g_vHgParams1",
                "g_vSky2SunRGB","g_vSky2SunDir","g_vSky2MoonRGB","g_vSky2MoonDir","g_vSky2RayleighExtinction",
                "g_vSky2MieExtinction","g_vStableDim","g_vClampValue"]);
        else Validate(pixel, pixelBytes, 448, ["litDir", "vEye", "fog", "Scat", "g_vBaseTexOffset", "g_vBaseTexScale",
            "g_vDetailTexOffset", "g_vDetailTexScale", "g_vDistortTexOffset", "g_vDistortTexScale",
            "g_vRaySampleNum", "g_vEarthRadius", "g_vCloudParams0", "g_vCloudParams1", "g_vCloudParams2",
            "g_vDistortScale", "g_vErodeScale", "g_vHgParams0", "g_vHgParams1", "g_vSky2SunRGB", "g_vSky2SunDir",
            "g_vSky2MoonRGB", "g_vSky2MoonDir", "g_vSky2RayleighExtinction", "g_vSky2MieExtinction", "g_vClampValue"]);
        return ReadValidated(pixel, vertexBytes, pixelBytes, explicitMaximumRaySamples);
    }

    private static OriginalSkyCloudConstantPacket ReadValidated(DxbcResourceDefinitions pixel,
        ReadOnlySpan<byte> vertexBytes, ReadOnlySpan<byte> pixelBytes, uint explicitMaximumRaySamples)
    {
        if (explicitMaximumRaySamples is < 1 or > 4096)
            throw new InvalidDataException("Cloud draw requires an explicit finite shader work budget.");
        byte[] retainedPixel = pixelBytes.ToArray();
        float Read(string name, int word)
        {
            var field = pixel.Buffers[0].Variables.Single(v => v.Name.Text == name);
            return BinaryPrimitives.ReadSingleLittleEndian(retainedPixel.AsSpan(checked((int)field.StartOffset + word * 4), 4));
        }
        bool enabled = Read("g_vCloudParams1", 0) < 1 && Read("g_vCloudParams1", 3) > 0;
        if (enabled)
        {
            float x = Read("g_vRaySampleNum", 0), y = Read("g_vRaySampleNum", 1), w = Read("g_vRaySampleNum", 3);
            // PS47: (abs(unitRay.y)*x+y)*1.31033/w, min(y), ceil/ftou.
            // Its current source x=minSamples-maxSamples is negative. Both
            // affine endpoints must stay positive;192/448 keep the old gate.
            bool applied512 = pixel.Buffers[0].ByteSize == 512;
            if ((!applied512 && x < 0) || (applied512 && (double)y + Math.Min(0d, x) <= 0) ||
                y <= 0 || w <= 0 || Math.Ceiling((double)y) > explicitMaximumRaySamples)
                throw new InvalidDataException("Cloud integration counts exceed the explicit work limit or supported finite domain.");
        }
        return new(vertexBytes.ToArray(), retainedPixel, enabled, explicitMaximumRaySamples);
    }

    private static void Validate(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> bytes,
        int size, string[] names)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (bytes.Length != size || definitions.Buffers.Count != 1 ||
            definitions.Buffers[0].Name.Text != "$Globals" || definitions.Buffers[0].ByteSize != size ||
            !definitions.Buffers[0].Variables.Select(v => v.Name.Text).SequenceEqual(names))
            throw new InvalidDataException("Original cloud packet differs from the verified complete field inventory.");
        foreach (var variable in definitions.Buffers[0].Variables)
        {
            var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            int words = checked(type.Rows * type.Columns * Math.Max(1, (int)type.Elements));
            if (type.BaseType != 3 || type.Class is not (1 or 2) || type.Members.Count != 0 ||
                variable.ByteSize != words * 4 || variable.StartOffset > bytes.Length - variable.ByteSize)
                throw new InvalidDataException("Unsupported original cloud field type: " + variable.Name.Text);
            for (int word = 0; word < words; word++)
                if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(checked((int)variable.StartOffset + word * 4), 4))))
                    throw new InvalidDataException("Original cloud field is nonfinite: " + variable.Name.Text);
        }
    }
}

