namespace San14.Data;

/// <summary>Reached SC230990/2C3A40/2C56B0/26A440 trait queries over the
/// explicitly loaded current Person/Personality tables. No trait names or UI
/// capability list supplies the native effect codes or activation bits.</summary>
public static class TacticRemakeCurrentTraits
{
    /// <summary>42B560/42B600's strict maximum over nine current selected
    /// keys. Unlike the sum caller, this adds no personality validity gate.</summary>
    internal static int? CommanderEffectMaximum(SourceWorldContext current, int personPhysical, int effect)
    {
        PersonalityReference? selected = null;
        int maximum = -1;
        for (int index = 0; index < 9; index++)
        {
            ushort? key = ResolveCommanderPersonalityLookup(current, personPhysical, index);
            if (key is null) return null;
            if (key == 0) continue;
            var reference = current.ReadPersonalityTableReference(key <= 355 ? key.Value : 0);
            if (reference is null || Contribution(reference, -1) is not int value) return null;
            if (value > maximum) { maximum = value; selected = reference; }
        }
        return selected is null ? 0 : Contribution(selected, 0);

        int? Contribution(PersonalityReference reference, int fallback)
        {
            for (int pair = 0; pair < 2; pair++)
            {
                byte? code = current.ReadPersonalityByte(reference, pair == 0 ? 0xB6 : 0xBA);
                if (code is null) return null;
                if (code != effect) continue;
                ushort? raw = current.ReadPersonalityWord(reference, pair == 0 ? 0xB8 : 0xBC);
                return raw is ushort word ? Math.Clamp((int)unchecked((short)word), -100, 2000) : null;
            }
            return fallback;
        }
    }

    /// <summary>2B2FB0 uses the first matching effect pair in each of nine
    /// selected personalities, including signed clamp/wrapping. It does not
    /// inspect activation1BC or Army trait caches.</summary>
    public static int? CommanderEffectSum(SourceWorldContext current, int personPhysical, int effect)
    {
        int sum = 0;
        for (int index = 0; index < 9; index++)
        {
            var reference = CurrentPersonality(current, personPhysical, index);
            if (reference is null || current.ReadSkillPersonalityValidity(reference) is not bool valid) return null;
            if (!valid) continue;
            for (int pair = 0; pair < 2; pair++)
            {
                if (current.ReadPersonalityByte(reference, pair == 0 ? 0xB6 : 0xBA) is not byte code) return null;
                if (code != effect) continue;
                if (current.ReadPersonalityWord(reference, pair == 0 ? 0xB8 : 0xBC) is not ushort raw) return null;
                sum = unchecked(sum + Math.Clamp((int)unchecked((short)raw), -100, 2000)); break;
            }
        }
        return sum;
    }
    /// <summary>230A30's current nine commander personalities, followed by
    /// 2B2B30's effect77 aggregation. This list does not test activation1BC.
    /// The first native matching pair supplies each personality's value.</summary>
    public static TacticCommanderCapacityTraits? CommanderCapacityTraits(
        SourceWorldContext current, int personPhysical)
    {
        int firstGroup = 0, secondGroup = 0;
        for (int index = 0; index < 9; index++)
        {
            PersonalityReference? reference = CurrentPersonality(current, personPhysical, index);
            if (reference is null || current.ReadSkillPersonalityValidity(reference) is not bool valid) return null;
            if (!valid) continue;
            for (int pair = 0; pair < 2; pair++)
            {
                byte? code = current.ReadPersonalityByte(reference, pair == 0 ? 0xB6 : 0xBA);
                if (code is null) return null;
                if (code != 77) continue;
                ushort? raw = current.ReadPersonalityWord(reference, pair == 0 ? 0xB8 : 0xBC);
                if (raw is null || reference.PhysicalSlot is not int physical) return null;
                int contribution = Math.Clamp((int)unchecked((short)raw.Value), -100, 2000);
                if (physical is >= 256 and <= 355) firstGroup = unchecked(firstGroup + contribution);
                else secondGroup = unchecked(secondGroup + contribution);
                break;
            }
        }
        return new(firstGroup, secondGroup);
    }
    /// <summary>Person fallback with a nonzero selector: current native
    /// validity, then the same nine entries and first matching pair per entry.
    /// No Army cache or activation mask participates in this Person mode.</summary>
    public static int? PersonContribution(SourceWorldContext current, int personPhysical, int effect)
    {
        if (effect is < 1 or >= 127) throw new ArgumentOutOfRangeException(nameof(effect));
        if (current.ReadPersonNativeValidity(personPhysical) is not bool valid) return null;
        return valid ? CommanderEffectSum(current, personPhysical, effect) : 0;
    }

    public static bool? CommanderHasEffect(SourceWorldContext current,
        int personPhysical, int effectCode)
    {
        for (int index = 0; index < 9; index++)
        {
            PersonalityReference? reference = CurrentPersonality(current, personPhysical, index);
            if (reference is null || current.ReadSkillPersonalityValidity(reference) is not bool valid) return null;
            if (!valid) continue;
            byte? first = current.ReadPersonalityByte(reference, 0xB6);
            if (first is null) return null;
            if (first == effectCode) return true;
            byte? second = current.ReadPersonalityByte(reference, 0xBA);
            if (second is null) return null;
            if (second == effectCode) return true;
        }
        return false;
    }

    public static bool? CommanderImmune(SourceWorldContext current,
        int personPhysical, int effectCode)
    {
        for (int index = 0; index < 9; index++)
        {
            PersonalityReference? reference = CurrentPersonality(current, personPhysical, index);
            if (reference is null || current.ReadSkillPersonalityValidity(reference) is not bool valid) return null;
            if (!valid) continue;
            byte? first = current.ReadPersonalityByte(reference, 0xB6);
            if (first is null) return null;
            if (first != effectCode)
            {
                byte? second = current.ReadPersonalityByte(reference, 0xBA);
                if (second is null) return null;
                if (second != effectCode) continue;
            }
            ushort? condition = current.ReadPersonalityWord(reference, 0xB4);
            if (condition is null) return null;
            if (condition == ushort.MaxValue) return true;
            ushort? active = current.ReadPersonTraitMask1BC(personPhysical);
            if (active is null) return null;
            if ((active & (1 << index)) != 0) return true;
        }
        return false;
    }

    /// <summary>26A440's actual status-query mode: effect selector0,
    /// commanderOnly0, evaluateConditions0. Cached Army36 is consumed before
    /// the nine commander entries. The complete127 i32 results retain wrapping.</summary>
    public static int[]? ArmyContributions(SourceWorldContext current,
        int personPhysical, IReadOnlyList<ushort>? armyTraits64)
    {
        if (armyTraits64 is not { Count: 36 }) return null;
        var result = new int[127];
        foreach (ushort requested in armyTraits64)
        {
            PersonalityReference? reference = current.ReadPersonalityTableReference(requested <= 355 ? requested : 0);
            if (reference is null || !Accumulate(reference)) return null;
        }
        for (int index = 0; index < 9; index++)
        {
            PersonalityReference? reference = CurrentPersonality(current, personPhysical, index);
            if (reference is null || current.ReadSkillPersonalityValidity(reference) is not bool valid) return null;
            if (!valid) continue;
            ushort? condition = current.ReadPersonalityWord(reference, 0xB4);
            if (condition is null) return null;
            if (condition != ushort.MaxValue)
            {
                ushort? active = current.ReadPersonTraitMask1BC(personPhysical);
                if (active is null) return null;
                if ((active & (1 << index)) == 0) continue;
            }
            if (!Accumulate(reference)) return null;
        }
        return result;

        bool Accumulate(PersonalityReference reference)
        {
            bool? valid = current.ReadSkillPersonalityValidity(reference);
            if (valid is null) return false;
            if (!valid.Value) return true;
            for (int pair = 0; pair < 2; pair++)
            {
                byte? code = current.ReadPersonalityByte(reference, pair == 0 ? 0xB6 : 0xBA);
                ushort? raw = current.ReadPersonalityWord(reference, pair == 0 ? 0xB8 : 0xBC);
                if (code is null || raw is null) return false;
                int contribution = Math.Clamp((int)unchecked((short)raw.Value), -100, 2000);
                if (code < 127) result[code.Value] = unchecked(result[code.Value] + contribution);
            }
            return true;
        }
    }

    /// <summary>230990 filters ordinary trait entries with current BF mask10
    /// and returns at most the first five raw slots. Custom IDs skip BF. Native
    /// invalid personalities can still return a raw first-five key; callers
    /// subsequently perform their own fresh personality validity query.</summary>
    /// <summary>Actual230990 current slot selector: ordinary flag4 filter,
    /// custom-ID exception and five returned slots. No activation permission.</summary>
    public static PersonalityReference? ResolveCommanderPersonality(SourceWorldContext current,
        int personPhysical, int index)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (index is < 0 or >= 9) throw new ArgumentOutOfRangeException(nameof(index));
        return CurrentPersonality(current, personPhysical, index);
    }

    /// <summary>The current selector's raw key, before pointer lookup. A
    /// first-five out-of-range key remains raw; pointer callers separately
    /// apply the table-zero fallback. Unknown is distinct from raw zero.</summary>
    public static ushort? ResolveCommanderPersonalityLookup(SourceWorldContext current,
        int personPhysical, int index)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (index is < 0 or >= 9) throw new ArgumentOutOfRangeException(nameof(index));
        if (current.ReadPersonIdentity(personPhysical) is not ushort identity) return null;
        bool custom = unchecked((uint)(identity - 5101)) <= 99;
        // Custom entries5..8 stop before consuming their raw fields. Ordinary
        // entries still execute their current validity/flag filter first.
        if (custom && index >= 5) return 0;
        if (current.ReadPersonTraitSlot150(personPhysical, index) is not ushort requested) return null;
        if (!custom)
        {
            PersonalityReference? candidate = current.ReadPersonalityTableReference(requested <= 355 ? requested : 0);
            if (candidate is null || current.ReadSkillPersonalityValidity(candidate) is not bool valid) return null;
            if (valid)
            {
                byte? flags = current.ReadSkillPersonalityFlags(candidate);
                if (flags is null) return null;
                if ((flags & 0x10) == 0) return 0;
            }
        }
        return index < 5 ? requested : (ushort)0;
    }

    private static PersonalityReference? CurrentPersonality(SourceWorldContext current,
        int personPhysical, int index)
    {
        ushort? requested = ResolveCommanderPersonalityLookup(current, personPhysical, index);
        return requested.HasValue ? current.ReadPersonalityTableReference(requested.Value <= 355 ? requested.Value : 0) : null;
    }
}

public sealed record TacticCommanderCapacityTraits(int Group256To355, int OtherGroup);
