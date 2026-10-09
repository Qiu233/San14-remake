using San14.Data;

public sealed partial class MapSourceCreatedDirectionalLightingDeviceOwner
{
    public sealed class PreparedSeasonSources
    {
        private readonly MapSourceCreatedDirectionalLightingDeviceOwner owner;
        internal PreparedSeasonSources(MapSourceCreatedDirectionalLightingDeviceOwner owner,
            MapSeasonDirectionalLightSourceSet declarations,
            MapSourceCreatedDirectionalLightingSource[] sources,
            MapDirectionalLightProviderOwner providers,
            DirectionalLightRetainedNormalization normalization)
        {this.owner=owner;Declarations=declarations;Sources=Array.AsReadOnly(sources);Providers=providers;Normalization=normalization;}
        public MapSeasonDirectionalLightSourceSet Declarations{get;}
        public IReadOnlyList<MapSourceCreatedDirectionalLightingSource> Sources{get;}
        internal MapDirectionalLightProviderOwner Providers{get;}
        internal DirectionalLightRetainedNormalization Normalization{get;}
        internal void RequireOwner(MapSourceCreatedDirectionalLightingDeviceOwner actual)
        {if(!ReferenceEquals(owner,actual))throw new InvalidDataException("Season sources belong to another actual lighting device owner.");}
    }

    /// <summary>Load the next five constructors/providers, borrowing the same
    /// live white/diffuse resources. This does not replace the current source
    /// set or mutate any retained World/Camera/Cloud/F1 state.</summary>
    public async Task<PreparedSeasonSources> PrepareSeasonSourcesAsync(GameResourceRepository repository,
        MapSeasonDirectionalLightSourceSet declarations,LoadedMapCameraConstructionSource mainCamera,
        uint actualContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(mainCamera);
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var normalization=RetainedNormalization??throw new NotSupportedException("This lighting owner has no admitted seasonal named-consumer representation.");
            var white=fixedWhite??throw new NotSupportedException("Season replacement requires this actual ready white resource owner.");
            if(mainCamera.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId)
                throw new InvalidDataException("Season constructors require the same actual Main38D source.");
            var providers=MapDirectionalLightProviderOwner.Load(repository,declarations);
            var next=normalization.CreateSeasonConstructionCandidate();
            var whiteBinding=await white.GetDirectionalBindingAsync();
            var diffuseBinding=await Diffuse.GetBindingAsync();
            var sources=declarations.Roots.Select(root=>
            {
                var available=new[]{providers.GetBinding(root.Root.ObjectId),whiteBinding,diffuseBinding};
                var requested=DirectionalLightConstruction.RequiredResourceKeys(declarations,root.Root.ObjectId,
                    actualContextFlags3E4,available);
                var used=available.Where(binding=>requested.Contains(binding.SourceObjectKey)).ToArray();
                if(!requested.Order().SequenceEqual(used.Select(binding=>binding.SourceObjectKey).Order()))
                    throw new NotSupportedException("Changed season requires an additional actual Directional GPU resource.");
                var template=next.CreateTemplate(declarations,root.Root.ObjectId);
                var construction=DirectionalLightConstruction.Apply(declarations,root.Root.ObjectId,
                    template.CompleteTemplate.Span,new(actualContextFlags3E4,template.SourceProvenance,used));
                next.AdoptConstruction(construction);
                return MapSourceCreatedDirectionalLightingSource.FromMainCamera(mainCamera,construction);
            }).ToArray();
            return new(this,declarations,sources,providers,next);
        }
        finally{gate.Release();}
    }

    /// <summary>The caller holds the drained World session gate. Only loaded
    /// source/provider metadata is installed; the physical resource owners and
    /// the Host's normalization identity stay alive.</summary>
    public void InstallSeasonSources(PreparedSeasonSources actual)
    {
        ArgumentNullException.ThrowIfNull(actual);actual.RequireOwner(this);
        ObjectDisposedException.ThrowIf(retiring,this);
        var normalization=RetainedNormalization??throw new InvalidDataException("Season lighting consumer owner is absent.");
        normalization.InstallSeasonConstructionCandidate(actual.Normalization);
        Sources=actual.Sources;Providers=actual.Providers;
    }

    public IReadOnlyList<MapSourceCreatedDirectionalLightingSource> RequireCurrentSeasonSources(
        MapSeasonDirectionalLightSourceSet actual)
    {
        ArgumentNullException.ThrowIfNull(actual);ObjectDisposedException.ThrowIf(retiring,this);
        if(!Sources.Select(source=>source.Construction.SourceObjectId).SequenceEqual(actual.Roots.Select(root=>root.Root.ObjectId))||
            Sources.Any(source=>source.Construction.SourceUri!=actual.Source.ResourceUri||
                source.Construction.SourceSha256!=actual.Source.DecodedSha256))
            throw new InvalidDataException("Current five-light request differs from the installed actual season declarations.");
        return Sources;
    }
    public MapDirectionalLightProviderOwner RequireCurrentSeasonProviders(MapSeasonDirectionalLightSourceSet actual)
    {_=RequireCurrentSeasonSources(actual);return Providers??throw new InvalidDataException("The actual season has no ready Directional providers.");}
}
