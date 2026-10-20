using San14.Data;
using San14.Data.Formats;

public sealed record AudioEventInputs(S14AudioEventParameters? Parameters,
    bool? WorldPointerPresent, int? WorldField1AC, IReadOnlyDictionary<int, bool?> SeSlotActivity);
public sealed record AudioEventManagerCall(string Manager, int Id, int Flags, float ScalarA, float ScalarB);
/// <summary>Independent client output policy. No cue/sample is an explicitly bound stop.
/// Scalar time/gain interpretation and native voice arbitration are not inferred.</summary>
public sealed record AudioEventBinding(string Channel, AudioCueRequest? Cue,
    float VolumeDb = 0, string Bus = "Master")
{
    public AudioSampleRequest? Sample { get; init; }
    public AudioVolumeGroup? VolumeGroup { get; init; }
}
public sealed record AudioEventCommand(AudioEventManagerCall? SourceCall,
    AudioEventBinding Binding);

/// <summary>Implements the event manager callbacks as a staged projection. Call
/// CommitAsync on the engine thread only after the independent game transaction
/// succeeds. Source manager IDs require an explicit resolver; staging never plays.</summary>
public sealed class AudioEventAdapter : IS14AudioEventExecutionState
{
    private readonly SceneAudioSession _session;
    private readonly long _sceneRevision;
    private readonly AudioEventInputs _inputs;
    private readonly Func<AudioEventManagerCall, AudioEventBinding?> _resolve;
    private readonly IReadOnlyDictionary<int, string> _slotChannels;
    private readonly IReadOnlyList<string> _seChannels;
    private readonly List<AudioEventCommand> _commands = [];
    private bool _committed;
    public IReadOnlyList<AudioEventCommand> PendingCommands => _commands.AsReadOnly();

    public AudioEventAdapter(SceneAudioSession session, AudioEventInputs inputs,
        Func<AudioEventManagerCall, AudioEventBinding?> resolve,
        IReadOnlyDictionary<int, string> seSlotChannels, IReadOnlyList<string> allSeChannels)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.SeSlotActivity);
        ArgumentNullException.ThrowIfNull(seSlotChannels);
        ArgumentNullException.ThrowIfNull(allSeChannels);
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _inputs = inputs with
        {
            Parameters = inputs.Parameters is null ? null :
                S14AudioEventParameters.Read(inputs.Parameters.BgmScalar, inputs.Parameters.SeMap96),
            SeSlotActivity = new Dictionary<int, bool?>(inputs.SeSlotActivity)
        };
        _slotChannels = new Dictionary<int, string>(seSlotChannels);
        _seChannels = allSeChannels.Distinct(StringComparer.Ordinal).ToArray();
        if (session.SceneKey is null) throw new InvalidOperationException("Event audio needs a begun explicit scene.");
        _sceneRevision = session.SceneRevision;
        foreach (string channel in _slotChannels.Values.Concat(_seChannels)) Validate(new(channel, null));
    }

    public float? ReadBgmScalar() => _inputs.Parameters?.BgmScalar;
    public int? ReadSeMapEntry(int index) => _inputs.Parameters is { } parameters &&
        index >= 0 && index < parameters.SeMap96.Count ? parameters.SeMap96[index] : null;
    public bool? ReadWorldPointerPresent() => _inputs.WorldPointerPresent;
    public int? ReadWorldField1AC() => _inputs.WorldField1AC;
    public bool? ReadSeSlotActive(int slot) => _inputs.SeSlotActivity.TryGetValue(slot, out bool? active) ? active : null;

    public void InvokeBgmManager(int rawId, int flags, float scalar3, float scalar5) =>
        Stage(new("bgm", rawId, flags, scalar3, scalar5));
    public void InvokeSeManager(int mappedId, float scalar2, float scalar3) =>
        Stage(new("se", mappedId, 0, scalar2, scalar3));

    private void Stage(AudioEventManagerCall call)
    {
        EnsureStaging();
        AudioEventBinding binding = _resolve(call) ??
            throw new InvalidDataException($"Current {call.Manager} manager ID {call.Id} has no explicit cue binding.");
        Validate(binding);
        _commands.Add(new(call, binding));
    }

    public void StopSeSlot(int slot)
    {
        EnsureStaging();
        if (!_slotChannels.TryGetValue(slot, out string? channel))
            throw new InvalidDataException($"Current SE slot {slot} has no explicit client channel binding.");
        _commands.Add(new(null, new(channel, null)));
    }

    public void StopAllSe()
    {
        EnsureStaging();
        foreach (string channel in _seChannels) _commands.Add(new(null, new(channel, null)));
    }

    /// <summary>Ordered callbacks remain ordered, including the source duplicate SE
    /// invocation. Client channel keys explicitly determine voice replacement.</summary>
    public async Task CommitAsync(bool startPlayback = true, CancellationToken cancellationToken = default)
    {
        EnsureStaging();
        _committed = true;
        foreach (AudioEventCommand command in _commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_session.SceneRevision != _sceneRevision)
                throw new OperationCanceledException("Event audio belongs to a retired scene.");
            AudioEventBinding binding = command.Binding;
            if (binding.Sample is not null)
            {
                if (startPlayback)
                    await _session.PlaySampleAsync(binding.Channel, binding.Sample, binding.VolumeDb, binding.Bus, cancellationToken, binding.VolumeGroup);
                else
                    await _session.PrepareSampleAsync(binding.Channel, binding.Sample, binding.VolumeDb, binding.Bus, cancellationToken, binding.VolumeGroup);
            }
            else if (binding.Cue is null) _session.StopChannel(binding.Channel);
            else if (startPlayback)
                await _session.PlayCueAsync(binding.Channel, binding.Cue, binding.VolumeDb, binding.Bus, cancellationToken, binding.VolumeGroup);
            else
                await _session.PrepareCueAsync(binding.Channel, binding.Cue, binding.VolumeDb, binding.Bus, cancellationToken, binding.VolumeGroup);
        }
    }

    private void EnsureStaging()
    {
        if (_committed) throw new InvalidOperationException("This event audio projection was already committed.");
        if (_session.SceneRevision != _sceneRevision) throw new OperationCanceledException("Event audio belongs to a retired scene.");
    }

    private static void Validate(AudioEventBinding binding)
    {
        if (binding.Cue is not null && binding.Sample is not null)
            throw new InvalidDataException("Event manager binding must select one cue or one named sample.");
        if (binding.VolumeGroup is { } group && !Enum.IsDefined(group))
            throw new InvalidDataException("Event audio volume group is invalid.");
        if (string.IsNullOrWhiteSpace(binding.Channel) || binding.Channel.Length > 64 ||
            !float.IsFinite(binding.VolumeDb) || binding.VolumeDb is < -80 or > 24 || string.IsNullOrWhiteSpace(binding.Bus))
            throw new InvalidDataException("Event audio client channel, gain or bus policy is invalid.");
    }
}
