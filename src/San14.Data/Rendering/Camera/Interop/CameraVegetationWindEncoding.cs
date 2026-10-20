namespace San14.Data;

/// <summary>Vegetation fields in the existing native camera interchange.</summary>
internal static class CameraVegetationWindEncoding
{
    internal static IEnumerable<KeyValuePair<int, uint>> WrittenWords(VegetationWindSettings value)
    {
        static KeyValuePair<int, uint> F(int offset, float scalar) => new(offset, BitConverter.SingleToUInt32Bits(scalar));
        yield return F(0xDC0, value.DirectionX);
        yield return F(0xDC4, value.DirectionY);
        yield return F(0xDC8, value.DirectionZ);
        yield return F(0xDCC, value.DirectionW);
        yield return F(0xDD0, value.BaseStrength);
        yield return F(0xDD4, value.StrengthVariation);
        yield return F(0xDD8, value.RiseSeconds);
        yield return F(0xDDC, value.FallSeconds);
        yield return F(0xDE0, value.PeakSeconds);
        yield return F(0xDE4, value.MinimumStrength);
        yield return F(0xDE8, value.RestSeconds);
        yield return F(0xDEC, value.RestVariation);
        yield return F(0xDF4, value.JitterDegrees);
        yield return F(0xDF8, value.JitterFrequency);
        yield return F(0x141C, value.InteractionExtent);
        yield return F(0x1438, value.WindExtent);
        yield return F(0x143C, value.StrengthScale);
    }
}
