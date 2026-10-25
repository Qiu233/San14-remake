using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Mutable timing of one initialized G1FX system. A negative finish
/// time delegates completion to the enabled modules' actual alive state.</summary>
public sealed class G1EffectSystemTimeline
{
    private readonly float initialElapsed;
    private readonly uint initialFlags;

    public float Elapsed { get; private set; }
    public float Start { get; }
    public float Finish { get; }
    public bool Enabled { get; private set; }
    public bool Started { get; private set; }
    public int ModuleCount { get; }

    public G1EffectSystemTimeline(G1EffectSystem declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ReadOnlySpan<byte> control = declaration.ClockControl.Span;
        if (control.Length != 16) throw new InvalidDataException("Invalid G1FX system clock declaration.");
        initialElapsed = BinaryPrimitives.ReadSingleLittleEndian(control);
        Start = BinaryPrimitives.ReadSingleLittleEndian(control[4..]);
        Finish = BinaryPrimitives.ReadSingleLittleEndian(control[8..]);
        initialFlags = BinaryPrimitives.ReadUInt32LittleEndian(control[12..]);
        ModuleCount = declaration.Modules.Count;
        Reset();
    }

    public void Reset()
    {
        Elapsed = initialElapsed;
        Enabled = (initialFlags & 1) != 0;
        Started = (initialFlags & 2) != 0;
    }

    public void Disable() => Enabled = false;

    /// <summary>Returns whether this system invokes its module updates. It uses
    /// the full accepted step even when that step crosses the start boundary.</summary>
    public bool Advance(float seconds)
    {
        if (!Enabled) return false;
        Elapsed += seconds;
        if (Start <= Elapsed) Started = true;
        return Started;
    }

    /// <param name="moduleAlive">For each source module, true only when its
    /// enabled runtime is actually alive. Declaration counts are not alive state.</param>
    public bool IsFinished(ReadOnlySpan<bool> moduleAlive)
    {
        if (moduleAlive.Length != ModuleCount)
            throw new ArgumentException("G1FX module lifetime count differs from its system.", nameof(moduleAlive));
        if (!Enabled) return true;
        if (Finish >= 0f) return Elapsed >= Finish;
        foreach (bool alive in moduleAlive) if (alive) return false;
        return true;
    }
}
