using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
namespace San14.Core;

public sealed record GameSessionSave(GameState State, int PlayerForceId)
{
    public CampaignProgressOperation? PendingProgress { get; init; }
    public ManualTacticProgressOperation? PendingManualTactic { get; init; }
    public CampaignArmyProgressOperation? PendingArmyProgress { get; init; }
    public NormalPeriodOperation? PendingNormalPeriod { get; init; }
    public long LastStorySequence { get; init; }
    public DateTimeOffset? SavedAt { get; init; }
}
public sealed class SessionSaveStore(ISaveCodec codec)
{
    private static readonly byte[] Magic = "S14S"u8.ToArray();
    private const int Version = 15;
    private static readonly JsonSerializerOptions Options = new() { RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public void Save(string path, GameState state, int playerForceId, CampaignProgressOperation? progress = null, long lastStorySequence = 0, ManualTacticProgressOperation? manualTactic = null, DateTimeOffset? savedAt = null, NormalPeriodOperation? normalPeriod = null, CampaignArmyProgressOperation? armyProgress = null)
    {
        //These legacy fields held offline combat-planner proof images, not
        //inputs used by a running continuation or capture reward. New saves
        //preserve the real candidate without generating diagnostic Worlds.
        if (progress is not null) progress = progress with { Captures = [] };
        if (manualTactic is not null) manualTactic = manualTactic with { Captures = [] };
        if (armyProgress is not null) armyProgress = armyProgress with { Captures = [] };
        ValidateSession(state, playerForceId); ValidateProgress(state, progress, lastStorySequence, manualTactic, normalPeriod, armyProgress);
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
        {
            writer.Write(Magic); writer.Write(Version); writer.Write(playerForceId);
            Write(writer, codec.Encode(state)); writer.Write(lastStorySequence); writer.Write((byte)(armyProgress is not null ? 4 : normalPeriod is not null ? 3 : manualTactic is not null ? 2 : progress is not null ? 1 : 0));
            if (progress is not null)
            {
                CampaignStorySelectionSaveFields.ValidateRequest(progress.Story);
                var operationCodec = codec as ICampaignOperationSaveCodec ?? throw new InvalidDataException("This codec cannot preserve an in-flight campaign.");
                // World values use their complete resource-state codec below.
                // Explicit null placeholders keep all constructor fields required.
                var metadata = progress with { Candidate = null! };
                Write(writer, JsonSerializer.SerializeToUtf8Bytes(metadata, Options));
                Write(writer, operationCodec.EncodeOperationWorld(progress.Candidate, progress.Context));
            }
            else if (manualTactic is not null)
            {
                var operationCodec = codec as ICampaignOperationSaveCodec ?? throw new InvalidDataException("This codec cannot preserve a manual tactic transaction.");
                var metadata = manualTactic with { Candidate = null! };
                Write(writer, JsonSerializer.SerializeToUtf8Bytes(metadata, Options));
                Write(writer, operationCodec.EncodeOperationWorld(manualTactic.Candidate, manualTactic.Context));
            }
            else if (armyProgress is not null)
            {
                var operationCodec = codec as ICampaignOperationSaveCodec ?? throw new InvalidDataException("This codec cannot preserve an in-flight Army progress transaction.");
                var metadata = armyProgress with { Candidate = null! };
                Write(writer, JsonSerializer.SerializeToUtf8Bytes(metadata, Options));
                Write(writer, operationCodec.EncodeOperationWorld(armyProgress.Candidate, armyProgress.Context));
            }
            if (normalPeriod is not null) Write(writer, codec.Encode(normalPeriod.Candidate));
            writer.Write((byte)(savedAt.HasValue ? 1 : 0));
            if (savedAt is { } timestamp)
            {
                writer.Write(timestamp.Ticks);
                writer.Write((short)(timestamp.Offset.Ticks / TimeSpan.TicksPerMinute));
            }
        }
        AtomicSaveFiles.Write(path, output.ToArray());
    }
    public GameSessionSave Load(string path)
    {
        try
        {
            using var input = new MemoryStream(File.ReadAllBytes(path));
            using var reader = new BinaryReader(input, Encoding.UTF8, true);
            int version = ReadHeader(reader);
            int player = reader.ReadInt32(); GameState state = codec.Decode(Read(reader)); ValidateSession(state, player);
            long sequence = 0; CampaignProgressOperation? progress = null; ManualTacticProgressOperation? manualTactic = null;
            NormalPeriodOperation? normalPeriod = null;
            CampaignArmyProgressOperation? armyProgress = null;
            if (version >= 2)
            {
                sequence = reader.ReadInt64(); byte present = ReadPendingPresence(reader, version);
                if (present == 1)
                {
                    var operationCodec = codec as ICampaignOperationSaveCodec ?? throw new InvalidDataException("This codec cannot restore an in-flight campaign.");
                    progress = ReadMetadata<CampaignProgressOperation>(reader, version);
                    progress = progress with { Candidate = operationCodec.DecodeOperationWorld(Read(reader), progress.Context) };
                    //Older framed proof images have no runtime consumer.
                    //Read their exact payload boundaries without inflating
                    //or copying any historical diagnostic World.
                    foreach (var capture in progress.Captures) SkipPayload(reader);
                    progress = progress with { Captures = [] };
                }
                else if (present == 2)
                {
                    var operationCodec = codec as ICampaignOperationSaveCodec ?? throw new InvalidDataException("This codec cannot restore a manual tactic transaction.");
                    manualTactic = ReadMetadata<ManualTacticProgressOperation>(reader, version);
                    manualTactic = manualTactic with { Candidate = operationCodec.DecodeOperationWorld(Read(reader), manualTactic.Context) };
                    foreach (var capture in manualTactic.Captures) SkipPayload(reader);
                    manualTactic = manualTactic with { Captures = [] };
                }
                else if (present == 3) normalPeriod = new(codec.Decode(Read(reader)));
                else if (present == 4)
                {
                    var operationCodec = codec as ICampaignOperationSaveCodec ?? throw new InvalidDataException("This codec cannot restore an in-flight Army progress transaction.");
                    armyProgress = ReadMetadata<CampaignArmyProgressOperation>(reader, version);
                    armyProgress = armyProgress with { Candidate = operationCodec.DecodeOperationWorld(Read(reader), armyProgress.Context) };
                    foreach (var capture in armyProgress.Captures) SkipPayload(reader);
                    armyProgress = armyProgress with { Captures = [] };
                }
            }
            DateTimeOffset? savedAt = ReadTimestamp(reader, version);
            RequireEnd(reader);
            ValidateProgress(state, progress, sequence, manualTactic, normalPeriod, armyProgress);
            return new(PlayableSkeleton.UpgradeSession(state), player) { PendingProgress = progress, PendingManualTactic = manualTactic, PendingNormalPeriod = normalPeriod, PendingArmyProgress = armyProgress, LastStorySequence = sequence, SavedAt = savedAt };
        }
        catch (Exception error) when (error is EndOfStreamException or JsonException or NullReferenceException or ArgumentException)
        { throw new InvalidDataException("Saved campaign continuation is incomplete or invalid.", error); }
    }
    /// <summary>Read the existing session timestamp without decoding or
    /// decompressing any committed, candidate or captured world. The framing
    /// is still checked from its real header; no footer guess is accepted.
    /// Operation metadata is parsed only to locate its capture blocks.</summary>
    public static DateTimeOffset? ReadSavedAt(string path)
    {
        try
        {
            using var input = File.OpenRead(path);
            using var reader = new BinaryReader(input, Encoding.UTF8, true);
            int version = ReadHeader(reader);
            _ = reader.ReadInt32(); //Existing player identity, not a world-validation request.
            SkipPayload(reader);
            if (version >= 2)
            {
                _ = reader.ReadInt64(); //Existing last-story sequence.
                byte present = ReadPendingPresence(reader, version);
                if (present == 3) SkipPayload(reader);
                else if (present != 0)
                {
                    using var metadata = JsonDocument.Parse(Read(reader));
                    if (metadata.RootElement.ValueKind != JsonValueKind.Object ||
                        !metadata.RootElement.TryGetProperty("Captures", out var captures) ||
                        captures.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException("Pending session capture metadata is invalid.");
                    int count = captures.GetArrayLength();
                    SkipPayload(reader); //The actual candidate block.
                    for (int index = 0; index < count; index++) SkipPayload(reader);
                }
            }
            var savedAt = ReadTimestamp(reader, version);
            RequireEnd(reader);
            return savedAt;
        }
        catch (Exception error) when (error is EndOfStreamException or JsonException or ArgumentException)
        { throw new InvalidDataException("Saved campaign continuation is incomplete or invalid.", error); }
    }

    private static int ReadHeader(BinaryReader reader)
    {
        if (!reader.ReadBytes(4).SequenceEqual(Magic)) throw new InvalidDataException("Session save magic is invalid.");
        int version = reader.ReadInt32();
        if (version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or Version)) throw new InvalidDataException("Session save format is unsupported.");
        return version;
    }
    private static byte ReadPendingPresence(BinaryReader reader, int version)
    {
        byte present = reader.ReadByte();
        if (present > (version >= 15 ? 4 : version >= 5 ? 3 : version == 2 ? 1 : 2)) throw new InvalidDataException("Pending campaign presence is invalid.");
        return present;
    }
    private static T ReadMetadata<T>(BinaryReader reader, int version) where T : class
    {
        if (JsonNode.Parse(Read(reader)) is not JsonObject metadata || metadata["ServiceChanges"] is not JsonObject service)
            throw new InvalidDataException("Pending campaign service metadata is missing.");
        foreach (string key in new[] { "CompletedScenarios", "CompletedEndingTracks", "UnlockedBonusOfficerIndices", "CompletedTutorialLessons" })
            if (version < 6)
            {
                if (service[key] is JsonNode node && (node is not JsonArray array || array.Count != 0))
                    throw new InvalidDataException("Legacy session contains future Ending service writes.");
                service[key] = new JsonArray();
            }
        CampaignStorySelectionSaveFields.MigrateOperationMetadata(metadata, version);
        CampaignStoryMessageBankSaveFields.MigrateOperationMetadata(metadata, version);
        CampaignStoryInvocationPresentationSaveFields.MigrateOperationMetadata(metadata, version);
        CampaignStoryPresentationSaveFields.MigrateOperationMetadata(metadata, version);
        return metadata.Deserialize<T>(Options) ?? throw new InvalidDataException("Pending campaign metadata is missing.");
    }
    private static DateTimeOffset? ReadTimestamp(BinaryReader reader, int version)
    {
        if (version < 4) return null;
        byte present = reader.ReadByte();
        if (present > 1) throw new InvalidDataException("Session save timestamp presence is invalid.");
        if (present == 0) return null;
        long ticks = reader.ReadInt64(); short offsetMinutes = reader.ReadInt16();
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || offsetMinutes is < -840 or > 840)
            throw new InvalidDataException("Session save timestamp is outside its date or offset range.");
        //The constructor also requires the actual UTC instant to be representable.
        return new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMinutes));
    }
    private static void RequireEnd(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Session save contains an unexpected suffix.");
    }

    private static void ValidateProgress(GameState state, CampaignProgressOperation? progress, long sequence, ManualTacticProgressOperation? manualTactic = null, NormalPeriodOperation? normalPeriod = null, CampaignArmyProgressOperation? armyProgress = null)
    {
        if (state.CampaignEventCommandPlayback is { } command &&
            (progress is not null || manualTactic is not null || normalPeriod is not null || armyProgress is not null || sequence < command.Invocation.Story.Sequence))
            throw new InvalidDataException("Saved ordinary event command has a conflicting owner or sequence.");
        if (state.EditedEventPlayback is { } edited &&
            (progress is not null || manualTactic is not null || normalPeriod is not null || armyProgress is not null || sequence < edited.Story.Sequence))
            throw new InvalidDataException("Saved manual edited-event playback has a conflicting owner or sequence.");
        if (sequence < 0 || progress is not null && progress.LastStorySequence != sequence) throw new InvalidDataException("Saved story sequence is inconsistent.");
        if (progress is not null && manualTactic is not null || manualTactic is not null && manualTactic.LastStorySequence != sequence)
            throw new InvalidDataException("Saved campaign operation is not exclusive or has the wrong sequence.");
        progress?.Validate(state); manualTactic?.Validate(state);
        if (armyProgress is not null && (progress is not null || manualTactic is not null || normalPeriod is not null || armyProgress.LastStorySequence != sequence))
            throw new InvalidDataException("Saved armyProgress operation is not exclusive or has the wrong sequence.");
        armyProgress?.Validate(state);
        if (normalPeriod is not null && (progress is not null || manualTactic is not null))
            throw new InvalidDataException("Saved campaign operations are not exclusive.");
        normalPeriod?.Validate(state);
    }
    private static void Write(BinaryWriter writer, byte[] bytes) { writer.Write(bytes.Length); writer.Write(bytes); }
    private static byte[] Read(BinaryReader reader) => reader.ReadBytes(ReadPayloadLength(reader));
    private static void SkipPayload(BinaryReader reader) => reader.BaseStream.Seek(ReadPayloadLength(reader), SeekOrigin.Current);
    private static int ReadPayloadLength(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 1 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Session save payload length is invalid.");
        return length;
    }
    private static void ValidateSession(GameState state, int playerForceId)
    {
        if (!PlayableSkeleton.IsPlayableRuleset(state.RulesetId) || state.NativePlayableTerminal is { } terminal && terminal.PlayerForcePhysical != playerForceId ||
            state.Phase is not (TurnPhase.Strategy or TurnPhase.Reporting) || !state.Forces.TryGetValue(playerForceId, out ForceState? force) ||
            !force.IsActive && (!state.ForceDefeats.ContainsKey(playerForceId) || PlayableOutcome.Evaluate(state, playerForceId) != PlayableResult.Defeated))
            throw new InvalidDataException("Session save has no playable force or phase.");
    }
}
