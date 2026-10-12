using System.Buffers.Binary;
using System.Numerics;
using San14.Core;
namespace San14.Data;

public sealed record D3D11AreaCapitalVisual(int NativeAreaSlot,string Identity,int Category,int RepresentativeHex,
    ushort SourceCreatedHeading70,int CurrentForce,MapStaticModelInstance Primary,
    MapModelNativeFreshVisibilityPrefix Prefix,MapModelNativeOrientedBounds Bounds,
    DeferredEarlyShadowCurrentWorldCallerFields Caller,float Opacity,
    UnitWorldTransformState AccessoryRoot,MapModelNativeOrientedBounds AccessoryBounds,
    ArmyFlagPaneNameSelection? CurrentPaneName);
public sealed record D3D11AreaCapitalFrame(IReadOnlyList<D3D11AreaCapitalVisual> Owners,
    IReadOnlyList<int> RegisteredAreas,IReadOnlyList<string> Excluded,
    IReadOnlyList<D3D11CityGateUnavailable> Unavailable)
{
    public int RequestedAccessories=>Owners.Count;
    public bool RequestsAreActualDraws=>false;
}

// Actual Area registry ->223EE0->CUnitCapital. The source-owned created unit
// retains its bound Area, P/Q and definition scale. Normal force reads use
// current Hex14. This is independent of the fixed51city/10gate GPU packet.
public sealed class D3D11AreaCapitalSource
{
    readonly GameResourceRepository repo;readonly LoadedSceneResources scene;
    readonly LoadedScenario scenario;readonly MapStaticModelAssetCatalog assets;
    readonly MapStaticModelGroundHeightSource ground;
    readonly LoadedCityGateAccessoryAnimation accessory;
    readonly LoadedArmyFlagPaneNames names;
    readonly UnitGeometry geometry=new();
    readonly Dictionary<int,float> heightCache=[];
    readonly Dictionary<int,MapStaticModelAsset> templates=[];
    readonly Dictionary<int,D3D11AreaCapitalVisual> created=[];
    readonly MapModelNativePreparedBounds accessoryBounds;
    int[]? registered;string[] excluded=[];
    D3D11CityGateUnavailable[] sourceUnavailable=[];
    public IReadOnlyDictionary<int,MapStaticModelAsset> SourceTemplates=>templates;
    public D3D11AreaCapitalSource(GameResourceRepository repository,LoadedSceneResources currentScene,LoadedScenario source){
        repo=repository;scene=currentScene;scenario=source;assets=new(repo,scene);
        ground=MapStaticModelGroundHeightSource.PrepareCurrentField(repo,scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
        accessory=LoadedCityGateAccessoryAnimation.Load(repo);names=ArmyFlagPaneNames.Load(source);
        geometry.MaterializeOriginXImageSeed();geometry.InitializeOriginZ();
        var h=accessory.Model.Geometry.RawGeometryHeader;
        accessoryBounds=MapModelNativeBoundsSources.PrepareHeader(new(F(h,20),F(h,24),F(h,28)),new(F(h,32),F(h,36),F(h,40)),
            accessory.Model.Source.ResourceUri,accessory.Model.Source.DecodedSha256);
    }
    /// <summary>The caller borrows one formal current World context on a World
    /// change. This source never creates a simulation or per-camera context.</summary>
    public D3D11AreaCapitalFrame Capture(SourceWorldContext current){
        var missing=new List<D3D11CityGateUnavailable>();
        if(registered is null){CreateSourceOwned(current,missing);sourceUnavailable=missing.ToArray();}
        else missing.AddRange(sourceUnavailable);
        var owners=new List<D3D11AreaCapitalVisual>();
        foreach(var held in created.Values){var area=new AreaReference(held.NativeAreaSlot);
            try{
                if(current.ReadAreaNativeValidity(area)!=true)throw new InvalidDataException("Previously created Area owner validity is unknown/removed; actual retirement caller required.");
                int hex=current.ReadAreaHexIndex(area)??throw new InvalidDataException("Current bound Area3A is unknown.");
                if(hex!=held.RepresentativeHex)throw new InvalidDataException("Changed fixed Area3A requires actual CUnitCapital rebind/P source writer.");
                int force=current.ReadAreaCurrentForceSlot(area)??throw new InvalidDataException("Current Area3A->Hex14 force is unknown.");
                ArmyFlagPaneNameSelection? pane=force is >=1 and <=51?names.Resolve(current,
                    current.ReadForceTableReference(force)??throw new InvalidDataException("Current capital Force pointer is unknown.")):null;
                owners.Add(held with{CurrentForce=force,CurrentPaneName=pane});
            }catch(InvalidDataException e){missing.Add(new(held.Identity,"current-capital",e.Message));}
        }
        return new(owners.AsReadOnly(),Array.AsReadOnly(registered!),Array.AsReadOnly(excluded),missing.AsReadOnly());
    }
    void CreateSourceOwned(SourceWorldContext current,List<D3D11CityGateUnavailable> missing){
        // Original31F2D0 registration runs over fixed Area34-valid receivers.
        // The501 value is the source table extent, never a rendered count.
        var registration=current;
        if(current.ReadRegisteredAreaReferences() is null){
            // A separate once roster owner borrows only the actual fixed
            // Area pointers/34. It cannot replace the Army Context's pool.
            registration=new SourceWorldContext();
            var pool=new WorldIndexContext(registration);
            registration.SetRegistryDescriptor(RegistryKind.Area,new(pool.AllocateDescriptor().Identity));
            for(int slot=0;slot<501;slot++){
                var pointer=current.ReadAreaTableReference(slot)??throw new InvalidDataException("Actual fixed Area table pointer unknown.");
                registration.SetAreaTablePointer(slot,pointer.PhysicalSlot);
                if(pointer.PhysicalSlot is int physical)registration.SetAreaValidityCode(physical,current.ReadAreaValidityCode(pointer));
                _=ObjectRegistration.Register(registration.ReadRegistryTableReference(RegistryKind.Area,slot),registration);
            }
        }
        registered=(registration.ReadRegisteredAreaReferences()??throw new InvalidDataException("Actual registered Area chain is unknown."))
            .Select(a=>a?.PhysicalSlot??throw new InvalidDataException("Registered Area contains a null payload.")).ToArray();
        var excludedRows=new List<string>();
        foreach(int slot in registered){var area=new AreaReference(slot);string identity="capital-area:"+slot;
            try{
                byte city=current.ReadAreaCitySlot(area)??throw new InvalidDataException("Current Area35 is unknown.");
                var cityPointer=current.ReadCityTableReference(city<=51?city:0)??throw new InvalidDataException("Current Area35 City pointer is unknown.");
                if(slot<=0||current.ReadBaseNativeValidity(cityPointer)!=true||current.IsAreaUnitModelSkipped(area)||current.IsAreaUnitModelTerrain17(area)){
                    excludedRows.Add(identity+":223EE0 city validity/representative-city/terrain17 source exclusion");continue;}
                int hex=current.ReadAreaHexIndex(area)??throw new InvalidDataException("Current Area3A unknown.");
                var hexPointer=current.ReadHexTableReference(hex<=48399?hex:0)??throw new InvalidDataException("Current capital Hex pointer unknown.");
                byte surface=current.ReadHexSurfaceCode(hexPointer)??throw new InvalidDataException("Current capital Hex10 unknown.");
                int category=surface==18?59:60;var asset=Template(category);
                ushort heading=BinaryPrimitives.ReadUInt16LittleEndian(Convert.FromHexString(scenario.Snapshot.Areas[slot].StateRemainingBytesHex).AsSpan(1,2));
                var p=geometry.NativeHexPosition187A40(hex);
                if(!heightCache.TryGetValue(hex,out float height)||height<=0){
                    height=ground.Query(p.X,p.Z)??throw new InvalidDataException("1889A0 actual terrain BA9890 height is unknown.");
                    if(height<445)height=445;heightCache[hex]=height;}
                float yaw=heading*BitConverter.Int32BitsToSingle(0x40490FDB);yaw/=180f;
                var q=geometry.NativeQuaternion1C58E0(new(0,yaw,0));
                var pose=UnitWorldTransform.ConstructDefinition(asset.TextureResources).PublishPosition(new(p.X,height,p.Z))
                    .PublishQuaternion(new(q.X,q.Y,q.Z,q.W));
                var locals=MapStaticModelPrimitiveLocalSource.Prepare(asset);
                var instance=new MapStaticModelInstance(identity,asset,pose.ShaderTransform(),asset.Draws.ToDictionary(d=>d.Index,
                    d=>locals[d.Index].Rows is {} rows?pose.ShaderTransformWithPrimitiveLocalRows(rows,locals[d.Index].EvidenceUri,locals[d.Index].EvidenceSha256):pose.ShaderTransform())){
                    SourceWorldState=pose,CurrentSourceIdentity="source-owned-current/"+identity,
                    NativePublication=MapStaticModelPublication.CurrentUnparented("source-owned-current/"+identity,pose,true)}.Snapshot();
                var prefix=MapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,new(instance.CurrentSourceIdentity!,false,true,true));
                var bounds=MapModelNativeBoundsSources.FromSourceWorld(instance,prefix.BoundsPublication);
                var caller=DeferredEarlyShadowCasterAdmissionSource.LoadStatic(asset).CaptureRetainedFresh();
                float opacity=MapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,instance.CurrentSourceIdentity!).ReadSlot(0);
                // 1CD8E0: shared source category2, count1, P=unitP, scale2.
                // Raw sin/cos(-pi/4) normalize precedes the World.v70 setter.
                var sc=NativeTrigonometry.SinCos014590(BitConverter.Int32BitsToSingle(unchecked((int)0xBF490FDB)));
                float length=MathF.Sqrt(sc.Sin*sc.Sin+0f+0f+sc.Cos*sc.Cos);float inv=1f/length;
                var accessoryRoot=accessory.DefinitionRoot.PublishScale(new(2)).PublishPosition(pose.Position18)
                    .PublishQuaternion(new(0*inv,sc.Sin*inv,0*inv,sc.Cos*inv));
                created.Add(slot,new(slot,identity,category,hex,heading,0,instance,prefix,bounds,caller,opacity,accessoryRoot,
                    MapModelNativeBoundsSources.BuildSingleNode(accessoryBounds,accessoryRoot,false,false),null));
            }catch(Exception e)when(e is InvalidDataException or NotSupportedException){missing.Add(new(identity,"capital-source",e.Message));}
        }
        excluded=excludedRows.ToArray();
    }
    MapStaticModelAsset Template(int category){
        if(templates.TryGetValue(category,out var held))return held;
        var asset=assets.LoadCapitalCategory(category);templates.Add(category,asset);return asset;
    }
    static float F(byte[] b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at,4));
}
