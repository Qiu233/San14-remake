namespace San14.Data;

/// <summary>Explicit CB7A70 inputs after the caller has resolved its current
/// Aux/Camera controls. These are not inferred from a source declaration.</summary>
public sealed record DirectionalProviderControls(int FirstIndex, int PairFirstIndex,
    int PairSecondIndex, float ScalarFirst, float ScalarSecond, float LocationX, float LocationZ,
    bool TemporaryDirectionalAvailable, bool ExternalCameraOverridePresent);

public sealed record DirectionalProviderSelection(uint RegisteredRootObjectId,
    uint SelectedObjectId, string NativeBranch, byte? SpatialIndex);

/// <summary>Root registration and selected shader-data ownership remain distinct.
/// CE35F0 selects first; CE3870 copies the selected CPU+50 and then applies CBC770.</summary>
public sealed class DirectionalProviderCurrentFrame
{
    private readonly MapDirectionalLightProviderOwner owner;
    internal DirectionalProviderCurrentFrame(MapDirectionalLightProviderOwner source,
        DirectionalLightConstructionPacket root, DirectionalProviderControls input,
        DirectionalProviderSelection selection, DirectionalLightConstructionPacket selected,
        ReadOnlySpan<byte> preparedCamera)
    {
        owner=source;RegisteredRoot=root;Selection=selection;SelectedConstruction=selected;
        CurrentCamera=DirectionalLightConstruction.ApplyCurrentCamera(selected,preparedCamera);
    }
    public DirectionalLightConstructionPacket RegisteredRoot {get;}
    public DirectionalLightConstructionPacket SelectedConstruction {get;}
    public DirectionalProviderSelection Selection {get;}
    public DirectionalLightCurrentCameraPacket CurrentCamera {get;}
    public string ProviderSourceProvenance=>owner.SourceProvenance;
}

/// <summary>Actual seasonal provider ListContainer, typed selector, and raw
/// selector-map CPU owner. Handles identify this remake's owned CPU data; they
/// are neither original pointers nor GPU RIDs. No original runtime readiness is claimed.</summary>
public sealed partial class MapDirectionalLightProviderOwner
{
    private static long nextHandle;
    private readonly MapSeasonDirectionalLightSourceSet source;
    private readonly Dictionary<uint, DirectionalLightSelectorMap> selectorMaps;
    private readonly Dictionary<uint,DirectionalLightResourceBinding> bindings;
    private readonly Dictionary<uint,DirectionalLightConstructionPacket> children=[];
    private MapDirectionalLightProviderOwner(MapSeasonDirectionalLightSourceSet value,
        Dictionary<uint, DirectionalLightSelectorMap> maps, string mapSources)
    {
        source=value;selectorMaps=maps;
        SourceProvenance=$"owned-Directional-provider-CPU:{Guid.NewGuid():N};{source.Source.ResourceUri}@{source.Source.DecodedSha256};raw-maps={mapSources}";
        bindings=source.Roots.ToDictionary(r=>r.Root.ObjectId,r=>new DirectionalLightResourceBinding(
            r.ProviderListObjectId,checked((ulong)Interlocked.Increment(ref nextHandle)),SourceProvenance,true,
            MapSeasonDirectionalLightSources.ListContainerClassId));
    }
    public string SourceProvenance {get;}
    public string SourceUri=>source.Source.ResourceUri;
    public string SourceSha256=>source.Source.DecodedSha256;
    public static MapDirectionalLightProviderOwner Load(GameResourceRepository repository,
        MapSeasonDirectionalLightSourceSet season)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(season);
        var resources = new Dictionary<uint, (byte[] Bytes, string Identity)>();
        var maps = new Dictionary<uint, DirectionalLightSelectorMap>();
        foreach(var r in season.Roots)
        {
            var s=r.Selector;
            if (s.MapObjectKey == 0)
                throw new NotSupportedException("Directional selector without a raw map requires an actual grid content writer.");
            if(s.XPoints.Count!=0||s.ZPoints.Count!=0||r.Children.Count!=4)
                throw new NotSupportedException("Directional provider needs another explicit selector source consumer.");
            if (!resources.TryGetValue(s.MapObjectKey, out var resource))
            {
                var asset = repository.LoadRdbAsset("RRPreview", s.MapObjectKey);
                resource = (asset.Bytes.ToArray(), $"{asset.ResourceUri}@{asset.DecodedSha256}");
                resources.Add(s.MapObjectKey, resource);
            }
            maps.Add(r.Root.ObjectId, new DirectionalLightSelectorMap(s, resource.Bytes));
        }
        return new(season, maps, string.Join(",", resources.Values.Select(r => r.Identity)));
    }
    public DirectionalLightResourceBinding GetBinding(uint registeredRootObjectId)
        =>bindings.TryGetValue(registeredRootObjectId,out var b)?b:
            throw new InvalidDataException("Directional provider binding requires an actual seasonal root.");

    /// <summary>Only a fully owned actual child constructor is registered ready.
    /// Absence is an implementation boundary, not an assertion of native null CPU.</summary>
    public void RegisterReadyChild(DirectionalLightConstructionPacket child)
    {
        ArgumentNullException.ThrowIfNull(child);ValidateSource(child);
        if(!source.Roots.SelectMany(r=>r.Children).Any(c=>c.ObjectId==child.SourceObjectId)||child.Metadata.ProviderObjectKey!=0)
            throw new InvalidDataException("Ready Directional child does not belong to this actual provider graph.");
        if(children.ContainsKey(child.SourceObjectId))throw new InvalidDataException("Ready Directional child generation is already registered.");
        children.Add(child.SourceObjectId,child);
    }
    public DirectionalProviderSelection Select(DirectionalLightConstructionPacket root,
        DirectionalProviderControls input)
    {
        ArgumentNullException.ThrowIfNull(root);ArgumentNullException.ThrowIfNull(input);
        ValidateRootBinding(root);
        var provider=source.Roots.Single(r=>r.Root.ObjectId==root.SourceObjectId);
        DirectionalProviderSelection Root(string branch,byte? index=null)=>new(root.SourceObjectId,root.SourceObjectId,branch,index);
        DirectionalProviderSelection Child(int index,string branch,byte? spatial=null)
        {
            uint id=provider.Children[index-1].ObjectId;
            if(!children.ContainsKey(id))throw new NotSupportedException($"Selected Directional child {id:X8} needs its actual owned ready CPU constructor.");
            return new(root.SourceObjectId,id,branch,spatial);
        }
        // CB7B0E..34 precedes all selector/current Camera overrides.
        if(input.FirstIndex>0&&input.FirstIndex<5)return Child(input.FirstIndex,"CB7B18 direct ready child");
        if(input.FirstIndex==0||(input.PairFirstIndex==0&&input.PairSecondIndex<0))return Root("CB7B3C..71 selector bypass");
        if(input.ExternalCameraOverridePresent)
            throw new NotSupportedException("Current Camera v4C0 external provider override requires its own typed selection owner.");
        if(input.PairFirstIndex>=0&&input.PairSecondIndex>=0)
            throw new NotSupportedException("CDC980 explicit Directional pair interpolation requires its own current temporary CPU owner.");
        if(!float.IsFinite(input.ScalarFirst)||!float.IsFinite(input.ScalarSecond)||
            !float.IsFinite(input.LocationX)||!float.IsFinite(input.LocationZ))
            throw new InvalidDataException("Directional provider consumed controls must be finite.");
        if(input.ScalarFirst>=0&&input.ScalarFirst<=24&&input.ScalarSecond>=0&&input.ScalarSecond<=1&&input.TemporaryDirectionalAvailable)
            return Root("CB7E1F B479B0 empty first axis -> CB8382");
        byte spatial=input.PairFirstIndex>0?unchecked((byte)(input.PairFirstIndex-1)):
            selectorMaps[root.SourceObjectId].Sample(input.LocationX, input.LocationZ);
        return spatial>=4?Root("CB8344..CB8370 spatial byte outside child count",spatial):
            Child(spatial+1,"CB8372..CB8391 spatial ready child",spatial);
    }
    public DirectionalProviderCurrentFrame PrepareCurrentFrame(DirectionalLightConstructionPacket root,
        DirectionalProviderControls input,ReadOnlySpan<byte> preparedCameraPrefix)
    {
        var selected=Select(root,input);
        return new(this,root,input,selected,SelectedConstruction(root,selected),preparedCameraPrefix);
    }
    internal DirectionalLightConstructionPacket SelectedConstruction(DirectionalLightConstructionPacket root,
        DirectionalProviderSelection selected)=>selected.SelectedObjectId==root.SourceObjectId?root:children[selected.SelectedObjectId];
    private void ValidateSource(DirectionalLightConstructionPacket value)
    {
        if(!ReferenceEquals(value.Source, source))
            throw new InvalidDataException("Directional provider and selected constructor have different actual season sources.");
    }
    private void ValidateRootBinding(DirectionalLightConstructionPacket root)
    {
        ValidateSource(root);var expected=GetBinding(root.SourceObjectId);
        if(root.Metadata.ProviderObjectKey!=expected.SourceObjectKey||!root.ResourceBindings.Contains(expected))
            throw new InvalidDataException("Directional root constructor does not retain this owned ready provider Data.");
    }
}
