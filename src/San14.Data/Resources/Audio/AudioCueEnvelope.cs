namespace San14.Data;

/// <summary>The static parameter-group envelope. Time belongs to the middleware's
/// 48000-tick mixer domain, independent of sample pitch and render frame count.</summary>
public sealed class AudioCueEnvelope
{
    public float Value { get; private set; } = 1;
    public float Target { get; private set; } = 1;
    public bool Complete => duration == 0;
    private float start;
    private uint duration, elapsed;
    private AudioCueEnvelopeCurve curve;

    public static uint DurationTicks(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0 || seconds * 48000f >= int.MaxValue)
            throw new InvalidDataException("Audio cue envelope duration exceeds its supported mixer range.");
        return (uint)(seconds * 48000f);
    }

    public void Begin(float from, float target, uint ticks, AudioCueEnvelopeCurve shape)
    {
        if (!float.IsFinite(from) || !float.IsFinite(target) || from < 0 || target < 0 ||
            !Enum.IsDefined(shape)) throw new ArgumentOutOfRangeException(nameof(from));
        start = from; Target = target; duration = ticks; elapsed = 0; curve = shape;
        Value = ticks == 0 ? target : from;
    }

    public float Advance(ulong ticks)
    {
        if (duration == 0) return Value;
        if (ticks >= duration - elapsed)
        { Value = Target; start = Target; elapsed = duration = 0; return Value; }
        elapsed += (uint)ticks;
        float t = (float)elapsed / duration;
        t = curve switch
        {
            AudioCueEnvelopeCurve.Sine => MathF.Sin(t * 1.5707964f),
            AudioCueEnvelopeCurve.Square => t * t,
            AudioCueEnvelopeCurve.SmoothSine => (MathF.Sin((t - .5f) * 3.1415927f) + 1) * .5f,
            _ => t
        };
        return Value = (Target - start) * t + start;
    }
}

public enum AudioCueEnvelopeCurve { Linear, Sine, Square, SmoothSine }
