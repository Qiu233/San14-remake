using San14.Core;

namespace San14.Data;

/// <summary>The ordinary two/three-person sworn relationship writer shared by
/// the event result wrapper. It owns no presentation, occurrence, or receipt.</summary>
internal static class PersonSwornRelationsSource
{
    internal static (GameState State, int NativeReturn) Apply(GameState state,
        SourceWorldContext current, int first, int second, int third) => Evaluate(state, current, first, second, third, true);

    internal static bool CanCreate(GameState state, SourceWorldContext current, int first, int second, int third)
        => Evaluate(state, current, first, second, third, false).NativeReturn != 0;

    private static (GameState State, int NativeReturn) Evaluate(GameState state,
        SourceWorldContext current, int first, int second, int third, bool create)
    {
        if (!Valid(first) || !Valid(second)) return (state, 0);
        bool hasThird = Valid(third);
        int[] selected = hasThird ? [first, second, third] : [first, second];

        //3B7410 uses these gates, without the editor-only 4001..5100,
        //special-force or bit14 gates used by Partner and Like.
        foreach (int person in selected)
            if (Word(person, 0xBC) != 0 || Unavailable(person)) return (state, 0);
        int sex = Sex(first);
        if (Sex(second) != (sex == 0 ? 0 : 1) ||
            hasThird && Sex(third) != (sex == 0 ? 0 : 1)) return (state, 0);
        for (int i = 0; i < selected.Length; i++)
            for (int j = i + 1; j < selected.Length; j++)
            {
                int a = selected[i], b = selected[j];
                ushort aid = Identity(a), bid = Identity(b);
                ushort sworn = Word(a, 0xBC);
                if (Word(a, 0xB2) == Word(b, 0xB2) ||
                    Word(a, 0xBA) == bid || Word(b, 0xBA) == aid ||
                    sworn != 0 && sworn == Word(b, 0xBC) ||
                    Word(a, 0xB4) == bid || Word(b, 0xB4) == aid ||
                    Word(a, 0xB6) == bid || Word(b, 0xB6) == aid ||
                    Dislikes(a, b) || Dislikes(b, a)) return (state, 0);
            }

        if (!create) return (state, 1);

        //A selected force ruler takes precedence; 22EA50 is the current
        //force's ruler lookup, not an existing sworn-group lookup.
        int? leader = null;
        foreach (int person in selected)
        {
            if (!IsRuler(person)) continue;
            if (leader is null) leader = person;
            else
            {
                int age = Age(person), priorAge = Age(leader.Value);
                if (priorAge < age || age == priorAge && Identity(person) < Identity(leader.Value)) leader = person;
            }
        }
        if (leader is null)
        {
            int a = Age(first), b = Age(second);
            if (!hasThird)
                leader = a == b ? Identity(first) < Identity(second) ? first : second : a > b ? first : second;
            else
            {
                int c = Age(third);
                if (a > b && a > c) leader = first;
                else if (b > a && b > c) leader = second;
                else if (c > a && c > b) leader = third;
                //The native three-person tie branch selects the smallest
                //identity among all three, including a younger third.
                else leader = selected.MinBy(Identity);
            }
        }
        ushort group = BoundIdentity(leader.Value);
        var result = state;
        foreach (int person in selected)
            result = OwnedPersonRelationInputs.WriteCurrentRelationIdentity(result, person, 0xBC, group);
        return (result, 1);

        bool Valid(int person) => Need(current.ReadPersonNativeValidity(person), "native_validity");
        ushort Identity(int person) => Need(current.ReadPersonIdentity(person), "Person10");
        ushort BoundIdentity(int person) => (ushort)Math.Min((int)Identity(person), 6000);
        ushort Word(int person, int field) => Need(
            OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "word_" + field.ToString("X"));
        bool Unavailable(int person) => Need(current.ReadPersonStatus(person), "PersonE6") is 6 or 7 or 9;
        int Sex(int person)
        {
            int id = Identity(person);
            if (current.ResolveOfficerSource(id) != person || !state.Officers.TryGetValue(id, out var owner))
                throw Missing("Officer_owner");
            return (int)Need(owner.Sex, "PersonA9");
        }
        bool Dislikes(int firstPerson, int other)
        {
            if (!Valid(other)) return false;
            ushort id = Identity(other);
            for (int index = 0; index < 8; index++)
                if (Need(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, firstPerson, index),
                    "dislike_slot") == id) return true;
            return false;
        }
        bool IsRuler(int person)
        {
            var force = current.ReadPersonForce(person) ?? throw Missing("ruler_Force_pointer");
            int physical = Need(current.ReadForcePhysicalSlot(force), "ruler_Force");
            if (!Need(current.ReadForceNativeValidity(force), "ruler_Force_validity")) return false;
            ushort id = Need(current.ReadConditionForceRulerPersonId(new(physical)), "ruler_Person");
            var pointer = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("ruler_lookup_pointer");
            return pointer.PhysicalSlot is int target && Identity(target) == Identity(person);
        }
        int Age(int person) => PersonCurrentAgeSource.Read(state, current, person);
    }

    private static T Need<T>(T? value, string input) where T : struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("person.sworn-relations." + input);
}
