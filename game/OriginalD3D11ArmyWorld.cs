using System.Collections.ObjectModel;
using System.Numerics;
using System.Text;
using San14.Core;
using San14.Data;

public readonly record struct OriginalD3D11ArmyActorKey(int UnitId,int Category,int DescriptorOffset,int InstanceIndex);
public readonly record struct OriginalD3D11ArmyFlagMaterialKey(int ForceVariant,int SourceColor49,ushort SourceGlyph);
public sealed record OriginalD3D11ArmyActor(OriginalD3D11ArmyActorKey Key,
    OriginalUnitWorldTransformState World,OriginalMapModelNativeOrientedBounds Bounds,
    int ActionIndex,float ReturnedPhase,OriginalD3D11ArmyFlagMaterialKey? FlagMaterial);
public sealed record OriginalD3D11ArmyUnavailable(int UnitId,string Reason);

/// <summary>Explicit caller phases for the preview route. Normal native playback
/// retains its source-player clocks in physical model slots.</summary>
public sealed class OriginalD3D11ArmyReturnedPhases
{
    internal IReadOnlyDictionary<OriginalD3D11ArmyActorKey,float> Values {get;}
    public string Source {get;}
    public OriginalD3D11ArmyReturnedPhases(IReadOnlyDictionary<OriginalD3D11ArmyActorKey,float> values,string source)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var copy=new Dictionary<OriginalD3D11ArmyActorKey,float>(values);
        if(copy.Values.Any(phase=>!float.IsFinite(phase)||phase<0))
            throw new InvalidDataException("Army returned phase must be finite and nonnegative.");
        Values=new ReadOnlyDictionary<OriginalD3D11ArmyActorKey,float>(copy);Source=source;
    }
}

/// <summary>Current formation candidates. The native unit setter decides when
/// their root/OBB reach the retained model. Source resources are loaded separately.</summary>
public sealed class OriginalD3D11ArmyPublication
{
    internal byte[] NativeBytes {get;}
    public ReadOnlyMemory<byte> Packet=>NativeBytes;
    public ulong Revision {get;}
    public IReadOnlyList<OriginalD3D11ArmyActor> Actors {get;}
    public IReadOnlyList<OriginalD3D11ArmyUnavailable> Unavailable {get;}
    public string AnimationScope {get;}
    public IReadOnlyDictionary<int,Vector3> UnitCenters {get;}
    public IReadOnlyDictionary<int,int> UnitGraphicsActions {get;}
    public IReadOnlyDictionary<int,float> UnitGroundHeights {get;}
    public IReadOnlyDictionary<int,uint> UnitMotionRevisions {get;}
    public IReadOnlyDictionary<int,uint> UnitModelModes {get;}
    public IReadOnlyDictionary<int,uint> UnitSelectedFormations {get;}
    public IReadOnlyDictionary<int,uint> UnitNavalFormations {get;}
    internal OriginalD3D11ArmyPublication(ulong revision,OriginalD3D11ArmyActor[] actors,
        OriginalD3D11ArmyUnavailable[] unavailable,string animationScope,IReadOnlyDictionary<int,Vector3> unitCenters,
        IReadOnlyDictionary<int,int>? unitGraphicsActions=null,
        IReadOnlyDictionary<int,float>? unitGroundHeights=null,IReadOnlyDictionary<int,uint>? unitMotionRevisions=null)
        :this(revision,actors,unavailable,animationScope,unitCenters,unitGraphicsActions,unitGroundHeights,
            unitMotionRevisions,null,null){}
    internal OriginalD3D11ArmyPublication(ulong revision,OriginalD3D11ArmyActor[] actors,
        OriginalD3D11ArmyUnavailable[] unavailable,string animationScope,IReadOnlyDictionary<int,Vector3> unitCenters,
        IReadOnlyDictionary<int,int>? unitGraphicsActions,IReadOnlyDictionary<int,float>? unitGroundHeights,
        IReadOnlyDictionary<int,uint>? unitMotionRevisions,IReadOnlyDictionary<int,uint>? unitModelModes,
        IReadOnlyDictionary<int,uint>? unitSelectedFormations)
        :this(revision,actors,unavailable,animationScope,unitCenters,unitGraphicsActions,unitGroundHeights,
            unitMotionRevisions,unitModelModes,unitSelectedFormations,null){}
    internal OriginalD3D11ArmyPublication(ulong revision,OriginalD3D11ArmyActor[] actors,
        OriginalD3D11ArmyUnavailable[] unavailable,string animationScope,IReadOnlyDictionary<int,Vector3> unitCenters,
        IReadOnlyDictionary<int,int>? unitGraphicsActions,IReadOnlyDictionary<int,float>? unitGroundHeights,
        IReadOnlyDictionary<int,uint>? unitMotionRevisions,IReadOnlyDictionary<int,uint>? unitModelModes,
        IReadOnlyDictionary<int,uint>? unitSelectedFormations,IReadOnlyDictionary<int,uint>? unitNavalFormations)
    {
        Revision=revision;Actors=Array.AsReadOnly(actors);Unavailable=Array.AsReadOnly(unavailable);AnimationScope=animationScope;
        UnitCenters=unitCenters;
        UnitGraphicsActions=unitGraphicsActions??new ReadOnlyDictionary<int,int>(unitCenters.Keys.ToDictionary(id=>id,_=>0));
        UnitGroundHeights=unitGroundHeights??new ReadOnlyDictionary<int,float>(unitCenters.ToDictionary(p=>p.Key,p=>p.Value.Y));
        UnitMotionRevisions=unitMotionRevisions??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        UnitModelModes=unitModelModes??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        UnitSelectedFormations=unitSelectedFormations??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        UnitNavalFormations=unitNavalFormations??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        using var output=new MemoryStream(24+unitCenters.Count*40+actors.Length*164);using var writer=new BinaryWriter(output,Encoding.ASCII,true);
        writer.Write(Encoding.ASCII.GetBytes("ARMYWLD6"));writer.Write(revision);writer.Write(actors.Length);writer.Write(unitCenters.Count);
        foreach(var (id,center) in unitCenters){writer.Write(id);writer.Write(center.X);writer.Write(center.Y);writer.Write(center.Z);writer.Write(UnitGraphicsActions[id]);writer.Write(UnitGroundHeights[id]);writer.Write(UnitMotionRevisions[id]);writer.Write(UnitModelModes[id]);writer.Write(UnitSelectedFormations[id]);writer.Write(UnitNavalFormations[id]);}
        foreach(var actor in actors){
            var key=actor.Key;writer.Write(key.UnitId);writer.Write(key.Category);writer.Write(key.DescriptorOffset);writer.Write(key.InstanceIndex);
            writer.Write(actor.ActionIndex);writer.Write(actor.ReturnedPhase);
            var world=actor.World;V(new(world.Scale34,1));var q=world.Quaternion24;V(new(q.X,q.Y,q.Z,q.W));V(new(world.Position18,1));
            var bound=actor.Bounds;V(bound.Center0);V(bound.Basis10);V(bound.Basis20);V(bound.Basis30);V(bound.HalfExtents40);
            writer.Write(actor.FlagMaterial?.ForceVariant??-1);writer.Write(actor.FlagMaterial?.SourceColor49??-1);
            writer.Write((uint)(actor.FlagMaterial?.SourceGlyph??0));
        }
        NativeBytes=output.ToArray();
        void V(Vector4 value){writer.Write(value.X);writer.Write(value.Y);writer.Write(value.Z);writer.Write(value.W);}
    }
}

/// <summary>Loaded source definitions plus existing owned ordinary/flag visual
/// state. Immutable Core dictionaries are the change signal. Camera-only calls
/// reuse the publication; discrete source Hex changes use the existing setter
/// prefix and grounding. Continuous original motion/cadence remains separate.</summary>
public sealed class OriginalD3D11ArmyPublisher:IDisposable
{
    private readonly OriginalPlayableArmyVisualAdapter _selection;
    private readonly OriginalRemakeArmySceneVisuals _ordinary;
    private readonly OriginalRemakeArmyFlagVisuals _flags;
    private readonly OriginalRemakeArmyFlagCurrentSource _flagCurrent;
    private readonly OriginalGameRepository _sourceRepository;
    private readonly Func<GameState,UnitState,OriginalArmyGraphicsCurrentInputs?>? _motionInputs;
    private readonly OriginalArmyGraphicsMotionClockSource? _motionClock;
    private OriginalD3D11ArmyDefaultActions? _defaultActions;
    private readonly Dictionary<int,OriginalMapModelNativePreparedBounds> _bounds=[];
    private OriginalRemakeArmyFlagCurrentFrame? _flagMaterials;
    private OriginalD3D11ArmyReturnedPhases? _lastAnimation;
    private OriginalD3D11ArmyPublication? _current;
    private GameState? _lastWorld;
    private OriginalD3D11ArmyUnavailable[] _planUnavailable=[];
    private bool _motionChanged;
    private uint _fpsMode;
    private ulong _revision;
    private bool _disposed;

    public OriginalD3D11ArmyPublisher(OriginalGameRepository repository,LoadedScenario scenario,LoadedMap map,
        MapWorldProjection projection,LoadedHeightField height,string locale,
        Func<GameState,UnitState,OriginalArmyGraphicsCurrentInputs?>? motionInputs=null)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(map);ArgumentNullException.ThrowIfNull(projection);ArgumentNullException.ThrowIfNull(height);
        _sourceRepository=repository;_selection=OriginalPlayableArmyVisualAdapter.Load(repository,map,locale);
        _ordinary=new(repository,locale,projection,height);_flags=new(repository);_flagCurrent=new(repository,scenario,map);
        _motionInputs=motionInputs;
        if(motionInputs is not null)_motionClock=OriginalArmyGraphicsMotionClockSource.Load(repository,locale);
    }

    /// <summary>A null phase publication writes the preview's phase0 placeholder.
    /// Normal native playback consumes the physical slot's retained clock and pose.
    /// Explicit preview phases require every current actor key.</summary>
    public OriginalD3D11ArmyPublication Capture(GameState world,OriginalD3D11ArmyReturnedPhases? returnedPhases=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(world);
        bool sameWorld=_current is not null&&ReferenceEquals(world,_lastWorld);
        if(sameWorld&&!_motionChanged&&ReferenceEquals(returnedPhases,_lastAnimation))return _current!;
        OriginalD3D11ArmyActor[] actors;OriginalD3D11ArmyUnavailable[] unavailable;
        if(sameWorld&&!_motionChanged){actors=_current!.Actors.Select(a=>a with{ReturnedPhase=Phase(a.Key,returnedPhases)}).ToArray();unavailable=_current.Unavailable.ToArray();}
        else{
            if(!sameWorld){
                var plan=_selection.Resolve(world);
                if(_motionInputs is null)_ordinary.Prepare(world,plan);
                else _ordinary.PrepareMotion(world,plan,_motionClock!.Resolve(_fpsMode),_motionInputs);
                var missingPlan=new List<OriginalD3D11ArmyUnavailable>();
                foreach(var failure in plan.Unresolved.Where(f=>!plan.Selections.ContainsKey(f.UnitId)))missingPlan.Add(new(failure.UnitId,failure.Reason));
                foreach(var selection in plan.Selections.Values)if(!_ordinary.Frames.ContainsKey(selection.UnitId))
                    missingPlan.Add(new(selection.UnitId,"Army visual requires current source formation, direction, commander and mobility; unsupported model modes need their source producer."));
                _planUnavailable=missingPlan.ToArray();
            }
            _flags.Prepare(_ordinary.Frames);
            var missing=new List<OriginalD3D11ArmyUnavailable>(_planUnavailable);
            if(!sameWorld)_flagMaterials=_flags.Frames.Count==0?null:_flagCurrent.Prepare(world,_flags.Frames.Keys);
            var material=_flagMaterials;
            var current=new List<OriginalD3D11ArmyActor>();
            foreach(var frame in _ordinary.Frames.Values.OrderBy(f=>f.UnitId)){
                var unit=world.Units[frame.UnitId];
                if(unit.NativeCurrentHex2A is ushort nativeHex&&nativeHex!=unit.HexId)
                    throw new InvalidDataException("Army visual Hex differs from its actual native current position.");
                foreach(var actor in frame.Publications.Values.OrderBy(a=>a.Key.DescriptorOffset).ThenBy(a=>a.Key.InstanceIndex)){
                    var key=new OriginalD3D11ArmyActorKey(frame.UnitId,actor.Category,actor.Key.DescriptorOffset,actor.Key.InstanceIndex);
                    Add(key,actor.WorldTransform,null);
                }
                if(material!.Unavailable.TryGetValue(frame.UnitId,out string? reason)){missing.Add(new(frame.UnitId,reason));continue;}
                foreach(var actor in _flags.Frames[frame.UnitId].Actors){
                    OriginalRemakeArmyFlagMaterialInputs selected;
                    try{selected=material.Select(actor);}catch(InvalidDataException error){missing.Add(new(frame.UnitId,error.Message));continue;}
                    if(selected.SourcePane.SourceForceColorIndex49 is not byte color||color>60){
                        missing.Add(new(frame.UnitId,"Flag pane source color49 is unresolved; no cloth material fallback is selected."));continue;}
                    var glyph=OriginalArmyFlagPaneColors.CopySourceNameGlyph(selected.SourcePane.SelectedSourceText,selected.SourcePane.CallerForceSource);
                    var flagKey=new OriginalD3D11ArmyFlagMaterialKey(selected.ExplicitOwnedForceVariant,color,glyph.TwoUtf16Units[0]);
                    var key=new OriginalD3D11ArmyActorKey(frame.UnitId,actor.Category,actor.DescriptorOffset,actor.DescriptorInstanceIndex);
                    Add(key,actor.World,flagKey);
                }
            }
            actors=current.ToArray();unavailable=missing.ToArray();
            void Add(OriginalD3D11ArmyActorKey key,OriginalUnitWorldTransformState transform,OriginalD3D11ArmyFlagMaterialKey? materialKey){
                if(!_bounds.TryGetValue(key.Category,out var bounds)){
                    bool body=key.Category<2||key.Category is >=4 and <=9;
                    var catalog=body?_ordinary.Models[key.Category].Catalog:_flags.Sources[key.Category].Catalog;
                    var model=body?_ordinary.Models[key.Category].Transforms.Source:_flags.Sources[key.Category].Model.Source;
                    var source=OriginalArmyPrimitivePublicationSource.ReadKnownCategory(catalog,key.Category,model);
                    _bounds.Add(key.Category,bounds=OriginalArmyModelWorldVisibilitySource.Read(source).PreparedModelBounds);
                }
                _defaultActions??=new(_sourceRepository,Enumerable.Range(0,10));
                current.Add(new(key,transform,OriginalMapModelNativeBoundsSources.BuildSingleNode(bounds,transform,false,false),
                    _defaultActions.Categories[key.Category].ActionIndex,Phase(key,returnedPhases),materialKey));
            }
        }
        var next=new OriginalD3D11ArmyPublication(++_revision,actors,unavailable,returnedPhases?.Source??
            "Source current-Hex targets, retained nineteen-record motion and graphics ActionID; paced native manager owns player phase.",
            new ReadOnlyDictionary<int,Vector3>(_ordinary.Frames.ToDictionary(pair=>pair.Key,pair=>pair.Value.CurrentUnitPosition)),
            new ReadOnlyDictionary<int,int>(_ordinary.Frames.ToDictionary(pair=>pair.Key,pair=>pair.Value.GraphicsAction60)),
            new ReadOnlyDictionary<int,float>(_ordinary.Frames.ToDictionary(pair=>pair.Key,pair=>pair.Value.CurrentGroundHeight)),
            new ReadOnlyDictionary<int,uint>(_ordinary.Frames.ToDictionary(pair=>pair.Key,pair=>pair.Value.MotionRevision)),
            new ReadOnlyDictionary<int,uint>(_ordinary.Frames.ToDictionary(pair=>pair.Key,pair=>(uint)pair.Value.Mode)),
            new ReadOnlyDictionary<int,uint>(_ordinary.Frames.ToDictionary(pair=>pair.Key,
                pair=>(uint)world.Units[pair.Key].NativeArmyConfiguration!.Formation1B!.Value)),
            new ReadOnlyDictionary<int,uint>(_ordinary.Frames.ToDictionary(pair=>pair.Key,
                pair=>(uint)(world.Units[pair.Key].NativeArmyConfiguration!.Formation1C??0))));
        _lastWorld=world;_motionChanged=false;_lastAnimation=returnedPhases;return _current=next;
    }
    public OriginalD3D11ArmyPublication AdvanceMotion(GameState world,uint fpsMode)
    {
        _fpsMode=fpsMode;
        Capture(world);
        if(_motionClock is not null)_motionChanged=_ordinary.AdvanceMotion(world,_motionClock.Resolve(fpsMode));
        return Capture(world,_lastAnimation);
    }
    private static float Phase(OriginalD3D11ArmyActorKey key,OriginalD3D11ArmyReturnedPhases? returned){
        if(returned is null)return 0;
        return returned.Values.TryGetValue(key,out float phase)?phase:throw new InvalidDataException($"Army returned phase missing for actual actor {key}.");
    }
    public void Dispose(){if(_disposed)return;_disposed=true;_ordinary.Dispose();_flags.Dispose();_bounds.Clear();_current=null;}
}
