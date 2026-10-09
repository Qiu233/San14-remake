namespace San14.Data.Formats;

public enum S14EventAreaDevelopmentKind { Agriculture, Commerce, Barracks }
public sealed record S14EventExecutionArea(int Identity);
public sealed record S14EventExecutionForce(int Identity);
public sealed record S14EventExecutionPerson(int Identity);
public sealed record S14EventExecutionCity(int Identity);

/// <summary>Null outer value is unknown; a lookup containing null is known absent.</summary>
public sealed record S14EventExecutionLookup<T>(T? Value) where T : class;

public interface IS14AreaEventExecutionState
{
    S14EventExecutionLookup<S14EventExecutionArea>? ResolveAreaSelector(int rawSelector);
    S14EventExecutionLookup<S14EventExecutionForce>? ResolveForceSelector(int rawSelector);
    S14EventExecutionLookup<S14EventExecutionPerson>? ResolvePersonSelector(int rawSelector);
    S14EventExecutionLookup<S14EventExecutionCity>? ResolveCitySelector(int rawSelector);
    bool? IsAreaValid(S14EventExecutionArea area);
    bool? IsForceValid(S14EventExecutionForce force);
    bool? IsPersonValid(S14EventExecutionPerson person);
    bool? IsCityValid(S14EventExecutionCity city);
    IReadOnlyList<S14EventExecutionArea>? ReadRegisteredAreasInNativeOrder();
    IReadOnlyList<S14EventExecutionArea>? ReadForceAreasInNativeOrder(S14EventExecutionForce force);
    S14EventExecutionLookup<S14EventExecutionForce>? ReadAreaCurrentForce(S14EventExecutionArea area);
    S14EventExecutionLookup<S14EventExecutionForce>? ReadPersonCurrentForce(S14EventExecutionPerson person);
    S14EventExecutionLookup<S14EventExecutionPerson>? ReadAreaCurrentOfficer(S14EventExecutionArea area);
    S14EventExecutionLookup<S14EventExecutionCity>? ReadAreaCurrentCity(S14EventExecutionArea area);
    ushort? ReadAreaDevelopment(S14EventExecutionArea area, S14EventAreaDevelopmentKind kind);
    int? ReadAreaCurrentForcePhysicalSlot(S14EventExecutionArea area) => null;
    byte? ReadAreaSecurity(S14EventExecutionArea area) => null;
    void WriteAreaSecurity(S14EventExecutionArea area, byte value, int sourceOperationIndex) =>
        throw new InvalidDataException("Current area security writer is unavailable.");

    // The receiver must implement the complete native setter, including any
    // level-change side effects. A numeric-only implementation is provisional.
    void WriteAreaDevelopment(S14EventExecutionArea area, S14EventAreaDevelopmentKind kind,
        int setterInput, bool processLevelChanges, int sourceOperationIndex);
}

public sealed record S14EventExecutionResult(S14EventConditionResult DispatchResult,
    IReadOnlyList<S14EventExecutionArea> FinalAreaSelection);

/// <summary>
/// CE area-list execution handlers 188..192 and security/development wrappers 193..200.
/// Input selectors and complete setter semantics remain explicit adapter
/// obligations. This class invokes no native machine code or original process.
/// </summary>
public sealed class S14AreaEventExecutionRuntime : IS14EventConditionRuntime
{
    private readonly IS14AreaEventExecutionState _state;
    private readonly Func<S14ConditionContext, S14ConditionDispatchInputs> _inputs;
    private readonly List<S14EventExecutionArea> _areas;
    private readonly IS14AudioEventExecutionState? _audio;
    private S14EventConditionProgram? _program;
    public bool ConditionBlocked => false;
    public IReadOnlyList<S14EventExecutionArea> AreaSelection => _areas.AsReadOnly();

    public S14AreaEventExecutionRuntime(IS14AreaEventExecutionState state,
        Func<S14ConditionContext, S14ConditionDispatchInputs> inputs,
        IReadOnlyList<S14EventExecutionArea> initialAreaSelection,
        IS14AudioEventExecutionState? audio = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        ArgumentNullException.ThrowIfNull(initialAreaSelection);
        if (initialAreaSelection.Any(area => area is null) || initialAreaSelection.Distinct().Count() != initialAreaSelection.Count)
            throw new InvalidDataException("Initial execution area selection is invalid.");
        _areas = initialAreaSelection.ToList();
        _audio = audio;
    }

    public static bool IsSupported(int code) => code is 0 or 5 or 6 or 7 or 8 or 90 or 91 or 92 or
        188 or 189 or 190 or 191 or 192 or 193 or 194 or 195 or 196 or 197 or 198 or 199 or 200;

    public bool TryGetBinding(int code, out S14ConditionBinding binding)
    {
        if (S14EventConditionEvaluator.BuiltInBindings.TryGetValue(code, out binding)) return true;
        if (code is 90 or 91 or 92) { binding = new(1, 1, true); return true; }
        if (code is >= 188 and <= 192) { binding = new(0, 1, true); return true; }
        if (code is >= 193 and <= 200) { binding = new(0, 0, true); return true; }
        binding = default; return false;
    }

    public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context)
    {
        if (_program is not null && !ReferenceEquals(_program, context.Program))
            throw new InvalidDataException("Execution runtime cannot be reused across different programs.");
        _program = context.Program;
        return _inputs(context);
    }

    public S14EventExecutionResult Execute(S14EventExecutionProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        // Publicly constructed programs still require the same complete profile.
        foreach (S14ConditionInstruction operation in program.DispatchProgram.Operations)
        {
            if (!IsSupported(operation.Code))
                throw new InvalidDataException($"Execution operation {operation.Index} has unsupported code {operation.Code}.");
            if (operation.Code is 91 or 92 && _audio is null)
                throw new InvalidDataException($"Execution operation {operation.Index} has no current audio-manager adapter.");
        }
        S14EventConditionResult result = S14EventConditionEvaluator.Evaluate(program.DispatchProgram, this);
        return new(result, Array.AsReadOnly(_areas.ToArray()));
    }

    public int InvokeLeaf(S14ConditionContext context)
    {
        IReadOnlyList<int> arguments = context.Operation.RawArguments;
        try
        {
            if (arguments.Count != 8) throw new InvalidDataException("Execution operands are incomplete.");
            switch (context.Operation.Code)
            {
                // This slot0 is exactly mov eax,1/ret. Still presentation is
                // handled by a separate native action path, not this leaf.
                case 90: return 1;
                case 91:
                case 92: return S14AudioEventExecution.Execute(context.Operation.Code, arguments[0], _audio!);
                case 188: _areas.Clear(); return 1;
                case 189:
                    foreach (int selector in arguments)
                    {
                        S14EventExecutionArea? area = Lookup(_state.ResolveAreaSelector(selector), "area selector");
                        if (area is not null && Known(_state.IsAreaValid(area), "area validity")) Add(area);
                    }
                    return 1;
                case 190:
                    S14EventExecutionForce? force = Lookup(_state.ResolveForceSelector(arguments[0]), "force selector");
                    if (force is null || !Known(_state.IsForceValid(force), "force validity")) return 1;
                    foreach (S14EventExecutionArea area in Areas(_state.ReadRegisteredAreasInNativeOrder(), "registered area list"))
                        if (Lookup(_state.ReadAreaCurrentForce(area), "current area force") == force) Add(area);
                    return 1;
                case 191:
                    foreach (int selector in arguments)
                    {
                        S14EventExecutionPerson? person = Lookup(_state.ResolvePersonSelector(selector), "person selector");
                        if (person is null || !Known(_state.IsPersonValid(person), "person validity")) continue;
                        S14EventExecutionForce? owner = Lookup(_state.ReadPersonCurrentForce(person), "person current force");
                        if (owner is null || !Known(_state.IsForceValid(owner), "person force validity")) continue;
                        foreach (S14EventExecutionArea area in Areas(_state.ReadForceAreasInNativeOrder(owner), "force area list"))
                            if (Lookup(_state.ReadAreaCurrentOfficer(area), "current area officer") == person) Add(area);
                    }
                    return 1;
                case 192:
                    var cities = new List<S14EventExecutionCity>();
                    foreach (int selector in arguments)
                    {
                        S14EventExecutionCity? city = Lookup(_state.ResolveCitySelector(selector), "city selector");
                        if (city is not null && Known(_state.IsCityValid(city), "city validity")) cities.Add(city);
                    }
                    if (cities.Count != 0)
                    {
                        var kept = new List<S14EventExecutionArea>();
                        foreach (S14EventExecutionArea area in _areas)
                            if (cities.Contains(Lookup(_state.ReadAreaCurrentCity(area), "current area city")!)) kept.Add(area);
                        _areas.Clear(); _areas.AddRange(kept);
                    }
                    return _areas.Count != 0 ? 1 : 0;
                case 193:
                case 194:
                    foreach (S14EventExecutionArea area in _areas)
                    {
                        int value = arguments[0];
                        if (context.Operation.Code == 194)
                        {
                            int current = Known(_state.ReadAreaSecurity(area), "current area security");
                            value = arguments[0] switch
                            {
                                1 => arguments[1], 2 => unchecked(current + arguments[1]),
                                3 => unchecked(current - arguments[1]), _ => current
                            };
                            value = Math.Clamp(value, 0, 100);
                        }
                        int forceSlot = Known(_state.ReadAreaCurrentForcePhysicalSlot(area), "current area force physical slot");
                        if (unchecked((uint)(forceSlot - 46)) <= 4u) continue;
                        _state.WriteAreaSecurity(area, checked((byte)Math.Clamp(value, 0, 100)), context.Operation.Index);
                    }
                    return 1;
                case >= 195 and <= 200:
                    S14EventAreaDevelopmentKind kind = context.Operation.Code is 195 or 196
                        ? S14EventAreaDevelopmentKind.Agriculture : context.Operation.Code is 197 or 198
                            ? S14EventAreaDevelopmentKind.Commerce : S14EventAreaDevelopmentKind.Barracks;
                    bool change = context.Operation.Code % 2 == 0;
                    foreach (S14EventExecutionArea area in _areas)
                    {
                        int input;
                        if (!change) input = Math.Max(arguments[0], 1);
                        else
                        {
                            int current = Known(_state.ReadAreaDevelopment(area, kind), "current area development");
                            int raw = arguments[0] switch
                            {
                                1 => arguments[1], 2 => unchecked(current + arguments[1]),
                                3 => unchecked(current - arguments[1]), _ => current
                            };
                            input = Math.Clamp(raw, 0, 1000);
                        }
                        _state.WriteAreaDevelopment(area, kind, input, true, context.Operation.Index);
                    }
                    return 1;
                default: throw new InvalidDataException("Execution leaf has no supported handler.");
            }
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Execution stream {context.Program.StreamIndex}, operation {context.Operation.Index}, " +
                $"offset {context.Operation.Offset}: {error.Message}", error);
        }
    }

    private void Add(S14EventExecutionArea area) { if (!_areas.Contains(area)) _areas.Add(area); }
    private static T Known<T>(T? value, string label) where T : struct =>
        value ?? throw new InvalidDataException($"Current {label} is unknown.");
    private static T? Lookup<T>(S14EventExecutionLookup<T>? lookup, string label) where T : class =>
        lookup is not null ? lookup.Value : throw new InvalidDataException($"Current {label} is unknown.");
    private static IReadOnlyList<S14EventExecutionArea> Areas(IReadOnlyList<S14EventExecutionArea>? areas, string label)
    {
        if (areas is null || areas.Any(area => area is null))
            throw new InvalidDataException($"Current {label} is unknown or invalid.");
        return areas;
    }
}
