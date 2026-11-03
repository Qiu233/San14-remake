using San14.Data;

/// <summary>Four declared domestic effects prepared in each actual Scene and
/// seasonal namespace. This CPU owner belongs to the application's static
/// generation; openings and their effect workers only borrow its resources.</summary>
internal sealed class MapDomesticEffectStaticResources
{
    internal sealed class Namespace
    {
        private readonly LoadedMapEffectResources[] effects;

        internal Namespace(LoadedMapEffectResources[] effects) => this.effects = effects;

        internal LoadedMapEffectResources Require(int kind)
        {
            int index = kind - 68;
            if ((uint)index >= (uint)effects.Length)
                throw new InvalidDataException($"Domestic effect {kind} was not included in startup preparation.");
            return effects[index];
        }
    }

    private readonly Dictionary<(string ResourceUri, string DecodedSha256), Namespace> namespaces;

    private MapDomesticEffectStaticResources(
        Dictionary<(string ResourceUri, string DecodedSha256), Namespace> namespaces)
        => this.namespaces = namespaces;

    internal static MapDomesticEffectStaticResources Load(GameResourceRepository repository,
        LoadedSceneResources scene, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        var namespaces = new Dictionary<(string ResourceUri, string DecodedSha256), Namespace>();
        var info = repository.LoadEffectInfo();
        for (int season = 0; season < 4; season++)
        {
            token.ThrowIfCancellationRequested();
            var action = MapSeasonResourceConsumer.LoadActionDatabase(repository,
                MapSeasonResourceConsumer.LoadActionSource(repository, season * 3 + 1));
            var key = Key(action);
            if (namespaces.ContainsKey(key)) continue;
            using var context = RenderResourceContext.LoadCurrentMap(repository, scene, action, out var seasonBatch);
            using (seasonBatch)
            {
                var effects = new LoadedMapEffectResources[4];
                for (int index = 0; index < effects.Length; index++)
                {
                    token.ThrowIfCancellationRequested();
                    var effect = MapEffectResources.Load(repository, index + 68, context, info);
                    foreach (int slot in effect.TextureSlots.Keys)
                    {
                        token.ThrowIfCancellationRequested();
                        _ = effect.LoadTextureSlot(repository, slot);
                    }
                    foreach (var bank in effect.ShaderBanks)
                    {
                        token.ThrowIfCancellationRequested();
                        var shader = effect.LoadShaderBank(repository, bank.Index);
                        // These immutable combination/binding tables belong to the
                        // retained startup generation, not the first live effect draw.
                        _ = shader?.ReadG1Bindings();
                    }
                    effects[index] = effect;
                }
                namespaces.Add(key, new(effects));
            }
        }
        token.ThrowIfCancellationRequested();
        return new(namespaces);
    }

    // Selection uses the same actual Action database that installed the opening's
    // RenderResourceContext. Source identities are already parsed; no I/O or new
    // hashing, World capture or resource-registry scan occurs while borrowing.
    internal Namespace Require(MapSeasonActionDatabase actualAction)
    {
        ArgumentNullException.ThrowIfNull(actualAction);
        return namespaces.TryGetValue(Key(actualAction), out var resources) ? resources :
            throw new InvalidDataException("The actual domestic-effect namespace was not included in startup preparation.");
    }

    private static (string ResourceUri, string DecodedSha256) Key(MapSeasonActionDatabase action)
        => (action.Objects.Source.ResourceUri, action.Objects.Source.DecodedSha256);
}
