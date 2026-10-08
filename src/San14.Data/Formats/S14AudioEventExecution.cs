namespace San14.Data.Formats;

/// <summary>Explicit native constant inputs. The two BGM scalars are not assigned a time unit.</summary>
public sealed record S14AudioEventParameters(float BgmScalar, IReadOnlyList<int> SeMap96)
{
    public static S14AudioEventParameters Read(float bgmScalar, IReadOnlyList<int> seMap)
    {
        ArgumentNullException.ThrowIfNull(seMap);
        if (!float.IsFinite(bgmScalar) || seMap.Count != 96)
            throw new InvalidDataException("Event audio constants are incomplete or nonfinite.");
        return new(bgmScalar, Array.AsReadOnly(seMap.ToArray()));
    }
}

/// <summary>
/// Calls into an explicit independent audio-manager adapter. Providers must
/// preserve the original helper's behavior; logging a call does not establish
/// that an original resource was decoded or sound was played.
/// </summary>
public interface IS14AudioEventExecutionState
{
    float? ReadBgmScalar();
    int? ReadSeMapEntry(int index);
    bool? ReadWorldPointerPresent();
    int? ReadWorldField1AC();
    void InvokeBgmManager(int rawId, int flags, float scalar3, float scalar5);
    void InvokeSeManager(int mappedId, float scalar2, float scalar3);
    bool? ReadSeSlotActive(int slot);
    void StopSeSlot(int slot);
    void StopAllSe();
}

/// <summary>
/// Current CE_SC SActPlayBGM/PlaySE slot0 wrappers, sourced from complete native
/// bodies. No source code is executed. ID resolution remains with the manager.
/// </summary>
public static class S14AudioEventExecution
{
    public static int Execute(int code, int rawId, IS14AudioEventExecutionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (code == 91)
        {
            float scalar = Known(state.ReadBgmScalar(), "BGM source scalar");
            if (!float.IsFinite(scalar)) throw new InvalidDataException("BGM source scalar is nonfinite.");
            state.InvokeBgmManager(rawId, 0, scalar, scalar);
            return 1;
        }
        if (code != 92) throw new InvalidDataException("No native audio execution binding exists for this opcode.");
        if (rawId > 0)
        {
            int index = rawId - 1;
            // The native wrapper still sends -1 to its manager for IDs outside
            // the 96-entry mapping. No fallback sound is selected here.
            int mapped = index < 96 ? Known(state.ReadSeMapEntry(index), "SE mapping entry") : -1;
            bool present = Known(state.ReadWorldPointerPresent(), "world pointer");
            if (present && Known(state.ReadWorldField1AC(), "world+1AC") == 0)
                state.InvokeSeManager(mapped, 0, 0);
            state.InvokeSeManager(mapped, 0, 0);
        }
        else if (rawId is 0 or -1) state.StopAllSe();
        else if (rawId == -3)
        {
            for (int slot = 0; slot < 62; slot++)
                if (Known(state.ReadSeSlotActive(slot), "current audio player slot")) state.StopSeSlot(slot);
        }
        return 1;
    }

    private static T Known<T>(T? value, string label) where T : struct =>
        value ?? throw new InvalidDataException($"Current {label} is unknown.");
}
