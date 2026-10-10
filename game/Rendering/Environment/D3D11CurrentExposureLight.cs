using System.Numerics;

/// <summary>Selected View exposure values. The import and native record layout
/// are handled by the exposure codec.</summary>
public readonly record struct D3D11CurrentExposureLight(
    uint AuxiliarySelector, uint ObjectId, bool Present, byte Type, byte Flags,
    Vector3 BaselineRgb)
{
    internal static D3D11CurrentExposureLight Capture(uint auxiliarySelector,
        IReadOnlyList<D3D11CurrentSeasonLight> lights)
    {
        if (auxiliarySelector == 0) return new(auxiliarySelector, 0, false, 0, 0, default);
        uint mask = 1u << (int)((auxiliarySelector - 1) & 31);
        var selected = lights.FirstOrDefault(light => (light.GroupMask & ~mask) == 0);
        if (selected is null) return new(auxiliarySelector, 0, false, 0, 0, default);
        var exposure = selected.Exposure;
        return new(auxiliarySelector, selected.ObjectId, true, exposure.Type, exposure.Flags, exposure.BaselineRgb);
    }
}
