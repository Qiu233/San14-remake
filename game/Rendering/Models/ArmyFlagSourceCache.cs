using San14.Data;

/// <summary>Flag preparations and producer images belong to their imported
/// resource owner. Accepted images survive replacement until the renderer drains.</summary>
internal sealed class ArmyFlagSourceCache : IAsyncDisposable
{
    private readonly GameResourceRepository repository;
    private readonly Vkd3dShaderCompiler? compiler;
    private readonly Dictionary<LoadedArmyFlagResources, Sources> owners = new(ReferenceEqualityComparer.Instance);
    private Task? disposal;
    private NativeMapBorrowerRetirementPackage? finalRetirement;

    public ArmyFlagSourceCache(GameResourceRepository repository, Vkd3dShaderCompiler? compiler = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        this.repository = repository;
        this.compiler = compiler;
    }

    public Sources Get(LoadedArmyFlagResources source)
    {
        ObjectDisposedException.ThrowIf(disposal is not null, this);
        if (!owners.TryGetValue(source, out var prepared))
            owners.Add(source, prepared = new(repository, compiler, source));
        return prepared;
    }

    public ValueTask DisposeAsync()
    {
        if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=DisposeCoreAsync();
        return new(disposal);
    }

    private async Task DisposeCoreAsync()
    {
        finalRetirement??=new("Army-flag-sources-retire/"+Guid.NewGuid().ToString("N"),[..owners.Values]);
        await finalRetirement.DisposeAsync();
        owners.Clear();
    }

    internal sealed class Sources : IAsyncDisposable
    {
        private sealed record TextureGroup(RemakeArmyFlagRuntimeTexture Image, NativeArmyFlagMaterialTextureGroupRecord Group);
        private readonly GameResourceRepository repository;
        private readonly Vkd3dShaderCompiler? compiler;
        private readonly Dictionary<int, RemakeArmyFlagGbufferSource> draws = [];
        private readonly Dictionary<(int Draw, int Force), RemakeArmyFlagVariantGbufferSource> variants = [];
        private readonly Dictionary<(int Draw, int Force, NativeWorldShaderTableState Table), RemakeArmyFlagVariantGbufferSource> finalVariants = [];
        private readonly Dictionary<LoadedArmyFlagMaterialVariation, TextureGroup> groups = new(ReferenceEqualityComparer.Instance);
        private readonly List<NativeArmyFlagMaterialTextureGroupRecord> retainedGroups = [];
        private readonly Dictionary<string, RemakeArmyFlagCaoPaneProducer> producers = [];
        private NativeMapBorrowerRetirementPackage? finalRetirement;
        private LoadedObjects? objects;
        private LoadedRenderGraph? graph;
        public LoadedArmyFlagResources Source { get; }
        public ArmyWorldPreparedTableSource WorldTables { get; } = new();
        public ArmyWorldQueueSourceCache WorldQueues { get; } = new();

        public Sources(GameResourceRepository repository, Vkd3dShaderCompiler? compiler, LoadedArmyFlagResources source)
            => (this.repository, this.compiler, Source) = (repository, compiler, source);

        public RemakeArmyFlagGbufferSource GetDraw(int draw)
        {
            if (!draws.TryGetValue(draw, out var prepared))
            {
                objects ??= repository.LoadObjects("system", 0x718CCB8B);
                graph ??= repository.LoadRenderGraph("system", 0x024496EE);
                prepared = RemakeArmyFlagGbufferSource.Load(repository, Source, objects, graph, draw);
                draws.Add(draw, prepared);
            }
            return prepared;
        }

        public RemakeArmyFlagVariantGbufferSource GetVariant(int draw, int force)
        {
            if (!variants.TryGetValue((draw, force), out var prepared))
                variants.Add((draw, force), prepared = RemakeArmyFlagVariantGbufferSource.Load(repository, Source, draw, force));
            return prepared;
        }

        public RemakeArmyFlagVariantGbufferSource GetFinalVariant(int draw,
            RemakeArmyFlagVariantGbufferSource regular, NativeWorldShaderTableState table)
        {
            var key = (draw, regular.Variation.OwnedForceVariant, table);
            if (!finalVariants.TryGetValue(key, out var prepared))
                finalVariants.Add(key, prepared = RemakeArmyFlagVariantGbufferSource.LoadForActualFinalTable(
                    repository, Source, draw, regular.Variation, table));
            return prepared;
        }

        public RemakeArmyFlagCaoPaneProducer GetProducer(string locale)
        {
            if (!producers.TryGetValue(locale, out var producer))
                producers.Add(locale, producer = new(repository, locale, compiler));
            return producer;
        }

        public NativeArmyFlagMaterialTextureGroupRecord GetTextureGroup(
            RemakeArmyFlagVariantGbufferSource variant, RemakeArmyFlagRuntimeTexture primary)
        {
            if (!groups.TryGetValue(variant.Variation, out var current))
            {
                var group = NativeArmyFlagMaterialTextureGroupRecord.LoadComplete(repository, variant.Variation,
                    variant.Shader, variant.Combination, variant.TextureUses,
                    new Dictionary<uint, RemakeArmyFlagRuntimeTexture> { [primary.ProxyObjectId] = primary });
                retainedGroups.Add(group);
                groups[variant.Variation] = current = new(primary, group);
            }
            else if (!current.Image.HasSameOwner(primary))
            {
                current.Group.PublishRuntimeImage(primary);
                groups[variant.Variation] = current = current with { Image = primary };
            }
            current.Group.RequireCurrent();
            return current.Group;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var group in retainedGroups) group.Invalidate("Army flag renderer disposed");
            finalRetirement??=new("Army-flag-source-retire/"+Guid.NewGuid().ToString("N"),[..producers.Values]);
            await finalRetirement.DisposeAsync();
            retainedGroups.Clear(); groups.Clear(); producers.Clear(); draws.Clear(); variants.Clear(); finalVariants.Clear();
        }
    }
}
