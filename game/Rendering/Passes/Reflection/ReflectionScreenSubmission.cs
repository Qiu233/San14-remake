using San14.Data;

public sealed record ReflectionScreenExecution(DeferredReflectionDecision Decision,
    ReflectionUpstreamSchedule? ActiveSchedule, int FallbackClearSubmissions);

/// <summary>A concrete screen submission over the actual prepared provider
/// order and current target generation. This joins target resolution to the
/// existing compute/final backend; it does not discover an ordinary Scene or
/// pretend an unresolved current Field/target is a successful empty source.</summary>
public sealed class ReflectionScreenSubmission : IAsyncDisposable
{
    public DeferredReflectionDecision Decision { get; }
    public ReflectionScreenTargetOwner? Targets { get; }
    public ReflectionUpstreamOwner? Active { get; }
    private readonly SemaphoreSlim gate = new(1, 1);
    private Task? disposal;
    private bool submitted;
    private ReflectionScreenSubmission(DeferredReflectionDecision decision,
        ReflectionScreenTargetOwner? targets, ReflectionUpstreamOwner? active)
        => (Decision, Targets, Active) = (decision, targets, active);

    public static async Task<ReflectionScreenSubmission> BuildFromFieldsAsync(
        LoadedReflectionProducerPlan source, Vkd3dShaderCompiler compiler,
        ReflectionUpstreamState actualState, MapCommonRenderState? actualCommon,
        IReadOnlyList<ReflectionUpstreamFieldProbe> actualFields,
        RenderTargetFrame? actualTargetStorage, RenderTargetFrame? actualCurrentFramebuffer,
        DeferredRasterState actualRaster)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(actualState); ArgumentNullException.ThrowIfNull(actualFields);
        ArgumentNullException.ThrowIfNull(actualRaster);
        var sourceGate = actualState.Gate;
        // Source skips occur before target/Common/provider input reads.
        if (!sourceGate.RendererValidFrame || sourceGate.ViewIndex >= 32 || !sourceGate.ViewPresent ||
            (sourceGate.Renderer3E4 & 3) != 1 || !sourceGate.PreparedShaderReady || !sourceGate.View1C80Present)
            return new(DeferredExtraLayerPlan.Reflection(sourceGate), null, null);
        if (sourceGate.ViewIndex != source.ViewId)
            throw new InvalidDataException("reflection.screen.selected_view");
        sourceGate = DeferredExtraLayerPlan.BindCurrentReflectionCommon(sourceGate, actualCommon);
        bool active = sourceGate.ProviderReady && sourceGate.FeatureEnabled;
        var targets = await ReflectionScreenTargetOwner.BuildAsync(source,
            actualTargetStorage ?? throw new InvalidDataException("reflection.screen.current_target_storage"),
            actualCurrentFramebuffer, active);
        try
        {
            // These outcomes follow successful construction of the actual
            // framebuffers above. They are not caller-issued ready receipts.
            sourceGate = sourceGate with { RawSliceDescriptorsPresent = targets.Plan.RawSliceDescriptorsPresent,
                SliceBindingSucceeded = active ? null : Array.AsReadOnly(new[] { true, true, true }) };
            var decision = DeferredExtraLayerPlan.Reflection(sourceGate);
            ReflectionUpstreamOwner? upstream = null;
            if (active)
            {
                var state = actualState with { Gate = sourceGate, IntermediateClearBindSucceeded = true,
                    IndirectScratchClearBindSucceeded = true, ReflectionScratchClearBindSucceeded = true,
                    FinalTargetBindSucceeded = true };
                // Existing backend validates/creates its actual intermediate
                // framebuffers and complete uniforms before an owner returns.
                upstream = await ReflectionUpstreamOwner.BuildFromFieldsAsync(source, compiler,
                    targets.Width, targets.Height, state, actualFields, targets.Attachments, actualRaster);
            }
            return new(decision, targets, upstream);
        }
        catch { await targets.DisposeAsync(); throw; }
    }

    public async Task<ReflectionScreenExecution> ExecuteAsync(RenderTargetFrame? actualTargetStorage,
        RenderTargetFrame? actualCurrentFramebuffer)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            if (submitted) throw new InvalidOperationException("This prepared reflection submission has already been consumed.");
            if (Targets is null) return new(Decision, null, 0);
            Targets.RequireCurrent(actualTargetStorage ?? throw new InvalidDataException("reflection.screen.current_target_storage"),
                actualCurrentFramebuffer);
            // An attempted GPU submission can have partial real writes when
            // it fails. Do not replay that immutable prepared work implicitly.
            submitted = true;
            if (Active is not null) return new(Decision, await Active.ExecuteAsync(), 0);
            return new(Decision, null, await Targets.ClearFallbackAsync());
        }
        finally { gate.Release(); }
    }

    public ValueTask DisposeAsync() => new(disposal ??= RetireAsync());
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try { if (Active is not null) await Active.DisposeAsync(); if (Targets is not null) await Targets.DisposeAsync(); }
        finally { gate.Release(); }
    }
}
