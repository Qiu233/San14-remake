using System.Collections.Immutable;
using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    //The third invocation-local ptr_list is independent of the thirteen
    //condition target slots, the Person list and message Force parameters.
    private readonly List<int> _storyForcePhysicalSlots = [];
    private bool _storyForcesKnown = true;

    internal IReadOnlyList<int>? ReadStoryForceSelections() => _storyForcesKnown
        ? _storyForcePhysicalSlots.AsReadOnly() : null;

    internal ImmutableArray<int>? CaptureStoryForceSelections() => _storyForcesKnown
        ? _storyForcePhysicalSlots.ToImmutableArray() : null;

    internal void RestoreStoryForceSelections(ImmutableArray<int>? slots)
    {
        _storyForcePhysicalSlots.Clear();
        _storyForcesKnown = slots.HasValue;
        if (slots is {} saved) _storyForcePhysicalSlots.AddRange(saved);
    }

    internal void ResetStoryForceSelections()
    {
        _storyForcePhysicalSlots.Clear();
        _storyForcesKnown = true;
    }

    internal int ExecuteStoryForceList(S14ConditionInstruction operation,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var a = operation.RawArguments;
        if (a.Count != 8) throw Missing("Force list arguments");
        if (operation.Code == 173) { ResetStoryForceSelections(); return 1; }
        RequireStoryForces();
        switch (operation.Code)
        {
            case 174:
                foreach (int selector in a)
                {
                    var force = ResolveForce(selector, readTargetForce);
                    if (force is not null && StoryForceValid(force)) AppendStoryForce(force);
                }
                return 1;
            case 175:
                AddCommonStoryForce(a, fromPeople: false, readTargetForce);
                return 1;
            case 176:
                AddCommonStoryForce(a, fromPeople: true, readTargetForce);
                return 1;
            case 177:
            {
                var selected = ResolvePerson(a[0], readTargetForce);
                if (!Known(Queries.ReadConditionPersonValidity(selected), "Force blood selector validity")) return 1;
                //The native comparison is ruler.B2 == selected.Person10,
                //not ruler.B2 == selected.B2 despite this opcode's name.
                ushort identity = Known(Queries.ReadConditionPersonCharacterId(selected), "Force blood selector identity");
                foreach (var force in Queries.ReadRegisteredConditionForcesInNativeOrder()
                    ?? throw Missing("Force list registered Forces"))
                {
                    var ruler = PersonLookup(Known(Queries.ReadConditionForceRulerPersonId(force), "Force list ruler identity"));
                    if (Known(Queries.ReadConditionPersonValidity(ruler), "Force list ruler validity") &&
                        Known(Queries.ReadConditionPersonBloodlineId(ruler), "Force list ruler bloodline") == identity)
                        AppendStoryForce(force); //There is no additional Force validity gate.
                }
                return 1;
            }
            case 178:
                foreach (var person in _candidates)
                {
                    //This producer reads every saved Person pointer, including
                    //ones that are no longer native-valid.
                    var force = StoryForceOfPerson(person);
                    if (StoryForceValid(force)) AppendStoryForce(force);
                }
                return 1;
            case 179:
                foreach (int selector in a)
                {
                    var ruler = ResolvePerson(selector, readTargetForce);
                    if (!Known(Queries.ReadConditionPersonValidity(ruler), "removed ruler validity") ||
                        Known(Queries.ReadConditionPersonStatus(ruler), "removed ruler status") != 1) continue;
                    int physical = StoryForceOfPerson(ruler).PhysicalSlot;
                    _storyForcePhysicalSlots.RemoveAll(value => value == physical);
                }
                return 1;
            case 180:
            {
                var target = ResolveForce(a[0], readTargetForce);
                if (target is not null && StoryForceValid(target))
                    _storyForcePhysicalSlots.RemoveAll(physical => !CompareStoryForceValue(
                        inputs.ReadForceFriendshipValue?.Invoke(physical, target.PhysicalSlot)
                            ?? throw Missing("Force list raw friendship value"), a[1], a[2]));
                return StoryForceListNonempty();
            }
            case 181:
            {
                var target = ResolveForce(a[0], readTargetForce);
                if (target is not null && StoryForceValid(target))
                    _storyForcePhysicalSlots.RemoveAll(physical =>
                        ((Known(Queries.ReadConditionForceAllianceByte(new(physical), target.PhysicalSlot),
                            "Force list alliance byte") & 127) != 0) != (a[1] != 0));
                return StoryForceListNonempty();
            }
            case 182:
                if ((uint)(a[1] - 1) < 12)
                    _storyForcePhysicalSlots.RemoveAll(physical => !CompareStoryForceValue(
                        Known(Queries.ReadConditionForceRank(new(physical)), "Force list rank47"), a[0], a[1]));
                return StoryForceListNonempty();
            case 183:
            {
                int? emperorForce = StoryEmperorHomeForce();
                bool wanted = a[0] != 0;
                _storyForcePhysicalSlots.RemoveAll(physical => (physical == emperorForce) != wanted);
                return StoryForceListNonempty();
            }
            default: throw Missing($"Force list handler {operation.Code}");
        }
    }

    private void RequireStoryForces()
    {
        if (!_storyForcesKnown) throw Missing("saved Force list");
    }

    private int StoryForceListNonempty() => _storyForcePhysicalSlots.Count != 0 ? 1 : 0;

    private bool StoryForceValid(S14EventConditionForceReference force) =>
        Known(Queries.ReadConditionForceValidity(force), "Force list Force validity");

    private void AppendStoryForce(S14EventConditionForceReference force)
    {
        int physical = force.PhysicalSlot;
        if ((uint)physical > 51) throw Missing("Force list pointer identity");
        if (!_storyForcePhysicalSlots.Contains(physical)) _storyForcePhysicalSlots.Add(physical);
    }

    private S14EventConditionForceReference StoryForceOfPerson(S14EventConditionPersonReference person)
    {
        var force = StoryForceCurrent.ReadPersonForce(person.PhysicalSlot)
            ?? throw Missing("Force list Person District Force pointer");
        return new(Known(StoryForceCurrent.ReadForcePhysicalSlot(force), "Force list Person Force identity"));
    }

    private S14EventConditionForceReference StoryForceOfBase(BaseReference foothold)
    {
        int raw = Known(StoryForceCurrent.ReadBaseForceSlot(foothold), "Force list Base Force ordinal");
        var force = StoryForceCurrent.ReadForceTableReference(raw < 52 ? raw : 0)
            ?? throw Missing("Force list Base Force pointer");
        return new(Known(StoryForceCurrent.ReadForcePhysicalSlot(force), "Force list Base Force identity"));
    }

    private void AddCommonStoryForce(IReadOnlyList<int> selectors, bool fromPeople,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        S14EventConditionForceReference? common = null;
        foreach (int selector in selectors)
        {
            S14EventConditionForceReference force;
            if (fromPeople)
            {
                var person = ResolvePerson(selector, readTargetForce);
                if (!Known(Queries.ReadConditionPersonValidity(person), "common Force Person validity")) continue;
                force = StoryForceOfPerson(person);
            }
            else
            {
                var foothold = ResolveBaseSelector(selector);
                if (!Known(Queries.ReadConditionBaseValidity(foothold), "common Force Base validity")) continue;
                force = StoryForceOfBase(foothold);
            }
            if (!StoryForceValid(force)) continue;
            if (common is not null && common != force) return;
            common = force;
        }
        if (common is not null) AppendStoryForce(common);
    }

    private int? StoryEmperorHomeForce()
    {
        ushort id = Known(Queries.ReadConditionEmperorPersonId(), "Force list emperor identity");
        if (id is < 1 or > 6000) return null;
        var emperor = PersonLookup(id);
        if (!Known(Queries.ReadConditionPersonValidity(emperor), "Force list emperor validity")) return null;
        ushort home = Known(StoryForceCurrent.ReadPersonFirstLocation(emperor.PhysicalSlot), "Force list emperor Home E2");
        var foothold = BaseLookup.Find(home, StoryForceCurrent);
        if (!Known(StoryForceCurrent.ReadBaseNativeValidity(foothold), "Force list emperor Home validity")) return null;
        return StoryForceOfBase(foothold).PhysicalSlot;
    }

    private SourceWorldContext StoryForceCurrent => inputs.AreaListCurrent
        ?? throw Missing("Force list current owner");

    private static bool CompareStoryForceValue(int value, int mode, int threshold) => mode switch
    {
        1 => value != threshold, 2 => value == threshold, 3 => value > threshold,
        4 => value >= threshold, 5 => value < threshold, 6 => value <= threshold, _ => false
    };
}
