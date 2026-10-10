using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Loaded EA6470/D43190 profiles for the actual earlier queues.
/// Shared leaf arithmetic is reused; their graph identity and state22/13
/// selection are retained independently of the later node58 profile.</summary>
public sealed class OrdinaryRs4CpuSource
{
    private readonly IReadOnlyDictionary<int, WorldGeometryNode58PackedParameters> packed;
    private readonly uint sourceClipMask380;
    public OrdinaryRs4CpuSource(LoadedOrdinaryRs4Source actualSource,
        LoadedObjects actualSavedCommonSource)
    {
        Source = actualSource ?? throw new ArgumentNullException(nameof(actualSource));
        _ = CommonRenderResources.ImportSettings(actualSavedCommonSource);
        sourceClipMask380 = CommonRenderResources.ReadScalar(actualSavedCommonSource, 0x880FE0E8, 4);
        packed = new[] {44,45,46}.ToDictionary(n => n,
            n => WorldGeometryNode58PackedParameterSource.Read(Source.Queues[n]));
        foreach (int n in new[] {44,45,46})
        {
            var q = Source.Queues[n]; var p = packed[n];
            if (q.RenderType != 0 || (q.Flag & 1) != 0 ||
                ((p.Packed728 >> 8) & 255) != 0 || (p.Packed728 & (1UL << 21)) != 0 ||
                p.ShaderState730 != q.ShaderStateOffset)
                throw new NotSupportedException("Earlier RS4 needs its actual ordinary first-bank/short-parameter source profile.");
        }
    }
    public LoadedOrdinaryRs4Source Source { get; }
    public WorldGeometryNode58PackedParameters Parameters(int queueNode) => packed[queueNode];
    /// <summary>E80816..26 creates the owned type10 value23 for these
    /// actual Flag0E/06, CW-clear profiles. D06390 calls D71710 even when
    /// every primitive renderer slot is null: Fill3 and sourceCull2/Back.
    /// This is a queue writer, not a prior frontend default.</summary>
    public ulong ReadSourceQueueRasterWord(int queueNode)
    { _ = Parameters(queueNode); return 0x23; }
    /// <summary>EA6470/D43190 current gate. Native View19D0 is read only
    /// for a negative19D4 store; a known nonnegative owned publication needs
    /// no invented capacity value.</summary>
    public OrdinaryRs4CurrentEntry PrepareCurrentEntry(int queueNode,
        WorldGeometryLateCurrentContext current, bool actualCommon240Present,uint actualCommonFlags8,
        int actualView19D4Stored,int? actualView19D0Capacity,object? actualView1C18)
    {
        var queue=Source.Queues[queueNode];_=Parameters(queueNode);
        uint view=unchecked(current.SelectedViewIndex+queue.ViewId);
        if(!current.CurrentTablePresent||unchecked((int)current.ContextFlags3F0)<0||view>=32||!current.SelectedViewPresent)
            return new(false,false,null,false);
        if(actualView1C18 is not null)throw new NotSupportedException("RS4 alternate1C18 requires its actual D48E10 consumer.");
        if(actualCommon240Present&&(actualCommonFlags8&1)==0)return new(true,false,null,false);
        int count=actualView19D4Stored>=0?actualView19D4Stored:
            LoadedWorldGeometryLateCpuSource.EffectiveCurrentCount(actualView19D4Stored,
                actualView19D0Capacity??throw new InvalidDataException("Negative actual19D4 requires its consumed current19D0 capacity."));
        return new(true,true,count,count!=0);
    }
    public uint ReadCurrentClipMask380(MapCommonRenderState actualCommon)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);
        if (!actualCommon.AuxiliaryPresent)
            throw new NotSupportedException("RS4 selected Aux clip fields are absent.");
        return actualCommon.Scope == CommonRenderPublicationKind.InitialSettings
            ? sourceClipMask380 : actualCommon.Auxiliary!.ClipPlaneMask!.Value;
    }
    /// <summary>D4A7A0's mask-zero branch still writes frontend clip=0.
    /// Nonzero enabled planes need the actual View100 matrix/plane writer,
    /// rather than a guessed no-clip state.</summary>
    public uint PrepareCurrentClipWord(MapCommonRenderState actualCommon)
    {
        if ((ReadCurrentClipMask380(actualCommon) & 63) != 0)
            throw new NotSupportedException("RS4 current Aux380 enables actual D4A7A0 clip planes; its plane producer is required.");
        return 0;
    }
    public WorldGeometryLateDepth PrepareEntryDepth(int queueNode, uint actualContextFlags3E4)
    {
        var depth = ModelRendererStateSourceReader.PrepareQueueDepth(Source.Queues[queueNode],
            (actualContextFlags3E4 & 0x20000000) != 0);
        return new(depth.DepthTest, depth.DepthWrite, depth.CompareCode, depth.StencilEnabled);
    }
    /// <summary>EA8C8E: only packed bit41 requests write/compare restoration.
    /// Node44/45 keep their actual dispatcher state;46 restores the ordered
    /// comparison derived at its own entry, without changing test/stencil.</summary>
    public WorldGeometryLateDepth PrepareExitDepth(int queueNode,
        uint entryContextFlags3E4, WorldGeometryLateDepth actualCurrent)
        => (Parameters(queueNode).Packed728 & (1UL << 41)) == 0 ? actualCurrent :
            actualCurrent with { Write = true, Compare =
                (entryContextFlags3E4 & 0x20000000) != 0 ? 6u : 3u };
    public ModelOrdinaryDispatcherStencilPacket PrepareRecord(int queueNode,
        ModelOrdinaryDispatcherRecord record, MapCommonRenderState actualCommon,
        uint currentContextFlags3E4)
        => ModelOrdinaryDispatcherStencilSource.Prepare(Source.Queues[queueNode], record,
            actualCommon, currentContextFlags3E4);
    /// <summary>D436BB full bound-mask disabled-blend writer. Factor words
    /// and inactive slots stay outside this active projection.</summary>
    public ModelOrdinaryActiveColorState PrepareActiveColorEntry(int queueNode, int actualBoundCount)
    {
        _ = Parameters(queueNode);
        if (actualBoundCount != Source.Targets43.ColorTargets.Count)
            throw new InvalidDataException("Earlier RS4 must use its actual seven bound color slots.");
        return new(Enumerable.Repeat((byte)15, actualBoundCount).ToArray());
    }
}
