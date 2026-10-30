namespace San14.Data;

public sealed record PersonalityEditorOption(int Code, int Minimum, int Maximum, int Step, int Category);

/// <summary>The ordinary custom-personality editor's ordered choice tables
/// and selection gates. These are editor ranges, independently of runtime
/// clamps. Code98's positive display amount is negated by the draft setter.</summary>
public static class PersonalityEditorOptions
{
    public static IReadOnlyList<PersonalityEditorOption> Conditions { get; } =
        Array.AsReadOnly<PersonalityEditorOption>(
        [
            new(0, 0, 0, 1, 0),
            new(9, 3, 3, 1, 1),
            new(10, 3, 3, 1, 1),
            new(16, 0, 0, 1, 3),
            new(17, 0, 0, 1, 3),
            new(14, 0, 0, 1, 3),
            new(19, 0, 0, 1, 3),
            new(34, 0, 0, 1, 3),
            new(37, 0, 0, 1, 2),
            new(12, 0, 0, 1, 2),
            new(30, 0, 0, 1, 2),
            new(26, 0, 0, 1, 2),
            new(4, 0, 0, 1, 2),
            new(25, 0, 0, 1, 2),
        ]);

    private static IReadOnlyList<PersonalityEditorOption> UnconditionalEffects { get; } =
        Array.AsReadOnly<PersonalityEditorOption>(
        [
            new(0, 0, 0, 1, 0),
            new(108, 25, 25, 1, 2),
            new(82, 1, 10, 1, 2),
            new(57, 1, 30, 1, 2),
            new(53, 1, 1, 1, 2),
            new(54, 1, 1, 1, 2),
            new(47, 1, 30, 1, 2),
            new(29, 1, 1, 1, 2),
            new(30, 1, 30, 1, 2),
            new(77, 100, 1000, 100, 2),
            new(13, 1, 30, 1, 2),
            new(20, 1, 20, 1, 2),
            new(14, 1, 30, 1, 2),
            new(21, 1, 1, 1, 3),
            new(22, 1, 1, 1, 3),
            new(23, 1, 1, 1, 3),
            new(24, 1, 1, 1, 3),
            new(9, 1, 50, 1, 3),
            new(10, 1, 50, 1, 3),
            new(11, 1, 50, 1, 3),
            new(12, 1, 50, 1, 3),
            new(96, 1, 1, 1, 3),
            new(98, 1, 30, 1, 3),
            new(26, 1, 30, 1, 4),
            new(25, 1, 1, 1, 4),
            new(81, 1, 1, 1, 4),
            new(27, 1, 30, 1, 4),
            new(28, 1, 30, 1, 4),
            new(94, 1, 6, 1, 5),
            new(32, 1, 20, 1, 5),
            new(113, 1, 20, 1, 7),
            new(114, 1, 10, 1, 7),
            new(115, 1, 1, 1, 7),
            new(118, 1, 30, 1, 7),
            new(18, 1, 50, 1, 7),
            new(63, 30, 30, 1, 7),
            new(64, 30, 30, 1, 7),
            new(65, 30, 30, 1, 7),
            new(17, 1, 30, 1, 8),
            new(16, 1, 30, 1, 8),
            new(33, 1, 30, 1, 8),
            new(34, 1, 30, 1, 8),
            new(15, 1, 10, 1, 8),
            new(100, 1, 1, 1, 8),
            new(119, 1, 50, 1, 8),
            new(120, 1, 30, 1, 8),
            new(123, 1, 10, 1, 8),
            new(125, 1, 1, 1, 8),
            new(126, 1, 1, 1, 8),
            new(68, 1, 30, 1, 9),
            new(43, 30, 30, 1, 9),
            new(44, 1, 30, 1, 9),
            new(45, 1, 30, 1, 9),
            new(74, 1, 30, 1, 9),
            new(66, 1, 1, 1, 9),
            new(49, 1, 1, 1, 10),
            new(50, 1, 1, 1, 10),
            new(122, 1, 40, 1, 10),
        ]);

    private static IReadOnlyList<PersonalityEditorOption> ConditionalEffects { get; } =
        Array.AsReadOnly<PersonalityEditorOption>(
        [
            new(0, 0, 0, 1, 0),
            new(19, -2, 4, 1, 1),
            new(5, -6, 6, 1, 1),
            new(6, -3, 6, 1, 1),
            new(7, -6, 6, 1, 1),
            new(8, -3, 6, 1, 1),
            new(4, 1, 6, 1, 1),
            new(53, 1, 1, 1, 2),
            new(54, 1, 1, 1, 2),
            new(55, 1, 1, 1, 2),
            new(62, 1, 10, 1, 2),
            new(13, -10, 30, 1, 2),
            new(51, 1, 1, 1, 2),
            new(20, -20, 20, 1, 2),
            new(9, -20, 50, 1, 3),
            new(10, -20, 50, 1, 3),
            new(11, -20, 50, 1, 3),
            new(12, -20, 50, 1, 3),
            new(117, 1, 1, 1, 7),
        ]);

    private static readonly IReadOnlyList<int> AllOptions = Array.AsReadOnly<int>([1, 11, 37]);
    private static readonly IReadOnlyList<int> FirstOption = Array.AsReadOnly<int>([1]);
    private static readonly IReadOnlyList<int> LastOption = Array.AsReadOnly<int>([37]);
    private static readonly IReadOnlyList<int> FirstAndLastOptions = Array.AsReadOnly<int>([1, 37]);
    private static readonly IReadOnlyList<int> FirstTwoOptions = Array.AsReadOnly<int>([1, 11]);

    //The source preserves [1,11,37] order. A condition with no narrowed
    //collector returns the complete list; its parent control can be disabled.
    public static IReadOnlyList<int> ReadOptions(int conditionKind) => conditionKind switch
    {
        4 or 12 or 14 or 15 or 16 or 17 or 20 or 21 or 22 or 25 or 26 or 28 or 30 or 31 or 32 or 34 or 39 => FirstOption,
        9 or 10 => FirstAndLastOptions,
        19 or 37 or 41 => LastOption,
        40 => FirstTwoOptions,
        _ => AllOptions
    };

    /// <summary>The parent's existing-value validation uses the range table,
    /// independently of the current menu's candidate and incompatibility gates.</summary>
    public static PersonalityEditorOption? ReadEffectRange(int conditionKind, int code)
    {
        var table = conditionKind == 0 ? UnconditionalEffects : ConditionalEffects;
        foreach (var source in table)
            if (source.Code == code) return Range(conditionKind, source);
        return null;
    }

    /// <summary>slot is 1 for the first effect or 2 for the second. The other
    /// effect owns duplicate/incompatibility exclusions; no draft is mutated.</summary>
    public static IReadOnlyList<PersonalityEditorOption> ReadEffects(int conditionKind, int optionKind,
        int slot, int otherEffectCode)
    {
        if (slot is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(slot));
        var result = new List<PersonalityEditorOption>();
        var table = conditionKind == 0 ? UnconditionalEffects : ConditionalEffects;
        foreach (var source in table)
        {
            if (source.Code == 0)
            {
                //The first effect cannot be cleared while the second is set.
                if (slot != 1 || otherEffectCode == 0) result.Add(source);
                continue;
            }
            if (otherEffectCode != 0 && (source.Code == otherEffectCode || Incompatible(source.Code, otherEffectCode)))
                continue;
            if (conditionKind != 0 && !ConditionalEffectAllowed(conditionKind, optionKind, source.Code)) continue;
            //The range getter preserves fixed values. For variable conditional
            //effects, kind10 is the negative branch and every other kind is
            //positive. Step/category retain the actual table values.
            result.Add(Range(conditionKind, source));
        }
        return result.AsReadOnly();
    }

    private static PersonalityEditorOption Range(int conditionKind, PersonalityEditorOption source) =>
        conditionKind == 0 || source.Minimum == source.Maximum ? source
            : conditionKind == 10 ? source with { Maximum = -1 }
            : source with { Minimum = 1 };

    private static bool ConditionalEffectAllowed(int condition, int option, int effect) => effect switch
    {
        4 => condition is not (10 or 12 or 19 or 30),
        5 or 7 => condition is not (12 or 19 or 30),
        6 => condition is not (14 or 19 or 30),
        8 => condition is not (19 or 30),
        9 or 10 or 11 or 12 => (condition is 9 or 10) && option == 1,
        13 => (condition is 4 or 30) || (condition is 9 or 10) && option == 1,
        19 => condition is not (12 or 19 or 30 or 37),
        20 or 53 or 54 or 62 or 117 => condition == 9 && option == 1,
        51 => condition is 14 or 19 or 37,
        55 => condition is 19 or 26,
        _ => false
    };

    //The ordinary editor asks for the complete incompatibility collector,
    //including its extended groups. Membership excludes the other entries
    //of each reached group; it is not a transitive closure across groups.
    private static bool Incompatible(int candidate, int other)
    {
        foreach (var group in IncompatibilityGroups)
            if (Array.IndexOf(group, other) >= 0 && Array.IndexOf(group, candidate) >= 0) return true;
        return false;
    }

    private static readonly int[][] IncompatibilityGroups =
    [
        [1, 2], [2, 3], [9, 21], [29, 30], [30, 31],
        [10, 21, 22], [11, 21, 23], [12, 21, 24],
        [9, 10], [9, 11], [9, 12], [25, 26, 56, 102],
        [83, 84, 85, 86, 87], [56, 112], [115, 116],
        [4, 19], [5, 19], [6, 19], [7, 19], [8, 19],
        [27, 81, 98], [47, 53], [21, 96]
    ];
}
