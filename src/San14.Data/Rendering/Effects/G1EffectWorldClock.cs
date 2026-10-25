namespace San14.Data;

/// <summary>The accepted effect-context clock input. Tick is an effect update
/// count, not a campaign day or the map publication identity.</summary>
public readonly record struct G1EffectClockInput(int FramesPerSecond, uint Tick,
    float Time, bool Jump = false);

/// <summary>Operations on the actual prepared effect instance. Implementations
/// must perform the requested operation synchronously before returning.</summary>
public interface IG1EffectClockConsumer
{
    void Reset();
    void Initialize();
    void Step(float seconds);
    void SetOpacity(float opacity);
}

/// <summary>Owns the effect World clock independently of resource declarations.
/// Preserves reset, initialization, split-step and fade ordering.</summary>
public sealed class G1EffectWorldClock
{
    public float Time { get; private set; } = -1f;
    public uint Tick { get; private set; } = uint.MaxValue;
    public bool Initialized { get; private set; }
    public bool ResetRequested { get; private set; }
    public int FadeStartTick { get; private set; } = -1;
    public int FadeLength { get; private set; }

    public void RequestReset() => ResetRequested = true;

    public void SetFade(int startTick, int length)
    {
        FadeStartTick = startTick;
        FadeLength = length;
    }

    public bool Advance(G1EffectClockInput input, IG1EffectClockConsumer instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        uint tick = unchecked((int)input.Tick) < 1 ? 0u : input.Tick;
        if (tick == Tick && input.Time == Time || input.FramesPerSecond == 0) return false;

        // The original saves this predicate before resetting the old clock.
        bool hadTime = Time >= 0f;
        uint count = unchecked(tick - Tick);
        if (tick == 0 && input.Time == 0f || unchecked((int)count) < 0 ||
            input.Time < Time || ResetRequested)
        {
            instance.Reset();
            Time = -1f;
            Tick = uint.MaxValue;
            FadeStartTick = -1;
            FadeLength = 0;
            Initialized = false;
            ResetRequested = false;
            count = tick;
        }
        if (!Initialized)
        {
            instance.Initialize();
            Initialized = true;
        }

        float seconds = (input.Time - Time) / input.FramesPerSecond;
        if (!hadTime) instance.Step(1f / input.FramesPerSecond);
        else if (unchecked((int)count) < 2 || input.Jump) instance.Step(seconds);
        else
        {
            // Retain the two divisions; a single division by FPS*count changes
            // the original float rounding and its source event boundaries.
            seconds /= unchecked((int)count);
            for (uint index = 0; index < count; index++) instance.Step(seconds);
        }

        int fadeTick = unchecked((int)(tick - (uint)FadeStartTick));
        if (fadeTick > 0 && FadeLength > 0)
        {
            float opacity;
            if (fadeTick > FadeLength)
            {
                FadeStartTick = -1;
                FadeLength = 0;
                opacity = 0f;
            }
            else opacity = Math.Clamp(1f - (float)fadeTick / FadeLength, 0f, 1f);
            instance.SetOpacity(opacity);
        }
        Time = input.Time;
        Tick = tick;
        return true;
    }
}
