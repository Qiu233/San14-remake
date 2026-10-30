using San14.Core;

namespace San14.Data;

/// <summary>The shared current partner and one-way affinity writers. Admission belongs to each caller.</summary>
internal static class PersonRelationshipSource
{
    internal static GameState SetPartners(GameState state, SourceWorldContext current, int first, int second)
    {
        int? previous = Lookup(current, Word(state, first, 0xBA));
        if (previous is int oldFirst && Valid(current, oldFirst)) state = Write(state, oldFirst, 0xBA, 0);
        //The second pointer is read after the first clear, including aliased operands.
        previous = Lookup(current, Word(state, second, 0xBA));
        if (previous is int oldSecond && Valid(current, oldSecond)) state = Write(state, oldSecond, 0xBA, 0);
        state = Write(state, first, 0xBA, BoundId(current, second));
        return Write(state, second, 0xBA, BoundId(current, first));
    }

    internal static GameState ClearPartner(GameState state, SourceWorldContext current, int first)
    {
        int? previous = Lookup(current, Word(state, first, 0xBA));
        if (previous is not int second || !Valid(current, second)) return state;
        state = Write(state, first, 0xBA, 0);
        return Write(state, second, 0xBA, 0);
    }

    internal static GameState AddLike(GameState state, SourceWorldContext current, int first, int second)
    {
        var retained = new List<int>(8);
        for (int i = 0; i < 8; i++)
        {
            ushort id = Need(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, first, i), "dislike_slot");
            if (id is < 1 or > 6000) continue;
            if (Lookup(current, id) is int other && other != second) retained.Add(other);
        }
        state = OwnedPersonRelationInputs.ClearCurrentDislikeIdentities(state, first);
        for (int i = 0; i < retained.Count; i++)
            state = OwnedPersonRelationInputs.WriteCurrentDislikeIdentity(state, first, i, BoundId(current, retained[i]));
        ushort target = Need(current.ReadPersonIdentity(second), "target_identity");
        for (int i = 0; i < 8; i++)
            if (Need(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i), "like_slot") == target) return state;
        for (int i = 0; i < 8; i++)
            if (Need(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i), "like_slot") == 0)
                return OwnedPersonRelationInputs.WriteCurrentLikeIdentity(state, first, i, BoundId(current, second));
        return state;
    }

    internal static bool Valid(SourceWorldContext current, int person) => Need(current.ReadPersonNativeValidity(person), "validity");
    internal static int Sex(GameState state, SourceWorldContext current, int person)
    {
        int id = Need(current.ReadPersonIdentity(person), "identity");
        if (current.ResolveOfficerSource(id) != person || !state.Officers.TryGetValue(id, out var officer))
            throw Missing("sex_owner");
        return (int)Need(officer.Sex, "sex");
    }
    private static int? Lookup(SourceWorldContext current, int identity)
    {
        var pointer = (identity <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("lookup_pointer");
        return pointer.PhysicalSlot;
    }
    private static ushort Word(GameState state, int person, int field) =>
        Need(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "relation_word");
    private static ushort BoundId(SourceWorldContext current, int person) =>
        (ushort)Math.Min((int)Need(current.ReadPersonIdentity(person), "identity"), 6000);
    private static GameState Write(GameState state, int person, int field, ushort value) =>
        OwnedPersonRelationInputs.WriteCurrentRelationIdentity(state, person, field, value);
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("person.relationship.current." + name);
}
