using Godot;
using San14.Data;

internal partial class OriginalAudioPlayer
{
    private AudioEffect? sourceEnvelope;
    private int sourceEnvelopeMixRate;
    private AudioServer.SpeakerMode sourceEnvelopeSpeakerMode;
    private string? sourceEnvelopeDevice;
    internal bool HasSourceEnvelope => sourceEnvelope is not null;

    internal void BeginSourceEnvelope(float from, float target, uint ticks, AudioCueEnvelopeCurve curve)
    {
        if (gainBus is null) throw new InvalidOperationException("audio.envelope.voice_not_installed");
        AudioServer.Lock();
        try
        {
            int bus = AudioServer.GetBusIndex(gainBus);
            if (sourceEnvelope is null)
            {
                var available = OriginalSpeakerPairGate.MatrixAvailability(GameAudioSpeakerLayout.Stereo);
                if (!available.Available) throw new NotSupportedException(available.UnresolvedOwner);
                if (!ClassDB.ClassExists("San14SourceEnvelope"))
                    throw new NotSupportedException("audio.envelope.bridge_version");
                using var instance = ClassDB.Instantiate("San14SourceEnvelope");
                sourceEnvelope = instance.AsGodotObject() as AudioEffect ??
                    throw new InvalidOperationException("audio.envelope.effect_type");
                sourceEnvelopeMixRate = checked((int)AudioServer.GetMixRate());
                sourceEnvelopeSpeakerMode = AudioServer.GetSpeakerMode();
                sourceEnvelopeDevice = AudioServer.OutputDevice;
                sourceEnvelope.Call("set_format", sourceEnvelopeMixRate);
                sourceEnvelope.Call("set_paused", paused ? 1 : 0);
                AudioServer.AddBusEffect(bus, sourceEnvelope);
            }
            sourceEnvelope.Call("begin", from, target, (long)ticks, (int)curve);
        }
        finally { AudioServer.Unlock(); }
    }

    internal (float Gain, bool Complete) ReadSourceEnvelope()
    {
        int bus = gainBus is null ? -1 : AudioServer.GetBusIndex(gainBus);
        if (sourceEnvelope is null || bus < 0 || AudioServer.GetMixRate() != sourceEnvelopeMixRate ||
            AudioServer.GetSpeakerMode() != sourceEnvelopeSpeakerMode || AudioServer.OutputDevice != sourceEnvelopeDevice ||
            AudioServer.IsBusBypassingEffects(bus) || AudioServer.GetBusEffectCount(bus) != 1 ||
            !AudioServer.IsBusEffectEnabled(bus, 0) || AudioServer.GetBusEffect(bus, 0) != sourceEnvelope)
            throw new InvalidOperationException("audio.envelope.output_epoch");
        return ((float)sourceEnvelope.Call("read_gain").AsDouble(),
            sourceEnvelope.Call("is_complete").AsInt64() != 0);
    }

    private void PauseSourceEnvelope(bool value)
    {
        if (sourceEnvelope is null) return;
        AudioServer.Lock();
        try { sourceEnvelope.Call("set_paused", value ? 1 : 0); }
        finally { AudioServer.Unlock(); }
    }

    private void ClearSourceEnvelope()
    {
        sourceEnvelope?.Dispose(); sourceEnvelope = null;
        sourceEnvelopeMixRate = 0;
        sourceEnvelopeDevice = null;
    }
}
