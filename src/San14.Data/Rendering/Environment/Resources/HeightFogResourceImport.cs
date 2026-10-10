using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import the three height-fog layers and mask from a selected camera package.</summary>
public static class HeightFogResourceImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, HeightFogSettings> imported = new();
    public static HeightFogSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static source => Read(source));
    private static HeightFogSettings Read(MapSeasonCameraPackage package)
    {
        if (package.Camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new InvalidDataException("Height fog requires a Perspective Camera resource.");
        var p = new CameraNumericPropertyReader(package.Camera);
        return new(
            new HeightFogLayerSettings(
            p.Word(37, 0x27E55007u, 4, 1, 0),
            p.Word(34, 0x56F94E85u, 4, 1, 0),
            p.Scalar(35, 0x2155E92Au, 1, 0),
            p.Scalar(36, 0xAA3EE8E3u, 1, 0),
            p.Scalar(38, 0xF241EAACu, 1, 0),
            p.Scalar(39, 0x7B2AEA65u, 1, 0),
            new Vector4(p.Scalar(40, 0x1705652Du, 4, 0), p.Scalar(40, 0x1705652Du, 4, 1), p.Scalar(40, 0x1705652Du, 4, 2), p.Scalar(40, 0x1705652Du, 4, 3)),
            p.Scalar(484, 0xF12DD891u, 1, 0),
            new HeightFogWaveSettings(p.Scalar(485, 0xFDAD2B5Eu, 1, 0), p.Scalar(486, 0x24B0FEA6u, 1, 0), p.Scalar(487, 0xCEBD7551u, 1, 0), p.Scalar(488, 0x31BC73E4u, 1, 0), p.Scalar(489, 0xDBC8EA8Fu, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(490, 0x0A4620DFu, 1, 0), p.Scalar(491, 0x3149F427u, 1, 0), p.Scalar(492, 0xDB566AD2u, 1, 0), p.Scalar(493, 0x3E556965u, 1, 0), p.Scalar(494, 0xE861E010u, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(495, 0x16DF1660u, 1, 0), p.Scalar(496, 0x3DE2E9A8u, 1, 0), p.Scalar(497, 0xE7EF6053u, 1, 0), p.Scalar(498, 0x4AEE5EE6u, 1, 0), p.Scalar(499, 0xF4FAD591u, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(500, 0x23780BE1u, 1, 0), p.Scalar(501, 0x4A7BDF29u, 1, 0), p.Scalar(502, 0xF48855D4u, 1, 0), p.Scalar(503, 0x57875467u, 1, 0), p.Scalar(504, 0x0193CB12u, 1, 0))),
            new HeightFogLayerSettings(
            p.Word(41, 0x09772DE6u, 4, 1, 0),
            p.Word(510, 0x6D1FDB25u, 4, 1, 0),
            p.Scalar(511, 0x9A85260Au, 1, 0),
            p.Scalar(512, 0x64D3B703u, 1, 0),
            p.Scalar(42, 0xD3D3C88Bu, 1, 0),
            p.Scalar(43, 0x5CBCC844u, 1, 0),
            new Vector4(p.Scalar(44, 0xF897430Cu, 4, 0), p.Scalar(44, 0xF897430Cu, 4, 1), p.Scalar(44, 0xF897430Cu, 4, 2), p.Scalar(44, 0xF897430Cu, 4, 3)),
            p.Scalar(513, 0xD2BFB670u, 1, 0),
            new HeightFogWaveSettings(p.Scalar(514, 0xDF3F093Du, 1, 0), p.Scalar(515, 0x0642DC85u, 1, 0), p.Scalar(516, 0xB04F5330u, 1, 0), p.Scalar(517, 0x134E51C3u, 1, 0), p.Scalar(518, 0xBD5AC86Eu, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(519, 0xEBD7FEBEu, 1, 0), p.Scalar(520, 0x12DBD206u, 1, 0), p.Scalar(521, 0xBCE848B1u, 1, 0), p.Scalar(522, 0x1FE74744u, 1, 0), p.Scalar(523, 0xC9F3BDEFu, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(524, 0xF870F43Fu, 1, 0), p.Scalar(525, 0x1F74C787u, 1, 0), p.Scalar(526, 0xC9813E32u, 1, 0), p.Scalar(527, 0x2C803CC5u, 1, 0), p.Scalar(528, 0xD68CB370u, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(529, 0x0509E9C0u, 1, 0), p.Scalar(530, 0x2C0DBD08u, 1, 0), p.Scalar(531, 0xD61A33B3u, 1, 0), p.Scalar(532, 0x39193246u, 1, 0), p.Scalar(533, 0xE325A8F1u, 1, 0))),
            new HeightFogLayerSettings(
            p.Word(537, 0xEB090BC5u, 4, 1, 0),
            p.Word(534, 0x4EB1B904u, 4, 1, 0),
            p.Scalar(535, 0x7C1703E9u, 1, 0),
            p.Scalar(536, 0x466594E2u, 1, 0),
            p.Scalar(538, 0xB565A66Au, 1, 0),
            p.Scalar(539, 0x3E4EA623u, 1, 0),
            new Vector4(p.Scalar(540, 0xDA2920EBu, 4, 0), p.Scalar(540, 0xDA2920EBu, 4, 1), p.Scalar(540, 0xDA2920EBu, 4, 2), p.Scalar(540, 0xDA2920EBu, 4, 3)),
            p.Scalar(541, 0xB451944Fu, 1, 0),
            new HeightFogWaveSettings(p.Scalar(542, 0xC0D0E71Cu, 1, 0), p.Scalar(543, 0xE7D4BA64u, 1, 0), p.Scalar(544, 0x91E1310Fu, 1, 0), p.Scalar(545, 0xF4E02FA2u, 1, 0), p.Scalar(546, 0x9EECA64Du, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(547, 0xCD69DC9Du, 1, 0), p.Scalar(548, 0xF46DAFE5u, 1, 0), p.Scalar(549, 0x9E7A2690u, 1, 0), p.Scalar(550, 0x01792523u, 1, 0), p.Scalar(551, 0xAB859BCEu, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(552, 0xDA02D21Eu, 1, 0), p.Scalar(553, 0x0106A566u, 1, 0), p.Scalar(554, 0xAB131C11u, 1, 0), p.Scalar(555, 0x0E121AA4u, 1, 0), p.Scalar(556, 0xB81E914Fu, 1, 0)),
            new HeightFogWaveSettings(p.Scalar(557, 0xE69BC79Fu, 1, 0), p.Scalar(558, 0x0D9F9AE7u, 1, 0), p.Scalar(559, 0xB7AC1192u, 1, 0), p.Scalar(560, 0x1AAB1025u, 1, 0), p.Scalar(561, 0xC4B786D0u, 1, 0))),
            new Vector4(p.Scalar(1106, 0x896A1BAFu, 1, 0), p.Scalar(1107, 0x58ADC86Du, 1, 0), p.Scalar(1108, 0x644867E2u, 1, 0), p.Scalar(1109, 0x7D7A52E4u, 1, 0)));
    }
}
