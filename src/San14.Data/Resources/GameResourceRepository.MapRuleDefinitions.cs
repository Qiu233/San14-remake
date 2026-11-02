using System.Collections.Concurrent;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One fixed-file generation's terrain rule facts. No current world,
/// table substitutions or writable source arrays are exposed by this owner.</summary>
internal sealed class InitialTerrainRuleDefinitions
{
    private readonly LoadedTerrainTypes owner;
    private readonly ushort?[] movementCosts;
    private readonly byte?[] woundedCategories;
    private readonly ushort?[][] tacticRates;
    internal SourceProvenance Source => owner.Source;
    internal string DecodedSha256 => owner.Catalog.DecodedSha256;

    internal InitialTerrainRuleDefinitions(LoadedTerrainTypes owner, ushort?[] movementCosts,
        byte?[] woundedCategories, ushort?[][] tacticRates)
    { this.owner=owner; this.movementCosts=movementCosts; this.woundedCategories=woundedCategories; this.tacticRates=tacticRates; }
    internal bool BelongsTo(LoadedTerrainTypes loaded) =>
        ReferenceEquals(owner.Catalog, loaded.Catalog) && ReferenceEquals(owner.Source, loaded.Source);
    internal ushort? ReadMovementCost(int physical) => movementCosts[physical];
    internal byte? ReadWoundedCategory(int physical) => woundedCategories[physical];
    internal ushort? ReadTacticRate(int physical, int category) => tacticRates[physical][category];
}

/// <summary>Immutable facility rules from the same ordinary fixed-file slot.
/// Current object receivers and facility runtime state never enter this owner.</summary>
internal sealed class InitialObjectRuleDefinitions
{
    private readonly LoadedObjectDefinitions owner;
    private readonly byte?[] categories;
    private readonly ushort?[] durabilities;
    private readonly FacilityConstructionDefinition?[] construction;
    private readonly FacilityEffectDefinition?[] effects;
    internal SourceProvenance Source => owner.Source;
    internal string DecodedSha256 => owner.Catalog.DecodedSha256;

    internal InitialObjectRuleDefinitions(LoadedObjectDefinitions owner, byte?[] categories, ushort?[] durabilities,
        FacilityConstructionDefinition?[] construction, FacilityEffectDefinition?[] effects)
    { this.owner=owner; this.categories=categories; this.durabilities=durabilities; this.construction=construction; this.effects=effects; }
    internal bool BelongsTo(LoadedObjectDefinitions loaded) =>
        ReferenceEquals(owner.Catalog, loaded.Catalog) && ReferenceEquals(owner.Source, loaded.Source);
    internal byte? ReadCategory(int physical) => categories[physical];
    internal ushort? ReadDurability(int physical) => durabilities[physical];
    //These existing internal readers treated an unavailable physical row as
    //unknown. Unlike scalar public accessors, they did not enforce a range.
    internal FacilityConstructionDefinition? ReadConstruction(int physical) =>
        (uint)physical < (uint)construction.Length ? construction[physical] : null;
    internal FacilityEffectDefinition? ReadEffect(int physical) =>
        (uint)physical < (uint)effects.Length ? effects[physical] : null;
}

public sealed partial class SourceWorldContext
{
    //Separate channels retain the narrow transaction copy contract: a return
    //transaction borrows movement costs but did not carry terrain combat rules.
    private InitialTerrainRuleDefinitions? _initialSurfaceMovementRules, _initialSurfaceCombatRules;
    private InitialObjectRuleDefinitions? _initialObjectScalarRules, _initialObjectFacilityRules;

    internal static InitialTerrainRuleDefinitions PrepareInitialTerrainRules(LoadedTerrainTypes loaded)
    {
        //Use the established loader once, including format admission and its
        //encoded nine-word rates. The builder itself is never shared.
        var builder = new SourceWorldContext();
        builder.MaterializeFixedTerrainTypes(loaded);
        var costs = new ushort?[31];
        var wounded = new byte?[31];
        var rates = new ushort?[31][];
        for (int physical=0; physical<31; physical++)
        {
            var reference = new SurfaceTypeReference(physical);
            costs[physical] = builder.ReadSurfaceTypeRawField2A(reference);
            wounded[physical] = builder.ReadSurfaceTypeWoundedCategory42(reference);
            rates[physical] = new ushort?[9];
            for (int category=0; category<9; category++)
                rates[physical][category] = builder.ReadSurfaceTypeTacticRateWord(reference, category);
        }
        return new(loaded, costs, wounded, rates);
    }

    internal static InitialObjectRuleDefinitions PrepareInitialObjectRules(LoadedObjectDefinitions loaded)
    {
        var builder = new SourceWorldContext();
        for (int physical=0; physical<S14ObjectDefinitionReader.Count; physical++)
            builder.SetStaticTablePointer(0x73958, physical, physical);
        builder.MaterializeFixedObjectDefinitions(loaded);
        var categories = new byte?[S14ObjectDefinitionReader.Count];
        var durabilities = new ushort?[S14ObjectDefinitionReader.Count];
        var construction = new FacilityConstructionDefinition?[S14ObjectDefinitionReader.Count];
        var effects = new FacilityEffectDefinition?[S14ObjectDefinitionReader.Count];
        for (int physical=0; physical<S14ObjectDefinitionReader.Count; physical++)
        {
            var reference = new ObjectDefinitionReference(physical);
            categories[physical] = builder.ReadObjectDefinitionRaw2C(reference);
            durabilities[physical] = builder.ReadObjectDefinitionBaseDurability(reference);
            construction[physical] = builder.ReadFacilityConstructionDefinition(reference);
            effects[physical] = builder.ReadFacilityEffectDefinition(reference);
        }
        return new(loaded, categories, durabilities, construction, effects);
    }

    //Only the normal fresh factory uses these binders, after independent
    //pointer allocation. Explicit later writes stay in private dictionaries.
    internal void BindInitialTerrainRules(InitialTerrainRuleDefinitions initial)
    {
        _initialSurfaceMovementRules = _initialSurfaceCombatRules = initial;
        _surfaceTypeFields2A.Clear(); _surfaceWoundedCategories42.Clear(); _surfaceTacticRateWords.Clear();
        TerrainTypeSource = initial.Source;
        TerrainTypeSourceSha256 = initial.DecodedSha256;
    }
    internal void BindInitialObjectRules(InitialObjectRuleDefinitions initial)
    {
        _initialObjectScalarRules = _initialObjectFacilityRules = initial;
        _objectDefinitionValues2C.Clear(); _objectDefinitionBaseDurability.Clear();
        _facilityConstructionDefinitions.Clear(); _facilityEffectDefinitions.Clear();
        ObjectDefinitionSource = initial.Source;
        ObjectDefinitionDecodedSha256 = initial.DecodedSha256;
    }
}

public sealed partial class GameResourceRepository
{
    private static LoadedTerrainTypes CopyTerrainTypesForReader(LoadedTerrainTypes loaded) =>
        new(loaded.Catalog with { Types = loaded.Catalog.Types.Select(row => row with { RawBytes = row.RawBytes.ToArray() }).ToArray() }, loaded.Source);
    private static LoadedObjectDefinitions CopyObjectDefinitionsForReader(LoadedObjectDefinitions loaded) =>
        new(loaded.Catalog with { Definitions = loaded.Catalog.Definitions.Select(row => row with { RawBytes = row.RawBytes.ToArray() }).ToArray() }, loaded.Source);

    private sealed partial class ResidentSourceCache
    {
        //The existing logical file slot owns at most one typed snapshot per
        //kind. Replacement changes that snapshot; older borrows remain valid.
        //Terrain and object tables stay independent so public older-edition
        //terrain loading does not acquire the CE-only object profile.
        private readonly ConcurrentDictionary<GameResourceId, (ResidentFile Source, LoadedTerrainTypes Value)> terrainRules =
            new(ResourceIdComparer.Instance);
        private readonly ConcurrentDictionary<GameResourceId, (ResidentFile Source, LoadedObjectDefinitions Value)> objectRules =
            new(ResourceIdComparer.Instance);

        internal LoadedTerrainTypes ReadTerrainRules(GameResourceRepository scope, ResidentFile source)
        {
            var slot = For(source.Generation.Id);
            lock (slot.Gate)
            {
                if (!ReferenceEquals(slot.File, source)) throw new ResourceReadGenerationChangedException(source.Generation.Id);
                if (terrainRules.TryGetValue(source.Generation.Id, out var cached) && ReferenceEquals(cached.Source, source)) return cached.Value;
                var loaded = new LoadedTerrainTypes(S14TerrainTypeReader.Read(S14Reader.Read(source.Bytes, false)), source.Source);
                var value = loaded with { InitialValues = SourceWorldContext.PrepareInitialTerrainRules(loaded) };
                scope.VerifyResourceRead(source.Generation);
                terrainRules[source.Generation.Id] = (source, value);
                return value;
            }
        }
        internal LoadedObjectDefinitions ReadObjectRules(GameResourceRepository scope, ResidentFile source)
        {
            var slot = For(source.Generation.Id);
            lock (slot.Gate)
            {
                if (!ReferenceEquals(slot.File, source)) throw new ResourceReadGenerationChangedException(source.Generation.Id);
                if (objectRules.TryGetValue(source.Generation.Id, out var cached) && ReferenceEquals(cached.Source, source)) return cached.Value;
                var loaded = new LoadedObjectDefinitions(S14ObjectDefinitionReader.ReadFixed(S14Reader.Read(source.Bytes, false)), source.Source);
                var value = loaded with { InitialValues = SourceWorldContext.PrepareInitialObjectRules(loaded) };
                scope.VerifyResourceRead(source.Generation);
                objectRules[source.Generation.Id] = (source, value);
                return value;
            }
        }
    }
}
