using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private sealed record BaseTierBgmScalarScope(object Owner, OriginalAudioOutputs Epoch, float SavedTarget);
    private BaseTierBgmScalarScope? baseTierBgmScalarScope;
    private OriginalAudioOutputs? sourceBgmScalarEpoch;
    private AudioEffect? sourceBgmScalar;
    private float sourceBgmScalarTarget;

    private void InitializeSourceBgmScalar()
    {
        //4014F0 initializes enabled=1; successful403250 initializes234=100.
        //The current application never changes that global sound-enable gate.
        //Category volume preferences and window focus remain separate gains.
        var epoch = OriginalOutputs;
        epoch.ValidateHost();
        // The bus scalar is created before any voice can load this bridge.
        // Use the same normal extension loader as voice envelopes and spread.
        var available = OriginalSpeakerPairGate.MatrixAvailability(GameAudioSpeakerLayout.Stereo);
        if (!available.Available) throw new NotSupportedException(available.UnresolvedOwner);
        AudioServer.Lock();
        try
        {
            if (sourceBgmScalar is not null) throw new InvalidOperationException("audio.bgm_scalar.already_initialized");
            if (!ClassDB.ClassExists("San14SourceEnvelope")) throw new NotSupportedException("audio.bgm_scalar.bridge_version");
            using var instance = ClassDB.Instantiate("San14SourceEnvelope");
            var scalar = instance.AsGodotObject() as AudioEffect ?? throw new InvalidOperationException("audio.bgm_scalar.effect_type");
            try
            {
                scalar.Call("set_format", checked((int)AudioServer.GetMixRate()));
                scalar.Call("set_paused", 0);
                scalar.Call("begin", 1f, 1f, 0L, (int)AudioCueEnvelopeCurve.Linear);
                AudioServer.AddBusEffect(AudioServer.GetBusIndex(epoch.MusicBus), scalar);
                sourceBgmScalar = scalar; sourceBgmScalarEpoch = epoch; sourceBgmScalarTarget = 100;
            }
            catch { scalar.Dispose(); throw; }
        }
        finally { AudioServer.Unlock(); }
    }

    private AudioEffect RequireSourceBgmScalar(OriginalAudioOutputs epoch)
    {
        if (!ReferenceEquals(sourceBgmScalarEpoch, epoch) || !ReferenceEquals(_originalOutputs, epoch) || sourceBgmScalar is not { } scalar)
            throw new InvalidOperationException("audio.bgm_scalar.output_epoch");
        epoch.ValidateHost();
        int bus = AudioServer.GetBusIndex(epoch.MusicBus);
        if (bus < 0 || AudioServer.IsBusBypassingEffects(bus) || AudioServer.GetBusEffectCount(bus) != 1 ||
            !AudioServer.IsBusEffectEnabled(bus, 0) || AudioServer.GetBusEffect(bus, 0) != scalar)
            throw new InvalidOperationException("audio.bgm_scalar.source_owner");
        return scalar;
    }

    internal void BeginBaseTierBgmScalar(object owner)
    {
        RequireEngineThread();
        if (baseTierBgmScalarScope is not null) throw new InvalidOperationException("audio.bgm_scalar.scope_owned");
        var epoch = OriginalOutputs;
        AudioServer.Lock();
        try
        {
            RequireSourceBgmScalar(epoch);
            //403240 returns234, the source target, not an interpolated gain.
            baseTierBgmScalarScope = new(owner, epoch, sourceBgmScalarTarget);
        }
        finally { AudioServer.Unlock(); }
    }

    internal void PrepareBaseTierBgmScalar(object owner)
    {
        RequireEngineThread();
        var scope = baseTierBgmScalarScope;
        if (scope is null || !ReferenceEquals(scope.Owner, owner)) throw new InvalidOperationException("audio.bgm_scalar.scope_owner");
        //860EA0: continue the same music at min(saved target,30) over1000ms.
        SetSourceBgmScalar(scope.Epoch, MathF.Min(scope.SavedTarget, 30), 1000);
    }

    internal void EndBaseTierBgmScalar(object owner)
    {
        RequireEngineThread();
        if (baseTierBgmScalarScope is not { } scope || !ReferenceEquals(scope.Owner, owner)) return;
        baseTierBgmScalarScope = null;
        //856550 leaves the500ms restore on the same application BGM owner.
        //No player is paused, restarted, sought or replaced by this operation.
        SetSourceBgmScalar(scope.Epoch, scope.SavedTarget, 500);
    }

    private void SetSourceBgmScalar(OriginalAudioOutputs epoch, float percent, int milliseconds)
    {
        if (!float.IsFinite(percent) || percent is < 0 or > 100 || milliseconds is < 0 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(percent));
        AudioServer.Lock();
        try
        {
            var scalar = RequireSourceBgmScalar(epoch);
            float current = (float)scalar.Call("read_gain").AsDouble();
            scalar.Call("begin", current, percent / 100f, checked((long)milliseconds * 48), (int)AudioCueEnvelopeCurve.Linear);
            sourceBgmScalarTarget = percent;
        }
        finally { AudioServer.Unlock(); }
    }

    private void RetireSourceBgmScalar(bool abandonScope = false)
    {
        // A replacement epoch invalidates a still-open scene. Keep its identity
        // until its real parent attempts to return, so it cannot falsely ACK.
        if (abandonScope) baseTierBgmScalarScope = null;
        var scalar = sourceBgmScalar; var epoch = sourceBgmScalarEpoch;
        sourceBgmScalar = null; sourceBgmScalarEpoch = null; sourceBgmScalarTarget = 0;
        if (scalar is null) return;
        if (epoch is not null)
        {
            int bus = AudioServer.GetBusIndex(epoch.MusicBus);
            if (bus >= 0)
                for (int index = AudioServer.GetBusEffectCount(bus) - 1; index >= 0; index--)
                    if (AudioServer.GetBusEffect(bus, index) == scalar) AudioServer.RemoveBusEffect(bus, index);
        }
        scalar.Dispose();
    }
}
