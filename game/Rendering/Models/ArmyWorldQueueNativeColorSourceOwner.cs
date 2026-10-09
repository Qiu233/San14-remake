using Godot;
using San14.Data;

/// <summary>Adapter for the actual original ordinary dispatcher entry and
/// each owning-record mask. The retained eight-slot state belongs to the
/// caller; this owner does not reconstruct it for each frame.</summary>
public sealed partial class ArmyWorldQueueNativeColorSourceOwner : IArmyWorldQueueNativeColorSource
{
    private readonly LoadedModelFrontendColorSource source;
    private readonly ArmyWorldQueuePrimitivePlan plan;
    private readonly int node;
    private readonly ModelAuxiliaryColorMasks masks;
    private readonly Action requireActualCurrentScope;
    private readonly Dictionary<string,IReadOnlyList<ModelBlendAttachment>> byRecord = new(StringComparer.Ordinal);
    private ArmyWorldQueueNativeColorSourceOwner(LoadedModelFrontendColorSource source,
        ArmyWorldQueuePrimitivePlan plan, ModelQueueStateSource actualQueue,
        MapFreshCommonResourceSnapshot common, ModelFrontendColorState actualPrevious, Action current)
    {
        this.source=source;this.plan=plan;node=actualQueue.Node.Index;requireActualCurrentScope=current;
        if(node is not(49 or 50)||common.FrameNumber!=plan.Publication.Counts.Queue.FrameNumber ||
            common.SelectedViewOwnerIdentity!=plan.Publication.Counts.Queue.Binding.ViewIdentity ||
            actualQueue.Graph.Source.DecodedSha256!=ArmyWorldGraphicsTargetSource.GraphSha256)
            throw new InvalidDataException("Army native color entry needs the actual queue49/50 declaration and same current World Aux.");
        EntryNativeState=source.PrepareOrdinaryQueueEntry(actualQueue,actualPrevious,6);
        masks=source.ReadCurrentAuxiliaryMasks(common);RequireCurrent(plan,node);
    }
    public ModelFrontendColorState EntryNativeState { get; }
    /// <summary>Each actual record override restores the bound mask. All
    /// factors remain the caller's retained words because blending is off.</summary>
    public ModelFrontendColorState ExitNativeState => EntryNativeState;
    public static ArmyWorldQueueNativeColorSourceOwner CaptureActualEntry(LoadedModelFrontendColorSource source,
        ArmyWorldQueuePrimitivePlan actualPlan, ModelQueueStateSource actualQueue,
        MapFreshCommonResourceSnapshot actualCommon, ModelFrontendColorState actualEightSlotPrevious,
        Action requireActualCurrentScope)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualPlan);
        ArgumentNullException.ThrowIfNull(actualQueue);ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(actualEightSlotPrevious);ArgumentNullException.ThrowIfNull(requireActualCurrentScope);
        return new(source,actualPlan,actualQueue,actualCommon,actualEightSlotPrevious,requireActualCurrentScope);
    }
    public void RequireCurrent(ArmyWorldQueuePrimitivePlan actualPlan,int sourceNode)
    {
        requireActualCurrentScope();
        if(!ReferenceEquals(actualPlan,plan)||sourceNode!=node)
            throw new InvalidDataException("Army color recipe belongs to another owning-record queue/scope.");
    }
    public IReadOnlyList<ModelBlendAttachment> ReadActualDrawTimeStates(ArmyWorldQueuePrimitiveCandidate candidate)
    {
        RequireCurrent(plan,node);
        if(candidate.QueueNodeIndex!=node||!plan.SourceCandidates.Contains(candidate))
            throw new InvalidDataException("Army native color recipe belongs to another candidate.");
        if(byRecord.TryGetValue(candidate.RecordOwnerIdentity,out var found))return found;
        var transition=source.PrepareLateRecord(EntryNativeState,6,candidate.RecordMask78,masks);
        var result=source.ReadDrawAttachments(transition.DrawState,6).Select(a=>new ModelBlendAttachment(a.BlendEnabled,
            Factor(a.SourceColor,false),Factor(a.DestinationColor,false),Operation(a.ColorOperation),
            Factor(a.SourceAlpha,true),Factor(a.DestinationAlpha,true),Operation(a.AlphaOperation),
            (a.RgbaWriteMask&1)!=0,(a.RgbaWriteMask&2)!=0,(a.RgbaWriteMask&4)!=0,(a.RgbaWriteMask&8)!=0)).ToArray();
        byRecord.Add(candidate.RecordOwnerIdentity,found=Array.AsReadOnly(result));return found;
    }
    private static RenderingDevice.BlendFactor Factor(uint value,bool alpha)=>value switch
    {
        1=>RenderingDevice.BlendFactor.Zero,2=>RenderingDevice.BlendFactor.One,
        3=>RenderingDevice.BlendFactor.SrcColor,4=>RenderingDevice.BlendFactor.OneMinusSrcColor,
        5=>RenderingDevice.BlendFactor.SrcAlpha,6=>RenderingDevice.BlendFactor.OneMinusSrcAlpha,
        7=>RenderingDevice.BlendFactor.DstAlpha,8=>RenderingDevice.BlendFactor.OneMinusDstAlpha,
        9=>RenderingDevice.BlendFactor.DstColor,10=>RenderingDevice.BlendFactor.OneMinusDstColor,
        11=>RenderingDevice.BlendFactor.SrcAlphaSaturate,
        14=>alpha?RenderingDevice.BlendFactor.ConstantAlpha:RenderingDevice.BlendFactor.ConstantColor,
        _=>throw new NotSupportedException("Army native disabled-blend factor has no exact device enum adapter.")
    };
    private static RenderingDevice.BlendOperation Operation(uint value)=>value switch
    {1=>RenderingDevice.BlendOperation.Add,2=>RenderingDevice.BlendOperation.Subtract,
        3=>RenderingDevice.BlendOperation.ReverseSubtract,4=>RenderingDevice.BlendOperation.Minimum,
        5=>RenderingDevice.BlendOperation.Maximum,_=>throw new NotSupportedException("Army native blend operation is unsupported.")};
}
