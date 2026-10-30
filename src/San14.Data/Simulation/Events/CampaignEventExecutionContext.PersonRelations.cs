using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    public int ExecutePersonRelationList(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        switch (code)
        {
            case 218: AddBloodRelatedPersons(); return 1;
            case 219: AddPersonsByRelation(arguments[0], true, readTargetForce); return 1;
            case 220: AddPersonsByRelation(arguments[0], false, readTargetForce); return 1;
            case 221: AddRulers(); return 1;
            case 222: AddAdvisers(arguments, readTargetForce); return 1;
            case 224: return RemovePersonsByHome(arguments[0]) ? 1 : 0;
            case 228: return NarrowPersonsByLocation(arguments[0]) ? 1 : 0;
            case 230: return NarrowPersonsByImperialCourt(arguments[0], arguments[1]) ? 1 : 0;
            case 240: SelectSpeakerByServiceAndLoyalty(arguments[0]); return 1;
            default: throw new InvalidDataException($"Unsupported Person relation list instruction {code}.");
        }
    }

    private void AddBloodRelatedPersons()
    {
        // Newly found relatives do not take part in this instruction's search.
        // Existing repetitions remain, and the separate result follows registry order.
        var additions = new List<S14EventConditionPersonReference>();
        foreach (var person in RelationRegisteredPersons())
        {
            if (_candidates.Contains(person)) continue;
            foreach (var original in _candidates)
                if (Known(Queries.ReadConditionPersonFamily(person, original), "list directional family"))
                { additions.Add(person); break; }
        }
        _candidates.AddRange(additions);
    }

    private void AddPersonsByRelation(int selector, bool like,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var target = ResolvePerson(selector, readTargetForce);
        if (!Known(Queries.ReadConditionPersonValidity(target), "list relation target validity")) return;
        foreach (var person in RelationRegisteredPersons())
        {
            if (_candidates.Contains(person)) continue;
            for (int index = 0; index < 8; index++)
                if (Known(Queries.ReadConditionPersonRelationListIdentity(person, index, like), "list current relation slot") ==
                    Known(Queries.ReadConditionPersonCharacterId(target), "list relation target identity"))
                { _candidates.Add(person); break; }
        }
    }

    private IEnumerable<S14EventConditionPersonReference> RelationRegisteredPersons()
    {
        var persons = Queries.ReadRegisteredConditionPersonPhysicalSlots() ?? throw Missing("list registered persons");
        foreach (int? physical in persons)
            yield return new(physical ?? throw Missing("list registered Person pointer"));
    }

    private void AddRulers()
    {
        var forces = Queries.ReadRegisteredConditionForcesInNativeOrder() ?? throw Missing("list registered Forces");
        foreach (var force in forces)
        {
            var ruler = PersonLookup(Known(Queries.ReadConditionForceRulerPersonId(force), "list Force ruler identity"));
            // The registry walk adds each valid ruler occurrence without deduplication.
            if (Known(Queries.ReadConditionPersonValidity(ruler), "list ruler validity")) _candidates.Add(ruler);
        }
    }

    private void AddAdvisers(IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        // The descriptor supplies eight Force selectors, including repeated ones.
        for (int index = 0; index < 8; index++)
        {
            var force = ResolveForce(arguments[index], readTargetForce);
            if (force is null || !Known(Queries.ReadConditionForceValidity(force), "list adviser Force validity")) continue;
            var adviser = PersonLookup(Known(Queries.ReadConditionForceAdvisorId(force), "list Force adviser identity"));
            if (Known(Queries.ReadConditionPersonValidity(adviser), "list adviser validity") && !_candidates.Contains(adviser))
                _candidates.Add(adviser);
        }
    }

    private bool RemovePersonsByHome(int selector)
    {
        var target = ResolveBaseSelector(selector);
        if (Known(Queries.ReadConditionBaseValidity(target), "list removed Home Base validity"))
            _candidates.RemoveAll(person =>
                (Queries.ReadConditionPersonHomeBase(person) ?? throw Missing("list removed Person Home Base")) == target);
        return _candidates.Count != 0;
    }

    private bool NarrowPersonsByLocation(int selector)
    {
        var target = ResolveBaseSelector(selector);
        if (Known(Queries.ReadConditionBaseValidity(target), "list location Base validity"))
            _candidates.RemoveAll(person =>
                (Queries.ReadConditionPersonLocationBase(person) ?? throw Missing("list Person location Base")) != target);
        return _candidates.Count != 0;
    }

    private bool NarrowPersonsByImperialCourt(int mode, int expected)
    {
        _candidates.RemoveAll(person => !Compare(
            Known(Queries.ReadConditionPersonImperialCourtTendency(person), "list Person imperial court tendency"), mode, expected));
        return _candidates.Count != 0;

        static bool Compare(int actual, int comparison, int operand) => comparison switch
        {
            1 => actual != operand, 2 => actual == operand,
            3 => actual > operand, 4 => actual >= operand,
            5 => actual < operand, 6 => actual <= operand, _ => false
        };
    }

    private void SelectSpeakerByServiceAndLoyalty(int role)
    {
        if ((uint)(role - 1) >= 5) return;
        var eligible = new List<S14EventConditionPersonReference>();
        foreach (var person in _candidates)
        {
            if (!Known(Queries.ReadConditionPersonValidity(person), "service speaker validity")) continue;
            if (Special(Known(Queries.ReadConditionPersonCharacterId(person), "service speaker identity")) ||
                (Known(Queries.ReadConditionPersonFlags(person), "service speaker flags") & 0x4000) != 0) continue;
            eligible.Add(person);
        }
        if (eligible.Count == 0) return;
        var selected = eligible[0];
        // A singleton does not read the date or a comparison key. Equal keys
        // retain the first eligible occurrence; the candidate list is untouched.
        for (int index = 1; index < eligible.Count; index++)
        {
            var date = (Queries as IS14BasicEventConditionQueries)?.ReadConditionDate()
                ?? throw Missing("service speaker current date");
            int year = unchecked((short)date.Year);
            uint candidateKey = ReadKey(eligible[index], year);
            uint selectedKey = ReadKey(selected, year);
            if (selectedKey < candidateKey) selected = eligible[index];
        }
        // Native +88+(14-role)*8 is +F0..+D0, the selectors -14..-10.
        _actors[role - 15] = selected;

        uint ReadKey(S14EventConditionPersonReference person, int year)
        {
            byte loyalty = Known(Queries.ReadConditionPersonRawLoyalty(person), "service speaker raw loyalty");
            ushort joined = inputs.ReadServiceYear?.Invoke(person) ?? throw Missing("service speaker current service year");
            uint veteran = year - joined > 4 ? 0x100u : 0u;
            ushort identity = Known(Queries.ReadConditionPersonCharacterId(person), "service speaker key identity");
            return ((veteran | loyalty) << 16) | unchecked((ushort)(6000 - identity));
        }
    }
}
