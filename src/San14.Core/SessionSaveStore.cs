using System.Text;

namespace San14.Core;

/// <summary>Player assignment is session metadata, outside shared simulation state.</summary>
public sealed record GameSessionSave(GameState State, int PlayerForceId);

/// <summary>Local, versioned session wrapper around the independent GameState codec.</summary>
public sealed class SessionSaveStore(ISaveCodec codec)
{
    private static readonly byte[] Magic = "S14S"u8.ToArray();
    private const int Version = 1;

    public void Save(string path, GameState state, int playerForceId)
    {
        ValidateSession(state, playerForceId);
        byte[] snapshot = codec.Encode(state);
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(playerForceId);
            writer.Write(snapshot.Length);
            writer.Write(snapshot);
        }
        AtomicSaveFiles.Write(path, output.ToArray());
    }

    public GameSessionSave Load(string path)
    {
        using var input = new MemoryStream(File.ReadAllBytes(path));
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic) || reader.ReadInt32() != Version)
            throw new InvalidDataException("Session save format is unsupported.");
        int playerForceId = reader.ReadInt32();
        int length = reader.ReadInt32();
        if (length < 1 || length != input.Length - input.Position)
            throw new InvalidDataException("Session save payload length is invalid.");
        GameState state = codec.Decode(reader.ReadBytes(length));
        ValidateSession(state, playerForceId);
        state = PlayableSkeleton.UpgradeSession(state);
        return new GameSessionSave(state, playerForceId);
    }

    private static void ValidateSession(GameState state, int playerForceId)
    {
        if (!PlayableSkeleton.IsPlayableRuleset(state.RulesetId) ||
            state.NativePlayableTerminal is { } terminal && terminal.PlayerForcePhysical != playerForceId ||
            state.Phase is not (TurnPhase.Strategy or TurnPhase.Reporting) ||
            !state.Forces.TryGetValue(playerForceId, out ForceState? force) ||
            !force.IsActive && (!state.ForceDefeats.ContainsKey(playerForceId) ||
                PlayableOutcome.Evaluate(state, playerForceId) != PlayableResult.Defeated))
            throw new InvalidDataException("Session save has no playable force or phase.");
    }
}
