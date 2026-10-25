using San14.Data.Formats;

namespace San14.Data;

/// <summary>The type-one module's stop envelope. It does not substitute the
/// stop period for particle/emitter lifetime.</summary>
public sealed class G1EffectModuleLifetime
{
    public ushort StopKind { get; }
    public float StopSeconds { get; }
    public bool Stopping { get; private set; }
    public bool Enabled { get; private set; } = true;
    public float StopElapsed { get; private set; }
    public float Strength { get; private set; } = 1f;

    public G1EffectModuleLifetime(G1EffectModule declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.Type != 1) throw new NotSupportedException("Unsupported G1FX module lifetime type.");
        StopKind = declaration.StopKind;
        StopSeconds = declaration.StopSeconds;
    }

    public void Reset()
    {
        Stopping = false;
        Enabled = true;
        StopElapsed = 0f;
        Strength = 1f;
    }

    /// <summary>Applies an actual stop request received by this module owner.</summary>
    public void BeginStop() => Stopping = true;

    /// <summary>Runs before emitter transforms and particle updates. Returns
    /// true when the source clears the generic roots and disables the module.</summary>
    public bool AdvanceStop(float seconds, bool actualModuleAlive)
    {
        if (!Stopping) return false;
        StopElapsed += seconds;
        if (StopKind == 1)
            Strength = StopSeconds <= StopElapsed ? 0f : (StopSeconds - StopElapsed) / StopSeconds;
        else
        {
            Strength -= Strength * 3f * seconds / StopSeconds;
            if (Strength <= 0f) Strength = 0f;
        }
        if (!(Strength < 1.1920929e-5f) || actualModuleAlive) return false;
        Enabled = false;
        Stopping = false;
        return true;
    }

    /// <summary>Combines actual generic/emitter state with the source module's
    /// asynchronous preparation state. An initially empty pool alone is not done.</summary>
    public static bool IsAlive(bool genericAlive, bool emitterCanEmit, float strength,
        int preparationCount, bool preparationPending)
    {
        if (genericAlive || strength >= 0.0011920929f && emitterCanEmit) return true;
        return preparationCount > 0 || preparationPending || preparationCount is -2 or -1;
    }
}
