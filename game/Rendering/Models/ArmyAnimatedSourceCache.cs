using San14.Data;
using San14.Data.Formats;

/// <summary>Current imported model per category. Replacing its owner replaces
/// derived draw, animation and World preparation caches together.</summary>
internal sealed class ArmyAnimatedSourceCache
{
    private readonly GameResourceRepository repository;
    private readonly Dictionary<int, Sources> current = [];

    public ArmyAnimatedSourceCache(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        this.repository = repository;
    }

    public Sources Get(LoadedArmyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        int category = model.Category.Category;
        if (!current.TryGetValue(category, out var sources) || !ReferenceEquals(sources.Model, model))
            current[category] = sources = new(repository, model);
        return sources;
    }

    public void Retain(IReadOnlySet<int> categories)
    {
        foreach (int category in current.Keys.Where(category => !categories.Contains(category)).ToArray())
            current.Remove(category);
    }

    public void Clear() => current.Clear();

    internal sealed class Sources
    {
        private readonly GameResourceRepository repository;
        private readonly Dictionary<int, ArmyAnimatedGbufferSource> prepared = [];
        private LoadedObjects? shaderObjects;
        private LoadedRenderGraph? graph;
        public LoadedArmyModel Model { get; }
        public IReadOnlyList<G1DrawRecord> Draws { get; }
        public ArmyWorldPreparedTableSource WorldTables { get; } = new();
        public ArmyWorldQueueSourceCache WorldQueues { get; } = new();

        public Sources(GameResourceRepository repository, LoadedArmyModel model)
        {
            this.repository = repository;
            Model = model;
            Draws = Array.AsReadOnly(G1ModelDrawReader.Read(model.Transforms.Geometry).Draws.ToArray());
        }

        public ArmyAnimatedGbufferSource GetDraw(int drawIndex)
        {
            if ((uint)drawIndex >= Draws.Count) throw new ArgumentOutOfRangeException(nameof(drawIndex));
            if (!prepared.TryGetValue(drawIndex, out var source))
            {
                shaderObjects ??= repository.LoadObjects("system", 0x718CCB8B);
                graph ??= repository.LoadRenderGraph("system", 0x024496EE);
                source = ArmyAnimatedGbufferSource.Load(repository, Model, shaderObjects, graph, 49, drawIndex);
                prepared.Add(drawIndex, source);
            }
            return source;
        }
    }
}
