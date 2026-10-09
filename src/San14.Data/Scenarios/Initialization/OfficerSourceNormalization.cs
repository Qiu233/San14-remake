using San14.Core;

namespace San14.Data;

/// <summary>
/// Verified CE post-load conversion of source values. Raw records and independent
/// saved worlds retain their own values; these bounds are not Core invariants.
/// </summary>
internal static class OfficerSourceNormalization
{
    public static OfficerAbilities Abilities(CharacterPreviewRecord person, uint marker)
    {
        int Value(byte raw) => marker == 0x66 ? Math.Clamp((int)raw, 1, 100) : raw;
        // CPersonData's post-load loop uses the charm cap for all five base values.
        // The ordinary final-ability cap of 120 belongs to a later calculation.
        return new(Value(person.Leadership), Value(person.Strength),
            Value(person.Intelligence), Value(person.Politics), Value(person.Charm));
    }

    public static int? PolicyBaseCost(byte? raw, uint marker) =>
        raw is not byte value ? null : marker == 0x66 ? Math.Min((int)value, 6) : value;
}
