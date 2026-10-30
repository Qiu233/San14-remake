using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Text;
using San14.Core;
using San14.Data;

public readonly record struct D3D11ArmyActorKey(int UnitId,int Category,int DescriptorOffset,int InstanceIndex);
public readonly record struct D3D11ArmyFlagMaterialKey(int ForceVariant,int SourceColor49,ushort SourceGlyph);
public sealed record D3D11ArmyActor(D3D11ArmyActorKey Key,
    UnitWorldTransformState World,MapModelNativeOrientedBounds Bounds,
    int ActionIndex,float ReturnedPhase,D3D11ArmyFlagMaterialKey? FlagMaterial);
public sealed record D3D11ArmyUnavailable(int UnitId,string Reason);

/// <summary>Explicit caller phases for the preview route. Normal native playback
/// retains its source-player clocks in physical model slots.</summary>
public sealed class D3D11ArmyReturnedPhases
{
    internal IReadOnlyDictionary<D3D11ArmyActorKey,float> Values {get;}
    public string Source {get;}
    public D3D11ArmyReturnedPhases(IReadOnlyDictionary<D3D11ArmyActorKey,float> values,string source)
    {
        ArgumentNullException.ThrowIfNull(values);ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var copy=new Dictionary<D3D11ArmyActorKey,float>(values);
        if(copy.Values.Any(phase=>!float.IsFinite(phase)||phase<0))
            throw new InvalidDataException("Army returned phase must be finite and nonnegative.");
        Values=new ReadOnlyDictionary<D3D11ArmyActorKey,float>(copy);Source=source;
    }
}

/// <summary>Current formation candidates. The native unit setter decides when
/// their root/OBB reach the retained model. Source resources are loaded separately.</summary>
public sealed class D3D11ArmyPublication
{
    private readonly object _generationLock = new();
    private D3D11ArmyPublication? _generationPublication;
    internal byte[] NativeBytes {get;}
    public ReadOnlyMemory<byte> Packet=>NativeBytes;
    public ulong Revision {get;}
    public ulong ResourceContextGeneration {get;}
    public IReadOnlyList<D3D11ArmyActor> Actors {get;}
    public IReadOnlyList<D3D11ArmyUnavailable> Unavailable {get;}
    internal ReadOnlyCollection<ArmyFlagMaterialRequest> FlagMaterialRequestOwner {get;}
    public IReadOnlyList<ArmyFlagMaterialRequest> FlagMaterialRequests=>FlagMaterialRequestOwner;
    public string AnimationScope {get;}
    public IReadOnlyDictionary<int,Vector3> UnitCenters {get;}
    public IReadOnlyDictionary<int,int> UnitGraphicsActions {get;}
    public IReadOnlyDictionary<int,float> UnitGroundHeights {get;}
    public IReadOnlyDictionary<int,uint> UnitMotionRevisions {get;}
    public IReadOnlyDictionary<int,uint> UnitModelModes {get;}
    public IReadOnlyDictionary<int,uint> UnitSelectedFormations {get;}
    public IReadOnlyDictionary<int,uint> UnitNavalFormations {get;}
    internal D3D11ArmyPublication(ulong revision,D3D11ArmyActor[] actors,
        D3D11ArmyUnavailable[] unavailable,string animationScope,IReadOnlyDictionary<int,Vector3> unitCenters,
        IReadOnlyDictionary<int,int>? unitGraphicsActions=null,
        IReadOnlyDictionary<int,float>? unitGroundHeights=null,IReadOnlyDictionary<int,uint>? unitMotionRevisions=null)
        :this(revision,actors,unavailable,animationScope,unitCenters,unitGraphicsActions,unitGroundHeights,
            unitMotionRevisions,null,null){}
    internal D3D11ArmyPublication(ulong revision,D3D11ArmyActor[] actors,
        D3D11ArmyUnavailable[] unavailable,string animationScope,IReadOnlyDictionary<int,Vector3> unitCenters,
        IReadOnlyDictionary<int,int>? unitGraphicsActions,IReadOnlyDictionary<int,float>? unitGroundHeights,
        IReadOnlyDictionary<int,uint>? unitMotionRevisions,IReadOnlyDictionary<int,uint>? unitModelModes,
        IReadOnlyDictionary<int,uint>? unitSelectedFormations)
        :this(revision,actors,unavailable,animationScope,unitCenters,unitGraphicsActions,unitGroundHeights,
            unitMotionRevisions,unitModelModes,unitSelectedFormations,null){}
    internal D3D11ArmyPublication(ulong revision,D3D11ArmyActor[] actors,
        D3D11ArmyUnavailable[] unavailable,string animationScope,IReadOnlyDictionary<int,Vector3> unitCenters,
        IReadOnlyDictionary<int,int>? unitGraphicsActions,IReadOnlyDictionary<int,float>? unitGroundHeights,
        IReadOnlyDictionary<int,uint>? unitMotionRevisions,IReadOnlyDictionary<int,uint>? unitModelModes,
        IReadOnlyDictionary<int,uint>? unitSelectedFormations,IReadOnlyDictionary<int,uint>? unitNavalFormations,
        ReadOnlyCollection<ArmyFlagMaterialRequest>? flagMaterialRequests=null,ulong resourceContextGeneration=0)
    {
        Revision=revision;ResourceContextGeneration=resourceContextGeneration;
        Actors=Array.AsReadOnly(actors);Unavailable=Array.AsReadOnly(unavailable);AnimationScope=animationScope;
        FlagMaterialRequestOwner=flagMaterialRequests??Array.AsReadOnly(Array.Empty<ArmyFlagMaterialRequest>());
        UnitCenters=unitCenters;
        UnitGraphicsActions=unitGraphicsActions??new ReadOnlyDictionary<int,int>(unitCenters.Keys.ToDictionary(id=>id,_=>0));
        UnitGroundHeights=unitGroundHeights??new ReadOnlyDictionary<int,float>(unitCenters.ToDictionary(p=>p.Key,p=>p.Value.Y));
        UnitMotionRevisions=unitMotionRevisions??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        UnitModelModes=unitModelModes??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        UnitSelectedFormations=unitSelectedFormations??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        UnitNavalFormations=unitNavalFormations??new ReadOnlyDictionary<int,uint>(unitCenters.Keys.ToDictionary(id=>id,_=>0u));
        using var output=new MemoryStream(32+unitCenters.Count*40+actors.Length*164);using var writer=new BinaryWriter(output,Encoding.ASCII,true);
        writer.Write(Encoding.ASCII.GetBytes("ARMYWLD7"));writer.Write(revision);writer.Write(resourceContextGeneration);
        writer.Write(actors.Length);writer.Write(unitCenters.Count);
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

    private D3D11ArmyPublication(D3D11ArmyPublication source, ulong generation)
    {
        Revision=source.Revision;ResourceContextGeneration=generation;
        Actors=source.Actors;Unavailable=source.Unavailable;AnimationScope=source.AnimationScope;
        FlagMaterialRequestOwner=source.FlagMaterialRequestOwner;UnitCenters=source.UnitCenters;
        UnitGraphicsActions=source.UnitGraphicsActions;UnitGroundHeights=source.UnitGroundHeights;
        UnitMotionRevisions=source.UnitMotionRevisions;UnitModelModes=source.UnitModelModes;
        UnitSelectedFormations=source.UnitSelectedFormations;UnitNavalFormations=source.UnitNavalFormations;
        // The publication is immutable. Only the ARMYWLD7 generation field
        // changes; keep the already encoded unit/actor payload and its owners.
        NativeBytes=source.NativeBytes.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(NativeBytes.AsSpan(16,8),generation);
    }

    internal D3D11ArmyPublication WithResourceContextGeneration(ulong generation)
    {
        if(ResourceContextGeneration==generation)return this;
        lock(_generationLock)
        {
            if(_generationPublication?.ResourceContextGeneration!=generation)
                _generationPublication=new(this,generation);
            return _generationPublication;
        }
    }
}

/// <summary>Loaded source definitions plus existing owned ordinary/flag visual
/// state. Immutable Core dictionaries are the change signal. Camera-only calls
/// reuse the publication; discrete source Hex changes use the existing setter
/// prefix and grounding. Continuous original motion/cadence remains separate.</summary>
public sealed class D3D11ArmyPublisher:IDisposable
{
    private readonly PlayableArmyVisualAdapter _selection;
    private RemakeArmySceneVisuals _ordinary;
    private RemakeArmyFlagVisuals _flags;
    private readonly RemakeArmyFlagCurrentSource _flagCurrent;
    private readonly GameResourceRepository _sourceRepository;
    private readonly Func<GameState,UnitState,ArmyGraphicsCurrentInputs?>? _motionInputs;
    private readonly ArmyGraphicsMotionClockSource? _motionClock;
    private D3D11ArmyDefaultActions? _defaultActions;
    private Dictionary<int,MapModelNativePreparedBounds> _bounds=[];
    private RemakeArmyFlagCurrentFrame? _flagMaterials;
    private D3D11ArmyReturnedPhases? _lastAnimation;
    private D3D11ArmyPublication? _current;
    private GameState? _lastWorld;
    private D3D11ArmyUnavailable[] _planUnavailable=[];
    private bool _motionChanged;
    private uint _fpsMode;
    private ulong _revision;
    private bool _disposed;

    public D3D11ArmyPublisher(GameResourceRepository repository,LoadedScenario scenario,LoadedMap map,
        MapWorldProjection projection,LoadedHeightField height,string locale,
        Func<GameState,UnitState,ArmyGraphicsCurrentInputs?>? motionInputs=null,
        ArmyGraphicsActions? graphicsActions=null)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(map);ArgumentNullException.ThrowIfNull(projection);ArgumentNullException.ThrowIfNull(height);
        _sourceRepository=repository;_selection=PlayableArmyVisualAdapter.Load(repository,map,locale);
        _ordinary=new(repository,locale,projection,height);_flags=new(repository);_flagCurrent=new(repository,scenario,map);
        _motionInputs=motionInputs;
        if(graphicsActions is not null)_defaultActions=new(graphicsActions);
        if(motionInputs is not null)_motionClock=ArmyGraphicsMotionClockSource.Load(repository,locale);
    }

    // Imported resources are shared; every mutable presentation field belongs
    // to the candidate until its complete publication has been constructed.
    private D3D11ArmyPublisher(D3D11ArmyPublisher source)
    {
        _selection=source._selection;_flagCurrent=source._flagCurrent;_sourceRepository=source._sourceRepository;
        _motionInputs=source._motionInputs;_motionClock=source._motionClock;
        _ordinary=source._ordinary.ForkLogicalState();_flags=source._flags.ForkLogicalState();
        _bounds=new(source._bounds);_defaultActions=source._defaultActions;
        _flagMaterials=source._flagMaterials;_lastAnimation=source._lastAnimation;
        _current=source._current;_lastWorld=source._lastWorld;_planUnavailable=source._planUnavailable;
        _motionChanged=source._motionChanged;_fpsMode=source._fpsMode;_revision=source._revision;
    }

    private void Adopt(D3D11ArmyPublisher candidate)
    {
        var oldOrdinary=_ordinary;var oldFlags=_flags;
        _ordinary=candidate._ordinary;_flags=candidate._flags;
        _bounds=candidate._bounds;_defaultActions=candidate._defaultActions;
        _flagMaterials=candidate._flagMaterials;_lastAnimation=candidate._lastAnimation;
        _current=candidate._current;_lastWorld=candidate._lastWorld;_planUnavailable=candidate._planUnavailable;
        _motionChanged=candidate._motionChanged;_fpsMode=candidate._fpsMode;_revision=candidate._revision;
        // Ownership moved above. Disposing the candidate must not clear the
        // transferred owners. The retired owners contain managed state only.
        candidate._disposed=true;
        oldOrdinary.Dispose();oldFlags.Dispose();
    }

    /// <summary>A null phase publication writes the preview's phase0 placeholder.
    /// Normal native playback consumes the physical slot's retained clock and pose.
    /// Explicit preview phases require every current actor key.</summary>
    public D3D11ArmyPublication Capture(GameState world,D3D11ArmyReturnedPhases? returnedPhases=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(world);
        if(_current is not null&&ReferenceEquals(world,_lastWorld)&&!_motionChanged&&ReferenceEquals(returnedPhases,_lastAnimation))
            return _current;
        using var candidate=new D3D11ArmyPublisher(this);
        var publication=candidate.CapturePrepared(world,returnedPhases);
        Adopt(candidate);
        return publication;
    }

    internal D3D11ArmyPublisher ForkPresentation() => new(this);

    public D3D11ArmyPublication CaptureDeparture(GameState sourceWorld, int sourceUnitId,
        ArmyGraphicsCurrentInputs sourceInputs, ArmyDepartureFlagInputs? sourceFlagInputs = null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        using var candidate=new D3D11ArmyPublisher(this);
        var publication=candidate.CapturePrepared(sourceWorld, null,
            (world,unit)=>unit.Id==sourceUnitId?sourceInputs:_motionInputs?.Invoke(world,unit),
            sourceUnitId,sourceFlagInputs);
        Adopt(candidate);
        return publication;
    }

    private D3D11ArmyPublication CapturePrepared(GameState world,D3D11ArmyReturnedPhases? returnedPhases=null,
        Func<GameState,UnitState,ArmyGraphicsCurrentInputs?>? sourceMotionInputs=null,
        int? sourceFlagUnitId=null,ArmyDepartureFlagInputs? sourceFlagInputs=null)
    {
        bool sameWorld=sourceMotionInputs is null&&sourceFlagInputs is null&&
            _current is not null&&ReferenceEquals(world,_lastWorld);
        if(sameWorld&&!_motionChanged&&ReferenceEquals(returnedPhases,_lastAnimation))return _current!;
        D3D11ArmyActor[] actors;D3D11ArmyUnavailable[] unavailable;
        ReadOnlyCollection<ArmyFlagMaterialRequest>? flagRequests=_current?.FlagMaterialRequestOwner;
        if(sameWorld&&!_motionChanged){actors=_current!.Actors.Select(a=>a with{ReturnedPhase=Phase(a.Key,returnedPhases)}).ToArray();unavailable=_current.Unavailable.ToArray();}
        else{
            if(!sameWorld){
                var plan=_selection.Resolve(world);
                if(_motionInputs is null)_ordinary.Prepare(world,plan);
                else _ordinary.PrepareMotion(world,plan,_motionClock!.Resolve(_fpsMode),sourceMotionInputs??_motionInputs);
                var missingPlan=new List<D3D11ArmyUnavailable>();
                foreach(var failure in plan.Unresolved.Where(f=>!plan.Selections.ContainsKey(f.UnitId)))missingPlan.Add(new(failure.UnitId,failure.Reason));
                foreach(var selection in plan.Selections.Values)if(!_ordinary.Frames.ContainsKey(selection.UnitId))
                    missingPlan.Add(new(selection.UnitId,"Army visual requires current source formation, direction, commander and mobility; unsupported model modes need their source producer."));
                _planUnavailable=missingPlan.ToArray();
            }
            _flags.Prepare(_ordinary.Frames);
            var missing=new List<D3D11ArmyUnavailable>(_planUnavailable);
            if(!sameWorld)_flagMaterials=_flags.Frames.Count==0?null:
                _flagCurrent.Prepare(world,_flags.Frames.Keys,sourceFlagUnitId,sourceFlagInputs);
            var material=_flagMaterials;
            var current=new List<D3D11ArmyActor>();
            // Motion changes transforms and bounds, while the source world
            // owns commander, force/name/color and the selected model mode.
            // Retain this immutable owner until that actual world changes.
            var requests=sameWorld?null:new Dictionary<(int Category,int Force,int Color,ushort Glyph),ArmyFlagMaterialRequest>();
            foreach(var frame in _ordinary.Frames.Values.OrderBy(f=>f.UnitId)){
                if(world.Units[frame.UnitId].PositionKnowledge==ArmyPositionKnowledge.Conflicting)
                    throw new InvalidDataException("Army visual position has unresolved compatibility knowledge.");
                foreach(var actor in frame.Publications.Values.OrderBy(a=>a.Key.DescriptorOffset).ThenBy(a=>a.Key.InstanceIndex)){
                    var key=new D3D11ArmyActorKey(frame.UnitId,actor.Category,actor.Key.DescriptorOffset,actor.Key.InstanceIndex);
                    Add(key,actor.WorldTransform,null);
                }
                if(material!.Unavailable.TryGetValue(frame.UnitId,out string? reason)){missing.Add(new(frame.UnitId,reason));continue;}
                foreach(var actor in _flags.Frames[frame.UnitId].Actors){
                    RemakeArmyFlagMaterialInputs selected;
                    try{selected=material.Select(actor);}catch(InvalidDataException error){missing.Add(new(frame.UnitId,error.Message));continue;}
                    byte? color=selected.SourcePane.SourceForceColorIndex49;
                    var glyph=ArmyFlagPaneColors.CopySourceNameGlyph(selected.SourcePane.SelectedSourceText,selected.SourcePane.CallerForceSource);
                    var flagKey=new D3D11ArmyFlagMaterialKey(selected.ExplicitOwnedForceVariant,color is byte value?value:-1,glyph.TwoUtf16Units[0]);
                    var key=new D3D11ArmyActorKey(frame.UnitId,actor.Category,actor.DescriptorOffset,actor.DescriptorInstanceIndex);
                    Add(key,actor.World,flagKey);
                    var binding=material.Bindings[frame.UnitId];
                    var sourceName=actor.Category==2?binding.BannerName:binding.CarriedKind2Name!;
                    requests?.TryAdd((actor.Category,flagKey.ForceVariant,flagKey.SourceColor49,flagKey.SourceGlyph),
                        new(actor.Category,flagKey.ForceVariant,color,sourceName,actor.Category==3));
                }
            }
            actors=current.ToArray();unavailable=missing.ToArray();
            if(requests is not null)flagRequests=Array.AsReadOnly(requests.Values.ToArray());
            void Add(D3D11ArmyActorKey key,UnitWorldTransformState transform,D3D11ArmyFlagMaterialKey? materialKey){
                if(!_bounds.TryGetValue(key.Category,out var bounds)){
                    bool body=key.Category<2||key.Category is >=4 and <=9;
                    var catalog=body?_ordinary.Models[key.Category].Catalog:_flags.Sources[key.Category].Catalog;
                    LoadedModelGeometry model=body?new(_ordinary.Models[key.Category].Transforms.Geometry,
                        _ordinary.Models[key.Category].Transforms.Source):new(_flags.Sources[key.Category].Model.Geometry,
                        _flags.Sources[key.Category].Model.Source);
                    var source=ArmyPrimitivePublicationSource.ReadKnownCategory(catalog,key.Category,model);
                    _bounds.Add(key.Category,bounds=ArmyModelWorldVisibilitySource.Read(source).PreparedModelBounds);
                }
                _defaultActions??=new(_sourceRepository,Enumerable.Range(0,10));
                current.Add(new(key,transform,MapModelNativeBoundsSources.BuildSingleNode(bounds,transform,false,false),
                    _defaultActions.Categories[key.Category].ActionIndex,Phase(key,returnedPhases),materialKey));
            }
        }
        IReadOnlyDictionary<int,Vector3> centers;
        IReadOnlyDictionary<int,int> actions;
        IReadOnlyDictionary<int,float> heights;
        IReadOnlyDictionary<int,uint> motions,modes,land,naval;
        if(sameWorld&&!_motionChanged){
            // Explicit animation phases do not change any unit-level input.
            centers=_current!.UnitCenters;actions=_current.UnitGraphicsActions;heights=_current.UnitGroundHeights;
            motions=_current.UnitMotionRevisions;modes=_current.UnitModelModes;
            land=_current.UnitSelectedFormations;naval=_current.UnitNavalFormations;
        }
        else{
            int count=_ordinary.Frames.Count;
            var centerValues=new Dictionary<int,Vector3>(count);var actionValues=new Dictionary<int,int>(count);
            var heightValues=new Dictionary<int,float>(count);var motionValues=new Dictionary<int,uint>(count);
            var modeValues=new Dictionary<int,uint>(count);
            var landValues=sameWorld?null:new Dictionary<int,uint>(count);
            var navalValues=sameWorld?null:new Dictionary<int,uint>(count);
            foreach(var (id,frame) in _ordinary.Frames){
                centerValues.Add(id,frame.CurrentUnitPosition);actionValues.Add(id,frame.GraphicsAction60);
                heightValues.Add(id,frame.CurrentGroundHeight);motionValues.Add(id,frame.MotionRevision);
                modeValues.Add(id,(uint)frame.Mode);
                if(!sameWorld){
                    landValues!.Add(id,(uint)ArmyConfigurationSourceAdapter.ReadCurrentLand(world,world.Units[id])!.Value);
                    navalValues!.Add(id,(uint)(ArmyConfigurationSourceAdapter.ReadNaval(world,world.Units[id])??0));
                }
            }
            centers=new ReadOnlyDictionary<int,Vector3>(centerValues);actions=new ReadOnlyDictionary<int,int>(actionValues);
            heights=new ReadOnlyDictionary<int,float>(heightValues);motions=new ReadOnlyDictionary<int,uint>(motionValues);
            modes=new ReadOnlyDictionary<int,uint>(modeValues);
            land=sameWorld?_current!.UnitSelectedFormations:new ReadOnlyDictionary<int,uint>(landValues!);
            naval=sameWorld?_current!.UnitNavalFormations:new ReadOnlyDictionary<int,uint>(navalValues!);
        }
        var next=new D3D11ArmyPublication(++_revision,actors,unavailable,returnedPhases?.Source??
            "Source current-Hex targets, retained nineteen-record motion and graphics ActionID; paced native manager owns player phase.",
            centers,actions,heights,motions,modes,land,naval,flagRequests);
        _lastWorld=world;_motionChanged=false;_lastAnimation=returnedPhases;return _current=next;
    }
    public D3D11ArmyPublication AdvanceMotion(GameState world,uint fpsMode)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(world);
        using var candidate=new D3D11ArmyPublisher(this);
        candidate._fpsMode=fpsMode;
        candidate.CapturePrepared(world);
        if(candidate._motionClock is not null)
            candidate._motionChanged=candidate._ordinary.AdvanceMotion(world,candidate._motionClock.Resolve(fpsMode));
        var publication=candidate.CapturePrepared(world,candidate._lastAnimation);
        Adopt(candidate);
        return publication;
    }
    private static float Phase(D3D11ArmyActorKey key,D3D11ArmyReturnedPhases? returned){
        if(returned is null)return 0;
        return returned.Values.TryGetValue(key,out float phase)?phase:throw new InvalidDataException($"Army returned phase missing for actual actor {key}.");
    }
    public void Dispose(){if(_disposed)return;_disposed=true;_ordinary.Dispose();_flags.Dispose();_bounds.Clear();_current=null;}
}
