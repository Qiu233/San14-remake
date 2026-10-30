using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int SampleStoryInvocationSourceSelection(int bound) =>
        (inputs.SampleStoryInvocationSourceSelection ?? throw Missing("speaker date sampler owner"))(bound);

    /// <summary>The ordinary invocation's affiliation and first-speaker
    /// producers. They write only the existing target/cast banks, at this
    /// reached leaf, without clearing unrelated selections.</summary>
    internal int ExecuteStoryInvocationSources(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce,
        Action<int, S14EventConditionForceReference?> storeTargetForce,
        Func<int, int> sampleCurrentDate)
    {
        switch (code)
        {
            case 132:
            {
                var person = ResolvePerson(arguments[0], readTargetForce);
                if (!Known(Queries.ReadConditionPersonValidity(person), "affiliation source Person validity")) return 1;
                var force = (Queries.ReadConditionPersonForce(person) ?? throw Missing("affiliation source Person Force")).Value;
                if (force is not null && Known(Queries.ReadConditionForceValidity(force), "affiliation source Force validity"))
                    storeTargetForce(arguments[1], force);
                return 1;
            }
            case 441:
            {
                var home = ResolveBaseSelector(arguments[0]);
                if (!Known(Queries.ReadConditionBaseValidity(home), "affiliation source Base validity")) return 1;
                var force = (Queries.ReadConditionBaseForce(home) ?? throw Missing("affiliation source Base Force")).Value;
                if (force is not null && Known(Queries.ReadConditionForceValidity(force), "affiliation source Base Force validity"))
                    storeTargetForce(arguments[1], force);
                return 1;
            }
            case 135:
            {
                var force = ResolveForce(arguments[0], readTargetForce);
                if (force is null || !Known(Queries.ReadConditionForceValidity(force), "preferred speaker Force validity")) return 1;
                S14EventConditionPersonReference? selected = null;
                for (int index = 1; index <= 6; index++)
                {
                    var person = ResolvePerson(arguments[index], readTargetForce);
                    if (!Known(Queries.ReadConditionPersonValidity(person), "preferred speaker Person validity")) continue;
                    if (Special(Known(Queries.ReadConditionPersonCharacterId(person), "preferred speaker Person identity"))) continue;
                    var owner = (Queries.ReadConditionPersonForce(person) ?? throw Missing("preferred speaker Person Force")).Value;
                    if (owner != force) continue;
                    selected = person;
                    break;
                }
                if (selected is null && arguments[7] is >= 1 and <= 6)
                {
                    int best = 0;
                    foreach (var person in InvocationSourceForcePeople(force))
                    {
                        if (Known(Queries.ReadConditionPersonStatus(person), "fallback speaker status") == 1 ||
                            InvocationSourceIsAdviser(person)) continue;
                        int value = arguments[7] == 6
                            ? Known(Queries.ReadConditionPersonRawLoyalty(person), "fallback speaker loyalty")
                            : inputs.ReadCurrentAbility(person, arguments[7] - 1) ?? throw Missing("fallback speaker effective ability");
                        //3E0810 keeps the first strict improvement over zero.
                        //It has no ID tie-break or extra native-validity gate.
                        if (value <= best) continue;
                        selected = person;
                        best = value;
                    }
                }
                if (selected is not null && Known(Queries.ReadConditionPersonValidity(selected), "selected first speaker validity"))
                    _actors[-14] = selected;
                return 1;
            }
            case 167:
            case 169:
            {
                var force = (Queries.ReadConditionCurrentPlayerForce() ?? throw Missing("health speaker player Force")).Value;
                if (force is null || !Known(Queries.ReadConditionForceValidity(force), "health speaker Force validity")) return 1;
                var current = inputs.AreaListCurrent ?? throw Missing("health speaker current owner");
                var candidates = new List<S14EventConditionPersonReference>();
                foreach (var person in InvocationSourceForcePeople(force))
                {
                    bool accepted = code == 167
                        ? Known(OfficerHealthSourceAdapter.ReadByte(current, person.PhysicalSlot), "speaker current health") != 0
                        : Known(current.ReadConditionPersonAtTerminalYear(person.PhysicalSlot), "speaker current terminal year");
                    if (accepted) candidates.Add(person);
                }
                if (candidates.Count == 0) return 1;
                var selected = candidates[candidates.Count == 1 ? 0 : sampleCurrentDate(candidates.Count)];
                if (Known(Queries.ReadConditionPersonValidity(selected), "health first speaker validity"))
                    _actors[-14] = selected;
                return 1;
            }
            default: throw Missing("invocation source opcode");
        }
    }

    private IEnumerable<S14EventConditionPersonReference> InvocationSourceForcePeople(S14EventConditionForceReference force)
    {
        if (force.PhysicalSlot is < 1 or > 51) yield break;
        foreach (int? entry in Queries.ReadRegisteredConditionPersonPhysicalSlots() ?? throw Missing("source Force Person registry"))
        {
            var person = new S14EventConditionPersonReference(entry ?? throw Missing("source Force Person payload"));
            if (Known(Queries.ReadConditionPersonRawForceSlot(person), "source Force Person membership") != force.PhysicalSlot) continue;
            byte status = Known(Queries.ReadConditionPersonStatus(person), "source Force Person status");
            if (status is >= 1 and <= 4) yield return person;
        }
    }

    private bool InvocationSourceIsAdviser(S14EventConditionPersonReference person)
    {
        var force = (Queries.ReadConditionPersonForce(person) ?? throw Missing("speaker adviser Person Force")).Value;
        if (force is null || !Known(Queries.ReadConditionForceValidity(force), "speaker adviser Force validity")) return false;
        var adviser = ReadPersonPointer(Known(Queries.ReadConditionForceAdvisorId(force), "speaker adviser identity"));
        return adviser is not null && Known(Queries.ReadConditionPersonValidity(adviser), "speaker adviser validity") &&
            Known(Queries.ReadConditionPersonCharacterId(adviser), "speaker adviser current identity") ==
            Known(Queries.ReadConditionPersonCharacterId(person), "speaker candidate current identity");
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>3E0E40's date-salted choice, using the pure source sampler.
    /// This neither reads nor advances the mutable App random owner.</summary>
    internal int ReadStoryInvocationSourceSelectionCurrent(GameState state, SourceWorldContext current, int bound)
    {
        if (bound < 2) return 0;
        var date = current.ReadConditionDate() ?? throw new InvalidDataException("story.invocation_source.current_date");
        var world = ReadArmyDestructionSampler(state, current)
            ?? throw new InvalidDataException("story.invocation_source.World450_45C");
        return TacticEffectSourceSamplerMath.Sample(bound, 1, unchecked((short)date.Year),
            unchecked((sbyte)date.Month), unchecked((sbyte)date.Day), unchecked((sbyte)date.Hour), world);
    }
}
