using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The selected camera's direct shadow bias publication, before Main caller overrides.</summary>
internal static class CameraShadowBiasImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, CameraRenderQualityPatch> selected = new();
    internal static CameraRenderQualityPatch Direct(MapSeasonCameraPackage package)
        => selected.GetValue(package, static source => Read(source));

    private static CameraRenderQualityPatch Read(MapSeasonCameraPackage source)
    {
        uint Word(uint hash, uint type)
        {
            var field = source.Camera.Properties.SingleOrDefault(value => value.NameHash == hash);
            if (field is null || field.TypeCode != type || field.ElementCount != 1 || field.RawValue.Length != 4)
                throw new InvalidDataException($"Selected camera shadow bias {hash:X8} has an unsupported resource layout.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        float Scalar(uint hash) => BitConverter.UInt32BitsToSingle(Word(hash, 8));
        // All five properties participate in the original selected-camera copy.
        // The package owns these values; the stock Main declaration is only a
        // startup fallback and can have a different cascade bias policy.
        return new()
        {
            FirstShadowDepthBias = Scalar(0x30647C5Cu),
            SecondShadowDepthBias = Scalar(0xEEC93539u),
            ThirdShadowDepthBias = Scalar(0x48A49F7Au),
            FourthShadowDepthBias = Scalar(0xA28009BBu),
            ShadowPerCascadeBias = Word(0xEE798D42u, 4)
        };
    }
}
