using San14.Core;
using San14.Data;

/// <summary>A complete source-selected facility and connection roster. Resource
/// preparation is distinct from installation in an accepted native map frame.</summary>
public sealed record D3D11FacilityPublication(ulong Revision,FacilitySceneModelFrame Source,
    ReadOnlyMemory<byte> Packet)
{
    public IReadOnlyList<FacilitySceneModel> Rows=>Source.Rows;
    public IReadOnlyList<MapStaticModelInstanceFailure> Missing=>Source.Missing;
    public bool IsReady=>Missing.Count==0&&Packet.Length!=0;
}

/// <summary>Uses the normal primary template bank and frame carrier. Connection
/// inputs belong to their real creation requests, never inferred map adjacency.</summary>
public sealed class D3D11FacilityPublisher
{
    readonly D3D11CityGateTemplateBank bank;
    readonly MapStaticModelAssetCatalog assets;
    readonly UnitGeometry geometry=new();
    readonly MapStaticModelGroundHeightSource ground;
    readonly Func<GameState,MapStaticModelAssetCatalog,UnitGeometry,MapStaticModelGroundHeightSource,
        IReadOnlyList<FacilitySceneConnection>,FacilitySceneModelFrame> capture;
    GameState? lastWorld;
    FacilitySceneModelFrame? lastCapturedSource;
    IReadOnlyList<FacilitySceneConnection>? lastConnections;
    uint lastMode;ulong revision,combinedRevision;
    D3D11FacilityPublication? current,lastCombinedFacilities;
    D3D11CityGatePublication? lastCities,combined;

    public D3D11FacilityPublisher(GameResourceRepository repository,LoadedSceneResources scene,
        D3D11CityGateTemplateBank preparedBank,
        Func<GameState,MapStaticModelAssetCatalog,UnitGeometry,MapStaticModelGroundHeightSource,
            IReadOnlyList<FacilitySceneConnection>,FacilitySceneModelFrame> actualCapture)
    {
        bank=preparedBank;capture=actualCapture;assets=new(repository,scene);
        geometry.MaterializeOriginXImageSeed();geometry.InitializeOriginZ();
        ground=MapStaticModelGroundHeightSource.PrepareCurrentField(repository,scene,
            UnitWorldGroundHeightFieldPreparationBranch.RetainedWrapperCreated,true,[]);
    }

    public D3D11FacilityPublication Capture(GameState world,uint commonFrustumMode,
        IReadOnlyList<FacilitySceneConnection> actualConnections)
    {
        if(current is not null&&ReferenceEquals(world,lastWorld)&&
            ReferenceEquals(actualConnections,lastConnections)&&lastMode==commonFrustumMode)return current;
        var source=capture(world,assets,geometry,ground,actualConnections);
        if(current is not null&&ReferenceEquals(source,lastCapturedSource)&&lastMode==commonFrustumMode)
        {lastWorld=world;lastConnections=actualConnections;return current;}
        var missing=new List<MapStaticModelInstanceFailure>(source.Missing);
        var owners=new List<D3D11CityGatePrimary>(source.Rows.Count);
        foreach(var row in source.Rows)
        {
            try
            {
                var instance=row.Instance;
                if(bank.Bind(instance.Asset,row.Category) is null)
                    throw new InvalidDataException("Actual facility category has no prepared primary resource.");
                var prefix=MapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,
                    new(instance.CurrentSourceIdentity!,false,true,true));
                var caller=DeferredEarlyShadowCasterAdmissionSource.LoadStatic(instance.Asset).CaptureRetainedFresh();
                if(!CeFacilityModels.UsesProjectedShadow(row.Category))caller=caller with
                {
                    WorldCasterFlags10C=int.MinValue,
                    CurrentWriterEvidence=caller.CurrentWriterEvidence+"; actual facility category projected-shadow0"
                };
                var opacity=MapModelNativeCurrentVisibilitySources.ConstructFresh(prefix,instance.CurrentSourceIdentity!).ReadSlot(0);
                owners.Add(new(owners.Count,row.Identity,row.ReceiverId,row.ReceiverId,row.Category,instance,prefix,
                    MapModelNativeBoundsSources.FromSourceWorld(instance,prefix.BoundsPublication),caller,opacity,null,null));
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {missing.Add(new(row.Identity,error.Message));}
        }
        var published=source with{Missing=missing.AsReadOnly()};
        current=new(++revision,published,missing.Count==0?bank.EncodeFacilities(owners,commonFrustumMode):ReadOnlyMemory<byte>.Empty);
        lastCapturedSource=source;
        lastWorld=world;lastConnections=actualConnections;lastMode=commonFrustumMode;
        return current;
    }

    public D3D11CityGatePublication Combine(D3D11CityGatePublication cities,D3D11FacilityPublication facilities)
    {
        if(ReferenceEquals(cities,lastCities)&&ReferenceEquals(facilities,lastCombinedFacilities)&&combined is not null)
            return combined;
        combinedRevision=checked(Math.Max(combinedRevision,cities.Revision)+1);
        combined=new(combinedRevision,cities.Source,cities.RequiredFlagMaterials,facilities);
        lastCities=cities;lastCombinedFacilities=facilities;
        return combined;
    }
}
