using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    public bool PersonListBlocked { get; private set; }

    public void AddPersons(IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        //216 consumes only the first five operands. Unlike Force collection,
        //each valid occurrence is appended, including duplicate pointers.
        foreach (int selector in arguments.Take(5))
        {
            var person = ResolvePerson(selector, readTargetForce);
            if (Known(Queries.ReadConditionPersonValidity(person), "added Person validity"))
                _candidates.Add(person);
        }
        //A real presentation execution owner returns here. It neither marks
        //an all-dead list blocked nor recursively evaluates through257.
    }

    public void RemovePersons(IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        foreach (int selector in arguments)
        {
            var person = ResolvePerson(selector, readTargetForce);
            if (Known(Queries.ReadConditionPersonValidity(person), "removed Person validity"))
                _candidates.RemoveAll(candidate => candidate == person);
        }
    }

    public bool NarrowPersonsByForce(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var target = ResolveForce(selector, readTargetForce);
        if (target is not null && Known(Queries.ReadConditionForceValidity(target), "list Force validity"))
            _candidates.RemoveAll(person =>
                (Queries.ReadConditionPersonForce(person) ?? throw Missing("list Person Force pointer")).Value != target);
        //An invalid target leaves the list intact; its current emptiness is
        //still the handler's returned predicate.
        return _candidates.Count != 0;
    }

    public bool NarrowPersonsByHome(int selector)
    {
        var target = ResolveBaseSelector(selector);
        if (Known(Queries.ReadConditionBaseValidity(target), "list Home Base validity"))
            _candidates.RemoveAll(person =>
                (Queries.ReadConditionPersonHomeBase(person) ?? throw Missing("list Person Home Base")) != target);
        return _candidates.Count != 0;
    }

    public bool NarrowPersonsByHumble(bool wanted)
    {
        //229 marks the shared invocation blocked before filtering. This test
        //is independent of group depth and also applies to presentation mode.
        if (!_candidates.Any(person =>
            (Known(Queries.ReadConditionPersonFlags(person), "list Person flags") & 0x80) == 0 &&
            Known(Queries.ReadConditionPersonStatus(person), "list Person status") != 9))
            PersonListBlocked = true;
        _candidates.RemoveAll(person =>
            Known(Queries.ReadConditionPersonHumble(person), "list Person humble state") != wanted);
        return _candidates.Count != 0;
    }

    public bool NarrowPersonsByCanJoinEvent(bool wanted)
    {
        _candidates.RemoveAll(person =>
            (Known(Queries.ReadConditionPersonStatus(person), "list Person status") is not (6 or 7 or 9)) != wanted);
        return _candidates.Count != 0;
    }
}
