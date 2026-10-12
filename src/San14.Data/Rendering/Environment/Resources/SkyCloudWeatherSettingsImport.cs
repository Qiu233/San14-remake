using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Validates the selected Camera declarations once at resource import.</summary>
public static class SkyCloudWeatherSettingsImport
{
    public static SkyCloudWeatherSettings Import(MapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var properties = package.Camera.Properties;
        uint Read(int ordinal, uint hash, uint type, uint count, int lane)
        {
            var p = properties.Single(value => value.Index == ordinal);
            if (p.NameHash != hash || p.TypeCode != type || p.ElementCount != count ||
                p.ElementSize != 4 || p.RawValue.Length != count * 4)
                throw new InvalidDataException("Cloud exact Camera loader property changed.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue.AsSpan(lane * 4, 4));
        }
        float Float(int ordinal, uint hash, uint count, int lane)
        {
            float value = BitConverter.UInt32BitsToSingle(Read(ordinal, hash, 8, count, lane));
            if (!float.IsFinite(value)) throw new InvalidDataException("Cloud source field is nonfinite.");
            return value;
        }
        uint Word(int ordinal, uint hash, uint type) => Read(ordinal, hash, type, 1, 0);
        return new()
        {
            BottomAltitude = Float(766, 0x7269D49Bu, 1u, 0),
            Thickness = Float(771, 0x6D91D1B1u, 1u, 0),
            RaySamples = Word(768, 0xAD3CC5E1u, 5u),
            MinimumRaySamples = Word(769, 0x91558673u, 5u),
            CoverageFactor = Float(770, 0x6C473EE9u, 1u, 0),
            DetailAmount = new(Float(767, 0xAA1206D2u, 1u, 0), Float(884, 0x8E2AC764u, 1u, 0)),
            HumidDetailThreshold = Float(922, 0x12B4C4C5u, 1u, 0),
            Erosion = new(Float(772, 0x79FA72D7u, 1u, 0), Float(865, 0x8D15E090u, 1u, 0)),
            Density = new(Float(773, 0x965A17A9u, 1u, 0), Float(883, 0x7A72D83Bu, 1u, 0)),
            Temporal = new(Float(774, 0xA78F7EB7u, 1u, 0), Float(775, 0xC7CB0B89u, 1u, 0), Word(776, 0x5C050D0Bu, 5u), Float(777, 0xBE086657u, 1u, 0)),
            LowCostLevel = Word(778, 0x2FB5219Au, 5u),
            RayExponent = Word(779, 0x0808750Du, 5u),
            Resolution = Word(838, 0x58804691u, 4u),
            SampleStep = Float(780, 0x5205A6FCu, 1u, 0),
            Hg = new(Float(783, 0x90D6FF06u, 1u, 0), Float(784, 0xA5BFC747u, 1u, 0), Float(785, 0xAF0D5A27u, 1u, 0), Float(786, 0xB192FD43u, 1u, 0)),
            Base = new(new(Float(787, 0xB343A1B4u, 3u, 0), Float(787, 0xB343A1B4u, 3u, 1), Float(787, 0xB343A1B4u, 3u, 2)), new(Float(788, 0xEECC5E46u, 3u, 0), Float(788, 0xEECC5E46u, 3u, 1), Float(788, 0xEECC5E46u, 3u, 2)), new(Float(789, 0x3B68CC4Cu, 3u, 0), Float(789, 0x3B68CC4Cu, 3u, 1), Float(789, 0x3B68CC4Cu, 3u, 2))),
            BaseErosion = Float(781, 0x10FCBAA5u, 1u, 0),
            DetailPeriod = new(new(Float(960, 0xAA9691D8u, 3u, 0), Float(960, 0xAA9691D8u, 3u, 1), Float(960, 0xAA9691D8u, 3u, 2)), new(Float(790, 0x8EAF526Au, 3u, 0), Float(790, 0x8EAF526Au, 3u, 1), Float(790, 0x8EAF526Au, 3u, 2)), new(Float(963, 0xE1F08DBAu, 1u, 0), Float(962, 0x8C8CCA68u, 1u, 0))),
            DetailVelocity = new(new(Float(961, 0x207C4640u, 3u, 0), Float(961, 0x207C4640u, 3u, 1), Float(961, 0x207C4640u, 3u, 2)), new(Float(792, 0x049506D2u, 3u, 0), Float(792, 0x049506D2u, 3u, 1), Float(792, 0x049506D2u, 3u, 2)), new(Float(965, 0x4388C286u, 1u, 0), Float(964, 0xEC741B98u, 1u, 0))),
            DetailPosition = new(Float(791, 0x7DC45186u, 3u, 0), Float(791, 0x7DC45186u, 3u, 1), Float(791, 0x7DC45186u, 3u, 2)),
            DetailErosion = Float(782, 0xD54F1225u, 1u, 0),
            Distort = new(new(Float(793, 0xA5912020u, 2u, 0), Float(793, 0xA5912020u, 2u, 1)), new(Float(794, 0xB2C1424Eu, 2u, 0), Float(794, 0xB2C1424Eu, 2u, 1)), new(Float(795, 0x02A8CE88u, 2u, 0), Float(795, 0x02A8CE88u, 2u, 1))),
            DistortAmplitude = new(Float(796, 0x581C699Eu, 3u, 0), Float(796, 0x581C699Eu, 3u, 1), Float(796, 0x581C699Eu, 3u, 2)),
            CirrusAltitude = Float(866, 0x25D30EB7u, 1u, 0),
            CirrusCoverage = new(Float(867, 0x06DA86C3u, 1u, 0), Float(868, 0x1F6079BCu, 1u, 0)),
            CirrusDistortion = new(Float(869, 0xBFEB0FF3u, 1u, 0), Float(870, 0x14618EECu, 1u, 0)),
            CirrusDistortionAuxiliary = Float(871, 0xEBD6415Fu, 1u, 0),
            Cirrus = new(new(Float(845, 0xCE543121u, 2u, 0), Float(845, 0xCE543121u, 2u, 1)), Vector2.Zero, new(Float(846, 0xE1D915B9u, 2u, 0), Float(846, 0xE1D915B9u, 2u, 1))),
            CirrusDistort = new(new(Float(847, 0x0EB2E27Cu, 2u, 0), Float(847, 0x0EB2E27Cu, 2u, 1)), Vector2.Zero, new(Float(848, 0xF85822E4u, 2u, 0), Float(848, 0xF85822E4u, 2u, 1))),
            CirrusDistortAmplitude = new(Float(849, 0x1D34957Au, 2u, 0), Float(849, 0x1D34957Au, 2u, 1)),
            HumidityResponse = new(Float(879, 0x550E67B9u, 1u, 0), Float(880, 0x1474A2B2u, 1u, 0)),
            CirrusHumidityResponse = new(Float(881, 0xE7149CB3u, 1u, 0), Float(882, 0x1475C0DAu, 1u, 0)),
        };
    }
}
