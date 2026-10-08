using System.Buffers.Binary;
using System.Numerics;
using San14.Core;
namespace San14.Data;

public sealed record OriginalD3D11AreaCapitalVisual(int NativeAreaSlot,string Identity,int Category,int RepresentativeHex,
    ushort SourceCreatedHeading70,int CurrentForce,OriginalMapStaticModelInstance Primary,
    OriginalMapModelNativeFreshVisibilityPrefix Prefix,OriginalMapModelNativeOrientedBounds Bounds,
    OriginalDeferredEarlyShadowCurrentWorldCallerFields Caller,float Opacity,
    OriginalUnitWorldTransformState AccessoryRoot,OriginalMapModelNativeOrientedBounds AccessoryBounds,
    OriginalArmyFlagPaneNameSelection? CurrentPaneName);
public sealed record OriginalD3D11AreaCapitalFrame(IReadOnlyList<OriginalD3D11AreaCapitalVisual> Owners,
    IReadOnlyList<int> RegisteredAreas,IReadOnlyList<string> Excluded,
    IReadOnlyList<OriginalD3D11CityGateUnavailable> Unavailable)
{
    public int RequestedAccessories=>Owners.Count;
    public bool RequestsAreActualDraws=>false;
}

// Actual Area registry ->223EE0->CUnitCapital. The source-owned created unit
// retains its bound Area, P/Q and definition scale. Normal force reads use
// current Hex14. This is independent of the fixed51city/10gate GPU packet.
public sealed class OriginalD3D11AreaCapitalSource
{
    readonly OriginalGameRepository repo;readonly LoadedSceneResources scene;
    readonly LoadedScenario scenario;readonly OriginalMapStaticModelAssetCatalog assets;
    readonly OriginalMapStaticModelGroundHeightSource ground;
    readonly LoadedOriginalCityGateAccessoryAnimation accessory;
    readonly LoadedOriginalArmyFlagPaneNames names;
    readonly OriginalUnitGeometry geometry=new();
    readonly Dictionary<int,float> heightCache=[];
    readonly Dictionary<int,OriginalMapStaticModelAsset> templates=[];
    readonly Dictionary<int,OriginalD3D11AreaCapitalVisual> created=[];
    readonly OriginalMapModelNativePreparedBounds accessoryBounds;
    int[]? registered;string[] excluded=[];
    OriginalD3D11CityGateUnavailable[] sourceUnavailable=[];
    public IReadOnlyDictionary<int,OriginalMapStaticModelAsset> SourceTemplates=>templates;
    public OriginalD3D11AreaCapitalSource(OriginalGameRepository repository,LoadedSceneResources currentScene,LoadedScenario source){
        repo=repository;scene=currentScene;scenario=source;assets=new(repo,scene);
        ground=OriginalMapStaticModelGroundHeightSource.PrepareCurrentField(repo,scene,
            OriginalUnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
        accessory=LoadedOriginalCityGateAccessoryAnimation.Load(repo);names=OriginalArmyFlagPaneNames.Load(source);
        geometry.MaterializeOriginXImageSeed();geometry.InitializeOriginZ();
        var h=accessory.Model.Geometry.RawGeometryHeader;
        accessoryBounds=OriginalMapModelNativeBoundsSources.PrepareHeader(new(F(h,20),F(h,24),F(h,28)),new(F(h,32),F(h,36),F(h,40)),
            accessory.Model.Source.ResourceUri,accessory.Model.Source.DecodedSha256);
    }
    /// <summary>The caller borrows one formal current World context on a World
    /// change. This source never creates a simulation or per-camera context.</summary>
    public OriginalD3D11AreaCapitalFrame Capture(OriginalEventInitializationContext current){
        var missing=new List<OriginalD3D11CityGateUnavailable>();
        if(registered is null){CreateSourceOwned(current,missing);sourceUnavailable=missing.ToArray();}
        else missing.AddRange(sourceUnavailable);
        var owners=new List<OriginalD3D11AreaCapitalVisual>();
        foreach(var held in created.Values){var area=new OriginalAreaReference(held.NativeAreaSlot);
            try{
                if(current.ReadAreaNativeValidity(area)!=true)throw new InvalidDataException("Previously created Area owner validity is unknown/removed; actual retirement caller required.");
                int hex=current.ReadAreaHexIndex(area)??throw new InvalidDataException("Current bound Area3A is unknown.");
                if(hex!=held.RepresentativeHex)throw new InvalidDataException("Changed fixed Area3A requires actual CUnitCapital rebind/P source writer.");
                int force=current.ReadAreaCurrentForceSlot(area)??throw new InvalidDataException("Current Area3A->Hex14 force is unknown.");
                OriginalArmyFlagPaneNameSelection? pane=force is >=1 and <=51?names.Resolve(current,
                    current.ReadForceTableReference(force)??throw new InvalidDataException("Current capital Force pointer is unknown.")):null;
                owners.Add(held with{CurrentForce=force,CurrentPaneName=pane});
            }catch(InvalidDataException e){missing.Add(new(held.Identity,"current-capital",e.Message));}
        }
        return new(owners.AsReadOnly(),Array.AsReadOnly(registered!),Array.AsReadOnly(excluded),missing.AsReadOnly());
    }
    void CreateSourceOwned(OriginalEventInitializationContext current,List<OriginalD3D11CityGateUnavailable> missing){
        // Original31F2D0 registration runs over fixed Area34-valid receivers.
        // The501 value is the source table extent, never a rendered count.
        var registration=current;
        if(current.ReadRegisteredAreaReferences() is null){
            // A separate once roster owner borrows only the actual fixed
            // Area pointers/34. It cannot replace the Army Context's pool.
            registration=new OriginalEventInitializationContext();
            var pool=new OriginalCurrentListPool(registration,1,501);
            registration.SetRegistryDescriptor(OriginalRegistryKind.Area,new(pool.AllocateDescriptor().Identity));
            for(int slot=0;slot<501;slot++){
                var pointer=current.ReadAreaTableReference(slot)??throw new InvalidDataException("Actual fixed Area table pointer unknown.");
                registration.SetAreaTablePointer(slot,pointer.PhysicalSlot);
                if(pointer.PhysicalSlot is int physical)registration.SetAreaValidityCode(physical,current.ReadAreaValidityCode(pointer));
                _=OriginalObjectRegistration.Register(registration.ReadRegistryTableReference(OriginalRegistryKind.Area,slot),registration);
            }
        }
        registered=(registration.ReadRegisteredAreaReferences()??throw new InvalidDataException("Actual registered Area chain is unknown."))
            .Select(a=>a?.PhysicalSlot??throw new InvalidDataException("Registered Area contains a null payload.")).ToArray();
        var excludedRows=new List<string>();
        foreach(int slot in registered){var area=new OriginalAreaReference(slot);string identity="capital-area:"+slot;
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
                var pose=OriginalUnitWorldTransform.ConstructDefinition(asset.TextureResources).PublishPosition(new(p.X,height,p.Z))
                    .PublishQuaternion(new(q.X,q.Y,q.Z,q.W));
                var locals=OriginalMapStaticModelPrimitiveLocalSource.Prepare(asset);
                var instance=new OriginalMapStaticModelInstance(identity,asset,pose.ShaderTransform(),asset.Draws.ToDictionary(d=>d.Index,
                    d=>locals[d.Index].Rows is {} rows?pose.ShaderTransformWithPrimitiveLocalRows(rows,locals[d.Index].EvidenceUri,locals[d.Index].EvidenceSha256):pose.ShaderTransform())){
                    SourceWorldState=pose,CurrentSourceIdentity="source-owned-current/"+identity,
                    NativePublication=OriginalMapStaticModelPublication.CurrentUnparented("source-owned-current/"+identity,pose,true)}.Snapshot();
                var prefix=OriginalMapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,new(instance.CurrentSourceIdentity!,false,true,true));
                var bounds=OriginalMapModelNativeBoundsSources.FromSourceWorld(instance,prefix.BoundsPublication);
                var caller=OriginalDeferredEarlyShadowCasterAdmissionSource.LoadStatic(asset).CaptureRetainedFresh();
                float opacity=OriginalMapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,instance.CurrentSourceIdentity!).ReadSlot(0);
                // 1CD8E0: shared source category2, count1, P=unitP, scale2.
                // Raw sin/cos(-pi/4) normalize precedes the World.v70 setter.
                var sc=OriginalNativeTrigonometry.SinCos014590(BitConverter.Int32BitsToSingle(unchecked((int)0xBF490FDB)));
                float length=MathF.Sqrt(sc.Sin*sc.Sin+0f+0f+sc.Cos*sc.Cos);float inv=1f/length;
                var accessoryRoot=accessory.DefinitionRoot.PublishScale(new(2)).PublishPosition(pose.Position18)
                    .PublishQuaternion(new(0*inv,sc.Sin*inv,0*inv,sc.Cos*inv));
                created.Add(slot,new(slot,identity,category,hex,heading,0,instance,prefix,bounds,caller,opacity,accessoryRoot,
                    OriginalMapModelNativeBoundsSources.BuildSingleNode(accessoryBounds,accessoryRoot,false,false),null));
            }catch(Exception e)when(e is InvalidDataException or NotSupportedException){missing.Add(new(identity,"capital-source",e.Message));}
        }
        excluded=excludedRows.ToArray();
    }
    OriginalMapStaticModelAsset Template(int category){
        if(templates.TryGetValue(category,out var held))return held;
        uint definition=category==59?0x17464C92u:0x13D7C146u;
        var dictionary=scene.Dependencies.Single(d=>d.Resource.Source.FileId==0xE755DAEE).Resource;
        var resources=OriginalModelTextureResources.LoadForDefinition(repo,dictionary,scene.ModelCatalog,definition);
        // Preserve the original WorldPQ catalog fields even though the generic
        // reference API loads the common immutable model/mesh/texture chain.
        var asset=assets.LoadReference(new(dictionary,resources.ModelObject,resources.Model.Source.FileId),"capital-category:"+category)
            with{TextureResources=resources};templates.Add(category,asset);return asset;
    }
    static float F(byte[] b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(at,4));
}

