using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The selected camera's declared Cloud ray exponent and sky-dome
/// light group. Current quality overrides belong to the accepted Main frame.</summary>
public readonly record struct SkyCloudShaderSettings(uint RayExponent, uint SkyDomeLightGroupId)
{
    public static SkyCloudShaderSettings Import(MapSeasonCameraPackage package)
    {
        uint Word(uint nameHash)
        {
            var field = package.Camera.Properties.Single(value => value.NameHash == nameHash);
            if (field.TypeCode != 5 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                throw new InvalidDataException("Cloud shader settings require scalar unsigned camera properties.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        return new(Word(0x0808750D), Word(0x15BCC238));
    }
}
