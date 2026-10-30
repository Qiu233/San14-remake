using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    /// <summary>Assign one explicit speaker without clearing the other four
    /// actors, automatic dialogue roles or invocation-local candidate list.</summary>
    public void SetSpeaker(int personSelector, int slot,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var person = ResolvePerson(personSelector, readTargetForce);
        if (Known(Queries.ReadConditionPersonValidity(person), "explicit speaker validity") &&
            (uint)(slot - 1) < 5)
            _actors[slot - 15] = person;
    }

    /// <summary>Collect registered Footholds belonging to this Force, then
    /// each Foothold's serving people and captives, in their original order.</summary>
    public void AddAllForcePersons(int forceSelector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var force = ResolveForce(forceSelector, readTargetForce);
        if (force is null || force.PhysicalSlot is < 1 or > 51) return;

        //This instruction does not test the Force's native validity. The
        //Force-Foothold iterator only tests its ordinal and each Base owner.
        var current = StoryPersonListCurrent;
        var bases = current.ReadRegisteredBases() ?? throw Missing("person list registered Footholds");
        var people = new Lazy<IReadOnlyList<int?>>(ReadPersonListRegistry);
        foreach (var entry in bases)
        {
            var foothold = entry ?? throw Missing("person list registered Foothold pointer");
            if (Known(current.ReadBaseForceSlot(foothold), "person list Foothold Force") != force.PhysicalSlot) continue;
            if (!Known(current.ReadBaseNativeValidity(foothold), "person list Foothold validity")) continue;
            AppendFootholdPersonPass(foothold, captives: false, people);
            AppendFootholdPersonPass(foothold, captives: true, people);
        }
    }

    /// <summary>Remove every occurrence whose current Force pointer is the
    /// resolved target; retain all other occurrences in their original order.</summary>
    public void RemoveForcePersons(int forceSelector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var force = ResolveForce(forceSelector, readTargetForce);
        if (force is null || !Known(Queries.ReadConditionForceValidity(force), "removed list Force validity")) return;
        if (_candidates.Count == 0) return;
        var current = StoryPersonListCurrent;
        //The original predicate reads22CB40 even for an invalid Person.
        //Adding a Person validity filter would retain entries it removes.
        _candidates.RemoveAll(person =>
        {
            var actual = current.ReadPersonForce(person.PhysicalSlot)
                ?? throw Missing("removed list Person District Force pointer");
            return Known(current.ReadForcePhysicalSlot(actual), "removed list Person Force receiver") == force.PhysicalSlot;
        });
    }

    /// <summary>Append serving people from one actual Foothold. This opcode
    /// has no second captive pass and adds no implicit Force restriction.</summary>
    public void AddFootholdPersons(int baseSelector)
    {
        var foothold = ResolveBaseSelector(baseSelector);
        if (!Known(Queries.ReadConditionBaseValidity(foothold), "added list Foothold validity")) return;
        AppendFootholdPersonPass(foothold, captives: false, new Lazy<IReadOnlyList<int?>>(ReadPersonListRegistry));
    }

    private SourceWorldContext StoryPersonListCurrent => inputs.AreaListCurrent
        ?? throw Missing("person list current owner");

    private IReadOnlyList<int?> ReadPersonListRegistry() =>
        Queries.ReadRegisteredConditionPersonPhysicalSlots() ?? throw Missing("person list registered Persons");

    private void AppendFootholdPersonPass(BaseReference foothold, bool captives,
        Lazy<IReadOnlyList<int?>> registeredPeople)
    {
        var current = StoryPersonListCurrent;
        //CFootholdPersonIterator captures Base4E and compares the actual raw
        //PersonE2 word. It does not map a Gate to its owning City, consult
        //PersonE4 or derive ownership from a domain residence collection.
        ushort location = Known(current.ReadBaseObjectLink(foothold), "person list Foothold location");
        if (location is < 1 or > 61) return;
        foreach (int? entry in registeredPeople.Value)
        {
            int physical = entry ?? throw Missing("person list registered Person pointer");
            if (Known(current.ReadPersonFirstLocation(physical), "person list Person Home E2") != location) continue;
            var person = new S14EventConditionPersonReference(physical);
            byte status = Known(Queries.ReadConditionPersonStatus(person), "person list iterator status");
            if (captives ? status != 6 : status is < 1 or > 4) continue;
            //Iterator status/Home tests precede the handler's explicit native
            //validity gate. Deduplication is by pointer/physical receiver,
            //against the entire existing list, preserving prior duplicates.
            if (Known(Queries.ReadConditionPersonValidity(person), "person list added Person validity") &&
                !_candidates.Contains(person))
                _candidates.Add(person);
        }
    }
}
