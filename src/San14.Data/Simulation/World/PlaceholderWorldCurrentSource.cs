using San14.Core;

namespace San14.Data;

/// <summary>The ordinary development setters' actual table-zero receiver
/// and the unaffiliated Force's administration flags. These current values
/// are saved separately from playable map Areas and Forces.
/// A missing old-save owner remains unknown; no opening data is substituted.</summary>
internal static class PlaceholderWorldCurrentSource
{
    internal static byte? ReadForceAdministrationFlags(GameState state, int force) => force == 0
        ? state.PlaceholderForceAdministrationFlags
        : state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags193;

    internal static GameState WritePlaceholderForceAdministrationFlags(GameState state, byte flags) =>
        state with { PlaceholderForceAdministrationFlags = flags };

    internal static ushort? ReadProgress(GameState state, int area, CeAreaDevelopmentField field)
    {
        if (area != 0)
        {
            var values = state.Areas.GetValueOrDefault(area)?.Development;
            int? number = field switch
            {
                CeAreaDevelopmentField.Commerce => values?.Commerce,
                CeAreaDevelopmentField.Agriculture => values?.Agriculture,
                CeAreaDevelopmentField.Barracks => values?.Barracks,
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            };
            return number is >= 0 and <= 65535 ? (ushort)number.Value : null;
        }
        var placeholder = state.PlaceholderAreaDevelopment;
        return field switch
        {
            CeAreaDevelopmentField.Commerce => placeholder?.Commerce,
            CeAreaDevelopmentField.Agriculture => placeholder?.Agriculture,
            CeAreaDevelopmentField.Barracks => placeholder?.Barracks,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }

    internal static GameState WritePlaceholderProgress(GameState state, CeAreaDevelopmentField field,
        ushort value)
    {
        var previous = state.PlaceholderAreaDevelopment
            ?? throw new InvalidDataException("area-development.placeholder_current_words");
        var after = field switch
        {
            CeAreaDevelopmentField.Commerce => previous with { Commerce = value },
            CeAreaDevelopmentField.Agriculture => previous with { Agriculture = value },
            CeAreaDevelopmentField.Barracks => previous with { Barracks = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        //The ordinary setter owns clamping and level-change effects. This
        //boundary stores only the word that setter has actually produced.
        return state with { PlaceholderAreaDevelopment = after };
    }
}
