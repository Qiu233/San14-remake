using San14.Data;

/// <summary>Current owned49/50 preparation. The complete actual CPU roster,
/// shared full-table resource owner and record admission precede GPU work.
/// Empty rosters follow the same node48/49/50 entry and target APIs.</summary>
public sealed class NativeMapArmyWorldQueuePreparation
{
    private readonly NativeMapArmyPopulationRenderer owner;
    private readonly Action requireActualCurrentWorld;
    internal NativeMapArmyWorldQueuePreparation(NativeMapArmyPopulationRenderer owner,
        NativeMapArmyPreparedFrame army, NativeArmyCurrentAdmission admission,
        ArmyWorldPrimitivePublication population, Action current)
        => (this.owner, Army, Admission, CpuPublication, requireActualCurrentWorld) = (owner, army, admission, population, current);
    public NativeMapArmyPreparedFrame Army { get; }
    public NativeArmyCurrentAdmission Admission { get; }
    public ArmyWorldPrimitivePublication CpuPublication { get; }
    public ArmyWorldQueuePreparedFrame SourceQueues { get; internal set; } = null!;
    public ArmyWorldCurrentTuplePublication Tuples { get; internal set; } = null!;
    internal IArmyWorldQueueOrderSource Ordering { get; set; } = null!;
    public void RequireCurrent()
    {
        owner.RequireCurrentWorldQueues(this); requireActualCurrentWorld();
        Admission.RequireCurrentWorldPublication(Army, CpuPublication);
    }
    public Task<ArmyWorldQueueSubmission> SubmitQueueAsync(int sourceNode,
        Army48TargetBinding actualBoundTarget, IArmyWorldQueueNativeColorSource actualColorSource)
        => owner.SubmitWorldQueueAsync(this, sourceNode, actualBoundTarget, actualColorSource);
    public Task<NativeMapArmyPopulationAcceptedFrame> CompleteAcceptedAsync()
        => owner.CompleteWorldQueuesAsync(this);
}

public sealed partial class NativeMapArmyPopulationRenderer
{
    private readonly ArmyWorldQueueGraphicsOwner _worldQueueGraphics;
    private readonly NativeArmyWorldCpuPublicationOwner _worldCpuPublication = new();
    private NativeMapArmyWorldQueuePreparation? _worldQueuePreparation;
    private NativeMapArmyWorldQueuePreparation? _worldAcceptedPreparation;

    public Task<ArmyWorldPrimitivePublication> PrepareCurrentWorldPrimitivePublicationAsync(
        NativeMapArmyPreparedFrame prepared, MapWorldKind1CurrentQueue actualWorldQueue,
        MapCommonRenderState actualCommon, ArmyCurrentWorldVisibilityView? actualView,
        string currentPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(prepared); Accept();
        return PrepareWorldCpuAcceptedAsync(prepared, actualWorldQueue, actualCommon, actualView, currentPublicationIdentity);
    }
    private async Task<ArmyWorldPrimitivePublication> PrepareWorldCpuAcceptedAsync(
        NativeMapArmyPreparedFrame prepared, MapWorldKind1CurrentQueue queue,
        MapCommonRenderState common, ArmyCurrentWorldVisibilityView? view, string identity)
    {
        await _gate.WaitAsync();
        try
        {
            RequireUnconsumedArmy(prepared);
            if (view is null)
            {
                RequireActualEmptyWorldInventory(prepared);
                return ArmyWorldPrimitivePublication.Prepare(queue, common,
                    Array.Empty<ArmyWorldCurrentPrimitiveActor>(), identity, prepared.Context.PreparedCamera);
            }
            return _worldCpuPublication.PrepareActualBeforeFrame(prepared, queue, common, view, identity);
        }
        finally { _gate.Release(); Complete(); }
    }

    public Task<NativeArmyCurrentAdmission> BindCurrentWorldPrimitiveAdmissionAsync(
        NativeMapArmyPreparedFrame prepared, ArmyWorldPrimitivePublication actualPublication,
        MapSourceCreatedStaticPrimitiveAdmissionPlan actualStaticAdmission)
    {
        ArgumentNullException.ThrowIfNull(prepared); Accept();
        return BindWorldAdmissionAcceptedAsync(prepared, actualPublication, actualStaticAdmission);
    }
    private async Task<NativeArmyCurrentAdmission> BindWorldAdmissionAcceptedAsync(
        NativeMapArmyPreparedFrame prepared, ArmyWorldPrimitivePublication publication,
        MapSourceCreatedStaticPrimitiveAdmissionPlan statics)
    {
        await _gate.WaitAsync();
        try
        {
            RequireUnconsumedArmy(prepared);
            var admission = _primitiveOwner.BindCurrentWorldFrame(prepared.CpuPrimitivePopulation!, publication, statics);
            _nativeAdmissions[prepared.Context] = admission; return admission;
        }
        finally { _gate.Release(); Complete(); }
    }

    public Task<NativeMapArmyWorldQueuePreparation> PrepareWorldQueuesAsync(
        NativeMapArmyPreparedFrame prepared, ArmyWorldPrimitivePublication actualPublication,
        NativeArmyCurrentAdmission actualAdmission, MainMapCameraFrame actualMainCamera,
        MapCommonRenderState actualCommon, uint actualContextFlags3E4,
        NativeWorldShaderTableRegistry actualSharedShaderTables, WorldPrimitiveOrderSource actualViewOrder,
        IReadOnlyList<ModelBlendAttachment> actualSixColorStates,
        IArmyWorldFlagMaterialTablePublication? actualFlagMaterialPublication, Action requireActualCurrentWorld)
    {
        ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(actualPublication);
        ArgumentNullException.ThrowIfNull(actualAdmission); ArgumentNullException.ThrowIfNull(actualSharedShaderTables);
        ArgumentNullException.ThrowIfNull(actualViewOrder); ArgumentNullException.ThrowIfNull(requireActualCurrentWorld);
        Accept(); return PrepareWorldQueuesAcceptedAsync(prepared, actualPublication, actualAdmission, actualMainCamera,
            actualCommon, actualContextFlags3E4, actualSharedShaderTables, actualViewOrder, actualSixColorStates,
            actualFlagMaterialPublication, requireActualCurrentWorld);
    }
    private async Task<NativeMapArmyWorldQueuePreparation> PrepareWorldQueuesAcceptedAsync(
        NativeMapArmyPreparedFrame prepared, ArmyWorldPrimitivePublication publication,
        NativeArmyCurrentAdmission admission, MainMapCameraFrame camera,
        MapCommonRenderState common, uint flags, NativeWorldShaderTableRegistry tables,
        WorldPrimitiveOrderSource viewOrder, IReadOnlyList<ModelBlendAttachment> colors,
        IArmyWorldFlagMaterialTablePublication? materialPublication, Action currentWorld)
    {
        await _gate.WaitAsync();
        try
        {
            RequireUnconsumedArmy(prepared); currentWorld();
            if (!_nativeAdmissions.TryGetValue(prepared.Context, out var bound) || !ReferenceEquals(bound, admission))
                throw new InvalidDataException("Army49/50 must use its actual current World primitive admission.");
            admission.RequireCurrentWorldPublication(prepared, publication);
            ArgumentNullException.ThrowIfNull(colors);
            if (colors.Count == 0) RequireActualEmptyWorldInventory(prepared);
            if (materialPublication is null && (prepared.Flags.Count != 0 || prepared.FlagActors != 0 ||
                publication.Primitives.Any(p=>p.Family=="ArmyFlag")))
                throw new NotSupportedException("Nonempty Army49/50 needs its actual mandatory flag container material publication.");
            _worldAcceptedPreparation = null; LastAcceptedFrame = null;
            var operation = _worldQueuePreparation = new(this, prepared, admission, publication, currentWorld);
            var body = _ordinaryRenderer.PrepareWorldQueuePackets(prepared, colors, tables);
            var flag = await _flagRenderer.PrepareWorldQueuePacketsAsync(prepared, colors, tables, materialPublication);
            var sourceCurrent = ArmyWorldQueueCurrentSourceOwner.CaptureFreshNoExternalAssignments(body, flag, publication, operation.RequireCurrent);
            operation.SourceQueues = _worldQueueGraphics.PrepareActualQueues(body, flag, publication, common, flags, sourceCurrent);
            var tablePublication = ArmyWorldPrimitiveTablePublication.CaptureActualPreparedPackets(tables, publication,
                body, flag, operation.RequireCurrent);
            var orderOwner = new NativeMapArmyWorldOrderOwner(viewOrder);
            operation.Tuples = orderOwner.PrepareCurrentTuples(publication, camera, tablePublication, operation.RequireCurrent);
            operation.Ordering = orderOwner.PrepareCurrentOrder(operation.SourceQueues.Plan, operation.Tuples);
            return operation;
        }
        catch (Exception error)
        {
            _worldQueuePreparation = null; _worldAcceptedPreparation = null; LastAcceptedFrame = null;
            _failedFrames++; LastFailure = error.Message; await _worldQueueGraphics.RetireTargetAsync(); throw;
        }
        finally { _gate.Release(); Complete(); }
    }

    internal Task<ArmyWorldQueueSubmission> SubmitWorldQueueAsync(NativeMapArmyWorldQueuePreparation prepared,
        int node, Army48TargetBinding target, IArmyWorldQueueNativeColorSource colors)
    { ArgumentNullException.ThrowIfNull(prepared); Accept(); return SubmitWorldQueueAcceptedAsync(prepared, node, target, colors); }
    private async Task<ArmyWorldQueueSubmission> SubmitWorldQueueAcceptedAsync(
        NativeMapArmyWorldQueuePreparation prepared, int node, Army48TargetBinding target,
        IArmyWorldQueueNativeColorSource colors)
    {
        await _gate.WaitAsync();
        try { prepared.RequireCurrent(); return await _worldQueueGraphics.SubmitQueueAsync(prepared.SourceQueues, node, target, colors, prepared.Ordering); }
        catch (Exception error)
        {
            _worldQueuePreparation = null; _worldAcceptedPreparation = null; LastAcceptedFrame = null;
            _failedFrames++; LastFailure = error.Message; await _worldQueueGraphics.RetireTargetAsync(); throw;
        }
        finally { _gate.Release(); Complete(); }
    }
    internal Task<NativeMapArmyPopulationAcceptedFrame> CompleteWorldQueuesAsync(NativeMapArmyWorldQueuePreparation prepared)
    { ArgumentNullException.ThrowIfNull(prepared); Accept(); return CompleteWorldQueuesAcceptedAsync(prepared); }
    private async Task<NativeMapArmyPopulationAcceptedFrame> CompleteWorldQueuesAcceptedAsync(NativeMapArmyWorldQueuePreparation prepared)
    {
        await _gate.WaitAsync();
        try
        {
            prepared.RequireCurrent();
            if (ReferenceEquals(_worldAcceptedPreparation, prepared)) throw new InvalidOperationException("This Army49/50 preparation was already accepted.");
            var completed = _worldQueueGraphics.CompleteActualQueues(prepared.SourceQueues).Publication;
            var primitiveFamilies = prepared.CpuPublication.Primitives.ToDictionary(p => p.PacketKey, p => p.Family, StringComparer.Ordinal);
            var actualFlagActors = prepared.Army.Flags.Values.SelectMany(f => f.Actors).ToDictionary(a => a.InstanceKey, StringComparer.Ordinal);
            var submittedCanonical = completed.SubmittedPrimitives.Select(p => p.CanonicalPacketKey).ToHashSet(StringComparer.Ordinal);
            var flagStatuses = prepared.CpuPublication.Primitives.Where(p => p.Family == "ArmyFlag").Select(p =>
            {
                var actor = actualFlagActors[p.ActorIdentity];
                return new RemakeArmyFlagRenderStatus(actor.UnitId, actor.DescriptorOffset, actor.OwnedNodeIdentity,
                    p.SourceDrawIndex, submittedCanonical.Contains(p.PacketKey), submittedCanonical.Contains(p.PacketKey) ?
                        "actual-source-node48/49/50-successful-command-submission" : "actual-current-World-first-bank-excluded;canonical-inventory-retained");
            }).ToArray();
            var actual = new NativeMapArmyPopulationAcceptedFrame(prepared.Army, Array.AsReadOnly(flagStatuses),
                new(_acceptedFrames + 1, _failedFrames, prepared.Army.OrdinaryActors, prepared.Army.FlagActors,
                    completed.SubmittedPrimitives.Count(p => primitiveFamilies[p.CanonicalPacketKey] == "ArmyOrdinary"),
                    completed.SubmittedPrimitives.Count(p => primitiveFamilies[p.CanonicalPacketKey] == "ArmyFlag"),
                    _worldQueueGraphics.ProgramCount, _worldQueueGraphics.GeometryCount, _worldQueueGraphics.DrawCreationCount, 0))
            {
                NativePrimitiveAdmission = prepared.Admission, WorldQueuesCompletedPublication = completed,
                CurrentViewFamilyCandidates = completed.CurrentMode16Candidates
            };
            prepared.Admission.ValidateCurrentAccepted(actual);
            _acceptedFrames++; LastFailure = null; _worldAcceptedPreparation = prepared;
            _prepared.Remove(prepared.Army.Context); _nativeAdmissions.Remove(prepared.Army.Context);
            return LastAcceptedFrame = actual;
        }
        catch (Exception error)
        {
            _worldQueuePreparation = null; _worldAcceptedPreparation = null; LastAcceptedFrame = null;
            _failedFrames++; LastFailure = error.Message; await _worldQueueGraphics.RetireTargetAsync(); throw;
        }
        finally { _gate.Release(); Complete(); }
    }

    private void RequireUnconsumedArmy(NativeMapArmyPreparedFrame prepared)
    {
        if (!_prepared.TryGetValue(prepared.Context, out var current) || !ReferenceEquals(current, prepared) ||
            prepared.Unresolved.Count != 0 || prepared.CpuPrimitivePopulation is null)
            throw new InvalidDataException("Army49/50 requires the exact complete unconsumed BeforeFrame CPU snapshot.");
    }
    private void RequireActualEmptyWorldInventory(NativeMapArmyPreparedFrame prepared)
    {
        if (_lastWorld is not { Units.Count: 0 } || prepared.Ordinary.Count != 0 || prepared.Flags.Count != 0 ||
            prepared.OrdinaryActors != 0 || prepared.FlagActors != 0 || prepared.Unresolved.Count != 0 ||
            prepared.CpuPrimitivePopulation is not { Actors.Count: 0, CompleteSourcePrimitiveCount: 0 })
            throw new NotSupportedException("Absent Army consumer inputs are valid only for this actual complete current zero-unit/actor/primitive inventory.");
    }
    internal void RequireCurrentWorldQueues(NativeMapArmyWorldQueuePreparation prepared)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (!ReferenceEquals(_worldQueuePreparation, prepared))
            throw new InvalidDataException("Army49/50 current preparation was superseded, failed or retired.");
    }
}
