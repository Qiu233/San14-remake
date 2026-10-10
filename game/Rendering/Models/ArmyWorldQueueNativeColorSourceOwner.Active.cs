using San14.Data;

public sealed partial class ArmyWorldQueueNativeColorSourceOwner
{
    /// <summary>Capture the fields effective in ordinary disabled blending
    /// from the actual current queue/Aux and six-slot target binding. No
    /// native inactive factors or unbound-slot history are reconstructed.</summary>
    public static ArmyWorldQueueActiveColorSourceOwner CaptureActualActiveEntry(
        LoadedModelFrontendColorSource source,ArmyWorldQueuePrimitivePlan actualPlan,
        ModelQueueStateSource actualQueue,MapCommonRenderState actualCommon,
        int actualBoundColorCount,Action requireActualCurrentScope)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualPlan);
        ArgumentNullException.ThrowIfNull(actualQueue);ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(requireActualCurrentScope);
        return new(source,actualPlan,actualQueue,actualCommon,actualBoundColorCount,requireActualCurrentScope);
    }
}

/// <summary>Ordinary Army49/50 effective color state, independent of inactive
/// native factor history. Actual prepared packet fields supply the inactive
/// device factors; the Army48 adapter separately handles PS output5 absence.</summary>
public sealed class ArmyWorldQueueActiveColorSourceOwner : IArmyWorldQueueNativeColorSource
{
    private readonly LoadedModelFrontendColorSource source;
    private readonly ArmyWorldQueuePrimitivePlan plan;
    private readonly int node;
    private readonly ModelAuxiliaryColorMasks masks;
    private readonly Action requireActualCurrentScope;
    private readonly Dictionary<string,ArmyWorldQueuePrimitiveCandidate> candidates;
    private readonly Dictionary<string,ModelOrdinaryActiveColorState> byRecord=new(StringComparer.Ordinal);

    internal ArmyWorldQueueActiveColorSourceOwner(LoadedModelFrontendColorSource source,
        ArmyWorldQueuePrimitivePlan plan,ModelQueueStateSource queue,
        MapCommonRenderState common,int actualBoundColorCount,Action current)
    {
        this.source=source;this.plan=plan;node=queue.Node.Index;requireActualCurrentScope=current;
        if(node is not(49 or 50)||actualBoundColorCount!=6||
            common.FrameNumber!=plan.Publication.Counts.Queue.FrameNumber||
            common.SelectedViewOwnerIdentity!=plan.Publication.Counts.Queue.Binding.ViewIdentity||
            queue.Graph.Source.DecodedSha256!=ArmyWorldGraphicsTargetSource.GraphSha256)
            throw new InvalidDataException("Army active color entry needs its actual queue49/50, current World Aux and six bound colors.");
        EntryActiveState=source.PrepareOrdinaryQueueActiveColorEntry(queue,actualBoundColorCount);
        masks=source.ReadCurrentAuxiliaryMasks(common);
        candidates=plan.SourceCandidates.Where(c=>c.QueueNodeIndex==node)
            .ToDictionary(c=>c.CandidateIdentity,StringComparer.Ordinal);
        RequireCurrent(plan,node);
    }

    /// <summary>The actual ordinary entry also writes these six effective
    /// fields when the queue has no primitive. No factor state is published.</summary>
    public ModelOrdinaryActiveColorState EntryActiveState{get;}
    /// <summary>A real per-record mask override restores this bound mask.</summary>
    public ModelOrdinaryActiveColorState ExitActiveState=>EntryActiveState;

    public void RequireCurrent(ArmyWorldQueuePrimitivePlan actualPlan,int sourceNode)
    {
        requireActualCurrentScope();
        if(!ReferenceEquals(actualPlan,plan)||sourceNode!=node)
            throw new InvalidDataException("Army active color source belongs to another current queue/scope.");
    }

    public ModelOrdinaryActiveColorState ReadActualActiveDrawTimeState(
        ArmyWorldQueuePrimitiveCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);RequireCurrent(plan,node);
        if(!candidates.TryGetValue(candidate.CandidateIdentity,out var actual)||!ReferenceEquals(actual,candidate))
            throw new InvalidDataException("Army active color source belongs to another owning-record candidate.");
        if(byRecord.TryGetValue(candidate.RecordOwnerIdentity,out var color))return color;
        color=source.PrepareLateRecordActiveColor(EntryActiveState,candidate.RecordMask78,masks).DrawState;
        byRecord.Add(candidate.RecordOwnerIdentity,color);return color;
    }

    /// <summary>Legacy full-state callers have no prepared packet here. The
    /// active subset requires the two-argument draw-time packet adapter.</summary>
    public IReadOnlyList<ModelBlendAttachment> ReadActualDrawTimeStates(
        ArmyWorldQueuePrimitiveCandidate candidate)
        =>throw new NotSupportedException("Army active color projection needs this candidate's actual prepared packet attachments.");

    public IReadOnlyList<ModelBlendAttachment> ReadActualDrawTimeStates(
        ArmyWorldQueuePrimitiveCandidate candidate,IReadOnlyList<ModelBlendAttachment> actualPreparedAttachments)
        =>ProjectActiveAttachments(actualPreparedAttachments,ReadActualActiveDrawTimeState(candidate));

    /// <summary>Only disable blending and project real six-slot masks.
    /// Preserve every supplied packet factor/op field without calling a
    /// native factor table adapter or claiming complete frontend history.</summary>
    public static IReadOnlyList<ModelBlendAttachment> ProjectActiveAttachments(
        IReadOnlyList<ModelBlendAttachment> actualPreparedAttachments,ModelOrdinaryActiveColorState actualColor)
    {
        ArgumentNullException.ThrowIfNull(actualPreparedAttachments);ArgumentNullException.ThrowIfNull(actualColor);
        if(actualPreparedAttachments.Count!=6||actualColor.RgbaWriteMasks.Count!=6)
            throw new InvalidDataException("Army node48 active projection requires exactly six prepared attachments and actual bound masks.");
        return Array.AsReadOnly(actualPreparedAttachments.Select((attachment,slot)=>
        {
            byte mask=actualColor.RgbaWriteMasks[slot];
            return attachment with{EnableBlend=false,WriteR=(mask&1)!=0,WriteG=(mask&2)!=0,
                WriteB=(mask&4)!=0,WriteA=(mask&8)!=0};
        }).ToArray());
    }
}
