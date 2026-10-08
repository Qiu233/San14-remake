using San14.Core;

namespace San14.Data;

/// <summary>Verified CE runtime focus codes; this does not supply new-game defaults.</summary>
public static class OriginalAreaAdministration
{
    // CAreaData +72 (runtime row +29). Native 279C50 selects localized labels through
    // 30C5A0: 1 -> 2B3, 2 -> 2B4, 3 -> 2B5, 0 -> 53F. Unknown original codes must
    // not inherit the native display fallback and become a falsely known None state.
    public static AreaDevelopmentFocus DecodeFocus(byte rawCode) => rawCode switch
    {
        0 => AreaDevelopmentFocus.None,
        1 => AreaDevelopmentFocus.Commerce,
        2 => AreaDevelopmentFocus.Agriculture,
        3 => AreaDevelopmentFocus.Barracks,
        _ => throw new InvalidDataException($"Unresolved original area development focus code: {rawCode}.")
    };
}
