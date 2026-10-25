using San14.Data;

public partial class MapSceneView
{
    internal bool TryApplyProvinceAttention(IReadOnlyList<CityProvinceAdvantageIndicators> rows, out string? missing)
    {
        missing = "city.attention.map_owner";
        return IsPlayableMapReady && _d3d11Markers is { } markers && markers.ApplyProvinceAttention(rows, out missing);
    }

    internal bool TryApplyMerchantAttention(IReadOnlyList<CityMerchantAttention> rows, Func<bool> enabled,
        out string? missing)
    {
        missing = "city.attention.map_owner";
        return IsPlayableMapReady && _d3d11Markers is { } markers && markers.ApplyMerchantAttention(rows, enabled, out missing);
    }
}
