using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit current dependencies for one complete story. Readers are
/// detached world queries, never callbacks into playback or the original process.</summary>
public sealed record CampaignEventExecutionInputs(
    IS14PlayerEventConditionQueries Persons,
    Func<S14EventConditionPersonReference, int, int?> ReadCurrentAbility,
    Func<SceneryReference, ushort?> ReadSceneryHex,
    Func<SceneryReference, string?> ReadSceneryName,
    bool? SkipCamera)
{
    public IReadOnlyList<int>? InitialPersonParameters { get; init; }
    public IReadOnlyList<string>? InitialNameParameters { get; init; }
}

/// <summary>Invocation-local candidate list and selected actors. Clearing the
/// candidates does not clear the actors. No mutation escapes a failed plan.</summary>
internal sealed class CampaignEventExecutionContext(CampaignEventExecutionInputs inputs)
{
    private readonly List<S14EventConditionPersonReference> _candidates = [];
    private readonly Dictionary<int, S14EventConditionPersonReference> _actors = [];
    public IReadOnlyList<S14EventConditionPersonReference> Candidates => _candidates.AsReadOnly();
    public IReadOnlyDictionary<int, S14EventConditionPersonReference> Actors => _actors;
    private IS14PlayerEventConditionQueries Queries => inputs.Persons;

    public S14EventConditionPersonReference ResolvePerson(int selector)
    {
        S14EventConditionPersonPointer? pointer;
        if (selector is >= -14 and <= -10)
            pointer = new(_actors.GetValueOrDefault(selector)); // constructor reset is an observed null actor
        else if (selector == -4) pointer = Queries.ReadConditionCurrentPlayerPerson();
        else if (selector < 0) throw new InvalidDataException($"Story person selector {selector} has no current producer.");
        else pointer = selector <= 6000 ? Queries.ReadConditionPersonTableSlot(selector) : Queries.ReadConditionBasePerson();
        if (pointer is null) throw Missing("person pointer");
        return pointer.Value ?? (Queries.ReadConditionPersonTableSlot(0) ?? throw Missing("person-zero pointer")).Value
            ?? throw new InvalidDataException("Current person-zero pointer is null.");
    }

    public void AddForcePersons(IReadOnlyList<int> arguments)
    {
        foreach (int raw in arguments)
        {
            var force = ResolveForce(raw);
            if (force is null || !Known(Queries.ReadConditionForceValidity(force), "force validity")) continue;
            for (int mode = 0; mode <= 1; mode++)
            {
                //2349D0/334810 iterate the registered person chain without an
                //extra Person validity gate. Both passes preserve that order.
                var chain = Queries.ReadRegisteredConditionPersonPhysicalSlots() ?? throw Missing("registered persons");
                foreach (int? physical in chain)
                {
                    var person = new S14EventConditionPersonReference(physical ?? throw Missing("registered person pointer"));
                    if (force.PhysicalSlot is < 1 or > 51) continue;
                    if (Known(Queries.ReadConditionPersonRawForceSlot(person), "person force byte") != force.PhysicalSlot) continue;
                    byte status = Known(Queries.ReadConditionPersonStatus(person), "person iterator status");
                    if ((mode == 0 ? status is >= 1 and <= 4 : status == 6) && !_candidates.Contains(person))
                        _candidates.Add(person);
                }
            }
        }
    }

    public bool NarrowPersons(IReadOnlyList<int> arguments)
    {
        var wanted = new bool[10];
        foreach (int value in arguments) if ((uint)value < 10) wanted[value] = true;
        _candidates.RemoveAll(person =>
        {
            byte status = Known(Queries.ReadConditionPersonStatus(person), "filter person status");
            return status >= 10 || !wanted[status];
        });
        return _candidates.Count != 0;
    }

    public void SelectSpeakers(IReadOnlyList<int> arguments)
    {
        int key = arguments[6];
        if ((uint)(key - 1) >= 6) return;
        var sorted = new List<S14EventConditionPersonReference>();
        foreach (var person in _candidates)
        {
            if (!Known(Queries.ReadConditionPersonValidity(person), "speaker candidate validity")) continue;
            int id = Known(Queries.ReadConditionPersonCharacterId(person), "speaker candidate identity");
            if (Special(id) || (Known(Queries.ReadConditionPersonFlags(person), "speaker candidate flags") & 0x4000) != 0) continue;
            sorted.Add(person);
        }
        //3D22E0 compares only genuine pairs. A single candidate needs no key;
        //strict insertion keeps equal values in registration order.
        for (int index = 1; index < sorted.Count; index++)
        {
            var value = sorted[index]; int prior = index - 1;
            while (prior >= 0 && ReadKey(sorted[prior]) < ReadKey(value))
            { sorted[prior + 1] = sorted[prior]; prior--; }
            sorted[prior + 1] = value;
        }
        int candidate = 0;
        for (int index = 0; index < 5 && candidate < sorted.Count; index++)
            if ((uint)(arguments[index] - 1) < 5)
                _actors[arguments[index] - 15] = sorted[candidate++];

        int ReadKey(S14EventConditionPersonReference person) => key == 6
            ? Known(Queries.ReadConditionPersonRawLoyalty(person), "current loyalty sort key")
            : inputs.ReadCurrentAbility(person, key - 1) ?? throw Missing("current effective ability sort key");
    }

    public void ClearCandidates() => _candidates.Clear();

    public SceneryReference ResolveScenery(int selector)
    {
        if (selector < 0) throw new InvalidDataException("Negative scenery index has no safe resource-table profile.");
        var value = Queries.ReadConditionSceneryTableSlot(selector < 101 ? selector : 0) ?? throw Missing("scenery pointer");
        if (!Known(Queries.ReadConditionSceneryValidity(value), "scenery validity"))
            value = Queries.ReadConditionSceneryTableSlot(0) ?? throw Missing("scenery-zero pointer");
        return value;
    }

    public ushort SceneryHex(SceneryReference scenery) => inputs.ReadSceneryHex(scenery) ?? throw Missing("scenery hex");
    public string SceneryName(SceneryReference scenery) => !Known(Queries.ReadConditionSceneryValidity(scenery), "name scenery validity")
        ? string.Empty : inputs.ReadSceneryName(scenery) ?? throw Missing("scenery name");
    public bool SkipCamera => Known(inputs.SkipCamera, "camera execution mode");

    private S14EventConditionForceReference? ResolveForce(int selector)
    {
        S14EventConditionForcePointer? force;
        if (selector == -1) force = Queries.ReadConditionCurrentPlayerForce();
        else if (selector < 0) throw new InvalidDataException($"Story force selector {selector} has no current producer.");
        else
        {
            var person = ResolvePerson(selector);
            force = Known(Queries.ReadConditionPersonValidity(person), "force selector person validity") &&
                Known(Queries.ReadConditionPersonStatus(person), "force selector person status") == 1
                ? Queries.ReadConditionPersonForce(person) : new(null);
        }
        if (force is null) throw Missing("selected force pointer");
        return force.Value ?? (Queries.ReadConditionForceTableSlot(0) ?? throw Missing("force-zero pointer")).Value;
    }

    private static bool Special(int id) => (uint)(id - 4111) <= 9 || (uint)(id - 4131) <= 9 ||
        (uint)(id - 4151) <= 9 || (uint)(id - 4171) <= 9 || (uint)(id - 4191) <= 9;
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new($"Current story {owner} is unknown.");
}
