using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import the fields needed by startup consumers once per camera resource generation.</summary>
internal static class CameraStartupImport
{
    private static readonly ConditionalWeakTable<LoadedMapCameraConstructionSource, CameraStartupSettings> imported = new();
    internal static CameraStartupSettings Load(LoadedMapCameraConstructionSource source)
        => imported.GetValue(source, static value => Read(value));
    private static CameraStartupSettings Read(LoadedMapCameraConstructionSource source)
    {
        var p = new CameraNumericPropertyReader(source.Camera);
        return new(new ShadowStartupSettings(
            p.Scalar(21, 0xEACC4096u, 1, 0),
            p.Scalar(22, 0x62409B6Fu, 1, 0),
            p.Scalar(23, 0xDE2CE52Eu, 1, 0),
            p.Scalar(238, 0x7E026E9Cu, 1, 0),
            p.Scalar(239, 0xD7DDD8DDu, 1, 0),
            p.Word(24, 0x0611DBC4u, 4, 1, 0),
            p.Word(1061, 0x2CEF0805u, 4, 1, 0),
            p.Scalar(25, 0x30647C5Cu, 1, 0),
            p.Scalar(352, 0xEEC93539u, 1, 0),
            p.Scalar(354, 0x48A49F7Au, 1, 0),
            p.Scalar(356, 0xA28009BBu, 1, 0),
            p.Scalar(129, 0x0D8D3389u, 1, 0),
            p.Scalar(743, 0x9932C840u, 1, 0),
            p.Word(229, 0xA3FB1464u, 5, 1, 0),
            p.Word(26, 0x96E32C4Au, 4, 1, 0),
            p.Word(358, 0xEE798D42u, 4, 1, 0),
            p.Word(578, 0x7D733C4Eu, 4, 1, 0),
            p.Word(975, 0x3E757721u, 4, 1, 0)),
            new VegetationWindSettings(
            p.Scalar(119, 0xB5CA685Eu, 4, 0),
            p.Scalar(119, 0xB5CA685Eu, 4, 1),
            p.Scalar(119, 0xB5CA685Eu, 4, 2),
            p.Scalar(119, 0xB5CA685Eu, 4, 3),
            p.Scalar(120, 0x105016AAu, 1, 0),
            p.Scalar(121, 0x640F1927u, 1, 0),
            p.Scalar(122, 0xA28C4EA0u, 1, 0),
            p.Scalar(123, 0x8781E6DDu, 1, 0),
            p.Scalar(124, 0x66B95B5Bu, 1, 0),
            p.Scalar(125, 0x8510B38Fu, 1, 0),
            p.Scalar(126, 0x98BA0001u, 1, 0),
            p.Scalar(127, 0xDB67A6E4u, 1, 0),
            p.Scalar(731, 0x149DBBA2u, 1, 0),
            p.Scalar(732, 0x349E2198u, 1, 0),
            p.Scalar(872, 0x74886A4Cu, 1, 0),
            p.Scalar(986, 0x251D99AAu, 1, 0),
            p.Scalar(1084, 0xE257C8DCu, 1, 0)),
            p.Scalar(76, 0xAE5641C9u, 1, 0), 0);
    }
}
