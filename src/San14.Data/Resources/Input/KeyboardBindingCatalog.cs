using San14.Data.Formats;

namespace San14.Data;

public sealed record KeyboardBindingAction(int ActionId, int Row, string Name,
    int DefaultPrimary, int DefaultSecondary);
public sealed record KeyboardBindingKey(int Usage, string Name, string JapaneseName);
public readonly record struct KeyboardBindingConflict(int ActionId, int Slot);

/// <summary>Original keyboard usages and the environment page's thirty editable
/// actions. Storage contains an additional reserved action zero, with two slots
/// for every action. Key values are USB usages, not Windows virtual-key values.</summary>
public sealed class KeyboardBindingCatalog
{
    public const int ActionCount = 31;
    public const int BindingCount = ActionCount * 2;
    public const int ShiftModifier = 0x1000;
    public const int ControlModifier = 0x2000;
    public const int ReservedBinding = 0x101;

    private static readonly int[] ActionMessages =
    [2114,2115,2116,2117,2118,2119,2120,2121,2122,2123,2124,2125,2126,2127,2128,
     2129,2130,2131,2143,2132,2133,2134,2135,2136,2137,2138,2139,2140,2141,2142];
    private static readonly int[] DefaultPrimary =
    [0,0x1D,0x1B,0x1A,0x16,4,7,6,0x19,0x14,8,0x29,0x2C,0x2A,0x1E,0x1F,
     0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x44,0x101,0x3A,0x3B,0x3C,0x3D,0x3E,0x2B];
    private static readonly int[] KeyUsages =
    [57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,
     81,82,83,101,136,138,139,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,
     21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,
     44,45,46,47,48,49,50,51,52,53,54,55,56,84,85,86,87,88,89,90,91,92,93,
     94,95,96,97,98,99,135,137];
    private static readonly HashSet<int> RejectedUsages =
        [74,75,76,77,78,57,40,70,71,72,83,101,136,53];
    private readonly Dictionary<int, KeyboardBindingKey> keysByUsage;
    private readonly string reservedName;
    private readonly Lazy<string> conflictQuestion;

    public IReadOnlyList<KeyboardBindingAction> Actions { get; }
    public IReadOnlyList<KeyboardBindingKey> Keys { get; }
    public IReadOnlyList<int> DefaultBindings { get; }

    private KeyboardBindingCatalog(GameResourceRepository repository, string locale)
    {
        var data = repository.LoadMessages(locale, MessageKind.Data).Messages;
        string Text(int section, int id) => data.Sections[section].Records[id].Text
            ?? throw new InvalidDataException("Missing original keyboard binding caption.");
        Actions = Array.AsReadOnly(ActionMessages.Select((id, row) =>
            new KeyboardBindingAction(row + 1, row, Text(0, id), DefaultPrimary[row + 1], 0)).ToArray());
        var bindings = new int[BindingCount];
        for (int action = 0; action < ActionCount; action++) bindings[action * 2] = DefaultPrimary[action];
        DefaultBindings = Array.AsReadOnly(bindings);
        reservedName = Text(1, 0x627);
        conflictQuestion = new(() =>
        {
            var record = repository.LoadMessages(locale, MessageKind.Game).Messages.Sections[1].Records[77];
            if (record.Literals.Count != 1 || record.Literals[0].Offset != 3 ||
                record.RawBytes.Length != record.Literals[0].ByteLength + 9 ||
                !record.RawBytes.AsSpan(0, 3).SequenceEqual(new byte[] { 7, 7, 1 }) ||
                !record.RawBytes.AsSpan(record.RawBytes.Length - 6).SequenceEqual(new byte[] { 7, 7, 2, 5, 5, 5 }))
                throw new InvalidDataException("Unsupported original keyboard conflict question.");
            return record.Literals[0].Text;
        });
        Keys = Array.AsReadOnly(KeyUsages.Select((usage, row) =>
        {
            string name = Text(0, 0x861 + row);
            // Numeric captions are native literals, since the message entries
            // are intentionally empty. Other substitutions follow host layout.
            if (usage is >= 30 and <= 39) name = ((usage - 29) % 10).ToString();
            string ordinary = usage switch
            { 46 => "=", 47 => "[", 48 => "]", 52 => "'", 53 => "`", _ => name };
            return new KeyboardBindingKey(usage, ordinary, name);
        }).ToArray());
        keysByUsage = Keys.ToDictionary(key => key.Usage);
    }

    public static KeyboardBindingCatalog Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        return new(repository, locale);
    }

    public string FormatBinding(int binding, bool japaneseKeyboard = false)
    {
        if (binding == ReservedBinding) return reservedName;
        if (!keysByUsage.TryGetValue(binding & 0xFFF, out var key)) return "";
        return ((binding & ShiftModifier) != 0 ? "Shift + " : "") +
            ((binding & ControlModifier) != 0 ? "Ctrl + " : "") +
            (japaneseKeyboard ? key.JapaneseName : key.Name);
    }

    public bool IsCapturable(int usage, bool japaneseKeyboard = false) =>
        keysByUsage.ContainsKey(usage) &&
        (!RejectedUsages.Contains(usage) || (usage == 53 && !japaneseKeyboard));

    public string FormatConflict(int conflictingActionId)
    {
        EditableAction(conflictingActionId);
        // The native 30CEF0(0x4D) template is exactly "%s\n%s": the full
        // literal Game275D, followed by the old action's name. Game275D itself
        // has no dynamic String0 token.
        return conflictQuestion.Value + "\n" + Actions[conflictingActionId - 1].Name;
    }

    /// <summary>Returns no candidate for unsupported/reserved keys. The caller
    /// handles Escape/current Cancel and mouse cancellation before capture.</summary>
    public int? NormalizeCaptured(int actionId, int usage, bool shift, bool control,
        bool japaneseKeyboard = false) => Normalize(actionId,
            usage | (shift ? ShiftModifier : control ? ControlModifier : 0), japaneseKeyboard);

    public int? Normalize(int actionId, int binding, bool japaneseKeyboard = false)
    {
        EditableAction(actionId);
        if ((binding & ~0x3FFF) != 0 || !IsCapturable(binding & 0xFFF, japaneseKeyboard)) return null;
        if ((binding & ControlModifier) != 0 && ((0x418027FEu >> actionId) & 1) != 0)
            binding &= ~ControlModifier;
        if ((binding & ShiftModifier) != 0 && (actionId <= 11 || actionId == 30))
            binding &= ~ShiftModifier;
        return binding;
    }

    public KeyboardBindingConflict? FindConflict(IReadOnlyList<int> bindings, int binding)
    {
        RequireLayout(bindings);
        if (binding == 0) return null;
        for (int action = 0; action < ActionCount; action++)
            for (int slot = 0; slot < 2; slot++)
                if (bindings[action * 2 + slot] == binding) return new(action, slot);
        return null;
    }

    /// <summary>One user edit: release the previous binding before assigning the
    /// selected slot. A conflict within the same action needs no confirmation.</summary>
    public int[] Rebind(IReadOnlyList<int> bindings, int actionId, int slot, int binding,
        bool replaceConflict)
    {
        RequireLayout(bindings); EditableAction(actionId);
        if ((uint)slot >= 2) throw new ArgumentOutOfRangeException(nameof(slot));
        if (bindings[actionId * 2 + slot] == ReservedBinding)
            throw new InvalidOperationException("The original reserved keyboard slot cannot be rebound.");
        if (binding == ReservedBinding || binding < 0 || (binding & ~0x3FFF) != 0 ||
            (binding != 0 && !keysByUsage.ContainsKey(binding & 0xFFF)))
            throw new ArgumentOutOfRangeException(nameof(binding));
        var conflict = FindConflict(bindings, binding);
        if (conflict is { } previous && previous.ActionId != actionId && !replaceConflict)
            throw new InvalidOperationException("The existing keyboard action requires confirmation before replacement.");
        int[] result = bindings.ToArray();
        if (conflict is { } released) result[released.ActionId * 2 + released.Slot] = 0;
        result[actionId * 2 + slot] = binding;
        return result;
    }

    private static void EditableAction(int actionId)
    {
        if (actionId is < 1 or >= ActionCount) throw new ArgumentOutOfRangeException(nameof(actionId));
    }
    private static void RequireLayout(IReadOnlyList<int> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count != BindingCount) throw new ArgumentException("Keyboard preferences require 31 pairs.", nameof(bindings));
    }
}
