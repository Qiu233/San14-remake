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
    private S14EventConditionProgram? _program;
    private bool _blocked;
    public bool ConditionBlocked => _blocked || _basic?.ConditionBlocked == true;

    public S14PersonEventConditionRuntime(IS14PersonEventConditionQueries queries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14BasicEventConditionQueries? basicQueries = null)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        if (basicQueries is not null) _basic = new(basicQueries, inputs);
    }

    public static bool IsSupported(int code) => code is 59 or 298;

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
        if (raw < 0) throw new InvalidDataException($"Dynamic person selector {raw} is unsupported.");
        return RawPersonPointer(raw) ?? RawPersonPointer(0)
            ?? throw new InvalidDataException("Required person slot0 fallback pointer is null.");
    }

    private S14EventConditionPersonReference? RawPersonPointer(int raw)
    {
        S14EventConditionPersonPointer lookup = raw <= 6000
            ? _queries.ReadConditionPersonTableSlot(raw) ?? throw new InvalidDataException($"Person table slot {raw} is unknown.")
            : _queries.ReadConditionBasePerson() ?? throw new InvalidDataException("Base-person pointer is unknown.");
        if (lookup.Value is { PhysicalSlot: < 0 }) throw new InvalidDataException("Person pointer has a negative physical identity.");
        return lookup.Value;
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current {field} is unknown.");
}
