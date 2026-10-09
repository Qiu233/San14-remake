using System.Numerics;
using San14.Data;

/// <summary>Actual earlier43-bound parameter lifecycle. Mutable engine
/// modules are borrowed, not reconstructed per queue. Null final shaders
/// leave the D19050 and D4E410 record work intact.</summary>
public sealed class OrdinaryRs4CpuParameterSourceOwner
{
    private readonly WorldGeometryTargetBinding binding;
    private readonly MapSourceCreatedWorldF103Frames world;
    private readonly OrdinaryRs4CpuSource source;
    private readonly WorldGeometryLateEffectParameterState effects;
    private readonly WorldGeometryLateGenericScopeState generic;
    private readonly WorldGeometryLateViewportScopeState viewport;
    private readonly Func<WorldGeometryLateViewportTarget?> currentWrapper;
    private readonly Func<Vector4?> currentView1540;
    private readonly Func<uint> currentFlags3F0;
    private readonly Func<WorldGeometryLateFogPublication?> currentHostQ88;
    private readonly WorldGeometryLateAuxParameters aux;
    private MapWorldKind1CurrentRecord? prepared;
    private WorldGeometryLateEffectCleanup cleanup;
    private int activeNode;

    public OrdinaryRs4CpuParameterSourceOwner(WorldGeometryTargetBinding actual43,
        MapSourceCreatedWorldF103Frames actualGeometryWorld, OrdinaryRs4CpuSource actualSource,
        LoadedWorldGeometryLateEffectSource loadedEffects,
        WorldGeometryLateEffectParameterState actualEngineEffects,
        WorldGeometryLateGenericScopeState actualEngineGeneric,
        WorldGeometryLateViewportScopeState actualViewport,
        Func<WorldGeometryLateViewportTarget?> readActualCurrentWrapper,
        Func<Vector4?> readActualSelectedView1540, Func<uint> readActualContextFlags3F0,
        Func<WorldGeometryLateFogPublication?> readActualHostQ88)
    {
        binding=actual43??throw new ArgumentNullException(nameof(actual43));
        world=actualGeometryWorld??throw new ArgumentNullException(nameof(actualGeometryWorld));
        source=actualSource??throw new ArgumentNullException(nameof(actualSource));
        ArgumentNullException.ThrowIfNull(loadedEffects);
        effects=actualEngineEffects??throw new ArgumentNullException(nameof(actualEngineEffects));
        generic=actualEngineGeneric??throw new ArgumentNullException(nameof(actualEngineGeneric));
        viewport=actualViewport??throw new ArgumentNullException(nameof(actualViewport));
        currentWrapper=readActualCurrentWrapper??throw new ArgumentNullException(nameof(readActualCurrentWrapper));
        currentView1540=readActualSelectedView1540??throw new ArgumentNullException(nameof(readActualSelectedView1540));
        currentFlags3F0=readActualContextFlags3F0??throw new ArgumentNullException(nameof(readActualContextFlags3F0));
        currentHostQ88=readActualHostQ88??throw new ArgumentNullException(nameof(readActualHostQ88));
        if(loadedEffects.Native.Locale!=source.Source.Native.Locale)
            throw new InvalidDataException("Earlier RS4 parameter rules use a different rendering layout.");
        aux=loadedEffects.ReadSourceCreatedAux(world.Passes[0].Frame.Common);
        RequireCurrent(actual43,actualGeometryWorld);
    }
    public WorldGeometryLateGenericScopeState GenericState=>generic;
    public WorldGeometryLateEffectParameterState EffectState=>effects;
    public void RequireCurrent(WorldGeometryTargetBinding actual43,
        MapSourceCreatedWorldF103Frames actualWorld)
    {
        binding.RequireCurrent();
        if(!ReferenceEquals(actual43,binding)||!ReferenceEquals(actualWorld,world)||binding.SourceNode!=43 ||
            !ReferenceEquals(binding.Frame.Source,source.Source.Targets43) ||
            world.Passes[0].Frame.FrameNumber!=binding.Accepted.Context.FrameNumber ||
            world.View.Binding.ViewIdentity!=binding.Accepted.Feature.Binding.ViewIdentity)
            throw new InvalidDataException("Earlier RS4 parameters require this actual43 binding and current World/View.");
    }
    internal void Begin(int node)
    {
        RequireCurrent(binding,world);_=source.Parameters(node);
        if(activeNode!=0)throw new InvalidOperationException("Earlier RS4 viewport scope is already active.");
        viewport.Begin(currentWrapper(),currentView1540(),currentFlags3F0());activeNode=node;
    }
    internal void Exit(int node)
    {
        if(activeNode!=node||prepared is not null)
            throw new InvalidOperationException("Earlier RS4 exit does not match the actual viewport/record scope.");
        viewport.Exit();activeNode=0;
    }
    internal float ReadTime()=>generic.ReadActualParameterTime();
    internal void SetTime(float value)=>generic.SetActualParameterTime(value);
    internal void EnterGeneric()
    {
        if(activeNode==0)throw new InvalidOperationException("RS4 Generic entry requires its actual queue scope.");
        generic.EnterActualZeroCountScope58(world.Passes[0].Frame.Lighting.PreparedCameraPrefix.Span[0x10..]);
        generic.SetActualWordD4(0);
    }
    internal void PrepareRecord(int node,MapWorldKind1CurrentRecord record)
    {
        RequireCurrent(binding,world);
        if(activeNode!=node||prepared is not null)throw new InvalidOperationException("RS4 record parameters have an unfinished lifecycle.");
        var payload=world.BorrowAcceptedRecordLighting(record);
        cleanup=effects.PrepareSource58(source.Parameters(node).Packed728,payload.LocalLightData.Span,aux,currentHostQ88());
        prepared=record;
    }
    internal void CleanupRecord(MapWorldKind1CurrentRecord record)
    {
        if(!ReferenceEquals(prepared,record))throw new InvalidOperationException("RS4 cleanup belongs to another record owner.");
        effects.Cleanup(cleanup);prepared=null;
    }
    internal void ClearSixResources()=>generic.ClearActualSixResourceLoans58();
}
