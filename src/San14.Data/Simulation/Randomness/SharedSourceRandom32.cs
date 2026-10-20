using System.Numerics;

namespace San14.Data;

/// <summary>A caller-owned shared stream with an explicit initial state.
/// Creating a campaign, loading a save or preparing a message does not reseed it.</summary>
public sealed class SharedSourceRandom32(uint initialState)
{
    public uint State { get; private set; } = initialState;

    public int Next(int bound)
    {
        if (bound < 2) return 0;
        uint next = SourceRandom32Mix.Advance(State);
        State = next;
        return (int)((next & 0x7FFF_FFFFu) % (uint)bound);
    }
}

internal static class SourceRandom32Mix
{
    internal static uint Advance(uint bits)
    {
        int iterations = (int)(bits & 15) + 1;
        for (int index = 0; index < iterations; index++)
            bits = BitOperations.RotateLeft(unchecked((bits - 123u) * 0x0693D4B5u + 123456u), 16);
        return BitOperations.RotateLeft(unchecked(bits * 0x41C64E6Du + 12345u), 16);
    }
}
