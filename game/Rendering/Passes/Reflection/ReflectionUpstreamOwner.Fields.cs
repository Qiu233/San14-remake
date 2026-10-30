using San14.Data;

/// <summary>Actual prepared-field inputs in the renderer's original provider
/// order, including skipped slots. Source readiness and retained shader flags
/// come from the caller that executed capture/filter work, never allocation.</summary>
public sealed record ReflectionUpstreamFieldProbe(ReflectionUpstreamProbeGate Gate,
    ReflectionCaptureFieldResources? Field, ReflectionComputeProbeGeometry? CurrentGeometry,
    DeferredCameraInputs? Camera, ReflectionComputeTuning? Tuning,
    bool SkyMapMissing, IReadOnlyList<DeferredDeviceBinding> IndirectInputs,
    IReadOnlyList<DeferredDeviceBinding> ReflectionInputs, string PreparedSourceIdentity);

public sealed partial class ReflectionUpstreamOwner
{
    /// <summary>Uses the normal field factory and named parameter producer,
    /// then enters the existing actual dispatch/final owner. Current depth,
    /// normals, samplers and other bindings remain real borrowed inputs. The
    /// final three attachments must still be supplied by their proved owner.</summary>
    public static Task<ReflectionUpstreamOwner> BuildFromFieldsAsync(
        LoadedReflectionProducerPlan source, Vkd3dShaderCompiler compiler,
        uint width, uint height, ReflectionUpstreamState state, MapCommonRenderState? actualCommon,
        IReadOnlyList<ReflectionUpstreamFieldProbe> fields,
        IReadOnlyList<ReflectionFinalAttachment> attachments, DeferredRasterState raster) =>
        BuildFromFieldsAsync(source, compiler, width, height, state with {
            Gate = DeferredExtraLayerPlan.BindCurrentReflectionCommon(state.Gate, actualCommon)
        }, fields, attachments, raster);

    public static Task<ReflectionUpstreamOwner> BuildFromFieldsAsync(
        LoadedReflectionProducerPlan source, Vkd3dShaderCompiler compiler,
        uint width, uint height, ReflectionUpstreamState state,
        IReadOnlyList<ReflectionUpstreamFieldProbe> fields,
        IReadOnlyList<ReflectionFinalAttachment> attachments, DeferredRasterState raster)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fields);
        var ordered = fields.Select(field => field ??
            throw new InvalidDataException("reflection.capture.field.provider")).ToArray();
        // Prepare only actual scheduled consumers. A known skipped slot, an
        // inactive owner, or the different pass4/pass5 loop bound must not
        // demand resources that the source will not read.
        var required = ReflectionUpstreamPlan.Schedule(state, ordered.Select(field => field.Gate).ToArray())
            .Steps.Select(step => (step.PassIndex, step.SourceListIndex)).ToHashSet();
        var providers = new ReflectionUpstreamProbe[ordered.Length];
        for (int index = 0; index < ordered.Length; ++index)
        {
            var field = ordered[index];
            bool indirect = required.Contains((4u, (uint)index));
            bool reflection = required.Contains((5u, (uint)index));
            if (!indirect && !reflection)
            {
                providers[index] = new(field.Gate, null, null);
                continue;
            }
            var resources = field.Field ?? throw new InvalidDataException("reflection.capture.field.storage_owner");
            resources.RequireCurrent();
            if (field.Camera is null || field.Tuning is null)
                throw new InvalidDataException("reflection.capture.field.current_parameters");
            if (string.IsNullOrWhiteSpace(field.PreparedSourceIdentity))
                throw new InvalidDataException("reflection.capture.field.prepared_identity");
            var parameters = new ReflectionComputeParameterInputs(field.Camera,
                field.CurrentGeometry ?? resources.CurrentGeometry(),
                resources.AtlasLayout, new(width, height, (uint)index, field.SkyMapMissing), field.Tuning);
            ReflectionUpstreamDispatchPacket Packet(uint pass, IReadOnlyList<DeferredDeviceBinding> inputs)
            {
                var stage = ReflectionUpstreamPlan.RequireStage(source, pass);
                var bytes = ReflectionComputeParameterSource.Encode(stage, parameters);
                return new(bytes, resources.BindScreenInputs(pass, inputs),
                    (width + 7) / 8, (height + 7) / 8, 1, field.PreparedSourceIdentity);
            }
            providers[index] = new(field.Gate,
                indirect ? Packet(4, field.IndirectInputs) : null,
                reflection ? Packet(5, field.ReflectionInputs) : null);
        }
        return BuildAsync(source, compiler, width, height, state, providers, attachments, raster);
    }
}
