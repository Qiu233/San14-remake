using San14.Core;

namespace San14.Data;

/// <summary>Reads historical saves using their resource-backed record directory.
/// Current saves use the ordinary independent game-state codec without loading
/// resource rows. Writing never depends on original files.</summary>
public sealed class ResourceSaveCodec : ICampaignOperationSaveCodec
{
    private readonly GameResourceRepository? _repository;

    public ResourceSaveCodec() { }
    public ResourceSaveCodec(GameResourceRepository repository) => _repository = repository;
    public string FormatId => "san14-remake-json-gzip";
    public byte[] Encode(GameState state) => EncodeOwned(state, null);
    public byte[] EncodeOperationWorld(GameState state, CampaignOperationWorldContext context) => EncodeOwned(state, context);
    private byte[] EncodeOwned(GameState state, CampaignOperationWorldContext? context)
    {
        state = CityDistrictSourceAdapter.PruneReplacedChannels(state);
        state = ArmyConfigurationSourceAdapter.PruneReplacedChannels(state);
        state = ArmyTraitSourceAdapter.PruneReplacedChannels(state);
        state = TransportRouteSourceAdapter.PruneReplacedChannels(state);
        state = ArmyOverrideSourceKnowledgeValidation.PruneReplacedChannels(state);
        state = ArmyPositionSourceKnowledge.PruneReplacedChannels(state);
        // Channel pruning is part of the saved representation. Cross-owner
        // consistency checks run when decoding external data, not before writing.
        return new JsonGzipSaveCodec(resourceStateCodec: new CampaignResourceStateCodec(), operationContext: context).Encode(state);
    }
    public GameState Decode(ReadOnlySpan<byte> bytes) => DecodeOwned(bytes, null);
    public GameState DecodeOperationWorld(ReadOnlySpan<byte> bytes, CampaignOperationWorldContext context) => DecodeOwned(bytes, context);
    private GameState DecodeOwned(ReadOnlySpan<byte> bytes, CampaignOperationWorldContext? context)
    {
        var resourceCodec = new CampaignResourceStateCodec();
        var state = new JsonGzipSaveCodec(_repository is null ? null : new LegacyOfficerAbilitySource(_repository),
            resourceCodec, context).Decode(bytes);
        state = CityDistrictSourceAdapter.MigrateLegacy(state, resourceCodec.DecodedVersion);
        ArmyTargetSourceKnowledgeValidation.Validate(state);
        ArmyOverrideSourceKnowledgeValidation.Validate(state);
        ArmyPositionSourceKnowledge.Validate(state);
        ArmyReceiverSourceValidation.Validate(state);
        TransportRouteSourceAdapter.Validate(state);
        ArmyTraitSourceAdapter.Validate(state);
        ArmyConfigurationSourceAdapter.Validate(state);
        CityDistrictSourceAdapter.Validate(state);
        OwnedMapObjectLifecycle.Validate(state);
        return state;
    }

    // One directory per resource in a single migration. Later save reads obtain
    // a fresh migration source so resource updates cannot borrow an older table.
    private sealed class LegacyOfficerAbilitySource(GameResourceRepository repository) : ILegacyOfficerAbilitySource
    {
        private readonly Dictionary<string, (string Hash, string Language, OfficerSourceDirectory Directory)> _sources = [];

        public int? ResolveRecord(string resourceUri, string sourceSha256, string language, int officerId)
        {
            if (!_sources.TryGetValue(resourceUri, out var source))
            {
                EffectiveScenario? selection = repository.EffectiveScenarios.SingleOrDefault(item =>
                    GameResourceId.File(item.SourcePath).ToString() == resourceUri);
                if (selection is null)
                    throw new InvalidDataException("Historical officer scenario resource is not installed.");
                if (selection.Locale != language)
                    throw new InvalidDataException("Historical officer scenario resource uses an incompatible language.");
                LoadedScenario scenario = repository.LoadScenario(selection);
                if (scenario.Metadata.FormatMarker != 0x66 || scenario.ScenarioSource.ResourceUri != resourceUri ||
                    scenario.ScenarioSource.Sha256 != sourceSha256)
                    throw new InvalidDataException("Historical officer scenario resource has an incompatible version.");
                var directory = new OfficerSourceDirectory();
                directory.ClearLogicalReferences();
                foreach (var row in scenario.Snapshot.Characters)
                {
                    directory.SetRecordIdentity(row.Slot, row.CharacterId);
                    if (row.Slot == 0 || row.CharacterId != 0) directory.SetLogicalReference(row.CharacterId, row.Slot);
                }
                source = (scenario.ScenarioSource.Sha256, scenario.Snapshot.Locale, directory);
                _sources.Add(resourceUri, source);
            }
            if (source.Hash != sourceSha256 || source.Language != language)
                throw new InvalidDataException("Historical officer scenario identities disagree within the save.");
            return source.Directory.ResolveOfficer(officerId);
        }
    }
}
