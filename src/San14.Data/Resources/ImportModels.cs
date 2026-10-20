namespace San14.Data;

public sealed record SourceFile(string Path, long Bytes, string Sha256)
{
    public string? FileVersion { get; init; }
    public string? ProductVersion { get; init; }
}

public sealed record SourceManifest(
    string SourceDirectory,
    string Edition,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<SourceFile> Files);

public sealed record MapEntry(string SourcePath, int Width, int Height,
    IReadOnlyDictionary<byte, int> SurfaceCodes);

public sealed record S14Entry(string SourcePath, string Signature,
    uint FormatMarker, string? Title, string Description, int DecodedBytes,
    string DecodedSha256, int? LeadingRecordCount)
{
    public int? StartYear { get; init; }
    public int? StartMonth { get; init; }
    public int? StartDay { get; init; }
    public int? CharacterNameCount { get; init; }
    public int? CharacterAbilityCount { get; init; }
    public int? UnresolvedCharacterReferenceCount { get; init; }
    public int? UnresolvedCharacterPlacementCount { get; init; }
    public int? CityRecordCount { get; init; }
    public int? ForceRecordCount { get; init; }
    public int? RankRecordCount { get; init; }
    public int? OfficeRecordCount { get; init; }
    public int? ForceRankReferenceCount { get; init; }
    public int? CharacterOfficeReferenceCount { get; init; }
    public int? FormationRecordCount { get; init; }
    public int? TacticRecordCount { get; init; }
    public int? TacticEffectRecordCount { get; init; }
    public int? PolicyRecordCount { get; init; }
    public int? PolicyEffectRecordCount { get; init; }
    public int? DoctrineRecordCount { get; init; }
    public int? CreditEffectRecordCount { get; init; }
    public int? CreditRankRecordCount { get; init; }
    public int? DoctrinePanelRecordCount { get; init; }
    public int? ItemDefinitionCount { get; init; }
    public int? ScenarioItemCount { get; init; }
    public int? CharacterCreditReferenceCount { get; init; }
    public int? CharacterPolicyReferenceCount { get; init; }
    public int? CharacterDoctrineReferenceCount { get; init; }
    public int? UnresolvedCharacterDoctrineReferenceCount { get; init; }
    public int? CharacterCombatReferenceCount { get; init; }
    public int? AreaAdjacencyReferenceCount { get; init; }
    public int? DistrictRecordCount { get; init; }
    public int? ScenarioAreaRecordCount { get; init; }
    public int? ScenarioGateRecordCount { get; init; }
    public int? CharacterPlacementCount { get; init; }
    public int? GateRecordCount { get; init; }
    public int? AreaRecordCount { get; init; }
    public int? ActiveAreaRecordCount { get; init; }
    public bool? MapCodesValidated { get; init; }
    public int? ScenarioWorldBytes { get; init; }
    public string? ScenarioWorldSha256 { get; init; }
    public int? ScenarioWorldMode3RowCount { get; init; }
    public int? ScenarioObjectSourceCount { get; init; }
    public string? ScenarioObjectSourceSha256 { get; init; }
}

public sealed record ForcePreviewRecord(int Slot, ushort RulerSlot,
    string? RulerName, string RemainingBytesHex)
{
    public int? RankId { get; init; }
    public byte? RawKingdomName48 { get; init; }
    public byte? RawReportColor49 { get; init; }
    public byte[]? RawIndexedBytes13 { get; init; }
    public byte[]? RawIndexedBytesEA { get; init; }
}

public sealed record DistrictPreviewRecord(int Slot, byte ForceSlot, byte Number,
    ushort LeaderSlot, string? LeaderName);

public sealed record CityPreviewRecord(int Slot, string Name, ushort OfficerSlot,
    string? OfficerName, byte DistrictSlot, byte ForceSlot, uint Gold, uint Supplies,
    uint Troops, byte Morale, ushort Durability, byte SizeCode,
    string RemainingBytesHex, string SecondaryText,
    IReadOnlyList<byte> LinkedLocationSlots, uint RawLimit0, uint RawLimit1,
    uint RawLimit2, byte GroupCode, int? HexId)
{
    // Legacy property name: this retains City+46, a base capacity input.
    // Current CE durability belongs to the associated Object+14 source field.
    public byte? RawValidityFlag69 { get; init; }
}

public sealed record GatePreviewRecord(int Slot, int LocationSlot, string Name,
    string SecondaryText, IReadOnlyList<byte> LinkedLocationSlots, int? HexId,
    ushort OfficerSlot, string? OfficerName, byte DistrictSlot, byte ForceSlot,
    uint Amount0, uint Amount1, uint Troops, string StateRemainingBytesHex)
{
    public byte? RawValidityFlag69 { get; init; }
    public byte? RawMorale44 { get; init; }
    public ushort? RawField46 { get; init; }
}

public sealed record AreaPreviewRecord(int Slot, string Name, string SecondaryText,
    byte CitySlot, ushort HexId, string RemainingBytesHex, ushort Amount0,
    byte Value2, byte ForceSlot, string StateRemainingBytesHex)
{
    public byte? FacilityCapacity { get; init; }
    public ushort? RawField44 { get; init; }
    public ushort? RawField46 { get; init; }
    public IReadOnlyList<int> AdjacentAreaSlots { get; init; } = [];
    public IReadOnlyList<ushort>? RawNeighborSlots { get; init; }
    public byte RegionalNeighborMask { get; init; }
    public byte? RawFlags38 { get; init; }
    public byte? RawFlag74 { get; init; }
}

public sealed record CharacterPreviewRecord(int Slot, string Name, byte DistrictSlot, byte ForceSlot,
    ushort CitySlot, ushort SecondaryCitySlot, byte StatusCode, string RawBytesHex,
    byte Leadership, byte Strength, byte Intelligence, byte Politics, byte Charm)
{
    public int CharacterId { get; init; }
    public int AppearanceCode { get; init; }
    public string? NameSortKey { get; init; }
    public string? SurnameSortKey { get; init; }
    public string? GivenNameSortKey { get; init; }
    public byte? RawSexCode { get; init; }
    public byte? DialogueStyle { get; init; }
    public ushort? BirthYear { get; init; }
    public byte? FamilyGeneration { get; init; }
    public string? FamilyName { get; init; }
    public string? GivenName { get; init; }
    public string? CourtesyName { get; init; }
    public ushort? AppearanceYear { get; init; }
    public ushort? TerminalYear { get; init; }
    public byte? LifespanKind { get; init; }
    public byte? RawOfficeId { get; init; }
    public byte? RawPolicySlot { get; init; }
    public byte? RawPolicyBaseCost { get; init; }
    public byte[]? RawPolicyFieldBytes { get; init; }
    public byte? RawDoctrineId { get; init; }
    public ushort? RawDoctrineWord { get; init; }
    public byte[]? RawDoctrineFieldBytes { get; init; }
    public Formats.S14CharacterCreditReference? CreditReference { get; init; }
    // Older previews lack these raw CE fields; null remains unknown rather than empty.
    public Formats.S14PersonSkillReference? SkillSource { get; init; }
    public Formats.S14PersonDuelConditionReference? DuelConditionSource { get; init; }
    public IReadOnlyList<int>? BaseFormationSlots { get; init; }
    public IReadOnlyList<int>? BaseTacticSlots { get; init; }
}

public sealed record ScenarioCityPreview(string SourcePath, string DecodedSha256,
    string FixedDataPath, int Number, string Locale,
    IReadOnlyList<CityPreviewRecord> Cities,
    IReadOnlyList<ForcePreviewRecord> Forces,
    IReadOnlyList<CharacterPreviewRecord> AffiliatedCharacters)
{
    public Formats.S14RankCatalog? RankParameters { get; init; }
    public Formats.S14FormationCatalog? CombatParameters { get; init; }
    public Formats.S14PolicyCatalog? PolicyParameters { get; init; }
    public Formats.S14DoctrineCatalog? DoctrineParameters { get; init; }
    public Formats.S14CreditEffectCatalog? CreditEffectParameters { get; init; }
    public Formats.S14CreditRankCatalog? CreditRankParameters { get; init; }
    public Formats.S14DoctrinePanelCatalog? DoctrinePanelParameters { get; init; }
    public Formats.S14ItemDefinitionCatalog? ItemDefinitions { get; init; }
    public Formats.S14ScenarioItemCatalog? ScenarioItems { get; init; }
    public Formats.S14ScenarioWorldDocument? WorldParameters { get; init; }
    public Formats.S14ScenarioObjectCatalog? ObjectSourceRecords { get; init; }
    public IReadOnlyList<GatePreviewRecord> Gates { get; init; } = [];
    public IReadOnlyList<AreaPreviewRecord> Areas { get; init; } = [];
    public IReadOnlyList<DistrictPreviewRecord> Districts { get; init; } = [];
    public IReadOnlyList<CharacterPreviewRecord> Characters { get; init; } = [];
    public IReadOnlyList<UnresolvedCharacterReference> UnresolvedCharacters { get; init; } = [];
    public IReadOnlyList<UnresolvedCharacterPlacement> UnresolvedPlacements { get; init; } = [];
}

public sealed record ImportIssue(string SourcePath, string Detail);

public sealed record RdbIndexSummary(string SourcePath,
    int RecordCount, int ResolvedContainers, int UnresolvedContainers)
{
    public int? SpriteAtlasRecordCount { get; init; }
    public int? SpriteRecordCount { get; init; }
    public int? UnresolvedSpriteAtlasCount { get; init; }
}

public sealed record MessageSectionSummary(int Index, int RecordCount,
    int LiteralCount, int UninterpretedByteCount);

public sealed record MessageFileSummary(string SourcePath, string Locale,
    string Kind, string Variant, int SourceBytes, string SourceSha256,
    int DecodedBytes, string DecodedSha256, int ChunkCount,
    IReadOnlyList<MessageSectionSummary> Sections)
{
    // Game message control bytes are retained but have no implemented evaluator.
    public bool? BytecodeInterpreted { get; init; }
}

public sealed record ExperienceTableSummary(int Index, int RecordCount,
    int RecordStride);

public sealed record ExperienceFileSummary(string SourcePath, int SourceBytes,
    string SourceSha256, int DecodedBytes, string DecodedSha256,
    IReadOnlyList<ExperienceTableSummary> Tables)
{
    // This flag concerns the entire file; the doctrine and growth projections do
    // not imply that other tables or complete runtime rules are interpreted.
    public bool ValuesInterpreted { get; init; }
    public Formats.ExperienceDoctrineCatalog? DoctrineTable { get; init; }
    public Formats.ExperienceGrowthCatalog? GrowthTable { get; init; }
}

public sealed record EventStreamSummary(int StreamCount, int OperationCount,
    IReadOnlyDictionary<int, int> CodeCounts);

public sealed record EventFileSummary(string SourcePath, int SourceBytes,
    string SourceSha256, int HeaderBytes, string HeaderSha256,
    int EventRecordOffset, int EventRecordStride, int EventRecordCount,
    string EventTableSha256, EventStreamSummary Conditions,
    EventStreamSummary Executions, int OpaqueByteCount)
{
    // Structural parsing preserves bytecode; it does not execute event gameplay.
    public bool BytecodeInterpreted { get; init; }
}

public sealed record ImportCatalog(
    string Edition,
    IReadOnlyList<string> Locales,
    IReadOnlyList<MapEntry> Maps,
    IReadOnlyList<S14Entry> Documents,
    IReadOnlyList<ImportIssue> Issues,
    IReadOnlyList<string> PendingFormats)
{
    public IReadOnlyList<RdbIndexSummary> RdbIndices { get; init; } = [];
    public IReadOnlyList<MessageFileSummary> Messages { get; init; } = [];
    public IReadOnlyList<ExperienceFileSummary> ExperienceData { get; init; } = [];
    public IReadOnlyList<EventFileSummary> Events { get; init; } = [];
    public IReadOnlyList<EffectiveScenario> EffectiveScenarios { get; init; } = [];
    public int LocalePairsValidated { get; init; }
    public bool BuildReady => Issues.Count == 0 && PendingFormats.Count == 0;
}
