using System.Collections.ObjectModel;
using System.Numerics;
using San14.Core;
using F=San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

public sealed record SceneryUnitPosition(int PhysicalSlot,int? UnitIdentity,Vector4? Position,
    bool CurrentPositionComplete,bool DescriptorMembershipComplete);
public sealed record D3D11SceneryFrame(long CameraFrame,IReadOnlyDictionary<int,SceneryUnitPosition> Units,
    IReadOnlyList<SceneryModelResourceState>? Resources,IReadOnlyList<D3D11CityGateUnavailable> Unavailable)
{
    public bool ResourceMembershipComplete=>Resources is not null;
    public bool RequestsAreActualDraws=>false;
    public bool DeviceResourcesReady=>false; // No successful native/device initialization is fabricated.
}

/// <summary>One actual map session's Scenery kind sub-owner. This composes
/// existing manager/storage/model business; it does not claim the other seven
/// startup kinds or create a simulation, a second camera or an app-mode owner.</summary>
public sealed class D3D11ScenerySource : IDisposable
{
    private readonly SourceWorldContext modelState=new();
    private readonly Func<GameState,SourceWorldContext?> capture;
    private readonly SceneryModelHost host;
    private readonly SceneryModelResourceOwner resources;
    private readonly UnitModelStorage storage;
    private readonly UnitManager manager;
    private readonly UnitManagerReference managerReference=new(0);
    private readonly MapStaticModelGroundHeightSource ground;
    private SourceWorldContext? current;
    private GameState? lastWorld;
    private IReadOnlyList<SceneryReference?>? initialRegistry;
    private readonly HashSet<int> unknownPositions=[];
    private int nextInitial;
    private bool initialized,disposed;

    /// <summary>Actual ordinary011470 bypass selection, read only when reached.
    /// Null/unbound is unknown. This is independent of Main's dynamic288 mode.</summary>
    public Func<MainMapCameraModePacket,bool?>? ReadProjectionClipBypass {get;init;}

    public D3D11ScenerySource(GameResourceRepository repository,LoadedSceneResources scene,LoadedSceneries definitions,
        Func<GameState,SourceWorldContext?> actualCurrentContextProducer)
    {
        capture=actualCurrentContextProducer??throw new ArgumentNullException(nameof(actualCurrentContextProducer));
        var geometry=new UnitGeometry();geometry.MaterializeOriginXImageSeed();geometry.InitializeOriginZ();
        // This is a genuinely new transient Scenery data owner. Repeated fixed
        // binding or queries do not run these constructor stores.
        for(int slot=0;slot<101;slot++)
        {modelState.SetSceneryAngle4A(slot,0);modelState.SetUnitModelPointer(new(RegistryKind.Scenery,slot),new(null));}
        modelState.MaterializeFixedScenery(definitions);
        resources=new(repository,scene,geometry);
        host=new(modelState,resources);storage=new(host);
        var models=Enumerable.Range(1,101).Select(id=>new UnitModelReference(id)).ToArray();
        storage.RegisterPool(managerReference,UnitModelKind.Scenery,models);
        foreach(var model in models)host.Construct(model,storage);
        manager=modelState.RegisterUnitManager(managerReference.Identity!.Value,storage);
        modelState.CurrentUnitManager=managerReference;
        ground=MapStaticModelGroundHeightSource.PrepareCurrentField(repository,scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
    }

    public D3D11SceneryFrame Capture(GameState world,MainMapCameraModePacket actualMode)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentNullException.ThrowIfNull(actualMode);
        var missing=new List<D3D11CityGateUnavailable>();
        if(!ReferenceEquals(lastWorld,world))
        {current=capture(world)??throw new InvalidDataException("Actual current Scenery query owner unavailable.");lastWorld=world;}
        if(!initialized)
        {
            initialRegistry??=current!.ReadRegisteredSceneryReferences()
                ??throw new InvalidDataException("Actual registered Scenery chain is unknown.");
            while(nextInitial<initialRegistry.Count)
            {
                var scenery=initialRegistry[nextInitial];
                if(scenery?.PhysicalSlot is not int physical)throw new InvalidDataException("Actual Scenery registry payload is unknown/null.");
                if(physical==0){nextInitial++;continue;}
                byte flags=current.ReadSceneryFlags47(scenery)??throw new InvalidDataException("Actual Scenery flags47 unknown.");
                if((flags&1)==0){nextInitial++;continue;}
                var receiver=new RegistryObjectReference(RegistryKind.Scenery,physical);
                var model=manager.Create(receiver);
                modelState.SetUnitModelPointer(receiver,model); // Only after real Initialize/Bind returned.
                unknownPositions.Remove(model.Identity!.Value);
                nextInitial++;
            }
            initialized=true;
        }
        var active=ActiveModels();var complete=new HashSet<int>();var descriptorsComplete=new HashSet<int>();
        foreach(var model in active)
        {
            int physical=model.Association?.PhysicalSlot??throw new InvalidDataException("Active Scenery association unknown/null.");
            try
            {
                var result=Update(model,actualMode,current!);
                if(result.PositionComplete){complete.Add(physical);unknownPositions.Remove(model.Reference.Identity!.Value);}
                else unknownPositions.Add(model.Reference.Identity!.Value);
                if(result.DescriptorComplete)descriptorsComplete.Add(physical);
                if(result.Issue is {} issue)missing.Add(new("scenery:"+physical,"descriptor-admission",issue));
            }
            catch(InvalidDataException e)
            {unknownPositions.Add(model.Reference.Identity!.Value);missing.Add(new("scenery:"+physical,"current-unit",e.Message));}
        }
        var units=new Dictionary<int,SceneryUnitPosition>();
        for(int physical=0;physical<101;physical++)
        {
            var pointer=modelState.ReadUnitModelPointer(new(RegistryKind.Scenery,physical))
                ??throw new InvalidDataException("Actual Scenery Unit association is unknown.");
            if(pointer.Identity is null)units.Add(physical,new(physical,null,new(0,0,0,1),true,true));
            else {var model=host.Get(pointer);units.Add(physical,new(physical,pointer.Identity,model.Position10,
                complete.Contains(physical),descriptorsComplete.Contains(physical)));}
        }
        return new(actualMode.FrameNumber,new ReadOnlyDictionary<int,SceneryUnitPosition>(units),
            resources.CaptureResources(active),missing.AsReadOnly());
    }

    private (bool PositionComplete,bool DescriptorComplete,string? Issue) Update(
        SceneryUnitModel model,MainMapCameraModePacket mode,SourceWorldContext queries)
    {
        var position=model.Position10??throw new InvalidDataException("Actual Scenery Unit position unknown.");
        bool positionKnown=!unknownPositions.Contains(model.Reference.Identity!.Value);
        bool? visible=positionKnown?Admission(position,mode):mode.Mode288>=3?false:null;
        string? issue=visible.HasValue?null:positionKnown?"Actual current Unit projection clip selection unknown.":"Actual current Unit position is unknown for admission.";
        int physical=model.Association?.PhysicalSlot??throw new InvalidDataException("Actual Scenery receiver unknown.");
        ushort sceneryHex=modelState.ReadSceneryHexIndex(new(physical))??throw new InvalidDataException("Actual bound Scenery44 unknown.");
        // The native Army scan is reached even when camera admission isfalse.
        for(int slot=0;slot<501;slot++)
        {
            var army=queries.ReadArmyUnitTableReference(slot);
            if(army?.PhysicalSlot is not int armyPhysical){if(visible!=false)visible=null;issue="Actual current Army table receiver unknown/null.";break;}
            if(armyPhysical==0)continue;
            bool? validity=queries.ReadArmyUnitNativeValidity(army);
            if(!validity.HasValue){if(visible!=false)visible=null;issue="Actual current Army validity unknown.";break;}
            bool valid=validity.Value;
            if(!valid)continue;
            ushort? hex=queries.ReadArmyUnitRawHex2A(army);
            if(!hex.HasValue){if(visible!=false)visible=null;issue="Actual current Army2A unknown.";break;}
            ushort raw=hex.Value;
            if(raw==sceneryHex){visible=false;issue=null;break;}
        }
        uint? prior=model.RawField34;uint? dirty=model.RawField30;
        if(prior==0&&visible==false)return (positionKnown,resources.DescriptorMembershipKnown,issue);
        bool outerKnown=prior is >0||visible==true;
        if(!visible.HasValue||!outerKnown)
        {
            // This branch may allocate/release shared category records. Its
            // uncertainty persists for subsequent units and later frames.
            resources.InvalidateDescriptorMembership();
            if(visible==false&&dirty==0)
            {
                // Both the skipped outer call and its hidden-descriptor path
                // leave dirty0 and the existing position unchanged.
                model.WriteRawField34(0);
                return (positionKnown,false,issue);
            }
            // Descriptor branches cannot mutate Unit.Position. If the outer
            // call and dirty writer are certain, its final ground write is
            // independent of unknown descriptor admission/readiness.
            bool positionComplete;
            if(outerKnown&&dirty is >0)
            {
                float value=ground.Query(position.X,position.Z)
                    ??throw new InvalidDataException("Actual Scenery ground height unavailable.");
                model.PublishGroundHeight(value);positionComplete=true;
            }
            else
            {
                // Both possible paths produce the same currentY once it
                // equals this immutable field's ground result. A speculative
                // pure lookup does not publish an unproved slot6 write.
                float? value=ground.Query(position.X,position.Z);
                float grounded=value is float h?(h>=445f?h:445f):float.NaN;
                positionComplete=positionKnown&&value.HasValue&&BitConverter.SingleToInt32Bits(position.Y)==BitConverter.SingleToInt32Bits(grounded);
            }
            model.WriteRawField34(visible.HasValue?(visible.Value?1u:0u):null);
            model.WriteRawField30(null);
            return (positionComplete,false,issue??"Actual Scenery outer visibility path unknown.");
        }
        model.WriteRawField34(visible.Value?1u:0u);
        if(!resources.DescriptorMembershipKnown)
        {
            // Hidden descriptor clearing returns zero independently of its
            // old list. With dirty0, slot6 therefore has a known early return.
            if(!visible.Value&&dirty==0)return (positionKnown,false,issue);
            bool positionComplete;
            if(dirty is >0)
            {
                float value=ground.Query(position.X,position.Z)
                    ??throw new InvalidDataException("Actual Scenery ground height unavailable.");
                model.PublishGroundHeight(value);positionComplete=true;
            }
            else
            {
                float? value=ground.Query(position.X,position.Z);
                positionComplete=positionKnown&&value is float h&&BitConverter.SingleToInt32Bits(position.Y)==BitConverter.SingleToInt32Bits(h>=445f?h:445f);
            }
            model.WriteRawField30(null);
            return (positionComplete,false,issue??"Actual shared Scenery descriptor membership is unknown.");
        }
        if(resources.UpdateDescriptor(model,visible.Value))model.WriteRawField30(1);
        if(model.RawField30==0)return (positionKnown,true,null);
        if(model.RawField30 is null)
        {
            float? candidate=ground.Query(position.X,position.Z);
            bool equal=positionKnown&&candidate is float h&&BitConverter.SingleToInt32Bits(position.Y)==BitConverter.SingleToInt32Bits(h>=445f?h:445f);
            return (equal,true,equal?null:"Actual Scenery dirty writer is unknown.");
        }
        float height=ground.Query(position.X,position.Z)??throw new InvalidDataException("Actual Scenery ground height unavailable.");
        model.PublishGroundHeight(height);
        // Pending device records keep their real factory World and dirty1;
        // slot6 must not apply world setters through a fabricated ready14.
        model.WriteRawField30(resources.HasPendingDescriptors(model)?1u:0u);
        return (true,true,null);
    }

    private bool? Admission(Vector4 position,MainMapCameraModePacket mode)
    {
        if(mode.Mode288>=3)return false;
        var frame=mode.AcceptedMainFrame;var eye=frame.Camera.CurrentPose.Position80;
        float dx=F.Subtract(eye.X,position.X),dy=F.Subtract(eye.Y,position.Y),dz=F.Subtract(eye.Z,position.Z);
        float distance=MathF.Sqrt(F.Add(F.Add(F.Multiply(dx,dx),F.Multiply(dy,dy)),F.Multiply(dz,dz)));
        float reference=(float)frame.ControllerState.Distance90;
        if(distance>F.Add(reference,reference))return false;
        int floor=(int)MathF.Floor(mode.EcB0Index);float margin=floor<2?1.5f:BitConverter.Int32BitsToSingle(0x3F8CCCCD);
        bool? bypass=ReadProjectionClipBypass?.Invoke(mode);
        if(bypass==true)return true;
        var m=frame.Camera.SourceWorldToProjection;
        float Column(float a,float b,float c,float d,bool yFirst=false)
        {
            float px=F.Multiply(position.X,a),py=F.Multiply(position.Y,b);
            return F.Add(F.Add(yFirst?F.Add(py,px):F.Add(px,py),F.Multiply(position.Z,c)),F.Multiply(position.W,d));
        }
        float reciprocal=F.Divide(1,Column(m.M14,m.M24,m.M34,m.M44));
        float x=F.Multiply(Column(m.M11,m.M21,m.M31,m.M41,true),reciprocal),
            y=F.Multiply(Column(m.M12,m.M22,m.M32,m.M42),reciprocal),
            z=F.Multiply(Column(m.M13,m.M23,m.M33,m.M43),reciprocal);
        // COMISS/JBE rejects unordered operands as well as the boundaries.
        bool inside=x>-margin&&x<margin&&y>-margin&&y<margin&&z>0&&z<margin;
        // bypass OR inside: a proven inside point is admitted on both branches.
        return inside?true:bypass==false?false:null;
    }

    private IReadOnlyList<SceneryUnitModel> ActiveModels()
    {
        var result=new List<SceneryUnitModel>();var reference=storage.ReadActiveModelHead(managerReference,UnitModelKind.Scenery);
        while(reference.Identity.HasValue)
        {result.Add(host.Get(reference));reference=storage.ReadActiveModelNext(reference)??throw new InvalidDataException("Actual active Scenery next unknown.");}
        return result;
    }
    public void Dispose()
    {
        if(disposed)return;
        foreach(var model in ActiveModels())
        {
            int? physical=model.Association?.PhysicalSlot;
            host.RetireModel(model.Reference);host.ReleaseModelResources(model.Reference);
            storage.ReturnModelToPool(managerReference,UnitModelKind.Scenery,model.Reference);
            if(physical is int known)
                modelState.SetUnitModelPointer(new(RegistryKind.Scenery,known),new(null));
        }
        resources.Dispose();unknownPositions.Clear();disposed=true;current=null;lastWorld=null;
    }
}
