using San14.Data;

/// <summary>Join a completed geometry snapshot to the later shader selection
/// on its same counted View. The original geometry admission stays immutable.</summary>
public static class OriginalWorldGeometryShadingPhase
{
    public static void RequireSameGeometrySnapshot(OriginalMapSourceCreatedF103CurrentFrame geometry,
        OriginalMapSourceCreatedF103CurrentFrame shading)
    {
        ArgumentNullException.ThrowIfNull(geometry); ArgumentNullException.ThrowIfNull(shading);
        if (ReferenceEquals(geometry, shading)) return;
        if (geometry.CpuPopulation is null || !ReferenceEquals(geometry.CpuPopulation, shading.CpuPopulation) ||
            !ReferenceEquals(geometry.ViewFields, shading.ViewFields) || !ReferenceEquals(geometry.Common, shading.Common) ||
            geometry.FrameNumber != shading.FrameNumber || geometry.Lighting.Binding != shading.Lighting.Binding ||
            geometry.Lighting.SourceIdentity != shading.Lighting.SourceIdentity ||
            geometry.Camera.SourceWorldToView != shading.Camera.SourceWorldToView ||
            geometry.Camera.SourceWorldToProjection != shading.Camera.SourceWorldToProjection ||
            !ReferenceEquals(geometry.Context.Source, shading.Context.Source) ||
            geometry.Context.Phase != OriginalMainMapContextCurrentPhase.DeferredPreparationReset ||
            shading.Context.Phase != OriginalMainMapContextCurrentPhase.NativeSpecializedGeometryFlagWriter ||
            shading.Context.ContextFlags3F0 != (geometry.Context.ContextFlags3F0 | 0x40u) ||
            shading.Context.ContextFlags3F4 != geometry.Context.ContextFlags3F4)
            throw new InvalidDataException("Shading does not follow the actual specialized geometry writer on this same geometry/View/Host snapshot.");
    }
}
