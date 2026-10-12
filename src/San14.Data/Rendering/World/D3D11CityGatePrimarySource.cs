using System.Buffers.Binary;
using System.Numerics;
using San14.Core;

namespace San14.Data;

public sealed record D3D11GateDefinition(int NativeSlot,int WorldSlot,int ObjectSlot,int HexId,int AreaSlot,ushort Heading70);
public sealed record D3D11GateCurrentBinding(D3D11GateDefinition Definition,byte Gate66,int Category);
public sealed record D3D11CityGatePrimary(int Index,string Identity,int PhysicalSlot,int NativeSlot,int Category,
    MapStaticModelInstance Instance,MapModelNativeFreshVisibilityPrefix Prefix,
    MapModelNativeOrientedBounds Bounds,DeferredEarlyShadowCurrentWorldCallerFields Caller,float Opacity,
    CityVisualSelection? CitySelection,D3D11GateCurrentBinding? Gate);
public sealed record D3D11CityGatePrimaryFrame(IReadOnlyList<D3D11CityGatePrimary> Owners,
    IReadOnlyList<MapStaticModelInstanceFailure> Unavailable,IReadOnlyList<string> Excluded);

/// <summary>The existing original primary factory/LOCAL/OBB prefix, restricted
/// to51 City and10 Gate owners. Actual City66/Gate66 writers are mandatory;
/// source Area70 is only the retained gate heading, never its damage selector.</summary>
public sealed class D3D11CityGatePrimarySource
{
    readonly UnitGeometry geometry=new();
    readonly MapStaticModelGroundHeightSource ground;
    readonly CityVisualAssetCatalog cities;
    readonly Dictionary<int,D3D11GateDefinition> gates=[];
    readonly Dictionary<string,(int Category,int Hex,MapStaticModelPublication Node)> nodes=[];
    readonly Dictionary<string,(MapModelNativeFreshVisibilityPrefix Prefix,float Opacity,
        DeferredEarlyShadowCurrentWorldCallerFields Caller)> visibility=[];
    public MapStaticModelAssetCatalog Assets{get;}
    public CeCityVariantTable Variants{get;}
    public CityVisualDescriptorOwner Descriptors{get;}
    public IReadOnlyDictionary<int,D3D11GateDefinition> GateDefinitions=>gates;

    public D3D11CityGatePrimarySource(GameResourceRepository repository,LoadedSceneResources scene,LoadedScenario scenario,
        LoadedMap map,NewGameOptions options)
    {
        Variants=CeCityVariantTable.LoadCurrent(scenario.Snapshot.Locale);
        Descriptors=CityOpeningPostLoadSource.LoadSourceCreatedOrdinary(repository,scenario,map,options,Variants)
            .CreateCurrentDescriptorOwner(Variants);
        Assets=new(repository,scene);cities=new(repository,scene,Variants);
        geometry.MaterializeOriginXImageSeed();geometry.InitializeOriginZ();
        ground=MapStaticModelGroundHeightSource.PrepareCurrentField(repository,scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
        var context=new SourceWorldContext();context.InitializeStaticTablePointers();context.ResetCityObjectLinks();context.MaterializeStaticSource(scenario);
        ObjectLinkReconstruction.ApplyRegisteredObjects(scenario.Snapshot.ObjectSourceRecords!.Objects
            .Select(o=>new Formats.S14ConditionNativeObject(o.PhysicalSlot,o.RawField10,o.RawField12)).ToArray(),1,context.WriteObjectLink);
        foreach(var cell in map.Map.Cells){context.SetHexTablePointer(cell.Id,cell.Id);context.SetHexAreaSlot(cell.Id,cell.AreaSlot);}
        foreach(var area in scenario.Snapshot.Areas){context.SetAreaTablePointer(area.Slot,area.Slot);context.SetAreaValidityCode(area.Slot,area.CitySlot);context.SetAreaHexIndex(area.Slot,area.HexId);}
        foreach(var gate in scenario.Snapshot.Gates.Where(g=>g.Slot!=0)){
            var reference=new BaseReference(BaseKind.Gate,gate.Slot);
            int objectSlot=context.ReadBaseObjectLink(reference)??throw new InvalidDataException("Gate4E source binding missing.");
            int hex=CurrentGateMapQueries.FindHex(reference,context).PhysicalSlot??throw new InvalidDataException("Gate Hex source binding missing.");
            int area=CurrentGateMapQueries.FindArea(reference,context).PhysicalSlot??throw new InvalidDataException("Gate Area source binding missing.");
            if(gate.HexId!=hex||scenario.Snapshot.Areas[area].HexId!=hex)throw new InvalidDataException("Gate Object/Hex/Area source identities disagree.");
            byte[] state=Convert.FromHexString(scenario.Snapshot.Areas[area].StateRemainingBytesHex);
            gates.Add(gate.LocationSlot,new(gate.Slot,gate.LocationSlot,objectSlot,hex,area,BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(1,2))));
        }
    }
    public D3D11CityGatePrimaryFrame Prepare(GameState world,IReadOnlyDictionary<int,CityVisualDamageInput> actualCity66,
        IReadOnlyDictionary<int,D3D11GateCurrentBinding> actualGate66,
        IReadOnlyDictionary<string,MapStaticModelPublication>? currentNodes=null)
    {
        var cityInputs=new List<MapStaticModelCityInput>();var gateInputs=new List<MapStaticModelGateInput>();
        var missing=new List<MapStaticModelInstanceFailure>();
        foreach(var city in world.Cities.Values.OrderBy(c=>c.Id)){
            string id="city:"+city.Id;if(!actualCity66.TryGetValue(city.Id,out var writer)){missing.Add(new(id,"Actual current City66 unavailable."));continue;}
            var state=CityVisualState.FromCity(city);Descriptors.PublishCurrentDescriptor(Variants,state,writer);
            var selected=Descriptors.SelectCurrentDescriptor(Variants,state);
            if(selected.Selection is not {} selection){missing.Add(new(id,selected.UnavailableReason!));continue;}
            cityInputs.Add(new(selection,Node(id,selection.Category,selection.HexId)));
        }
        foreach(var gate in world.Gates.Values.OrderBy(g=>g.Id)){
            string id="gate:"+gate.Id;if(!actualGate66.TryGetValue(gate.Id,out var binding)){missing.Add(new(id,"Actual current Gate66 unavailable."));continue;}
            if(gate.HexId!=binding.Definition.HexId){missing.Add(new(id,"Changed Gate physical Hex needs actual source rebind."));continue;}
            gateInputs.Add(new(gate.Id,gate.HexId,binding.Category,binding.Definition.Heading70,Node(id,binding.Category,binding.Definition.HexId)));
        }
        var result=MapStaticModelInstanceFactory.FromNativePrimaryPopulation(Assets,cities,cityInputs,gateInputs,[],geometry,ground);
        missing.AddRange(result.Unresolved);var current=new List<D3D11CityGatePrimary>();
        foreach(var instance in result.Instances){bool city=instance.CitySelection is not null;int slot=int.Parse(instance.InstanceKey.Split(':')[1]);
            var gate=city?null:actualGate66[slot];int category=instance.CitySelection?.Category??gate!.Category;
            string key=instance.InstanceKey+"/"+instance.Asset.SourceKey;
            if(!visibility.TryGetValue(key,out var held)){
                var prefix=MapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,new(instance.CurrentSourceIdentity!,false,true,true));
                var caller=DeferredEarlyShadowCasterAdmissionSource.LoadStatic(instance.Asset).CaptureRetainedFresh();
                if(!ArmyGraphicsRules.UsesProjectedShadow(category))caller=caller with{WorldCasterFlags10C=int.MinValue,
                    CurrentWriterEvidence=caller.CurrentWriterEvidence+";CUnit1CBA20 category44=0->1C5360 World10C=80000000"};
                visibility.Add(key,held=(prefix,MapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,instance.CurrentSourceIdentity!).ReadSlot(0),caller));
            }
            current.Add(new(slot-1,instance.InstanceKey+"/primary",slot,city?slot:gate!.Definition.NativeSlot,category,instance,held.Prefix,
                MapModelNativeBoundsSources.FromSourceWorld(instance,held.Prefix.BoundsPublication),held.Caller,held.Opacity,instance.CitySelection,gate));
            var retained=MapStaticModelPublication.CurrentUnparented(instance.CurrentSourceIdentity!,instance.SourceWorldState!,instance.NativePublication!.SourceDefaultLocalPoseIsCurrent)
                with{CurrentPrimitiveLocalRows=instance.NativePublication.CurrentPrimitiveLocalRows};
            nodes[instance.InstanceKey]=(category,instance.CitySelection?.HexId??gate!.Definition.HexId,retained);
        }
        var excluded=result.ExcludedInstanceKeys.Concat(world.Structures?.Values.OrderBy(s=>s.Id).Select(s=>$"structure:{s.Id}: outside City/Gate visual scope")??[]).ToArray();
        return new(current.AsReadOnly(),missing.AsReadOnly(),excluded);
        MapStaticModelPublication Node(string id,int category,int hex){
            if(currentNodes?.TryGetValue(id,out var explicitNode)==true)return explicitNode;
            if(nodes.TryGetValue(id,out var held)&&held.Category==category){if(held.Hex==hex)return held.Node;
                var p=geometry.NativeHexPosition187A40(hex);return held.Node with{CurrentTransform=held.Node.CurrentTransform!.PublishPosition(new(p.X,held.Node.CurrentTransform.Position18.Y,p.Z))};}
            return MapStaticModelPublication.FreshUnparented("source-owned-current/"+id+"/primary");
        }
    }
}
