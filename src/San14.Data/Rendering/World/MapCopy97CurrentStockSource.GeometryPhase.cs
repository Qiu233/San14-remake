namespace San14.Data;

public sealed partial class MapCopy97CurrentStockSource
{
    /// <summary>The geometry population and shading selector are different
    /// current snapshots. Only the executed specialized57 OR40 transition is
    /// admitted here; bit6 does not change the original F103 selection keys.
    /// This joins CPU sources, never a geometry/GPU completion receipt.</summary>
    internal static void RequireGeometryContextJoin(MapSourceCreatedF103CurrentFrame shading,
        MapSourceCreatedF103CurrentFrame? geometry, TerrainGbufferColorMaskRecipe terrain)
    {
        if(!DeferredEarlyCausticsOwner.IsSupportedCurrentContext(shading.Context))
            throw new NotSupportedException("Copy97 has no source stock proof for this current Q phase/writer.");
        if(shading.Context.ContextFlags3F0==0)
        {
            if(terrain.Current.CurrentContextFlags3F0!=0 || geometry is not null && !ReferenceEquals(geometry,shading))
                throw new InvalidDataException("Q0 stock preparation requires its original terrain/frame snapshot.");
            return;
        }
        if(geometry is null || geometry.Context.ContextFlags3F0!=0 || geometry.Context.ContextFlags3F4!=0 ||
            terrain.Current.CurrentContextFlags3F0!=geometry.Context.ContextFlags3F0 ||
            geometry.FrameNumber!=shading.FrameNumber || !ReferenceEquals(geometry.Context.Source,shading.Context.Source) ||
            geometry.CpuPopulation is null || !ReferenceEquals(geometry.CpuPopulation,shading.CpuPopulation) ||
            !ReferenceEquals(geometry.ViewFields,shading.ViewFields) || !ReferenceEquals(geometry.Common,shading.Common) ||
            geometry.Lighting.Binding!=shading.Lighting.Binding ||
            !geometry.Camera.Prefix.Span.SequenceEqual(shading.Camera.Prefix.Span) ||
            geometry.Camera.CurrentPose!=shading.Camera.CurrentPose ||
            !geometry.Lighting.LocalLightData.Span.SequenceEqual(shading.Lighting.LocalLightData.Span) ||
            !geometry.Feature.EffectiveLocalLightData.Span.SequenceEqual(shading.Feature.EffectiveLocalLightData.Span) ||
            !geometry.Feature.SelectedModifierLightData.Span.SequenceEqual(shading.Feature.SelectedModifierLightData.Span) ||
            !geometry.Selection.CurrentFeature48.Span.SequenceEqual(shading.Selection.CurrentFeature48.Span) ||
            !geometry.Selection.RequestedVariant96.Span.SequenceEqual(shading.Selection.RequestedVariant96.Span))
            throw new InvalidDataException("Post57 stock Q40 requires its exact prior Q0 geometry population, counted View/Common, Camera/light and unchanged native shader keys.");
    }
}
