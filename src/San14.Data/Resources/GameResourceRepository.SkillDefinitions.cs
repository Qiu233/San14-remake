using System.Collections.Concurrent;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One fixed file's completed tactic/gem definition values. Mutable
/// contexts borrow reads and own all later row/vector writers independently.</summary>
internal sealed class InitialSkillDefinitionValues
{
    private readonly LoadedSkillDefinitions owner;
    private readonly byte?[][] tactics, gems;
    private readonly IReadOnlyList<int>[] codes;
    private readonly int[] capacities;
    internal SourceProvenance Source => owner.Source;
    internal string DecodedSha256 => owner.Formations.DecodedSha256;
    internal TacticSourceCatalog TacticCatalog { get; }

    internal InitialSkillDefinitionValues(LoadedSkillDefinitions owner, byte?[][] tactics,
        byte?[][] gems, IReadOnlyList<int>[] codes, int[] capacities)
    {
        this.owner=owner; this.tactics=tactics; this.gems=gems; this.codes=codes; this.capacities=capacities;
        //TacticSourceRow already deep-copies its raw bytes and exposes only
        //read-only values/copy getters. This projection belongs to this source.
        TacticCatalog = TacticSourceParameters.Read(owner.Formations, owner.Source);
    }
    internal bool BelongsTo(LoadedSkillDefinitions loaded) =>
        ReferenceEquals(owner.Formations, loaded.Formations) && ReferenceEquals(owner.Gems, loaded.Gems) &&
        ReferenceEquals(owner.Source, loaded.Source);
    internal byte? ReadTacticByte(int physical, int offset) => tactics[physical][offset];
    internal byte? ReadGemByte(int physical, int offset) => gems[physical][offset];
    internal byte?[] CopyTacticRow(int physical) => tactics[physical].ToArray();
    internal byte?[] CopyGemRow(int physical) => gems[physical].ToArray();
    internal IReadOnlyList<int> ReadCodes(int physical) => codes[physical];
    internal int ReadCapacity(int physical) => capacities[physical];
}

public sealed partial class SourceWorldContext
{
    private InitialSkillDefinitionValues? _initialSkillDefinitions;

    internal static InitialSkillDefinitionValues PrepareInitialSkillDefinitions(LoadedSkillDefinitions loaded)
    {
        //Use the existing constructor/serializer once, including its vector
        //rotation, reset values and exact reservation capacities. This mutable
        //builder never escapes and is never shared by currents.
        var seed = new SourceWorldContext();
        for (int physical=0; physical<201; physical++) seed.SetStaticTablePointer(0x774D0, physical, physical);
        for (int physical=0; physical<S14GemReader.Count; physical++) seed.SetStaticTablePointer(0x79118, physical, physical);
        seed.InitializeSkillDefinitionStorage();
        seed.MaterializeFixedSkillDefinitions(loaded);
        var tactics = new byte?[201][];
        var gems = new byte?[S14GemReader.Count][];
        var codes = new IReadOnlyList<int>[201];
        var capacities = new int[201];
        for (int physical=0; physical<201; physical++)
        {
            tactics[physical] = seed._skillTacticDefinitionBytes[physical].ToArray();
            codes[physical] = Array.AsReadOnly(seed._tacticEncodedValues70[physical]!.ToArray());
            capacities[physical] = seed._skillTacticVectorCapacities[physical];
        }
        for (int physical=0; physical<S14GemReader.Count; physical++)
            gems[physical] = seed._skillGemDefinitionBytes[physical].ToArray();
        return new(loaded, tactics, gems, codes, capacities);
    }

    internal void BindInitialSkillDefinitions(InitialSkillDefinitionValues initial)
    {
        _initialSkillDefinitions = initial;
        _skillTacticDefinitionBytes.Clear(); _skillGemDefinitionBytes.Clear();
        _tacticFlags44.Clear(); _gemTacticCodes52.Clear(); _gemValidityCodes53.Clear();
        _tacticEncodedValues70.Clear(); _skillTacticVectorCapacities.Clear();
        SkillDefinitionSource = initial.Source;
        SkillDefinitionDecodedSha256 = initial.DecodedSha256;
    }
}

public sealed partial class GameResourceRepository
{
    private static LoadedSkillDefinitions CopySkillDefinitionsForReader(LoadedSkillDefinitions loaded)
    {
        //No shared writable row/list storage crosses the public loader. This
        //copies already parsed metadata; it neither serializes native tables nor
        //rebuilds the initialized owner. Local raw edits retain the legacy path.
        var formations = loaded.Formations with
        {
            Formations = loaded.Formations.Formations.Select(row => row with
            { RawParameterBytes = row.RawParameterBytes.ToArray(), RawBytes = row.RawBytes.ToArray() }).ToArray(),
            Tactics = loaded.Formations.Tactics.Select(row => row with
            { FormationSlots = row.FormationSlots.ToArray(), RawParameterBytes = row.RawParameterBytes.ToArray(),
                RawBytes = row.RawBytes.ToArray() }).ToArray(),
            Effects = loaded.Formations.Effects.Select(row => row with
            { RawParameterBytes = row.RawParameterBytes.ToArray(), RawBytes = row.RawBytes.ToArray() }).ToArray()
        };
        var gems = loaded.Gems with
        {
            Gems = loaded.Gems.Gems.Select(row => row with { RawBytes = row.RawBytes.ToArray() }).ToArray()
        };
        return new(formations, gems, loaded.Source);
    }

    private sealed partial class ResidentSourceCache
    {
        //The same resident file slot owns this typed value. There is one entry
        //per logical resource, replaced on a new source snapshot; older borrows
        //remain immutable. Different resource keys can parse independently.
        private readonly ConcurrentDictionary<GameResourceId, (ResidentFile Source, LoadedSkillDefinitions Value)> skillDefinitions =
            new(ResourceIdComparer.Instance);

        internal LoadedSkillDefinitions ReadSkillDefinitions(GameResourceRepository scope, ResidentFile source)
        {
            var slot = For(source.Generation.Id);
            lock (slot.Gate)
            {
                if (!ReferenceEquals(slot.File, source))
                    throw new ResourceReadGenerationChangedException(source.Generation.Id);
                if (skillDefinitions.TryGetValue(source.Generation.Id, out var cached) && ReferenceEquals(cached.Source, source))
                    return cached.Value;
                var document = S14Reader.Read(source.Bytes, false);
                if (document.FormatMarker != 0x66)
                    throw new InvalidDataException("Skill definitions require the inspected CE fixed-data profile.");
                var loaded = new LoadedSkillDefinitions(S14FormationReader.Read(document), S14GemReader.ReadFixed(document), source.Source);
                var value = loaded with { InitialValues = SourceWorldContext.PrepareInitialSkillDefinitions(loaded) };
                scope.VerifyResourceRead(source.Generation);
                skillDefinitions[source.Generation.Id] = (source, value);
                return value;
            }
        }
    }
}
