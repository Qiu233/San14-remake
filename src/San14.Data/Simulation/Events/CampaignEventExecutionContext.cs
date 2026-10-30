using San14.Data.Formats;
using San14.Core;

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
    public SourceWorldContext? AreaListCurrent { get; init; }
    public Func<int, int, int?>? ReadStoryPersonRelationWord { get; init; }
    public Func<int, int, bool?>? ReadStoryPersonKinship { get; init; }
    public Func<int, int>? SampleStoryInvocationSourceSelection { get; init; }
    public Func<int, int?>? ReadPersonDoctrineValue { get; init; }
    public Func<bool?>? ReadReturnToTitlePending { get; init; }
    public Func<CampaignStoryMessageContext,IReadOnlyList<int>,IReadOnlyList<string>>? ResolveMessageStringParameters { get; init; }
    public Func<int, int, int?>? ReadForceFriendshipValue { get; init; }
    public bool? ForcedIfResult { get; init; }
    public Func<CampaignNarrationExperienceBonus>? ReadNarrationExperienceBonus { get; init; }
    public CampaignEventDialogueCast? InitialDialogueCast { get; init; }
    public Func<S14EventConditionPersonReference, ushort?>? ReadServiceYear { get; init; }
    public IReadOnlyList<int>? InitialPersonParameters { get; init; }
    public IReadOnlyList<int?>? InitialMessagePeople { get; init; }
    public IReadOnlyList<int>? InitialCityParameters { get; init; }
    public IReadOnlyList<int>? InitialForceParameters { get; init; }
    public IReadOnlyList<int>? InitialAreaParameters { get; init; }
    public IReadOnlyList<int>? InitialItemParameters { get; init; }
    public IReadOnlyList<int?>? InitialNumberParameters { get; init; }
    public IReadOnlyList<int?>? InitialProvinceParameters { get; init; }
    public Func<S14EventConditionPersonReference, int?>? ReadCurrentPersonAge { get; init; }
    public Func<int, string?>? ReadDoctrineMessageName { get; init; }
    public Func<int, string?>? ReadMonthMessageName { get; init; }
    public IReadOnlyList<string>? InitialNameParameters { get; init; }
    public Func<BaseReference, ushort?>? ReadCameraBaseHex { get; init; }
}

/// <summary>Invocation-local candidate list and selected actors. Clearing the
/// candidates does not clear the actors. No mutation escapes a failed plan.</summary>
internal sealed partial class CampaignEventExecutionContext(CampaignEventExecutionInputs inputs)
{
    private readonly List<S14EventConditionPersonReference> _candidates = [];
    private readonly Dictionary<int, S14EventConditionPersonReference> _actors = [];
    public IReadOnlyList<S14EventConditionPersonReference> Candidates => _candidates.AsReadOnly();
    public IReadOnlyDictionary<int, S14EventConditionPersonReference> Actors => _actors;
    private IS14PlayerEventConditionQueries Queries => inputs.Persons;
    internal int? ReadCurrentStoryPersonRelationWord(int person, int field) =>
        (inputs.ReadStoryPersonRelationWord ?? throw Missing("current relationship word reader"))(person, field);
    internal bool? ReadCurrentStoryPersonKinship(int first, int second) =>
        (inputs.ReadStoryPersonKinship ?? throw Missing("current kinship reader"))(first, second);
    internal int? ReadStoryPersonDoctrineValue(int physical) =>
        (inputs.ReadPersonDoctrineValue ?? throw Missing("current person Doctrine reader"))(physical);

    public void AddForcePersons(IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        foreach (int raw in arguments)
        {
            var force = ResolveForce(raw, readTargetForce);
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

    public int CityMessageParameter(int selector)
    {
        var receiver = ResolveBaseSelector(selector);
        return Known(Queries.ReadConditionBaseValidity(receiver), "city parameter validity")
            ? Known(Queries.ReadConditionBaseObjectId(receiver), "city parameter object identity") : 0;
    }

    private static bool Special(int id) => (uint)(id - 4111) <= 9 || (uint)(id - 4131) <= 9 ||
        (uint)(id - 4151) <= 9 || (uint)(id - 4171) <= 9 || (uint)(id - 4191) <= 9;
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new($"Current story {owner} is unknown.");
}
