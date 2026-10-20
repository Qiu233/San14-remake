namespace San14.Data.Formats;

/// <summary>Pointer identity is separate from the character ID carried by that object.</summary>
public sealed record S14EventConditionPersonReference(int PhysicalSlot);
public sealed record S14EventConditionForceReference(int PhysicalSlot);
public sealed record S14EventConditionPersonPointer(S14EventConditionPersonReference? Value);

/// <summary>
/// Current inputs consumed by CE conditions59/298. An absent outer lookup is
/// unknown; a present lookup containing null is an observed null pointer.
/// Force order and pointer aliases are supplied, never inferred from Core IDs.
/// </summary>
public interface IS14PersonEventConditionQueries
{
    S14EventConditionPersonPointer? ReadConditionPersonTableSlot(int slot);
    S14EventConditionPersonPointer? ReadConditionBasePerson();
    byte? ReadConditionPersonStatus(S14EventConditionPersonReference person);
    bool? ReadConditionPersonValidity(S14EventConditionPersonReference person);
    IReadOnlyList<S14EventConditionForceReference>? ReadRegisteredConditionForcesInNativeOrder();
    ushort? ReadConditionForceRulerPersonId(S14EventConditionForceReference force);
    ushort? ReadConditionPersonCharacterId(S14EventConditionPersonReference person) => null;
    ushort? ReadConditionPersonPartnerId(S14EventConditionPersonReference person) => null;
    IReadOnlyList<int?>? ReadRegisteredConditionPersonPhysicalSlots() => null;
    S14EventConditionPersonPointer? ReadConditionCurrentPlayerPerson() => null;
    ushort? ReadConditionPersonFlags(S14EventConditionPersonReference person) => null;
    bool? ReadConditionPersonHumble(S14EventConditionPersonReference person) => null;
    byte? ReadConditionPersonRawLoyalty(S14EventConditionPersonReference person) => null;
    byte? ReadConditionPersonBaseAbility(S14EventConditionPersonReference person, int abilityIndex) => null;
    bool? ReadConditionPersonIsAreaOfficer(S14EventConditionPersonReference person) => null;
    ushort? ReadConditionPersonHome(S14EventConditionPersonReference person) => null;
    bool? ReadConditionPersonDislikes(S14EventConditionPersonReference first, S14EventConditionPersonReference second) => null;
    bool? ReadConditionPersonFamily(S14EventConditionPersonReference first, S14EventConditionPersonReference second) => null;
}

/// <summary>
/// Complete current CE SComparePersonStatusOR(3A7170) and
/// SCompareForceExistingByPerson(3B49E0) slot0 leaves, including sticky blocking.
/// The positive identity-resolver branch3DAB20 is supported; dynamic negative
/// selectors remain unresolved. A fresh runtime is required for each event.
/// </summary>
public sealed class S14PersonEventConditionRuntime : IS14EventConditionRuntime
{
    private readonly IS14PersonEventConditionQueries _queries;
    private readonly Func<S14ConditionContext, S14ConditionDispatchInputs> _inputs;
    private readonly S14BasicEventConditionRuntime? _basic;
    private readonly Func<int, S14EventConditionPersonReference?>? _targetPersonResolver;
    private S14EventConditionProgram? _program;
    private bool _blocked;
    public bool ConditionBlocked => _blocked || _basic?.ConditionBlocked == true;

    public S14PersonEventConditionRuntime(IS14PersonEventConditionQueries queries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14BasicEventConditionQueries? basicQueries = null,
        Func<int, S14EventConditionPersonReference?>? targetPersonResolver = null)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        _targetPersonResolver = targetPersonResolver;
        if (basicQueries is not null) _basic = new(basicQueries, inputs);
    }

    public static bool IsSupported(int code) => code is 59 or 60 or 61 or 62 or 64 or 66 or 67 or 69 or 75 or 79 or 244 or 298;

    public bool TryGetBinding(int code, out S14ConditionBinding binding)
    {
        if (S14EventConditionEvaluator.BuiltInBindings.TryGetValue(code, out binding)) return true;
        if (IsSupported(code)) { binding = new(0, 1, true); return true; }
        if (_basic is not null) return _basic.TryGetBinding(code, out binding);
        binding = default; return false;
    }

    public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context)
    {
        if (_program is not null && !ReferenceEquals(_program, context.Program))
            throw new InvalidDataException("Person condition state cannot be reused across programs.");
        _program = context.Program;
        return _inputs(context);
    }

    public int InvokeLeaf(S14ConditionContext context)
    {
        try
        {
            IReadOnlyList<int> arguments = context.Operation.RawArguments;
            if (arguments.Count != 8) throw new InvalidDataException("Person condition operands are incomplete.");
            return context.Operation.Code switch
            {
                59 => PersonStatus(context, arguments) ? 1 : 0,
                64 => PersonsAvailable(context, arguments) ? 1 : 0,
                66 => PersonHasPartner(arguments[0], arguments[1]) ? 1 : 0,
                60 or 61 or 62 or 67 => PersonComparison(context.Operation.Code, arguments) ? 1 : 0,
                69 => PersonsHumble(context, arguments) ? 1 : 0,
                75 => PersonParameter(arguments) ? 1 : 0,
                79 => PersonAreaOfficer(arguments[0], arguments[1]) ? 1 : 0,
                244 => PersonsDead(arguments) ? 1 : 0,
                298 => ForceExisting(context, arguments[0], arguments[1]) ? 1 : 0,
                _ when _basic is not null => _basic.InvokeLeaf(context),
                _ => throw new InvalidDataException("No person condition leaf is bound.")
            };
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Person condition stream {context.Program.StreamIndex}, " +
                $"operation {context.Operation.Index}, code {context.Operation.Code}, offset {context.Operation.Offset}: {error.Message}", error);
        }
    }

    private bool PersonsAvailable(S14ConditionContext context, IReadOnlyList<int> arguments)
    {
        bool result = true, blocked = false;
        for (int i = 0; i < 5; i++)
        {
            int raw = arguments[i];
            if (raw == 0) continue;
            var person = RawPersonPointer(raw) ?? RawPersonPointer(0);
            if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "person validity"))
            { result = false; blocked = true; continue; }
            byte status = Known(_queries.ReadConditionPersonStatus(person), "person status");
            if (status is 6 or 7 or 9) result = false;
            if (status == 9) blocked = true;
        }
        if (context.OrDepth == 0 && blocked) _blocked = true;
        return result;
    }

    private bool PersonHasPartner(int raw, int expected)
    {
        var person = RawPersonPointer(raw) ?? RawPersonPointer(0);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "partner person validity")) return false;
        bool found = Known(_queries.ReadConditionPersonPartnerId(person), "person partner ID") is >= 1 and <= 6000;
        if (!found)
        {
            ushort id = Known(_queries.ReadConditionPersonCharacterId(person), "partner target identity");
            var people = _queries.ReadRegisteredConditionPersonPhysicalSlots() ?? throw new InvalidDataException("Registered people are unknown.");
            foreach (int? slot in people)
            {
                if (slot is not int physical) throw new InvalidDataException("Registered person pointer is unknown or null.");
                if (Known(_queries.ReadConditionPersonPartnerId(new(physical)), "registered person partner ID") == id)
                { found = true; break; }
            }
        }
        return (found ? 1 : 0) == expected;
    }

    private bool PersonStatus(S14ConditionContext context, IReadOnlyList<int> arguments)
    {
        S14EventConditionPersonReference person = ResolveIdentityOperand(arguments[0]);
        bool matches = false, wantsDead = false, wantsNotAppeared = false,
            wantsUndiscovered = false, wantsOther = false;
        // Only the five fields+10..+20 are consumed; the final two operands are ignored.
        for (int index = 1; index <= 5; index++)
        {
            int wanted = arguments[index];
            if (wanted == 0) continue;
            byte actual = Known(_queries.ReadConditionPersonStatus(person), "person+11E status");
            if (actual == wanted) matches = true;
            if (wanted == 9) wantsDead = true;
            else if (wanted == 7) wantsNotAppeared = true;
            else if (wanted == 8) wantsUndiscovered = true;
            else wantsOther = true;
        }
        if (!wantsDead)
        {
            byte actual = Known(_queries.ReadConditionPersonStatus(person), "person+11E status for blocking");
            bool blocked = actual == 9;
            if ((wantsNotAppeared || wantsUndiscovered) && !wantsOther)
                blocked |= wantsNotAppeared && !wantsUndiscovered
                    ? unchecked((byte)(actual - 1)) <= 7 && actual != 7
                    : unchecked((byte)(actual - 1)) <= 5;
            if (context.OrDepth == 0 && blocked) _blocked = true;
        }
        return matches;
    }

    private bool PersonsHumble(S14ConditionContext context, IReadOnlyList<int> arguments)
    {
        bool result = true, blocked = false;
        foreach (int raw in arguments.Take(5))
        {
            if (raw == 0) continue;
            var person = RawPersonPointer(raw) ?? RawPersonPointer(0)
                ?? throw new InvalidDataException("Person availability receiver is null.");
            if (!Known(_queries.ReadConditionPersonValidity(person), "availability person validity"))
            { result = false; blocked = true; }
            bool available = Known(_queries.ReadConditionPersonHumble(person), "current person availability");
            result &= available;
            if (context.OrDepth == 0 && Known(_queries.ReadConditionPersonValidity(person), "availability validity reread"))
            {
                blocked |= (Known(_queries.ReadConditionPersonFlags(person), "availability flags") & 0x80) != 0;
                blocked |= Known(_queries.ReadConditionPersonStatus(person), "availability status reread") == 9;
            }
        }
        _blocked |= blocked;
        return result;
    }

    private bool PersonsDead(IReadOnlyList<int> arguments)
    {
        foreach (int raw in arguments.Take(5))
        {
            var person = RawPersonPointer(raw) ?? RawPersonPointer(0);
            if (raw != 0 && (person is null || !Known(_queries.ReadConditionPersonValidity(person), "dead-test person validity")))
                _blocked = true;
            if (person is not null && Known(_queries.ReadConditionPersonValidity(person), "dead-test person validity reread") &&
                Known(_queries.ReadConditionPersonStatus(person), "dead-test person status") != 9) return false;
        }
        return true;
    }

    private bool PersonAreaOfficer(int raw, int expected)
    {
        var person = RawPersonPointer(raw) ?? RawPersonPointer(0);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "area officer person validity")) return false;
        bool exists = Known(_queries.ReadConditionPersonIsAreaOfficer(person), "current registered area officer");
        return (exists ? 1 : 0) == expected;
    }

    private bool PersonParameter(IReadOnlyList<int> arguments)
    {
        var person = RawPersonPointer(arguments[0]) ?? RawPersonPointer(0);
        if (person is null || !Known(_queries.ReadConditionPersonValidity(person), "parameter person validity") ||
            arguments[1] is < 1 or > 6) return false;
        byte value = arguments[1] == 6 ? Known(_queries.ReadConditionPersonRawLoyalty(person), "raw person loyalty")
            : Known(_queries.ReadConditionPersonBaseAbility(person, arguments[1] - 1), "current base ability");
        return Compare(value, arguments[2], arguments[3]);
    }

    private bool PersonComparison(int code, IReadOnlyList<int> arguments)
    {
        var first = RawPersonPointer(arguments[0]) ?? RawPersonPointer(0);
        var second = code == 61 ? null : RawPersonPointer(arguments[2]) ?? RawPersonPointer(0);
        if (first is null || !Known(_queries.ReadConditionPersonValidity(first), "comparison person validity")) return false;
        if (code == 61) return Compare(Known(_queries.ReadConditionPersonRawLoyalty(first), "raw person loyalty"), arguments[1], arguments[2]);
        if (second is null || !Known(_queries.ReadConditionPersonValidity(second), "comparison second person validity")) return false;
        if (code == 67) return Compare(Known(_queries.ReadConditionPersonHome(first), "first person home"), arguments[1],
            Known(_queries.ReadConditionPersonHome(second), "second person home"));
        bool result = code == 60 ? Known(_queries.ReadConditionPersonDislikes(first, second), "person dislike")
            : Known(_queries.ReadConditionPersonFamily(first, second), "person family") ||
                Known(_queries.ReadConditionPersonFamily(second, first), "reverse person family");
        return (result ? 1 : 0) == arguments[1];
    }

    private static bool Compare(int left, int mode, int right) => mode switch
    { 1 => left != right, 2 => left == right, 3 => left > right, 4 => left >= right, 5 => left < right, 6 => left <= right, _ => false };

    private bool ForceExisting(S14ConditionContext context, int rawPerson, int expected)
    {
        S14EventConditionPersonReference person = ResolveIdentityOperand(rawPerson);
        bool result = false, block;
        if (!Known(_queries.ReadConditionPersonValidity(person), "current native person validity")) block = true;
        else
        {
            block = Known(_queries.ReadConditionPersonStatus(person), "person+11E status") == 9;
            int found = 0;
            IReadOnlyList<S14EventConditionForceReference> forces = _queries.ReadRegisteredConditionForcesInNativeOrder()
                ?? throw new InvalidDataException("Current registered force list is unknown.");
            foreach (S14EventConditionForceReference force in forces)
            {
                if (force is null) throw new InvalidDataException("Registered force node contains a null receiver.");
                ushort ruler = Known(_queries.ReadConditionForceRulerPersonId(force), "force+10 ruler ID");
                // The source list loop performs this lookup without the resolver's null fallback.
                S14EventConditionPersonReference? rulerPointer = RawPersonPointer(ruler);
                if (rulerPointer == person) { found = 1; break; }
            }
            result = found == expected;
        }
        if (context.OrDepth == 0 && expected == 1 && block) _blocked = true;
        return result;
    }

    private S14EventConditionPersonReference ResolveIdentityOperand(int raw)
    {
        return RawPersonPointer(raw) ?? RawPersonPointer(0)
            ?? throw new InvalidDataException("Required person slot0 fallback pointer is null.");
    }

    private S14EventConditionPersonReference? RawPersonPointer(int raw)
    {
        if (raw is >= -8 and <= -5)
            return (_targetPersonResolver ?? throw new InvalidDataException("Event target-person context is unavailable."))(raw);
        if (raw == -4) return (_queries.ReadConditionCurrentPlayerPerson()
            ?? throw new InvalidDataException("Current player ruler pointer is unknown.")).Value;
        if (raw < 0) throw new InvalidDataException($"Dynamic person selector {raw} is unsupported.");
        S14EventConditionPersonPointer lookup = raw <= 6000
            ? _queries.ReadConditionPersonTableSlot(raw) ?? throw new InvalidDataException($"Person table slot {raw} is unknown.")
            : _queries.ReadConditionBasePerson() ?? throw new InvalidDataException("Base-person pointer is unknown.");
        if (lookup.Value is { PhysicalSlot: < 0 }) throw new InvalidDataException("Person pointer has a negative physical identity.");
        return lookup.Value;
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current {field} is unknown.");
}
