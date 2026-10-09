namespace San14.Data.Formats;

/// <summary>Raw fields required by the verified CPersonData validity and whereabouts getters.</summary>
public sealed record S14ConditionNativePerson(int PhysicalSlot, ushort CharacterId,
    ushort RawFlags196, byte RawFlag1FD, byte RawStatus11E, ushort RawLocation11C);

/// <summary>Raw fields required by the verified CCityData validity getter.</summary>
public sealed record S14ConditionNativeCity(int PhysicalSlot, ushort RawObjectId4E, byte RawFlag69);

/// <summary>Raw fields required by the verified CObjectData validity getter.</summary>
public sealed record S14ConditionNativeObject(int PhysicalSlot, ushort RawField10, byte RawField12);

// An absent lookup means unknown input. A present lookup with Value=null means
// the original pointer was explicitly observed to be null. Neither is an ID guess.
public sealed record S14ConditionPersonLookup(S14ConditionNativePerson? Value);
public sealed record S14ConditionCityLookup(S14ConditionNativeCity? Value);
public sealed record S14ConditionObjectLookup(S14ConditionNativeObject? Value);

/// <summary>
/// Explicit current native inputs, not static scenario defaults. Nullable results
/// mean unavailable evidence and cause an error when consumed. Pointer-table slots
/// and the base-person pointer are distinct queries, including explicit sentinels.
/// Implementations must not infer native validity from a Core dictionary entry.
/// </summary>
public interface IS14BasicEventConditionQueries
{
    bool? ReadWorldEventFlag(int index);
    bool? ReadEventManagerPresent();
    bool? ReadManagerEventFlag(int index);
    int? ReadPastGameTime();
    ushort? ReadEmperorId();
    S14ConditionPersonLookup? ReadPersonTableSlot(int slot);
    S14ConditionPersonLookup? ReadBasePerson();
    S14ConditionCityLookup? ReadCityTableSlot(int slot);
    S14ConditionObjectLookup? ReadObjectTableSlot(int slot);
}

/// <summary>
/// The CE basic condition profile supports codes 9/15/17/19 and the existing group/false
/// built-ins, with step one and the sticky execution+38 blocking signal.
/// Negative dynamic ID selectors and unsafe bitmap indices are unsupported.
/// This is not a Core state adapter or a complete first-player event context.
/// </summary>
public sealed class S14BasicEventConditionRuntime : IS14EventConditionRuntime
{
    private readonly IS14BasicEventConditionQueries _queries;
    private readonly Func<S14ConditionContext, S14ConditionDispatchInputs> _readInputs;
    private S14EventConditionProgram? _program;

    public bool ConditionBlocked { get; private set; }

    /// <summary>Create a fresh instance for each evaluation; execution state must not cross events.</summary>
    public S14BasicEventConditionRuntime(IS14BasicEventConditionQueries queries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> readInputs)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(readInputs);
        _queries = queries;
        _readInputs = readInputs;
    }

    /// <summary>Creates fresh execution state while sampling caller inputs on every operation.</summary>
    public static S14EventConditionResult Evaluate(S14EventConditionProgram program,
        IS14BasicEventConditionQueries queries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> readInputs) =>
        S14EventConditionEvaluator.Evaluate(program, new S14BasicEventConditionRuntime(queries, readInputs));

    public bool TryGetBinding(int code, out S14ConditionBinding binding)
    {
        if (S14EventConditionEvaluator.BuiltInBindings.TryGetValue(code, out binding)) return true;
        if (code is 9 or 15 or 17 or 19) { binding = new(0, 1, true); return true; }
        binding = default;
        return false;
    }

    public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_program is not null && !ReferenceEquals(_program, context.Program))
            throw Failure(context, "Execution state cannot be shared between condition programs.");
        _program = context.Program;
        return _readInputs(context);
    }

    public int InvokeLeaf(S14ConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            if (context.Operation.RawArguments.Count != 8 || context.OrDepth < 0)
                throw new InvalidDataException("Invalid condition arguments or OR depth.");
            int first = context.Operation.RawArguments[0];
            int second = context.Operation.RawArguments[1];
            bool result = context.Operation.Code switch
            {
                9 => EventFlag(context, first, second),
                15 => PastTime(context, first, second),
                17 => EmperorPerson(first, second),
                19 => EmperorCity(context, first, second),
                _ => throw new InvalidDataException("This provider has no executable leaf binding.")
            };
            return result ? 1 : 0;
        }
        catch (InvalidDataException error)
        {
            throw Failure(context, error.Message, error);
        }
    }

    private bool EventFlag(S14ConditionContext context, int index, int expected)
    {
        if (index is < 0 or >= S14EventSourceProfile.WorldFlagCount)
            throw new InvalidDataException($"Event bitmap index {index} is outside the supported 128-byte source window.");
        bool firstRead = Known(_queries.ReadWorldEventFlag(index), "world+460 event bit");
        if (context.OrDepth == 0 && Known(_queries.ReadEventManagerPresent(), "event-manager pointer"))
        {
            // The manager bitmap has 124 bytes; the separate world bitmap has 128.
            // Only the expected-one branch reads manager bits in the native body.
            if (expected == 1 && index >= S14EventSourceProfile.ManagerFlagCount)
                throw new InvalidDataException($"Manager event bitmap index {index} is outside the supported 124-byte source window.");
            if (expected == 1 && Known(_queries.ReadManagerEventFlag(index), "manager+24 event bit"))
                ConditionBlocked = true;
            // Original slot0 rereads the world bit rather than reusing the comparison input.
            else if (expected == 0 && Known(_queries.ReadWorldEventFlag(index), "world+460 event bit reread"))
                ConditionBlocked = true;
        }
        return (firstRead ? 1 : 0) == expected;
    }

    private bool PastTime(S14ConditionContext context, int mode, int threshold)
    {
        int actual = Known(_queries.ReadPastGameTime(), "signed world+454 time");
        if (context.OrDepth == 0 && ((mode is 2 or 6 && actual > threshold) || (mode == 5 && actual >= threshold)))
            ConditionBlocked = true;
        return Compare(actual, mode, threshold);
    }

    private bool EmperorPerson(int mode, int operand)
    {
        S14ConditionNativePerson wanted = ResolvePersonOperand(operand);
        ushort actual = Known(_queries.ReadEmperorId(), "world+1654 emperor ID");
        bool valid = EmperorIdValid(actual);
        // This signal is deliberately not guarded by OR depth or comparison mode.
        if (!valid || (wanted.CharacterId == 5001 && actual is 5002 or 5003) ||
            (wanted.CharacterId == 5002 && actual == 5003)) ConditionBlocked = true;
        return valid && Compare(actual, mode, wanted.CharacterId);
    }

    private bool EmperorCity(S14ConditionContext context, int mode, int operand)
    {
        S14ConditionNativeCity? specified = ResolveCityOperand(operand);
        bool result = false;
        if (CityValid(specified))
        {
            S14ConditionNativeCity? emperor = ResolveEmperorCity();
            if (CityValid(emperor)) result = Compare(specified!.PhysicalSlot, mode, emperor!.PhysicalSlot);
        }
        // The original mode-2 path rereads the current emperor ID, even after city failure.
        if (mode == 2)
        {
            ushort actual = Known(_queries.ReadEmperorId(), "world+1654 emperor ID for blocking");
            if (context.OrDepth == 0 && !EmperorIdValid(actual)) ConditionBlocked = true;
        }
        return result;
    }

    private S14ConditionNativePerson ResolvePersonOperand(int operand)
    {
        if (operand < 0) throw new InvalidDataException($"Dynamic person selector {operand} is unsupported.");
        S14ConditionNativePerson? value = ReadPersonPointer(operand);
        // The identity resolver falls back to table slot0. Its raw ID must be supplied, not guessed.
        return value ?? ReadPersonPointer(0)
            ?? throw new InvalidDataException("Required person slot0 fallback pointer is null.");
    }

    private S14ConditionNativePerson? ReadPersonPointer(int operand)
    {
        S14ConditionNativePerson? person = operand <= 6000
            ? Known(_queries.ReadPersonTableSlot(operand), $"person table slot {operand}").Value
            : Known(_queries.ReadBasePerson(), "base-person pointer").Value;
        if (person is not null && person.PhysicalSlot is < 0 or > 6000)
            throw new InvalidDataException("Person physical slot is outside the supported source table.");
        return person;
    }

    private S14ConditionNativeCity? ResolveCityOperand(int operand)
    {
        if (operand < 0) throw new InvalidDataException($"Dynamic city selector {operand} is unsupported.");
        S14ConditionNativeCity? city = ReadCityPointer(operand <= 51 ? operand : 0);
        return city ?? ReadCityPointer(0);
    }

    private S14ConditionNativeCity? ReadCityPointer(int slot)
    {
        S14ConditionNativeCity? city = Known(_queries.ReadCityTableSlot(slot), $"city table slot {slot}").Value;
        if (city is not null && city.PhysicalSlot is < 0 or > 51)
            throw new InvalidDataException("City physical slot is outside the supported source table.");
        return city;
    }

    private S14ConditionNativeCity? ResolveEmperorCity()
    {
        ushort id = Known(_queries.ReadEmperorId(), "world+1654 emperor ID for whereabouts");
        S14ConditionNativePerson? person = ReadPersonPointer(id);
        // Unlike the identity operand resolver, a null or invalid person goes directly to city0.
        if (person is null || !PersonValid(person)) return ReadCityPointer(0);
        ushort location = person.RawLocation11C;
        return ReadCityPointer(location is >= 1 and <= 51 ? location : 0);
    }

    private bool CityValid(S14ConditionNativeCity? city)
    {
        if (city is null || city.PhysicalSlot == 0) return false;
        int slot = city.RawObjectId4E <= 3000 ? city.RawObjectId4E : 0;
        S14ConditionNativeObject? linked = Known(_queries.ReadObjectTableSlot(slot), $"object table slot {slot}").Value;
        if (linked is null) return false;
        if (linked.PhysicalSlot is < 0 or > 3000)
            throw new InvalidDataException("Object physical slot is outside the supported source table.");
        return linked.PhysicalSlot != 0 && linked.RawField10 < 48400 && linked.RawField12 != 0 && city.RawFlag69 == 0;
    }

    private static bool PersonValid(S14ConditionNativePerson person) => NativeValidity.Person(person);

    private static bool EmperorIdValid(ushort id) => id is >= 1 and <= 6000;

    private static bool Compare(int left, int mode, int right) => mode switch
    {
        1 => left != right, 2 => left == right, 3 => left > right,
        4 => left >= right, 5 => left < right, 6 => left <= right, _ => false
    };

    private static T Known<T>(T? value, string field) where T : struct =>
        value ?? throw new InvalidDataException($"Current input is unknown: {field}.");
    private static T Known<T>(T? value, string field) where T : class =>
        value ?? throw new InvalidDataException($"Current input is unknown: {field}.");

    private static InvalidDataException Failure(S14ConditionContext context, string message, Exception? inner = null) =>
        new($"Basic event condition stream {context.Program.StreamIndex}, operation {context.Operation.Index}, " +
            $"code {context.Operation.Code}, source offset {context.Operation.Offset}: {message}", inner);
}
