namespace San14.Data;

/// <summary>The selected light modifier's material plugin inputs. They belong
/// to that light, independently of its directional/ambient color multipliers.
/// A missing publication remains unknown to a live plugin consumer.</summary>
public sealed record HdrMaterialModifierSettings(
    DirectionalHatchingShadowSettings Hatching, float ShadowMaskFade);
