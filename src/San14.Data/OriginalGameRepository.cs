using System.Security.Cryptography;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SourceProvenance(string SourcePath, long Bytes, string Sha256,
    string? FrozenSha256)
{
    public string ResourceUri { get; init; } = "";
    public string PhysicalPath { get; init; } = "";
    public bool IsOverride { get; init; }
    public bool MatchesFrozenBaseline => FrozenSha256 is not null &&
        Sha256.Equals(FrozenSha256, StringComparison.OrdinalIgnoreCase);
}

public sealed record LoadedMap(MapDocument Map, SourceProvenance Source);
public sealed record LoadedScenario(S14Entry Metadata, ScenarioCityPreview Snapshot,
    SourceProvenance ScenarioSource, SourceProvenance FixedDataSource);
public sealed record LoadedPolicies(S14PolicyCatalog Catalog, SourceProvenance Source);
public sealed record LoadedDoctrines(S14DoctrineCatalog Catalog, SourceProvenance Source);
public sealed record LoadedCreditEffects(S14CreditEffectCatalog Catalog, SourceProvenance Source);
public sealed record LoadedTerrainTypes(S14TerrainTypeCatalog Catalog, SourceProvenance Source);
public sealed record LoadedRankPersonCapacities(OriginalRankPersonCapacityCatalog Catalog,SourceProvenance Source);
public sealed record LoadedBaseParameters(OriginalBaseParameterCatalog Catalog, SourceProvenance Source);
public sealed record LoadedCreditRanks(S14CreditRankCatalog Catalog, SourceProvenance Source);
public sealed record LoadedDoctrinePanels(S14DoctrinePanelCatalog Catalog, SourceProvenance Source);
public sealed record LoadedSceneries(S14SceneryCatalog Catalog, SourceProvenance Source);
public sealed record LoadedPersonalities(S14PersonalityCatalog Catalog, SourceProvenance Source);
public sealed record LoadedSkillDefinitions(S14FormationCatalog Formations, S14GemCatalog Gems,
    SourceProvenance Source);
public sealed record LoadedObjectDefinitions(S14ObjectDefinitionCatalog Catalog, SourceProvenance Source);
public sealed record LoadedItemDefinitions(S14ItemDefinitionCatalog Catalog, SourceProvenance Source);
public sealed record LoadedScenarioItems(S14ScenarioItemCatalog Catalog, SourceProvenance Source);
public sealed record LoadedExperienceData(ExperienceDataDocument Document,
    ExperienceDoctrineCatalog Doctrine, ExperienceGrowthCatalog Growth, SourceProvenance Source);
public sealed record LoadedEvents(S14EventDocument Document, SourceProvenance Source);
public sealed record LoadedScenarioWorld(S14ScenarioWorldDocument Document, SourceProvenance Source);
public sealed record LoadedScenarioObjects(S14ScenarioObjectCatalog Catalog,
    IReadOnlyList<S14ScenarioCity> Cities, SourceProvenance Source);
public sealed record LoadedTexture(RgbaTexture Texture, string SourceContainerPath,
    uint FileId, string DecodedSha256, SourceProvenance IndexSource)
{
    public SourceProvenance? OverrideSource { get; init; }
    public int TextureIndex { get; init; }
}

public sealed record LoadedRdbAsset(byte[] Bytes, string ResourceUri, string SourceContainerPath,
    uint FileId, string DecodedSha256, SourceProvenance IndexSource,
    SourceProvenance? OverrideSource);

public sealed record LoadedSprite(SpriteDefinition Definition, LoadedTexture Texture,
    LoadedRdbAsset AtlasSource);

public sealed record LoadedLayout(LcskDocument Layout, LoadedRdbAsset Source);

public sealed record LoadedModelHeaders(G1ModelHeaders Headers, LoadedRdbAsset Source);

public sealed record LoadedModelGeometry(G1ModelGeometry Geometry, LoadedRdbAsset Source);

public sealed record LoadedModelDraws(G1ModelGeometry Geometry, G1ModelDrawBindings Bindings,
    LoadedRdbAsset Source);

public sealed record LoadedModelVertices(G1ModelGeometry Geometry, G1ModelVertexData VertexData,
    LoadedRdbAsset Source);

public sealed record LoadedModelMaterials(G1ModelGeometry Geometry, G1ModelMaterialRecords Materials,
    LoadedRdbAsset Source);

public sealed record LoadedModelTransforms(G1ModelGeometry Geometry, G1ModelTransforms Transforms,
    LoadedRdbAsset Source);

public sealed record LoadedTextureContainer(G1TextureContainer Container, LoadedRdbAsset Source);
public sealed record LoadedShaderContainer(G2ShaderContainer Container, LoadedRdbAsset Source)
{
    public DxbcProgramSignatures ReadSignatures(int programIndex)
    {
        if ((uint)programIndex >= Container.Programs.Count)
            throw new ArgumentOutOfRangeException(nameof(programIndex), $"Program index is outside {Source.ResourceUri}.");
        try { return DxbcSignatureReader.Read(Container.GetProgramBytes(Source.Bytes, programIndex)); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader signatures failed: {Source.ResourceUri}, program {programIndex}. {error.Message}", error); }
    }

    public G2ShaderMetadata ReadMetadata()
    {
        try { return G2ShaderMetadataReader.Read(Source.Bytes); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader metadata failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public G1ShaderBindingTables ReadG1Bindings()
    {
        try { return G1ShaderBindingReader.Read(Source.Bytes); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"G1S shader binding tables failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public G1ShaderResourceBindings ReadG1ResourceBindings(int explicitCombinationIndex)
    {
        try { return G1ShaderResourceBindingReader.Read(Source.Bytes, explicitCombinationIndex); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"G1S resource binding table failed: {Source.ResourceUri}, combination {explicitCombinationIndex}. {error.Message}", error); }
    }

    public DxbcResourceDefinitions ReadResourceDefinitions(int programIndex)
    {
        if ((uint)programIndex >= Container.Programs.Count)
            throw new ArgumentOutOfRangeException(nameof(programIndex), $"Program index is outside {Source.ResourceUri}.");
        try
        {
            return DxbcResourceDefinitionReader.Read(Container.GetProgramBytes(Source.Bytes, programIndex));
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Shader resource definitions failed: {Source.ResourceUri}, program {programIndex}. {error.Message}", error);
        }
    }
}

public sealed record LoadedObjects(DokDocument Document, LoadedRdbAsset Source);
public sealed record LoadedTaskGraph(CompiledGraphDocument Document, LoadedRdbAsset Source);
public sealed record LoadedRenderGraph(CompiledGraphDocument Document, LoadedRdbAsset Source);

public sealed record LoadedHeightField(MhkHeightField HeightField, LoadedRdbAsset Source);

public enum OriginalMessageVariant { Standard, ExCe }
public enum OriginalExperienceVariant { Standard, ExCe }

public sealed record LoadedMessages(OriginalMessageDocument Messages, Zp1Document Container,
    SourceProvenance Source, string DecodedSha256);

/// <summary>
/// Read-only, on-demand view of an installed game. Decoded game resources stay in memory;
/// the frozen baseline is diagnostic so a compatible Steam update can still be parsed.
/// </summary>
public sealed class OriginalGameRepository
{
    public const string DefaultSource =
        @"C:\Program Files (x86)\Steam\steamapps\common\Romance_of_the_Three_Kingdoms_14";

    private readonly Dictionary<string, SourceFile> _baseline;
    private readonly ResourceOverrides _overrides;
    private readonly Dictionary<string, SourceProvenance> _readFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RdbIndex> _indices =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<uint, RdbRecord>> _assetRecords =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RdbSpriteDirectory> _spriteDirectories =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<S14Entry>? _scenarioDocuments;
    private IReadOnlyList<EffectiveScenario>? _effectiveScenarios;

    public string SourceDirectory { get; }
    public string? OverrideDirectory => _overrides.DirectoryPath;
    public IReadOnlyCollection<SourceProvenance> ReadFiles => _readFiles.Values;
    public IReadOnlyList<SourceProvenance> ActiveOverrides =>
        _readFiles.Values.Where(item => item.IsOverride)
            .OrderBy(item => item.ResourceUri, StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<SourceProvenance> ChangedFromFrozenBaseline =>
        _readFiles.Values.Where(item => !item.IsOverride && !item.MatchesFrozenBaseline)
            .OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase).ToArray();

    public OriginalGameRepository(string? sourceDirectory = null, string? overridesDirectory = null)
    {
        string location = sourceDirectory ?? Environment.GetEnvironmentVariable("SAN14_SOURCE_DIR")
            ?? DefaultSource;
        SourceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
        _overrides = new ResourceOverrides(overridesDirectory ??
            Environment.GetEnvironmentVariable("SAN14_OVERRIDE_DIR"));
        if (!Directory.Exists(SourceDirectory))
            throw new DirectoryNotFoundException($"Original game directory not found: {SourceDirectory}");
        foreach (string relative in new[] { "0000/MapExCe.bin", "0002/ScreenLayout.rdb",
                     "0010_SC", "0010_TC" })
        {
            string path = SourcePath(relative);
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException($"Original game source is missing: {relative}", path);
        }
        using Stream baselineStream = typeof(OriginalGameRepository).Assembly
            .GetManifestResourceStream("San14.Data.FrozenBaseline.json")
            ?? throw new InvalidDataException("Frozen source baseline metadata is missing.");
        SourceBaseline baseline = JsonSerializer.Deserialize<SourceBaseline>(baselineStream)
            ?? throw new InvalidDataException("Frozen source baseline metadata is empty.");
        _baseline = baseline.Files.ToDictionary(item => item.Path,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<S14Entry> ScenarioDocuments
    {
        get
        {
            if (_scenarioDocuments is null) DiscoverScenarios();
            return _scenarioDocuments!;
        }
    }

    public IReadOnlyList<EffectiveScenario> EffectiveScenarios
    {
        get
        {
            if (_effectiveScenarios is null) DiscoverScenarios();
            return _effectiveScenarios!;
        }
    }

    public LoadedMap LoadCeMap()
    {
        const string relative = "0000/MapExCe.bin";
        (byte[] data, SourceProvenance source) = ReadSource(relative);
        return new LoadedMap(MapReader.Read(data), source);
    }

    /// <summary>
    /// Loads one original message file on demand. Variant names select installed filenames;
    /// they do not infer an unverified PK/CE text overlay from table sizes or record IDs.
    /// Overrides contain the full zp1 file and malformed overrides are reported without fallback.
    /// </summary>
    public LoadedMessages LoadMessages(string locale, OriginalMessageKind kind,
        OriginalMessageVariant variant = OriginalMessageVariant.ExCe)
    {
        if (locale is not ("SC" or "TC"))
            throw new ArgumentException("Original message locale must be SC or TC.", nameof(locale));
        string basename = kind switch
        {
            OriginalMessageKind.Data => "msgdata",
            OriginalMessageKind.Game => "msggame",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        string suffix = variant switch
        {
            OriginalMessageVariant.Standard => "",
            OriginalMessageVariant.ExCe => "_exce",
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        string relative = $"0001_{locale}/{basename}{suffix}.bin";
        (byte[] bytes, SourceProvenance source) = ReadSource(relative);
        try
        {
            Zp1Document container = Zp1Reader.Read(bytes);
            OriginalMessageDocument messages = MessageTableReader.Read(container.DecodedPayload, kind);
            string decodedHash = Convert.ToHexString(SHA256.HashData(container.DecodedPayload));
            return new LoadedMessages(messages, container, source, decodedHash);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original messages failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>
    /// Loads the selected experience file and its verified doctrine and growth tables.
    /// File selection is not a guess at runtime force experience or difficulty modifiers.
    /// </summary>
    public LoadedExperienceData LoadExperienceData(OriginalExperienceVariant variant = OriginalExperienceVariant.ExCe)
    {
        string relative = variant switch
        {
            OriginalExperienceVariant.Standard => "0000/experience_data.bin",
            OriginalExperienceVariant.ExCe => "0000/experience_data_exce.bin",
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        (byte[] bytes, SourceProvenance source) = ReadSource(relative);
        try
        {
            ExperienceDataDocument document = ExperienceDataReader.Read(bytes);
            return new LoadedExperienceData(document, ExperienceDoctrineReader.Read(document),
                ExperienceGrowthReader.Read(document), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original experience data failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads the verified CE static-world source fields without completing new-game initialization.</summary>
    public LoadedScenarioWorld LoadScenarioWorld(string relativePath, string? locale = null)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            locale ??= source.SourcePath.StartsWith("0010_SC/", StringComparison.OrdinalIgnoreCase) ? "SC"
                : source.SourcePath.StartsWith("0010_TC/", StringComparison.OrdinalIgnoreCase) ? "TC"
                : throw new InvalidDataException("Scenario world source requires an explicit SC or TC locale outside its original locale directory.");
            return new LoadedScenarioWorld(S14ScenarioWorldReader.Read(S14Reader.Read(bytes, true), locale), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original scenario world failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads CE static object rows and city raw validity flags; no current world is inferred.</summary>
    public LoadedScenarioObjects LoadScenarioObjects(string relativePath, string? locale = null)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            locale ??= source.SourcePath.StartsWith("0010_SC/", StringComparison.OrdinalIgnoreCase) ? "SC"
                : source.SourcePath.StartsWith("0010_TC/", StringComparison.OrdinalIgnoreCase) ? "TC"
                : throw new InvalidDataException("Scenario object source requires an explicit SC or TC locale outside its original locale directory.");
            if (locale is not ("SC" or "TC"))
                throw new InvalidDataException("Scenario object source locale must be SC or TC.");
            S14Document document = S14Reader.Read(bytes, true);
            return new(S14ScenarioObjectReader.Read(document), S14ScenarioTables.ReadScenarioCities(document), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original scenario objects failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads an event resource through the original-file URI and override resolver, without running its scripts.</summary>
    public LoadedEvents LoadEvents(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedEvents(S14EventReader.Read(bytes), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original events failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads fixed CTerrainData through the original-file URI and override resolver.</summary>
    public LoadedTerrainTypes LoadTerrainTypes(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new(S14TerrainTypeReader.Read(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original terrain types failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    public LoadedRankPersonCapacities LoadRankPersonCapacities(string relativePath="SAN14CE_SC.exe")
    {
        (byte[] bytes,SourceProvenance source)=ReadSource(relativePath);
        try{return new(OriginalRankPersonCapacityReader.Read(bytes),source);}
        catch(InvalidDataException error)
        {throw new InvalidDataException($"Original rank person capacities failed to load: {source.ResourceUri} ({source.PhysicalPath}). {error.Message}",error);}
    }

    public LoadedBaseParameters LoadBaseParameters(string relativePath = "SAN14CE_SC.exe")
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try { return new(OriginalBaseParameterReader.Read(bytes, source.Sha256), source); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Original base parameters failed to load: {source.ResourceUri} ({source.PhysicalPath}). {error.Message}", error); }
    }

    /// <summary>Loads one fixed-data policy catalog through the original-file URI and override resolver.</summary>
    public LoadedPolicies LoadPolicies(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedPolicies(S14PolicyReader.Read(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original policies failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads fixed-data doctrine definitions using the same URI and override resolver as other source data.</summary>
    public LoadedDoctrines LoadDoctrines(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedDoctrines(S14DoctrineReader.Read(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original doctrines failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads CE credit effects with source provenance and full-file override validation.</summary>
    public LoadedCreditEffects LoadCreditEffects(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedCreditEffects(S14CreditEffectReader.Read(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original credit effects failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads CE credit-rank definitions without initializing officer rank or merit.</summary>
    public LoadedCreditRanks LoadCreditRanks(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedCreditRanks(S14CreditRankReader.Read(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original credit ranks failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    public LoadedSceneries LoadSceneries(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try { return new(S14SceneryReader.ReadFixed(S14Reader.Read(bytes,false)),source); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original scenery definitions failed to load: {source.ResourceUri} ({source.PhysicalPath}). {error.Message}",error);
        }
    }

    public LoadedPersonalities LoadPersonalities(string relativePath)
    {
        (byte[] bytes,SourceProvenance source)=ReadSource(relativePath);
        try{return new(S14PersonalityReader.ReadFixed(S14Reader.Read(bytes,false)),source);}
        catch(InvalidDataException error)
        {throw new InvalidDataException($"Original officer trait definitions failed to load: {source.ResourceUri} ({source.PhysicalPath}). {error.Message}",error);}
    }

    public LoadedObjectDefinitions LoadObjectDefinitions(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try { return new(S14ObjectDefinitionReader.ReadFixed(S14Reader.Read(bytes, false)), source); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original object definitions failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    public LoadedGrowthModifierDefinitions LoadGrowthModifierDefinitions(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try { return new(S14GrowthModifierReader.Read(S14Reader.Read(bytes, false)), source); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original growth modifiers failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    public LoadedSkillDefinitions LoadSkillDefinitions(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            S14Document document = S14Reader.Read(bytes, false);
            if (document.FormatMarker != 0x66)
                throw new InvalidDataException("Skill definitions require the inspected CE fixed-data profile.");
            return new(S14FormationReader.Read(document), S14GemReader.ReadFixed(document), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original skill definitions failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads CE doctrine-panel parameters without inferring unlocked or active panels.</summary>
    public LoadedDoctrinePanels LoadDoctrinePanels(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedDoctrinePanels(S14DoctrinePanelReader.Read(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original doctrine panels failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads original item definitions without merging scenario-owned definition fields.</summary>
    public LoadedItemDefinitions LoadItemDefinitions(string relativePath)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedItemDefinitions(S14ItemReader.ReadDefinitions(S14Reader.Read(bytes, false)), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original item definitions failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Loads original ownership and any embedded definitions, preserving unknown source fields.</summary>
    public LoadedScenarioItems LoadScenarioItems(string relativePath, string locale)
    {
        (byte[] bytes, SourceProvenance source) = ReadSource(relativePath);
        try
        {
            return new LoadedScenarioItems(S14ItemReader.ReadScenarioItems(S14Reader.Read(bytes, true), locale), source);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original scenario items failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    public LoadedMap LoadMapForScenario(EffectiveScenario selection)
    {
        if (!EffectiveScenarios.Contains(selection))
            throw new InvalidDataException($"Scenario is not an effective installed revision: {selection.SourcePath}");
        S14Entry entry = ScenarioDocuments.Single(item =>
            item.SourcePath.Equals(selection.SourcePath, StringComparison.OrdinalIgnoreCase));
        string relative = entry.FormatMarker switch
        {
            0x33 => "0000/Map.bin",
            0x5c => "0000/MapEx.bin",
            0x66 => "0000/MapExCe.bin",
            _ => throw new InvalidDataException($"Unsupported scenario map edition: {entry.FormatMarker:X8}")
        };
        (byte[] data, SourceProvenance source) = ReadSource(relative);
        return new LoadedMap(MapReader.Read(data), source);
    }

    public LoadedScenario LoadScenario(EffectiveScenario selection)
    {
        S14Entry listed = ScenarioDocuments.Single(item =>
            item.SourcePath.Equals(selection.SourcePath, StringComparison.OrdinalIgnoreCase));
        if (!EffectiveScenarios.Contains(selection))
            throw new InvalidDataException($"Scenario is not an effective installed revision: {selection.SourcePath}");
        (byte[] scenarioBytes, SourceProvenance scenarioSource) = ReadSource(listed.SourcePath);
        S14Document scenario = S14Reader.Read(scenarioBytes, true);
        if (scenario.FormatMarker != listed.FormatMarker || scenario.Title != listed.Title ||
            scenario.StartYear != listed.StartYear || scenario.StartMonth != listed.StartMonth ||
            scenario.StartDay != listed.StartDay)
            throw new InvalidDataException($"Scenario header changed while loading: {listed.SourcePath}");
        string fixedName = scenario.FormatMarker switch
        {
            0x33 => "fixdata.s14",
            0x5c => "fixdataex.s14",
            0x66 => "fixdataexce.s14",
            _ => throw new InvalidDataException($"Unsupported scenario edition: {scenario.FormatMarker:X8}")
        };
        string fixedPath = $"0010_{selection.Locale}/{fixedName}";
        (byte[] fixedBytes, SourceProvenance fixedSource) = ReadSource(fixedPath);
        S14Document fixedData = S14Reader.Read(fixedBytes, false);
        string decodedSha = Convert.ToHexString(SHA256.HashData(scenario.DecodedPayload));
        ScenarioCityPreview snapshot;
        try
        {
            snapshot = ScenarioSnapshotBuilder.Build(listed.SourcePath,
                fixedPath, selection.Number, selection.Locale, scenario, fixedData, decodedSha);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Scenario snapshot failed: {scenarioSource.ResourceUri} " +
                $"({scenarioSource.PhysicalPath}); fixed data {fixedSource.ResourceUri} " +
                $"({fixedSource.PhysicalPath}). {error.Message}", error);
        }
        S14Entry metadata = new(listed.SourcePath, scenario.Signature,
            scenario.FormatMarker, scenario.Title, scenario.Description,
            scenario.DecodedPayload.Length, decodedSha, scenario.LeadingRecordCount)
        {
            StartYear = scenario.StartYear,
            StartMonth = scenario.StartMonth,
            StartDay = scenario.StartDay,
            CharacterNameCount = scenario.LeadingRecordCount - 1,
            CharacterAbilityCount = scenario.LeadingRecordCount - 1,
            CharacterCombatReferenceCount = snapshot.Characters.Count,
            CharacterOfficeReferenceCount = snapshot.Characters.Count,
            CharacterPolicyReferenceCount = snapshot.Characters.Count,
            CreditEffectRecordCount = snapshot.CreditEffectParameters?.Effects.Count,
            CreditRankRecordCount = snapshot.CreditRankParameters?.Ranks.Count,
            DoctrinePanelRecordCount = snapshot.DoctrinePanelParameters?.Panels.Count,
            ItemDefinitionCount = snapshot.ItemDefinitions?.Definitions.Count,
            ScenarioItemCount = snapshot.ScenarioItems?.Items.Count,
            ScenarioObjectSourceCount = snapshot.ObjectSourceRecords?.Objects.Count,
            ScenarioObjectSourceSha256 = snapshot.ObjectSourceRecords?.ObjectsSha256,
            ScenarioWorldBytes = snapshot.WorldParameters?.RawBytes.Length,
            ScenarioWorldSha256 = snapshot.WorldParameters?.WorldSha256,
            ScenarioWorldMode3RowCount = snapshot.WorldParameters is null ? null
                : snapshot.WorldParameters.Mode3Extension?.Groups.Sum(group => group.Rows.Count) ?? 0,
            CharacterCreditReferenceCount = snapshot.Characters.Any(person => person.CreditReference is not null)
                ? snapshot.Characters.Count(person => person.CreditReference is not null) : null,
            CharacterDoctrineReferenceCount = snapshot.Characters.Count,
            UnresolvedCharacterDoctrineReferenceCount = snapshot.Characters.Count(person =>
                person.CharacterId != 0 && person.RawDoctrineId is not (>= 1 and <= 6)),
            UnresolvedCharacterReferenceCount = snapshot.UnresolvedCharacters.Count,
            UnresolvedCharacterPlacementCount = snapshot.UnresolvedPlacements.Count,
            CityRecordCount = snapshot.Cities.Count,
            ForceRecordCount = snapshot.Forces.Count,
            ForceRankReferenceCount = S14RankReader.ReadForceRanks(scenario).Count,
            DistrictRecordCount = snapshot.Districts.Count,
            ScenarioAreaRecordCount = snapshot.Areas.Count,
            ScenarioGateRecordCount = snapshot.Gates.Count,
            CharacterPlacementCount = scenario.LeadingRecordCount - 1
        };
        return new LoadedScenario(metadata, snapshot, scenarioSource, fixedSource);
    }

    public LoadedTexture LoadTexture(string archiveName, uint fileId, int textureIndex = 0)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        return new LoadedTexture(G1TextureReader.Decode(asset.Bytes, textureIndex), asset.SourceContainerPath,
            asset.FileId, asset.DecodedSha256, asset.IndexSource)
        {
            OverrideSource = asset.OverrideSource,
            TextureIndex = textureIndex
        };
    }

    public LoadedLayout LoadLayout(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        return new LoadedLayout(LcskLayoutReader.Read(asset.Bytes), asset);
    }

    /// <summary>
    /// Loads a single model on demand, retaining its complete content hash and override
    /// provenance. Only the verified container headers are interpreted; geometry remains raw.
    /// </summary>
    public LoadedModelHeaders LoadModelHeaders(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            return new LoadedModelHeaders(G1ModelHeaderReader.Read(asset.Bytes, asset.Bytes.LongLength), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original model headers failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    /// <summary>
    /// Loads the verified raw geometry structures of one model with content/override provenance.
    /// Attribute meanings, draw bindings, materials and world coordinates remain unresolved.
    /// </summary>
    public LoadedModelGeometry LoadModelGeometry(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            return new LoadedModelGeometry(G1ModelGeometryReader.Read(asset.Bytes, asset.Bytes.LongLength), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original model geometry failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    /// <summary>
    /// Reads one model once, then validates its raw geometry and draw/group references.
    /// Primitive meaning, material resources and additional stream behavior remain unresolved.
    /// </summary>
    public LoadedModelDraws LoadModelDraws(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            G1ModelGeometry geometry = G1ModelGeometryReader.Read(asset.Bytes, asset.Bytes.LongLength);
            return new LoadedModelDraws(geometry, G1ModelDrawReader.Read(geometry), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original model draw bindings failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedModelVertices LoadModelVertices(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            G1ModelGeometry geometry = G1ModelGeometryReader.Read(asset.Bytes, asset.Bytes.LongLength);
            return new(geometry, G1ModelVertexReader.Read(geometry), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original model vertex attributes failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedModelMaterials LoadModelMaterials(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            G1ModelGeometry geometry = G1ModelGeometryReader.Read(asset.Bytes, asset.Bytes.LongLength);
            return new(geometry, G1ModelMaterialReader.Read(geometry), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original model material records failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedTextureContainer LoadTextureContainer(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try { return new(G1TextureContainerReader.Read(asset.Bytes), asset); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original texture container failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedShaderContainer LoadShaderContainer(string archiveName, uint fileId)
    {
        LoadedRdbAsset source = LoadRdbAsset(archiveName, fileId);
        try { return new(G2ShaderContainerReader.Read(source.Bytes), source); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader container inspection failed: {source.ResourceUri}. {error.Message}", error); }
    }

    public LoadedModelTransforms LoadModelTransforms(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            G1ModelGeometry geometry = G1ModelGeometryReader.Read(asset.Bytes, asset.Bytes.LongLength);
            return new(geometry, G1ModelTransformReader.Read(asset.Bytes, geometry), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original model transforms failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedHeightField LoadHeightField(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            return new LoadedHeightField(MhkHeightFieldReader.Read(asset.Bytes), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original height field failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedTaskGraph LoadTaskGraph(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try { return new(TaskGraphReader.Read(asset.Bytes), asset); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"TaskGraph decode failed: {asset.ResourceUri}. {error.Message}", error); }
    }

    public LoadedRenderGraph LoadRenderGraph(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try { return new(RenderGraphReader.Read(asset.Bytes), asset); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"RenderGraph decode failed: {asset.ResourceUri}. {error.Message}", error); }
    }

    public LoadedObjects LoadObjects(string archiveName, uint fileId)
    {
        LoadedRdbAsset asset = LoadRdbAsset(archiveName, fileId);
        try
        {
            return new LoadedObjects(DokReader.Read(asset.Bytes), asset);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original objects failed to load: {asset.ResourceUri}. {error.Message}", error);
        }
    }

    public LoadedSprite LoadSprite(string archiveName, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Sprite name is missing.", nameof(name));
        if (!_spriteDirectories.TryGetValue(archiveName, out RdbSpriteDirectory? directory))
        {
            RdbIndex index = LoadRdbIndex(archiveName);
            directory = RdbSpriteDirectory.Read(SourcePath($"0002/{archiveName}.rdb"), index,
                record => LoadRdbAsset(archiveName, record.FileId).Bytes);
            _spriteDirectories.Add(archiveName, directory);
        }
        RdbSpriteBinding binding = directory.Find(name);
        LoadedRdbAsset atlas = LoadRdbAsset(archiveName, binding.AtlasId);
        SpriteDefinition definition = SpriteAtlasReader.Read(atlas.Bytes).SingleOrDefault(sprite => sprite.Name == name)
            ?? throw new InvalidDataException($"Sprite atlas changed after discovery: {name}.");
        if (definition.Value64 > int.MaxValue)
            throw new InvalidDataException($"Sprite {name} has an invalid texture index {definition.Value64}.");
        LoadedTexture texture = LoadTexture(archiveName, binding.TextureId!.Value, (int)definition.Value64);
        _ = definition.GetRectangle(texture.Texture.Width, texture.Texture.Height);
        return new LoadedSprite(definition, texture, atlas);
    }

    private RdbIndex LoadRdbIndex(string archiveName)
    {
        _ = GameResourceId.Rdb(archiveName, 0);
        string relative = $"0002/{archiveName}.rdb";
        if (_indices.TryGetValue(relative, out RdbIndex? index)) return index;
        (byte[] bytes, _) = ReadSource(relative);
        index = RdbIndexReader.Read(bytes);
        _indices.Add(relative, index);
        _assetRecords.Add(relative, index.Records.ToDictionary(record => record.FileId));
        return index;
    }

    public LoadedRdbAsset LoadRdbAsset(string archiveName, uint fileId)
    {
        GameResourceId resourceId = GameResourceId.Rdb(archiveName, fileId);
        string relative = $"0002/{archiveName}.rdb";
        RdbIndex index = LoadRdbIndex(archiveName);
        if (!_assetRecords[relative].TryGetValue(fileId, out RdbRecord? record))
            throw new KeyNotFoundException($"RDB asset {archiveName}/{fileId:X8} is missing.");
        string rdbPath = SourcePath(relative);
        string container = RdbArchiveReader.ContainerPath(rdbPath, index, record);
        string containerRelative = Path.GetRelativePath(SourceDirectory, container)
            .Replace('\\', '/');
        _ = SourcePath(containerRelative); // Reject references outside the installation.
        string? overridePath = _overrides.Find(resourceId);
        byte[] decoded = overridePath is null
            ? RdbArchiveReader.ReadPayload(rdbPath, index, record)
            : File.ReadAllBytes(overridePath);
        string hash = Convert.ToHexString(SHA256.HashData(decoded));
        SourceProvenance? overrideSource = null;
        if (overridePath is not null)
        {
            overrideSource = new SourceProvenance(resourceId.OverrideRelativePath,
                decoded.LongLength, hash, null)
            {
                ResourceUri = resourceId.ToString(),
                PhysicalPath = overridePath,
                IsOverride = true
            };
            _readFiles[resourceId.ToString()] = overrideSource;
        }
        else
            _readFiles.Remove(resourceId.ToString());
        return new LoadedRdbAsset(decoded, resourceId.ToString(), containerRelative,
            fileId, hash, _readFiles[GameResourceId.File(relative).ToString()], overrideSource);
    }

    private void DiscoverScenarios()
    {
        var entries = new List<S14Entry>();
        foreach (string locale in new[] { "SC", "TC" })
        {
            string relativeDirectory = $"0010_{locale}";
            foreach (string path in Directory.EnumerateFiles(SourcePath(relativeDirectory),
                         "sceda*.s14").Order(StringComparer.OrdinalIgnoreCase))
            {
                string relative = $"{relativeDirectory}/{Path.GetFileName(path)}";
                string selected = _overrides.Find(GameResourceId.File(relative)) ?? path;
                using var stream = new FileStream(selected, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.SequentialScan);
                byte[] headerBytes = new byte[(int)Math.Min(stream.Length, 754 + 268)];
                stream.ReadExactly(headerBytes);
                S14Header header = S14Reader.ReadHeader(headerBytes, true);
                entries.Add(new S14Entry(relative, header.Signature,
                    header.FormatMarker, header.Title, header.Description, 0, "", null)
                {
                    StartYear = header.StartYear,
                    StartMonth = header.StartMonth,
                    StartDay = header.StartDay
                });
            }
        }
        IReadOnlyList<EffectiveScenario> resolved = ScenarioResolver.Resolve(entries);
        _scenarioDocuments = entries;
        _effectiveScenarios = resolved;
    }

    private (byte[] Bytes, SourceProvenance Provenance) ReadSource(string relative)
    {
        GameResourceId resourceId = GameResourceId.File(relative);
        string? overridePath = _overrides.Find(resourceId);
        string physicalPath = overridePath ?? SourcePath(relative);
        byte[] data = File.ReadAllBytes(physicalPath);
        string hash = Convert.ToHexString(SHA256.HashData(data));
        _baseline.TryGetValue(relative, out SourceFile? expected);
        var provenance = new SourceProvenance(relative, data.LongLength, hash,
            overridePath is null ? expected?.Sha256 : null)
        {
            ResourceUri = resourceId.ToString(),
            PhysicalPath = physicalPath,
            IsOverride = overridePath is not null
        };
        _readFiles[resourceId.ToString()] = provenance;
        return (data, provenance);
    }

    private string SourcePath(string relative)
    {
        string path = Path.GetFullPath(Path.Combine(SourceDirectory,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(SourceDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Source path escapes the installed game: {relative}");
        return path;
    }
}
