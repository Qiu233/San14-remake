using System.Numerics;
using San14.Data;

/// <summary>Actual normal57/58 CPU parameter adapter. The source-created
/// World supplies all independently owned record packets. Mutable Generic and
/// module owners persist in the engine and are borrowed by each current frame;
/// selected null shaders do not skip their preparation or cleanup.</summary>
public sealed class OriginalWorldGeometryLateParameterSourceOwner : IOriginalWorldGeometryLateCpuParameterSource
{
    private readonly OriginalWorldGeometryTargetBinding binding;
    private readonly OriginalMapSourceCreatedWorldF103Frames world;
    private readonly OriginalWorldGeometryLateEffectParameterState effects;
    private readonly OriginalWorldGeometryLateGenericScopeState generic;
    private readonly OriginalWorldGeometryLateViewportScopeState viewport;
    private readonly Func<OriginalWorldGeometryLateViewportTarget?> currentWrapper;
    private readonly Func<Vector4?> currentView1540;
    private readonly Func<uint> currentContextFlags;
    private readonly Action<uint> writeContextFlags;
    private readonly Func<OriginalWorldGeometryLateFogPublication?> currentHostQ88;
    private readonly Func<ulong> currentCpu728;
    private readonly OriginalWorldGeometryLateAuxParameters aux;
    private readonly Dictionary<uint,OriginalMapSourceCreatedLightingPacket> lighting = [];
    private OriginalWorldGeometryLateRecord? preparedRecord;
    private OriginalWorldGeometryLateEffectCleanup cleanup;
    private int activeScope;

    public OriginalWorldGeometryLateParameterSourceOwner(OriginalWorldGeometryTargetBinding actual53,
        OriginalMapSourceCreatedWorldF103Frames actualGeometryWorld,
        LoadedOriginalWorldGeometryLateEffectSource loadedEffectSource,
        OriginalWorldGeometryLateEffectParameterState actualEngineEffectState,
        OriginalWorldGeometryLateGenericScopeState actualEngineGenericState,
        OriginalWorldGeometryLateViewportScopeState actualViewportScope,
        Func<OriginalWorldGeometryLateViewportTarget?> readActualCurrentWrapper,
        Func<Vector4?> readActualSelectedView1540,
        Func<uint> readActualContextFlags3F0, Action<uint> writeActualContextFlags3F0,
        Func<OriginalWorldGeometryLateFogPublication?> readActualHostQ88,
        Func<ulong> readActualCpu728)
    {
        ArgumentNullException.ThrowIfNull(actual53); ArgumentNullException.ThrowIfNull(actualGeometryWorld);
        ArgumentNullException.ThrowIfNull(loadedEffectSource); ArgumentNullException.ThrowIfNull(actualEngineEffectState);
        ArgumentNullException.ThrowIfNull(actualEngineGenericState); ArgumentNullException.ThrowIfNull(actualViewportScope);
        ArgumentNullException.ThrowIfNull(readActualCurrentWrapper); ArgumentNullException.ThrowIfNull(readActualSelectedView1540);
        ArgumentNullException.ThrowIfNull(readActualContextFlags3F0); ArgumentNullException.ThrowIfNull(writeActualContextFlags3F0);
        ArgumentNullException.ThrowIfNull(readActualHostQ88); ArgumentNullException.ThrowIfNull(readActualCpu728);
        binding=actual53;world=actualGeometryWorld;effects=actualEngineEffectState;generic=actualEngineGenericState;
        viewport=actualViewportScope;currentWrapper=readActualCurrentWrapper;currentView1540=readActualSelectedView1540;
        currentContextFlags=readActualContextFlags3F0;writeContextFlags=writeActualContextFlags3F0;
        currentHostQ88=readActualHostQ88;currentCpu728=readActualCpu728;
        var first=world.Passes.FirstOrDefault()?.Frame ?? throw new InvalidDataException("Late parameters require the actual World frame.");
        aux=loadedEffectSource.ReadSourceCreatedAux(first.Common);
        var queue=world.View.CurrentWorldQueue ?? throw new InvalidDataException("Late parameters require the complete current World record queue.");
        foreach(var row in queue.Records)
        {
            if(!lighting.TryAdd(row.Mask78,world.BorrowAcceptedRecordLighting(row)))
                throw new InvalidDataException("Current late World record masks are not independent unique owners.");
        }
        RequireCurrent(binding);
    }
    public OriginalWorldGeometryLateEffectParameterState EffectState => effects;
    public OriginalWorldGeometryLateGenericScopeState GenericState => generic;
    public void RequireCurrent(OriginalWorldGeometryTargetBinding actual53)
    {
        binding.RequireCurrent();
        if(!ReferenceEquals(actual53,binding)||binding.SourceNode!=53 ||
            world.Passes[0].Frame.FrameNumber!=binding.Accepted.Context.FrameNumber||
            world.View.Binding.ViewIdentity!=binding.Accepted.Feature.Binding.ViewIdentity)
            throw new InvalidDataException("Late parameters require this accepted node53 and exact current World/View owner.");
    }
    public void BeginActualViewportScope(int sourceNode)
    {
        RequireCurrent(binding);
        if(activeScope!=0||sourceNode is not(57 or 58))
            throw new InvalidOperationException("Late viewport scope is nested or belongs to another node.");
        viewport.Begin(currentWrapper(),currentView1540(),currentContextFlags());activeScope=sourceNode;
    }
    public void RestoreActualViewportScope(int sourceNode)
    {
        if(activeScope!=sourceNode)throw new InvalidOperationException("Late viewport exit belongs to another scope.");
        viewport.Exit();activeScope=0;
    }
    public void WriteActualContextFlags3F0(uint flags) => writeContextFlags(flags);
    public float ReadActualParameterTime() => generic.ReadActualParameterTime();
    public void SetActualParameterTime(float actualTime) => generic.SetActualParameterTime(actualTime);
    public void EnterActualGenericScope58()
    {
        var camera=world.Passes[0].Frame.Lighting.PreparedCameraPrefix;
        generic.EnterActualZeroCountScope58(camera.Span[0x10..]);
    }
    public void SetActualGenericWordD4(uint value) => generic.SetActualWordD4(value);
    public void PrepareActualRecordParameters58(OriginalWorldGeometryLateRecord actualRecord)
    {
        RequireCurrent(binding); ArgumentNullException.ThrowIfNull(actualRecord);
        if(preparedRecord is not null || actualRecord.Record.FrameNumber!=world.Passes[0].Frame.FrameNumber ||
            actualRecord.Record.ViewIdentity!=world.View.Binding.ViewIdentity ||
            !lighting.TryGetValue(actualRecord.Record.RecordMask78,out var payload))
            throw new InvalidDataException("Late record preparation requires its actual independent World payload and clean prior lifecycle.");
        cleanup=effects.PrepareSource58(currentCpu728(),payload.LocalLightData.Span,aux,currentHostQ88());
        preparedRecord=actualRecord;
    }
    public void CleanupActualRecord58(OriginalWorldGeometryLateRecord actualRecord)
    {
        if(!ReferenceEquals(preparedRecord,actualRecord))throw new InvalidOperationException("Late cleanup does not match the prepared record.");
        effects.Cleanup(cleanup);preparedRecord=null;
    }
    public void ClearActualSixGenericResources58() => generic.ClearActualSixResourceLoans58();
}
