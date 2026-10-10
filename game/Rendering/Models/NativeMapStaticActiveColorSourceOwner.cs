using San14.Data;

/// <summary>Actual queue54/55 disabled-blend color projection. The owner
/// retains the selected current Aux and owning-record masks; inactive packet
/// factors are retained without claiming native eight-slot factor history.</summary>
public sealed class NativeMapStaticActiveColorSourceOwner
{
    private readonly LoadedModelFrontendColorSource source;
    private readonly NativeMapStaticQueuePlan plan;
    private readonly int node;
    private readonly ModelAuxiliaryColorMasks masks;
    private readonly Action requireActualCurrentScope;
    private readonly Dictionary<string,NativeMapStaticQueueCandidate> candidates;
    private readonly Dictionary<string,ModelOrdinaryActiveColorState> byRecord=new(StringComparer.Ordinal);

    private NativeMapStaticActiveColorSourceOwner(LoadedModelFrontendColorSource source,
        NativeMapStaticQueuePlan plan,ModelQueueStateSource queue,
        MapCommonRenderState common,Action current)
    {
        this.source=source;this.plan=plan;node=queue.Node.Index;requireActualCurrentScope=current;
        if(node is not(54 or 55)||!ReferenceEquals(common,plan.Admission.Frame.Common)||
            common.FrameNumber!=plan.Context.FrameNumber||common.SelectedViewOwnerIdentity!=plan.Admission.ViewIdentity||
            plan.Context.TerrainFrame.Source.Node.Index!=53||plan.Context.TerrainFrame.ColorViews.Count!=5||
            queue.Graph.Source.DecodedSha256!=plan.Context.TerrainFrame.Source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Static active color entry needs this actual queue54/55 and current World Aux.");
        EntryActiveState=source.PrepareOrdinaryQueueActiveColorEntry(queue,5);
        masks=source.ReadCurrentAuxiliaryMasks(common);
        candidates=plan.SourceCandidates.Where(c=>c.QueueNodeIndex==node)
            .ToDictionary(c=>c.CandidateIdentity,StringComparer.Ordinal);
        RequireCurrent(plan,node);
    }

    /// <summary>The ordinary dispatcher writes this entry even with no
    /// candidates. It says nothing about inactive factor words/unbound slots.</summary>
    public ModelOrdinaryActiveColorState EntryActiveState{get;}
    /// <summary>Every real record mask override restores the bound mask.</summary>
    public ModelOrdinaryActiveColorState ExitActiveState=>EntryActiveState;

    public static NativeMapStaticActiveColorSourceOwner CaptureActualEntry(
        LoadedModelFrontendColorSource source,NativeMapStaticQueuePlan actualPlan,
        ModelQueueStateSource actualQueue,MapCommonRenderState actualCommon,
        Action requireActualCurrentScope)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualPlan);
        ArgumentNullException.ThrowIfNull(actualQueue);ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(requireActualCurrentScope);
        return new(source,actualPlan,actualQueue,actualCommon,requireActualCurrentScope);
    }

    public void RequireCurrent(NativeMapStaticQueuePlan actualPlan,int sourceNode)
    {
        requireActualCurrentScope();
        if(!ReferenceEquals(actualPlan,plan)||sourceNode!=node)
            throw new InvalidDataException("Static active color source belongs to another actual queue/scope.");
    }

    public ModelGbufferInstancePacket PrepareActualDrawPacket(NativeMapStaticQueueCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);RequireCurrent(plan,node);
        if(!candidates.TryGetValue(candidate.CandidateIdentity,out var actual)||!ReferenceEquals(actual,candidate))
            throw new InvalidDataException("Static active color source belongs to another owning-record candidate.");
        if(!byRecord.TryGetValue(candidate.RecordOwnerIdentity,out var color))
        {
            color=source.PrepareLateRecordActiveColor(EntryActiveState,candidate.SourceStencil.DrawTimeState.RecordMask78,masks).DrawState;
            byRecord.Add(candidate.RecordOwnerIdentity,color);
        }
        return candidate.Packet with{Raster=candidate.Packet.Raster with{
            Attachments=ProjectActiveAttachments(candidate.Packet.Raster.Attachments,color)}};
    }

    /// <summary>Changes only fields effective while blending is disabled.
    /// Source/destination factors and operations remain the prepared packet's
    /// inactive device values, not newly observed native frontend fields.</summary>
    public static IReadOnlyList<ModelBlendAttachment> ProjectActiveAttachments(
        IReadOnlyList<ModelBlendAttachment> prepared,ModelOrdinaryActiveColorState actualColor)
    {
        ArgumentNullException.ThrowIfNull(prepared);ArgumentNullException.ThrowIfNull(actualColor);
        if(prepared.Count!=5||actualColor.RgbaWriteMasks.Count!=5)
            throw new InvalidDataException("Static node53 color projection requires exactly five actual bound attachments.");
        return Array.AsReadOnly(prepared.Select((attachment,slot)=>
        {
            byte mask=actualColor.RgbaWriteMasks[slot];
            return attachment with{EnableBlend=false,WriteR=(mask&1)!=0,WriteG=(mask&2)!=0,
                WriteB=(mask&4)!=0,WriteA=(mask&8)!=0};
        }).ToArray());
    }
}
