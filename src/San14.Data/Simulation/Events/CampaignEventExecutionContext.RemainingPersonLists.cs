using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int ExecuteStoryPersonFamilyList(int code, IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce,
        Func<int, int, int?>? readRelationWord, Func<int, int, bool?>? related)
    {
        if (arguments.Count != 8) throw Missing("remaining Person-list operands");
        if (code is not (262 or 271 or 272)) throw Missing("remaining Person-list opcode");
        var selected = ResolvePerson(arguments[0], readTargetForce);
        if (!Known(Queries.ReadConditionPersonValidity(selected), "remaining Person-list selector validity")) return 1;
        if (code == 271)
            _candidates.RemoveAll(person => related?.Invoke(person.PhysicalSlot, selected.PhysicalSlot)
                ?? throw Missing("Person-list complete kinship predicate"));
        else if (code == 272)
            _candidates.RemoveAll(person =>
            {
                ushort target = Known(Queries.ReadConditionPersonCharacterId(selected), "removed like target identity");
                for (int index = 0; index < 8; index++)
                    if (Known(Queries.ReadConditionPersonRelationListIdentity(person, index, true),
                        "removed like current slot") == target) return true;
                return false;
            });
        else
        {
            foreach (var person in RelationRegisteredPersons())
            {
                //Registry order, no candidate validity gate, no deduplication.
                //BA is tested first, followed by B4 and B6 only when needed.
                if (Pointer(person, 0xBA) == selected || Pointer(person, 0xB4) == selected || Pointer(person, 0xB6) == selected)
                    _candidates.Add(person);
            }
        }
        return 1;

        S14EventConditionPersonReference Pointer(S14EventConditionPersonReference person, int field)
        {
            int raw = readRelationWord?.Invoke(person.PhysicalSlot, field)
                ?? throw Missing("spouse/child current relation word");
            if ((uint)raw > ushort.MaxValue) throw Missing("spouse/child relation word width");
            return PersonLookup(raw);
        }
    }
}
