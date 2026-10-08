using San14.Data;

/// <summary>Current owned49/50 preparation. The complete actual CPU roster,
/// shared full-table resource owner and record admission precede GPU work.
/// Empty rosters follow the same node48/49/50 entry and target APIs.</summary>
public sealed class OriginalNativeMapArmyWorldQueuePreparation
{
    private readonly OriginalNativeMapArmyPopulationRenderer owner;
    private readonly Action requireActualCurrentWorld;
    internal OriginalNativeMapArmyWorldQueuePreparation(OriginalNativeMapArmyPopulationRenderer owner,
        OriginalNativeMapArmyPreparedFrame army, OriginalNativeArmyCurrentAdmission admission,
        OriginalArmyWorldPrimitivePublication population, Action current)
        => (this.owner, Army, Admission, CpuPublication, requireActualCurrentWorld) = (owner, army, admission, population, current);
    public OriginalNativeMapArmyPreparedFrame Army { get; }
    public OriginalNativeArmyCurrentAdmission Admission { get; }
    public OriginalArmyWorldPrimitivePublication CpuPublication { get; }
    public OriginalArmyWorldQueuePreparedFrame SourceQueues { get; internal set; } = null!;
    public OriginalArmyWorldCurrentTuplePublication Tuples { get; internal set; } = null!;
    internal IOriginalArmyWorldQueueOrderSource Ordering { get; set; } = null!;
    public void RequireCurrent()
    {
        owner.RequireCurrentWorldQueues(this); requireActualCurrentWorld();
        Admission.RequireCurrentWorldPublication(Army, CpuPublication);
    }
    public Task<OriginalArmyWorldQueueSubmission> SubmitQueueAsync(int sourceNode,
        OriginalArmy48TargetBinding actualBoundTarget, IOriginalArmyWorldQueueNativeColorSource actualColorSource)
        => owner.SubmitWorldQueueAsync(this, sourceNode, actualBoundTarget, actualColorSource);
    public Task<OriginalNativeMapArmyPopulationAcceptedFrame> CompleteAcceptedAsync()
        => owner.CompleteWorldQueuesAsync(this);
}

public sealed partial class OriginalNativeMapArmyPopulationRenderer
{
    private readonly OriginalArmyWorldQueueGraphicsOwner _worldQueueGraphics;
    private readonly OriginalNativeArmyWorldCpuPublicationOwner _worldCpuPublication = new();
    private OriginalNativeMapArmyWorldQueuePreparation? _worldQueuePreparation;
    private OriginalNativeMapArmyWorldQueuePreparation? _worldAcceptedPreparation;

    public Task<OriginalArmyWorldPrimitivePublication> PrepareCurrentWorldPrimitivePublicationAsync(
        OriginalNativeMapArmyPreparedFrame prepared, OriginalMapWorldKind1CurrentQueue actualWorldQueue,
        OriginalMapFreshCommonResourceSnapshot actualCommon, OriginalArmyCurrentWorldVisibilityView? actualView,
        string currentPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(prepared); Accept();
        return PrepareWorldCpuAcceptedAsync(prepared, actualWorldQueue, actualCommon, actualView, currentPublicationIdentity);
    }
    private async Task<OriginalArmyWorldPrimitivePublication> PrepareWorldCpuAcceptedAsync(
        OriginalNativeMapArmyPreparedFrame prepared, OriginalMapWorldKind1CurrentQueue queue,
        OriginalMapFreshCommonResourceSnapshot common, OriginalArmyCurrentWorldVisibilityView? view, string identity)
    {
        await _gate.WaitAsync();
        try
        {
            RequireUnconsumedArmy(prepared);
            if (view is null)
            {
                RequireActualEmptyWorldInventory(prepared);
                return OriginalArmyWorldPrimitivePublication.Prepare(queue, common,
                    Array.Empty<OriginalArmyWorldCurrentPrimitiveActor>(), identity, prepared.Context.PreparedCamera);
            }
            return _worldCpuPublication.PrepareActualBeforeFrame(prepared, queue, common, view, identity);
        }
        finally { _gate.Release(); Complete(); }
    }

    public Task<OriginalNativeArmyCurrentAdmission> BindCurrentWorldPrimitiveAdmissionAsync(
        OriginalNativeMapArmyPreparedFrame prepared, OriginalArmyWorldPrimitivePublication actualPublication,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan actualStaticAdmission)
    {
        ArgumentNullException.ThrowIfNull(prepared); Accept();
        return BindWorldAdmissionAcceptedAsync(prepared, actualPublication, actualStaticAdmission);
    }
    private async Task<OriginalNativeArmyCurrentAdmission> BindWorldAdmissionAcceptedAsync(
        OriginalNativeMapArmyPreparedFrame prepared, OriginalArmyWorldPrimitivePublication publication,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan statics)
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

    public Task<OriginalNativeMapArmyWorldQueuePreparation> PrepareWorldQueuesAsync(
        OriginalNativeMapArmyPreparedFrame prepared, OriginalArmyWorldPrimitivePublication actualPublication,
        OriginalNativeArmyCurrentAdmission actualAdmission, OriginalMainMapCameraFrame actualMainCamera,
        OriginalMapFreshCommonResourceSnapshot actualCommon, uint actualContextFlags3E4,
        OriginalNativeWorldShaderTableRegistry actualSharedShaderTables, OriginalWorldPrimitiveOrderSource actualViewOrder,
        IReadOnlyList<OriginalModelBlendAttachment> actualSixColorStates,
        IOriginalArmyWorldFlagMaterialTablePublication? actualFlagMaterialPublication, Action requireActualCurrentWorld)
    {
        ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(actualPublication);
        ArgumentNullException.ThrowIfNull(actualAdmission); ArgumentNullException.ThrowIfNull(actualSharedShaderTables);
        ArgumentNullException.ThrowIfNull(actualViewOrder); ArgumentNullException.ThrowIfNull(requireActualCurrentWorld);
        Accept(); return PrepareWorldQueuesAcceptedAsync(prepared, actualPublication, actualAdmission, actualMainCamera,
            actualCommon, actualContextFlags3E4, actualSharedShaderTables, actualViewOrder, actualSixColorStates,
            actualFlagMaterialPublication, requireActualCurrentWorld);
    }
    private async Task<OriginalNativeMapArmyWorldQueuePreparation> PrepareWorldQueuesAcceptedAsync(
        OriginalNativeMapArmyPreparedFrame prepared, OriginalArmyWorldPrimitivePublication publication,
        OriginalNativeArmyCurrentAdmission admission, OriginalMainMapCameraFrame camera,
        OriginalMapFreshCommonResourceSnapshot common, uint flags, OriginalNativeWorldShaderTableRegistry tables,
        OriginalWorldPrimitiveOrderSource viewOrder, IReadOnlyList<OriginalModelBlendAttachment> colors,
        IOriginalArmyWorldFlagMaterialTablePublication? materialPublication, Action currentWorld)
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
            var sourceCurrent = OriginalArmyWorldQueueCurrentSourceOwner.CaptureFreshNoExternalAssignments(body, flag, publication, operation.RequireCurrent);
            operation.SourceQueues = _worldQueueGraphics.PrepareActualQueues(body, flag, publication, common, flags, sourceCurrent);
            var tablePublication = OriginalArmyWorldPrimitiveTablePublication.CaptureActualPreparedPackets(tables, publication,
                body, flag, operation.RequireCurrent);
            var orderOwner = new OriginalNativeMapArmyWorldOrderOwner(viewOrder);
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

    internal Task<OriginalArmyWorldQueueSubmission> SubmitWorldQueueAsync(OriginalNativeMapArmyWorldQueuePreparation prepared,
        int node, OriginalArmy48TargetBinding target, IOriginalArmyWorldQueueNativeColorSource colors)
    { ArgumentNullException.ThrowIfNull(prepared); Accept(); return SubmitWorldQueueAcceptedAsync(prepared, node, target, colors); }
    private async Task<OriginalArmyWorldQueueSubmission> SubmitWorldQueueAcceptedAsync(
        OriginalNativeMapArmyWorldQueuePreparation prepared, int node, OriginalArmy48TargetBinding target,
        IOriginalArmyWorldQueueNativeColorSource colors)
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
    internal Task<OriginalNativeMapArmyPopulationAcceptedFrame> CompleteWorldQueuesAsync(OriginalNativeMapArmyWorldQueuePreparation prepared)
    { ArgumentNullException.ThrowIfNull(prepared); Accept(); return CompleteWorldQueuesAcceptedAsync(prepared); }
    private async Task<OriginalNativeMapArmyPopulationAcceptedFrame> CompleteWorldQueuesAcceptedAsync(OriginalNativeMapArmyWorldQueuePreparation prepared)
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
                return new OriginalRemakeArmyFlagRenderStatus(actor.UnitId, actor.DescriptorOffset, actor.OwnedNodeIdentity,
                    p.SourceDrawIndex, submittedCanonical.Contains(p.PacketKey), submittedCanonical.Contains(p.PacketKey) ?
                        "actual-source-node48/49/50-successful-command-submission" : "actual-current-World-first-bank-excluded;canonical-inventory-retained");
            }).ToArray();
            var actual = new OriginalNativeMapArmyPopulationAcceptedFrame(prepared.Army, Array.AsReadOnly(flagStatuses),
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

    private void RequireUnconsumedArmy(OriginalNativeMapArmyPreparedFrame prepared)
    {
        if (!_prepared.TryGetValue(prepared.Context, out var current) || !ReferenceEquals(current, prepared) ||
            prepared.Unresolved.Count != 0 || prepared.CpuPrimitivePopulation is null)
            throw new InvalidDataException("Army49/50 requires the exact complete unconsumed BeforeFrame CPU snapshot.");
    }
    private void RequireActualEmptyWorldInventory(OriginalNativeMapArmyPreparedFrame prepared)
    {
        if (_lastWorld is not { Units.Count: 0 } || prepared.Ordinary.Count != 0 || prepared.Flags.Count != 0 ||
            prepared.OrdinaryActors != 0 || prepared.FlagActors != 0 || prepared.Unresolved.Count != 0 ||
            prepared.CpuPrimitivePopulation is not { Actors.Count: 0, CompleteSourcePrimitiveCount: 0 })
            throw new NotSupportedException("Absent Army consumer inputs are valid only for this actual complete current zero-unit/actor/primitive inventory.");
    }
    internal void RequireCurrentWorldQueues(OriginalNativeMapArmyWorldQueuePreparation prepared)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (!ReferenceEquals(_worldQueuePreparation, prepared))
            throw new InvalidDataException("Army49/50 current preparation was superseded, failed or retired.");
    }
}
