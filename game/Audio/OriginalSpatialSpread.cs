using Godot;
using San14.Data;

/// <summary>The existing speaker bridge's PCM detector and per-mix-block
/// spread bank. All classifications of one CGRS output epoch share it.</summary>
internal sealed class OriginalSpatialSpread : IDisposable
{
    private Resource? bank;
    private readonly List<(string Bus, int Index, AudioEffect Effect)> effects = [];

    internal OriginalSpatialSpread(AudioSpatialOutputPlan plan, int mixRate,
        string masterBus, IReadOnlyDictionary<uint, string> detectorBuses)
    {
        var supported = OriginalSpeakerPairGate.MatrixAvailability(GameAudioSpeakerLayout.Stereo);
        if (!supported.Available) throw new NotSupportedException(supported.UnresolvedOwner);
        if (!ClassDB.ClassExists("San14SpatialSpreadBank") || !ClassDB.ClassExists("San14SpatialSpreadEffect"))
            throw new NotSupportedException("audio.spatial.spread_bridge_version");
        AudioServer.Lock();
        try
        {
            using var created = ClassDB.Instantiate("San14SpatialSpreadBank");
            bank = created.AsGodotObject() as Resource ?? throw new InvalidOperationException("audio.spatial.spread_bank_type");
            bank.Call("configure", mixRate);
            var detectorIndices = new Dictionary<uint, int>();
            foreach (var detector in detectorBuses)
            {
                //Fresh kind4 construction and its initial8003 setter produce
                //threshold1, hang0. This is not inferred from voice activity.
                using var index = bank.Call("add_detector", 1.0, 0.0);
                detectorIndices.Add(detector.Key, checked((int)index.AsInt64()));
            }
            for (int i = 0; i < plan.SpreadControls.Count; i++)
            {
                var control = plan.SpreadControls[i];
                using var index = bank.Call("add_control", (double)control.MaximumGain,
                    Samples(control.AttackSeconds), Samples(control.ReleaseSeconds));
                if (index.AsInt64() != i) throw new InvalidOperationException("audio.spatial.spread_control_index");
                foreach (uint key in control.TriggerOutputKeys)
                    if (detectorIndices.TryGetValue(key, out int detector)) bank.Call("add_trigger", i, detector);
            }
            bank.Call("seal");
            foreach (var detector in detectorBuses) Install(detector.Value, 0, detectorIndices[detector.Key]);
            Install(masterBus, 1, -1);
        }
        catch { Dispose(); throw; }
        finally { AudioServer.Unlock(); }

        long Samples(float seconds)
        {
            float count = (float)mixRate * seconds;
            if (!float.IsFinite(count) || count < 0f || (double)count > uint.MaxValue)
                throw new InvalidDataException("audio.spatial.spread_duration_samples");
            return (uint)count;
        }
    }

    private void Install(string name, int role, int detector)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus < 0) throw new InvalidOperationException("audio.spatial.spread_output_bus");
        int pairs = AudioServer.GetBusChannels(bus);
        if (pairs is < 1 or > 4) throw new InvalidOperationException("audio.spatial.spread_output_pairs");
        using var created = ClassDB.Instantiate("San14SpatialSpreadEffect");
        var effect = created.AsGodotObject() as AudioEffect ?? throw new InvalidOperationException("audio.spatial.spread_effect_type");
        int slot = AudioServer.GetBusEffectCount(bus);
        try
        {
            effect.Call("configure", bank!, role, detector, pairs);
            AudioServer.AddBusEffect(bus, effect);
            effects.Add((name, slot, effect));
            for (int pair = 0; pair < pairs; pair++)
            {
                using var instance = AudioServer.GetBusEffectInstance(bus, slot, pair)
                    ?? throw new InvalidOperationException("audio.spatial.spread_instance");
                instance.Call("bind_output_pair", pair);
            }
        }
        catch
        {
            if (!effects.Any(entry => ReferenceEquals(entry.Effect, effect))) effect.Dispose();
            throw;
        }
    }

    internal float Read(AudioSpatialOutputPlan plan)
    {
        ObjectDisposedException.ThrowIf(bank is null, this);
        AudioServer.Lock();
        try
        {
            float spread = 0f;
            foreach (int index in plan.SpreadControlIndices)
            {
                using var current = bank!.Call("read_control", index);
                float value = (float)current.AsDouble();
                if (!float.IsFinite(value)) throw new InvalidOperationException("audio.spatial.spread_mixer_format_changed");
                spread = 1f - (1f - value) * (1f - spread);
            }
            return spread;
        }
        finally { AudioServer.Unlock(); }
    }

    public void Dispose()
    {
        AudioServer.Lock();
        try
        {
            foreach (var entry in effects.AsEnumerable().Reverse())
            {
                int bus = AudioServer.GetBusIndex(entry.Bus);
                if (bus >= 0 && entry.Index < AudioServer.GetBusEffectCount(bus)) AudioServer.RemoveBusEffect(bus, entry.Index);
                entry.Effect.Dispose();
            }
            effects.Clear();
            bank?.Dispose(); bank = null;
        }
        finally { AudioServer.Unlock(); }
    }
}
