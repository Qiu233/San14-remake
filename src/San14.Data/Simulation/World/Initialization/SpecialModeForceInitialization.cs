namespace San14.Data;

public sealed record ScenarioPersonReference(int? PhysicalSlot);
public sealed record SpecialModeForceWrite(int PersonEntryIndex, ForceReference Force, int Value);
public sealed record SpecialModeForceInitializationResult(bool NativeReturnValue,
    IReadOnlyList<SpecialModeForceWrite> Writes);

public interface ISpecialModeForceInitializationState
{
    bool? ReadSpecialModeWorldPresent();
    int? ReadSpecialModeWorldField48();
    int? ReadSpecialModePersonId(int entryIndex);
    ScenarioPersonReference? ReadSpecialModePersonTableReference(int id);
    ScenarioPersonReference? ReadSpecialModeBasePersonReference();
    bool? ReadSpecialModePersonNativeValidity(ScenarioPersonReference person);
    ForceReference? ReadSpecialModePersonForce(ScenarioPersonReference person);
    void WriteForceRawField164(ForceReference force, int value);
}

/// <summary>
/// Complete 330C00 branch against explicit CURRENT inputs. This is not the full
/// 32F910 initialization, a default source projection, or final doctrine level.
/// Negative signed person IDs would index before the native table and are outside
/// this safe adapter profile. Completed writes remain after a later failure.
/// </summary>
public static class SpecialModeForceInitialization
{
    public static SpecialModeForceInitializationResult Apply(int staticFlag,
        ISpecialModeForceInitializationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var writes = new List<SpecialModeForceWrite>();
        if (staticFlag == 0) return Result();
        if (!Known(state.ReadSpecialModeWorldPresent(), "world pointer presence")) return Result();
        int mode = Known(state.ReadSpecialModeWorldField48(), "world+48 mode");
        if (unchecked((uint)mode - 100u) > 99u) return Result();
        for (int index = 0; index < 3; index++)
        {
            int id = Known(state.ReadSpecialModePersonId(index), $"world+{0x64 + index * 4:X} signed person ID");
            if (id < 0) throw new InvalidDataException("Negative special-mode person ID would index before the native pointer table.");
            ScenarioPersonReference person = (id <= 6000
                ? state.ReadSpecialModePersonTableReference(id)
                : state.ReadSpecialModeBasePersonReference())
                ?? throw new InvalidDataException("Current special-mode person pointer is unknown.");
            if (!person.PhysicalSlot.HasValue) continue;
            if (person.PhysicalSlot.Value is < 0 or >= 1851)
                throw new InvalidDataException("Special-mode person receiver is outside the supported CE physical table.");
            if (!Known(state.ReadSpecialModePersonNativeValidity(person), "person native validity")) continue;
            ForceReference first = state.ReadSpecialModePersonForce(person)
                ?? throw new InvalidDataException("Current special-mode first force pointer is unknown.");
            if (!first.PhysicalSlot.HasValue) continue;
            ForceReference current = state.ReadSpecialModePersonForce(person)
                ?? throw new InvalidDataException("Current special-mode second force pointer is unknown.");
            if (!current.PhysicalSlot.HasValue)
                throw new InvalidDataException("Current special-mode second force pointer is null at the native write.");
            if (current.PhysicalSlot.Value is < 0 or >= 52)
                throw new InvalidDataException("Special-mode force write receiver is outside the supported CE physical table.");
            state.WriteForceRawField164(current, 9999999);
            writes.Add(new(index, current, 9999999));
        }
        return Result();

        SpecialModeForceInitializationResult Result() => new(true, writes.AsReadOnly());
    }
    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current special-mode input {field} is unknown.");
}
