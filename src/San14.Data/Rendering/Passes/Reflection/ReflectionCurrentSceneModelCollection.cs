namespace San14.Data;

public sealed record ReflectionCurrentModelCollectionCall(
    DeferredEarlyShadowCurrentModelCandidate Model,
    bool ActualCategory0Reached, bool ActualCategory1Reached);

public sealed record ReflectionCurrentCaptureAppend(ReflectionCurrentViewNode Node,
    int Face, int CounterIndex, int Category, bool TupleSpanAvailable,
    bool CompleteSpanAppended, IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> SharedTuples);

/// <summary>The C394F0 capture tail in actual Model callback order. The same
/// tuple bank must already contain any genuinely allocated Main/shadow spans.
/// Model calls are appended by their real collection owner, never inferred
/// from family grouping or the ordinary camera's visible primitive queue.</summary>
public sealed class ReflectionCurrentSceneModelCollection
{
    private readonly ReflectionCurrentViewWorkOwner view;
    private readonly DeferredEarlyShadowCurrentTupleBank tuples;
    private bool finished;
    private readonly HashSet<DeferredEarlyShadowCurrentModelCandidate> actualCalls=[];
    public ReflectionCurrentSceneModelCollection(ReflectionCurrentViewWorkOwner actualView,
        DeferredEarlyShadowCurrentTupleBank actualSharedTupleBank)
    {
        ArgumentNullException.ThrowIfNull(actualView); ArgumentNullException.ThrowIfNull(actualSharedTupleBank);
        actualView.RequireCurrent();
        if(!actualView.IsSealedForCollection||actualSharedTupleBank.FrameNumber!=actualView.Frame.FrameNumber||
            actualSharedTupleBank.ViewIdentity!=actualView.ViewIdentity||
            !ReferenceEquals(actualSharedTupleBank.Arena,actualView.Frame.Arena))
            throw new InvalidOperationException("Capture Model collection requires the same acquired Frame, View and shared tuple arena after registration.");
        view=actualView;tuples=actualSharedTupleBank;
    }
    public int ActualModelCallCount {get;private set;}

    /// <summary>An actual repeated/aliased World callback is a separate
    /// C394F0 stack invocation. Its category pointers start null again; retain
    /// its same current geometry/bounds but keep its tuple allocation distinct.
    /// Use this identity for Main/shadow and capture of that same invocation.</summary>
    public static DeferredEarlyShadowCurrentModelCandidate CreateDistinctActualCallbackCandidate(
        DeferredEarlyShadowCurrentModelCandidate actualModel)
    {
        ArgumentNullException.ThrowIfNull(actualModel);
        return new(actualModel.ModelIdentity,actualModel.Family,actualModel.CurrentSourceIdentity,
            actualModel.Geometry,actualModel.Bounds,actualModel.PerViewOpacityB0,
            actualModel.WorldCaller,actualModel.CurrentProducerProvenance);
    }

    /// <summary>One reached complete Model callback. No ordinary frustum test,
    /// opacity guess or category concatenation is introduced at this boundary.
    /// D08F80 updates depth even when a later arena/list reservation refuses.</summary>
    public IReadOnlyList<ReflectionCurrentCaptureAppend> AppendActualModel(ReflectionCurrentModelCollectionCall actual)
    {
        ArgumentNullException.ThrowIfNull(actual);ArgumentNullException.ThrowIfNull(actual.Model);
        RequireCollecting();
        if(!actualCalls.Add(actual.Model))
            throw new InvalidOperationException("A repeated native Model callback needs its own current callback candidate and tuple span identity.");
        ActualModelCallCount++;
        var result=new List<ReflectionCurrentCaptureAppend>();
        if(!(actual.ActualCategory0Reached&&actual.Model.Category0.Count!=0)&&
            !(actual.ActualCategory1Reached&&actual.Model.Category1.Count!=0))return result.AsReadOnly();
        foreach(var node in view.WorkInsertionOrder)
        {
            if(node.Capture is not {} capture)continue;
            var visible=capture.TestAndAccumulate(actual.Model.Bounds);
            for(int face=0;face<6;face++)
            {
                if((visible.FaceMask&(1u<<face))==0)continue;
                float distance=visible.Distances[face]??throw new InvalidDataException("An accepted capture face has no current distance.");
                ulong firstKey=DeferredEarlyShadowCasterSource.PrepareFirstAcceptedDistanceKey(distance);
                // Actual node40 is8: face+6 exists only for faces0 and1.
                // Both lists receive both reached categories, in this order.
                Publish(face); if(face+6<node.CaptureCounters.Count)Publish(face+6);
                void Publish(int counter)
                {
                    if(counter>=node.CaptureCounters.Count)return;
                    if(actual.ActualCategory0Reached&&actual.Model.Category0.Count!=0)Category(0);
                    if(actual.ActualCategory1Reached&&actual.Model.Category1.Count!=0)Category(1);
                    void Category(int category)
                    {
                        var span=tuples.GetOrAllocateCurrentCategory(actual.Model,category,firstKey);
                        bool appended=span is not null&&node.CaptureCounters[counter].Append(span);
                        result.Add(new(node,face,counter,category,span is not null,appended,
                            span??Array.Empty<DeferredEarlyShadowCurrentPrimitiveTuple>()));
                    }
                }
            }
        }
        return result.AsReadOnly();
    }

    /// <summary>D799D0/D79920 closes all actual work-node counters after
    /// model callbacks finish. Root-only nodes from a failed tail allocation
    /// remain published but have no queued collection/finalization work.</summary>
    public void FinishActualModelTraversal()
    {
        RequireCollecting();view.SealCaptureCounters();finished=true;
    }

    /// <summary>Reuse the existing D4B7E0 stable radix implementation with the
    /// actual current ViewParam Shadow descriptor and actual tuple field reader.
    /// Insertion order is retained for equal keys and an empty sort chain.</summary>
    public IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> ReadActualSortedCounter(
        ReflectionCurrentViewNode actualNode,int actualCounter,
        WorldPrimitiveOrderSource actualOrder,
        Func<DeferredEarlyShadowCurrentPrimitiveTuple,int,ulong> readActualUnsignedField)
    {
        view.RequireCurrent();ArgumentNullException.ThrowIfNull(actualNode);
        ArgumentNullException.ThrowIfNull(actualOrder);ArgumentNullException.ThrowIfNull(readActualUnsignedField);
        if(!finished||!ReferenceEquals(actualNode.Owner,view)||!view.WorkInsertionOrder.Contains(actualNode)||
            (uint)actualCounter>=(uint)actualNode.CaptureCounters.Count)
            throw new InvalidOperationException("Sorted capture primitives require a completed actual work-node counter.");
        return Array.AsReadOnly(actualOrder.Sort(WorldPrimitiveSortFamily.Shadow,
            actualNode.CaptureCounters[actualCounter].InsertionOrder,readActualUnsignedField));
    }
    private void RequireCollecting()
    {
        view.RequireCurrent();if(finished)throw new InvalidOperationException("Capture Model traversal has already finished.");
    }
}
