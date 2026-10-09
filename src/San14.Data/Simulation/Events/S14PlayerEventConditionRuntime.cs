namespace San14.Data.Formats;

public sealed record S14EventConditionForcePointer(S14EventConditionForceReference? Value);

/// <summary>
/// Explicit native inputs for SCompareUserForce. Person-force and current-player
/// getters include the native district/force-zero fallbacks described below;
/// an unknown outer lookup is distinct from a known null pointer.
/// </summary>
public interface IS14PlayerEventConditionQueries : IS14PersonEventConditionQueries
{
    S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot);

    // Complete 22CE20: person+118 -> district table (0 when >51), native
    // district validity -> district+10 -> force table (0 when invalid or >51).
    S14EventConditionForcePointer? ReadConditionPersonForce(S14EventConditionPersonReference person);
    bool? ReadConditionForceValidity(S14EventConditionForceReference force);

    // Complete 32F200 -> force.slot12: world+3A force slot (>51 selects force0).
    int? ReadConditionCurrentPlayerForcePhysicalSlot();
    bool? ReadConditionApplicationPresent();

    // Complete 1D2A80: signed world+BC; a known null world pointer returns -1.
    int? ReadConditionCurrentUserIndex();
    ushort? ReadConditionPersonCharacterId(S14EventConditionPersonReference person);

    // Signed i32 world+64+index*4. No guessed player-selection contents.
    int? ReadConditionSelectedUserPersonId(int index);
}

/// <summary>
/// Current CE SCompareUserForce slot0 3B5A10..3B5B5A, composed with the complete
/// person conditions59/298 and optional basic conditions. Supports the literal
/// nonnegative person-selector branch; dynamic selectors remain explicit errors.
/// Current queries are sampled only when the original branch consumes them.
/// </summary>
public sealed class S14PlayerEventConditionRuntime : IS14EventConditionRuntime
{
    private readonly IS14PlayerEventConditionQueries _queries;
    private readonly Func<S14ConditionContext, S14ConditionDispatchInputs> _inputs;
    private readonly S14PersonEventConditionRuntime _person;
    private S14EventConditionProgram? _program;
    public bool ConditionBlocked => _person.ConditionBlocked;

    public S14PlayerEventConditionRuntime(IS14PlayerEventConditionQueries queries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IS14BasicEventConditionQueries? basicQueries = null)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        _person = new(queries, inputs, basicQueries);
    }

    public static bool IsSupported(int code) => code == 36 || S14PersonEventConditionRuntime.IsSupported(code);

    public bool TryGetBinding(int code, out S14ConditionBinding binding)
    {
        if (code == 36) { binding = new(0, 1, true); return true; }
        return _person.TryGetBinding(code, out binding);
    }

    public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_program is not null && !ReferenceEquals(_program, context.Program))
            throw new InvalidDataException("Player condition state cannot be reused across programs.");
        _program = context.Program;
        return _inputs(context);
    }

    public int InvokeLeaf(S14ConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Operation.Code != 36) return _person.InvokeLeaf(context);
        try
        {
            IReadOnlyList<int> arguments = context.Operation.RawArguments;
            if (arguments.Count != 8) throw new InvalidDataException("Player condition operands are incomplete.");
            int player = CompareUserForce(arguments[1]);
            // This callback compares its actual flag to1 with the source jump
            // table's own direction, which differs from the basic comparer.
            return arguments[0] switch
            {
                1 => player != 1 ? 1 : 0,
                2 => player == 1 ? 1 : 0,
                3 => player < 1 ? 1 : 0,
                4 => player <= 1 ? 1 : 0,
                5 => player > 1 ? 1 : 0,
                6 => player >= 1 ? 1 : 0,
                _ => 0
            };
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Player condition stream {context.Program.StreamIndex}, " +
                $"operation {context.Operation.Index}, code36, offset {context.Operation.Offset}: {error.Message}", error);
        }
    }

    private int CompareUserForce(int rawPerson)
    {
        S14EventConditionForceReference? force = ResolveRulerForce(rawPerson);
        if (force is not null && Known(_queries.ReadConditionForceValidity(force), "native force validity"))
        {
            int currentPlayerSlot = Known(_queries.ReadConditionCurrentPlayerForcePhysicalSlot(),
                "world player force physical slot");
            if (currentPlayerSlot != 0 && currentPlayerSlot == force.PhysicalSlot) return 1;
        }

        if (!Known(_queries.ReadConditionApplicationPresent(), "application pointer")) return 0;
        if (Known(_queries.ReadConditionCurrentUserIndex(), "signed world+BC user index") == -1) return 0;

        // This branch rereads the raw selector's pointer without identity or
        // force-selector null fallback, and does not test person validity.
        S14EventConditionPersonReference? person = RawPersonPointer(rawPerson);
        if (person is null) return 0;
        ushort characterId = Known(_queries.ReadConditionPersonCharacterId(person), "person+10 character ID");
        int index = Known(_queries.ReadConditionCurrentUserIndex(), "signed world+BC user index reread");
        if (index < 0) throw new InvalidDataException($"Negative selected-user index {index} is outside the supported query window.");
        int selected = Known(_queries.ReadConditionSelectedUserPersonId(index), "signed world+64 selected character ID");
        return characterId == selected ? 1 : 0;
    }

    private S14EventConditionForceReference? ResolveRulerForce(int rawPerson)
    {
        if (rawPerson < 0) throw new InvalidDataException($"Dynamic person selector {rawPerson} is unsupported.");
        S14EventConditionPersonReference? person = RawPersonPointer(rawPerson);
        if (person is not null && Known(_queries.ReadConditionPersonValidity(person), "native person validity") &&
            Known(_queries.ReadConditionPersonStatus(person), "person+11E status") == 1)
        {
            S14EventConditionForcePointer lookup = _queries.ReadConditionPersonForce(person)
                ?? throw new InvalidDataException("Current native person-force getter is unknown.");
            if (lookup.Value is not null) return CheckForce(lookup.Value);
        }
        S14EventConditionForcePointer fallback = _queries.ReadConditionForceTableSlot(0)
            ?? throw new InvalidDataException("Force table slot0 fallback pointer is unknown.");
        return fallback.Value is null ? null : CheckForce(fallback.Value);
    }

    private S14EventConditionPersonReference? RawPersonPointer(int raw)
    {
        S14EventConditionPersonPointer lookup = raw <= 6000
            ? _queries.ReadConditionPersonTableSlot(raw)
                ?? throw new InvalidDataException($"Person table slot {raw} is unknown.")
            : _queries.ReadConditionBasePerson()
                ?? throw new InvalidDataException("Base-person pointer is unknown.");
        if (lookup.Value is { PhysicalSlot: < 0 })
            throw new InvalidDataException("Person pointer has a negative physical identity.");
        return lookup.Value;
    }

    private static S14EventConditionForceReference CheckForce(S14EventConditionForceReference force) =>
        force.PhysicalSlot >= 0 ? force : throw new InvalidDataException("Force pointer has a negative physical identity.");

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current {field} is unknown.");
}
