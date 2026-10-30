namespace San14.Data;

/// <summary>The loaded definition owner's completed personality initialization.
/// Arrays and vectors never escape as writable storage; each current owns its
/// actual byte/vector writers independently.</summary>
internal sealed class InitialPersonalityImage
{
    private readonly byte?[][] rows;
    private readonly IReadOnlyList<int>[] codes;
    private readonly int[] capacities;
    internal SourceProvenance? DefinitionSource { get; }
    internal SourceProvenance? StateSource { get; }
    internal string? DefinitionDecodedSha256 { get; }
    internal IReadOnlyList<CustomPersonalityRecord> CustomRecords { get; }

    internal InitialPersonalityImage(byte?[][] rows, IReadOnlyList<int>[] codes, int[] capacities,
        SourceProvenance? definitionSource, SourceProvenance? stateSource, string? decodedSha256,
        CustomPersonalityRecord[] customRecords)
    {
        this.rows=rows; this.codes=codes; this.capacities=capacities;
        DefinitionSource=definitionSource; StateSource=stateSource; DefinitionDecodedSha256=decodedSha256;
        CustomRecords=Array.AsReadOnly(customRecords);
    }
    internal byte? ReadByte(int physical, int offset) => rows[physical][offset];
    internal byte?[] CopyRow(int physical) => rows[physical].ToArray();
    internal IReadOnlyList<int> ReadSourceCodes(int physical) => codes[physical];
    internal int ReadCapacity(int physical) => capacities[physical];
}

public sealed partial class SourceWorldContext
{
    private InitialPersonalityImage? _initialPersonalities;

    //Only ArmyOperationContext's immutable loaded-source owner calls this.
    //Standalone constructors/materializers retain their existing write paths.
    internal static InitialPersonalityImage PrepareInitialPersonalities(LoadedScenario scenario,
        LoadedPersonalities definitions)
    {
        var seed = new SourceWorldContext();
        seed.InitializePersonalityStorage();
        seed.MaterializeStaticPersonalityState(scenario);
        seed.MaterializeFixedPersonalities(definitions);
        PersonalityEditorRuntime.Apply(scenario.InitialCreatedPersonalities, seed);
        var rows = new byte?[356][];
        var codes = new IReadOnlyList<int>[356];
        var capacities = new int[356];
        for (int physical=0; physical<356; physical++)
        {
            rows[physical] = seed._personalityFields[physical].ToArray();
            var vector = seed._personalityVectors[physical];
            codes[physical] = Array.AsReadOnly(vector.Values.ToArray());
            capacities[physical] = vector.Capacity;
        }
        return new(rows, codes, capacities, seed.PersonalityDefinitionSource, seed.PersonalityStateSource,
            seed.PersonalityDefinitionDecodedSha256, seed._customPersonalityRecords.Values.ToArray());
    }

    internal void BindInitialPersonalities(InitialPersonalityImage initial)
    {
        _initialPersonalities = initial;
        _personalityFields.Clear(); _personalityVectors.Clear();
        PersonalityDefinitionSource = initial.DefinitionSource;
        PersonalityStateSource = initial.StateSource;
        PersonalityDefinitionDecodedSha256 = initial.DefinitionDecodedSha256;
        //Initial editor records were part of the original prefix as well.
        //Saved campaign edits still run afterwards and replace their own rows.
        foreach (var row in initial.CustomRecords) _customPersonalityRecords[row.PhysicalSlot] = row;
    }
}
