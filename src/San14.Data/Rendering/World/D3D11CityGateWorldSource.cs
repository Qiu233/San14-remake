using System.Buffers.Binary;
using System.Numerics;
using San14.Core;

namespace San14.Data;

public sealed record D3D11CityGateDamage(string Identity,int Hp,int Capacity,byte Flag66,int Force)
{
    public byte? Morale44 { get; init; }
    public int? MoraleCapacity { get; init; }
}
public sealed record D3D11CityGateUnavailable(string Identity,string Field,string Reason);
public sealed record D3D11CityGateRequiredAsset(int Category,string SourceKey,int DrawCount);
public readonly record struct D3D11CityGateFlagMaterialKey(int Force,int Color49,ushort Glyph);
public sealed record D3D11CityGateAccessoryUnit(int Index,string Identity,int Force,int Requested,
    int Style,int OffsetStart,Vector3 Position,Quaternion Rotation,float Heading,
    ArmyFlagPaneNameSelection? PaneName,D3D11CityGateFlagMaterialKey? Material);
public sealed record D3D11CityGateWorldFrame(D3D11CityGatePrimaryFrame Primary,
    IReadOnlyList<D3D11CityGateDamage> Damage,IReadOnlyList<D3D11CityGateAccessoryUnit> Accessories,
    IReadOnlyList<D3D11CityGateRequiredAsset> RequiredAssets,IReadOnlyList<D3D11CityGateUnavailable> Unavailable,
    ReadOnlyMemory<byte> PrimaryPacket,ReadOnlyMemory<byte> AccessoryPacket)
{
    public bool PrimaryReady=>PrimaryPacket.Length!=0;
    public bool AccessorySourceReady=>AccessoryPacket.Length!=0;
    public bool AccessoryRequestsAreActualDraws=>false;
}

/// <summary>Actual immutable current World ->61 primary and224 accessory
/// source inputs. It borrows formal current Base/policy/settings queries and
/// performs no simulation update or AreaCapital creation.</summary>
public sealed class D3D11CityGateWorldSource
{
    readonly Func<GameState,SourceWorldContext?> capture;
    readonly D3D11CityGateTemplateBank bank;
    readonly LoadedArmyFlagPaneNames names;
    readonly UnitWorldTransformState accessoryRoot;
    readonly MapModelNativePreparedBounds accessoryBounds;
    readonly UnitGeometry geometry=new();
    readonly CityGateAccessoryResources accessoryResources;
    GameState? lastWorld;object? lastNodes,lastGates;uint lastMode;
    object? lastCities,lastGateStates,lastForces,lastDistricts,lastOfficers,lastTerritory,lastAreas,lastParameters,lastNativeForces,lastStructures;
    bool lastScenarioPreview;
    D3D11CityGateWorldFrame? current;
    public D3D11CityGatePrimarySource Primaries{get;}

    public D3D11CityGateWorldSource(GameResourceRepository repository,LoadedSceneResources scene,LoadedScenario scenario,
        LoadedMap map,NewGameOptions options,D3D11CityGateTemplateBank preparedBank,CityGateAccessoryResources accessories,
        Func<GameState,SourceWorldContext?> actualCurrentContextProducer)
    {
        bank=preparedBank;capture=actualCurrentContextProducer;Primaries=new(repository,scene,scenario,map,options);names=ArmyFlagPaneNames.Load(scenario);
        accessoryResources=accessories;
        var accessory=accessories.Animation;accessoryRoot=accessory.DefinitionRoot;
        var h=accessory.Model.Geometry.RawGeometryHeader;
        accessoryBounds=MapModelNativeBoundsSources.PrepareHeader(new(F(h,20),F(h,24),F(h,28)),new(F(h,32),F(h,36),F(h,40)),
            accessory.Model.Source.ResourceUri,accessory.Model.Source.DecodedSha256);
    }
    public D3D11CityGateWorldFrame Capture(GameState world,uint commonFrustumMode,
        IReadOnlyDictionary<string,MapStaticModelPublication>? actualNodes=null,
        IReadOnlyDictionary<int,D3D11GateDefinition>? actualGateDefinitions=null)
        => CaptureCore(world,commonFrustumMode,actualNodes,actualGateDefinitions,scenarioPreview:false);

    /// <summary>Read the original static post-load source through this opening's
    /// actual preview context. Core has not started, so its raw scenario HP is
    /// not the writer of the post-load clamp/66 used by the created map units.</summary>
    public D3D11CityGateWorldFrame CaptureScenarioPreview(GameState world,uint commonFrustumMode,
        IReadOnlyDictionary<string,MapStaticModelPublication>? actualNodes=null,
        IReadOnlyDictionary<int,D3D11GateDefinition>? actualGateDefinitions=null)
    {
        if(!ArmyOperationContext.IsNewGamePreview(world))
            throw new InvalidDataException("new-game.city-gate.requires-initializing-world-without-simulation");
        return CaptureCore(world,commonFrustumMode,actualNodes,actualGateDefinitions,scenarioPreview:true);
    }

    private D3D11CityGateWorldFrame CaptureCore(GameState world,uint commonFrustumMode,
        IReadOnlyDictionary<string,MapStaticModelPublication>? actualNodes,
        IReadOnlyDictionary<int,D3D11GateDefinition>? actualGateDefinitions,bool scenarioPreview)
    {
        bool sameWorld=ReferenceEquals(world,lastWorld)||ReferenceEquals(world.Cities,lastCities)&&ReferenceEquals(world.Gates,lastGateStates)&&
            ReferenceEquals(world.Forces,lastForces)&&ReferenceEquals(world.Districts,lastDistricts)&&ReferenceEquals(world.Officers,lastOfficers)&&
            ReferenceEquals(world.Territory,lastTerritory)&&ReferenceEquals(world.Areas,lastAreas)&&ReferenceEquals(world.NativeArmySimulation?.Parameters,lastParameters)&&
            ReferenceEquals(world.NativeArmySimulation?.Forces,lastNativeForces)&&ReferenceEquals(world.Structures,lastStructures);
        if(sameWorld&&ReferenceEquals(actualNodes,lastNodes)&&ReferenceEquals(actualGateDefinitions,lastGates)&&
            commonFrustumMode==lastMode&&scenarioPreview==lastScenarioPreview&&current is not null){lastWorld=world;return current;}
        if(!world.Cities.Keys.Order().SequenceEqual(Enumerable.Range(1,51))||!world.Gates.Keys.Order().SequenceEqual(Enumerable.Range(52,10)))
            throw new InvalidDataException("Current source51/10 Base identities incomplete; Core absence is not a retirement writer.");
        var native=capture(world)??throw new InvalidDataException("Formal current Base Object/policy/settings queries unavailable.");
        foreach(var city in world.Cities.Values)if(city.DistrictId is null&&city.ForceId is null&&native.ReadObjectForceSlot13(new(city.Id))==0)
            native.SetCityDistrictSlot(city.Id,0); // Inverse of OriginalScenarioInitializer.OptionalId(0), operation-local only.
        var damage=new List<D3D11CityGateDamage>();var missing=new List<D3D11CityGateUnavailable>();
        var city66=new Dictionary<int,CityVisualDamageInput>();var gate66=new Dictionary<int,D3D11GateCurrentBinding>();
        foreach(var city in world.Cities.Values.OrderBy(c=>c.Id)){
            var row=Read("city:"+city.Id,new(BaseKind.City,city.Id),city.Durability,true);
            if(row is not null)city66.Add(city.Id,CityVisualDamageInput.FromTier(row.Flag66,new(CityVisualEvidence.CurrentDamageUri,
                world.MapSha256,scenarioPreview
                    ? "Actual scenario static post-load Object14/City66/policy/settings; no simulation World writer"
                    : "Actual current size/HP/district/policy32/settings; no retained opening tier")));
        }
        foreach(var gate in world.Gates.Values.OrderBy(g=>g.Id)){
            var definition=actualGateDefinitions?.GetValueOrDefault(gate.Id)??Primaries.GateDefinitions[gate.Id];
            var row=Read("gate:"+gate.Id,new(BaseKind.Gate,definition.NativeSlot),gate.Durability,false);
            if(row is not null)gate66.Add(gate.Id,new(definition,row.Flag66,row.Flag66 switch{1=>56,2=>57,3=>58,_=>55}));
        }
        var frame=Primaries.Prepare(world,city66,gate66,actualNodes);
        missing.AddRange(frame.Unavailable.Select(m=>new D3D11CityGateUnavailable(m.InstanceKey,"primary",m.Reason)));
        var required=frame.Owners.Where(o=>bank.Bind(o.Instance.Asset,o.Category) is null).DistinctBy(o=>o.Instance.Asset.SourceKey)
            .Select(o=>new D3D11CityGateRequiredAsset(o.Category,o.Instance.Asset.SourceKey,o.Instance.Asset.Draws.Count)).ToArray();
        var byIdentity=damage.ToDictionary(d=>d.Identity);var units=new List<D3D11CityGateAccessoryUnit>();
        var placements=new List<(int Index,int Ordinal,UnitWorldTransformState Root,MapModelNativeOrientedBounds Bounds)>();
        foreach(var owner in frame.Owners){bool city=owner.CitySelection is not null;int force=byIdentity[owner.Instance.InstanceKey].Force;
            ArmyFlagPaneNameSelection? pane=null;D3D11CityGateFlagMaterialKey? material=null;
            if(force is >=1 and <=51){try{
                pane=names.Resolve(native,native.ReadForceTableReference(force)??throw new InvalidDataException("Current flag Force pointer missing."));
                if(pane.CurrentForceColor49 is not byte color||color>60)throw new InvalidDataException("Current flag source color49 unavailable.");
                material=new(force,color,pane.Glyph.TwoUtf16Units[0]);
            }catch(InvalidDataException error){missing.Add(new(owner.Identity,"flag-material",error.Message));}}
            int style=city?Primaries.Variants.VariantForCitySlot(owner.PhysicalSlot):5,start=city?FirstOffset(owner.Category):0;
            var offsets=accessoryResources.GetOffsets(style);
            var root=owner.Instance.SourceWorldState!;float heading=owner.Gate?.Definition.Heading70??0;int requested=city?4:2;
            units.Add(new(owner.Index,owner.Identity+"/accessory",force,requested,style,start,root.Position18,root.Quaternion24,heading,pane,material));
            for(int ordinal=0;ordinal<requested;ordinal++){
                if(start+ordinal>=offsets.Count){missing.Add(new(owner.Identity,"manager-offset","Actual current source offset unavailable."));break;}
                var offset=offsets[start+ordinal];var pose=city?CityPose(accessoryRoot,root.Position18,offset,geometry):GatePose(accessoryRoot,root.Position18,root.Quaternion24,heading,offset,geometry);
                placements.Add((owner.Index,ordinal,pose,MapModelNativeBoundsSources.BuildSingleNode(accessoryBounds,pose,false,false)));
            }
        }
        byte[] primary=frame.Unavailable.Count==0&&required.Length==0?bank.Encode(frame,commonFrustumMode):[];
        byte[] accessories=units.Count==61&&placements.Count==224&&missing.Count==0?EncodeAccessories():[];
        current=new(frame,damage.AsReadOnly(),units.AsReadOnly(),Array.AsReadOnly(required),missing.AsReadOnly(),primary,accessories);
        lastWorld=world;lastNodes=actualNodes;lastGates=actualGateDefinitions;lastMode=commonFrustumMode;lastScenarioPreview=scenarioPreview;
        lastCities=world.Cities;lastGateStates=world.Gates;lastForces=world.Forces;lastDistricts=world.Districts;lastOfficers=world.Officers;
        lastTerritory=world.Territory;lastAreas=world.Areas;lastParameters=world.NativeArmySimulation?.Parameters;
        lastNativeForces=world.NativeArmySimulation?.Forces;lastStructures=world.Structures;return current;
        D3D11CityGateDamage? Read(string id,BaseReference reference,int? hp,bool city){try{
            if(scenarioPreview)
            {
                var visual=native.ReadNewGameBaseVisualPostLoad(reference);
                var created=new D3D11CityGateDamage(id,visual.Durability,visual.Capacity,visual.Flag66,visual.Force)
                { Morale44=visual.Morale44,MoraleCapacity=visual.MoraleCapacity };
                damage.Add(created);return created;
            }
            if(hp is null||BasePostLoad.ReadLinkedField14(reference,native)!=hp.Value)
                throw new InvalidDataException("Current Object14/World HP unavailable or inconsistent.");
            int capacity=BasePostLoad.ReadCapacities(reference,native).Field46;byte flag=BasePostLoad.ReadFlag66(reference,native);
            int force=city?native.ReadBaseForceSlot(reference)??throw new InvalidDataException("Current City District force unavailable."):CurrentGateMapQueries.ReadForceSlot(reference,native);
            var morale=native.ReadBaseMorale44(reference);
            var moraleForce=native.ReadForceTableReference(force);
            var row=new D3D11CityGateDamage(id,hp.Value,capacity,flag,force)
            {
                Morale44=morale,
                MoraleCapacity=moraleForce is not null ? BasePostLoad.ReadMoraleUpper(moraleForce,native) : null
            };
            damage.Add(row);return row;
        }catch(InvalidDataException error){missing.Add(new(id,"current-damage",error.Message));return null;}}
        byte[] EncodeAccessories(){using var output=new MemoryStream();using var w=new BinaryWriter(output);w.Write(units.Count);w.Write(placements.Count);
            foreach(var u in units){w.Write(u.Index);w.Write(u.Force);w.Write(u.Requested);w.Write(u.Position.X);w.Write(u.Position.Y);w.Write(u.Position.Z);}
            foreach(var p in placements){w.Write(p.Index);w.Write(p.Ordinal);V(new(p.Root.Scale34,0));var q=p.Root.Quaternion24;V(new(q.X,q.Y,q.Z,q.W));V(new(p.Root.Position18,1));
                V(p.Bounds.Center0);V(p.Bounds.Basis10);V(p.Bounds.Basis20);V(p.Bounds.Basis30);V(p.Bounds.HalfExtents40);}return output.ToArray();
            void V(Vector4 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);w.Write(v.W);}}
    }
    // Same original1CDB90/1CE0F0 setters as the existing verified lab adapter.
    static UnitWorldTransformState CityPose(UnitWorldTransformState node,Vector3 p,Vector4 offset,UnitGeometry geometry){
        var q=geometry.NativeQuaternion1C58E0(new(0,-1.5707964f,0));return node.PublishScale(Vector3.One).PublishPosition(new(p.X+offset.X,p.Y+offset.Y,p.Z+offset.Z)).PublishQuaternion(new(q.X,q.Y,q.Z,q.W));}
    static UnitWorldTransformState GatePose(UnitWorldTransformState node,Vector3 p,Quaternion q,float heading,Vector4 offset,UnitGeometry geometry){
        var moved=ArmyAnimationPoseMath.Rotate(q,offset);float yaw=(heading-90f)*BitConverter.Int32BitsToSingle(0x40490FDB);yaw/=180f;
        var rotation=geometry.NativeQuaternion1C58E0(new(0,yaw,0));return node.PublishScale(Vector3.One).PublishPosition(new(p.X+moved.X,p.Y+moved.Y,p.Z+moved.Z)).PublishQuaternion(new(rotation.X,rotation.Y,rotation.Z,rotation.W));}
    static int FirstOffset(int category)=>category switch{15 or 16 or 21 or 22 or 31 or 32 or 39 or 40=>8,17 or 18 or 27 or 28 or 33 or 34 or 41 or 42=>4,19 or 20 or 45 or 46=>12,43 or 44=>16,_=>0};
    static float F(byte[] b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at,4));
}
