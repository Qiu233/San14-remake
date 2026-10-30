using San14.Core;

namespace San14.Data;

/// <summary>The complete directed276C30 predicate over existing current owners.
/// It is distinct from the event dialogue-family preparation predicate.</summary>
internal static class PersonKinshipCurrent
{
    internal static bool Read(GameState state, SourceWorldContext current, int first, int second)
    {
        if (!Valid(first) || !Valid(second)) return false;
        bool Serves(int person) => K(current.ReadPersonStatus(person), "status") is >= 1 and <= 4;
        if (Serves(first) && Serves(second) &&
            K(current.ReadPersonOldForceSlot(first), "first_Force") !=
            K(current.ReadPersonOldForceSlot(second), "second_Force")) return false;
        if (Kin(first, second, 0xB4) || Kin(first, second, 0xB6) ||
            Kin(second, first, 0xB4) || Kin(second, first, 0xB6) || Kin(first, second, 0xBA)) return true;
        if (Id(first) != Id(second) &&
            (Word(first, 0xB4) != 0 && Word(first, 0xB4) == Word(second, 0xB4) ||
             Word(first, 0xB6) != 0 && Word(first, 0xB6) == Word(second, 0xB6))) return true;
        if (Kin(first, second, 0xBC)) return true;
        return Word(first, 0xB2) != 0 && Word(first, 0xB2) == Word(second, 0xB2);

        bool Valid(int physical) => K(current.ReadPersonNativeValidity(physical), "Person_validity");
        ushort Id(int physical) => K(current.ReadPersonIdentity(physical), "Person_identity");
        ushort Word(int physical, int field) => K(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, physical, field),
            "relationship_" + field.ToString("X"));
        bool Kin(int a, int b, int field)
        {
            if (!Valid(a) || !Valid(b) || Id(a) == Id(b)) return false;
            ushort value = Word(a, field);
            return value != 0 && value == (field == 0xBC ? Word(b, field) : Id(b));
        }
    }

    private static T K<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException("person.kinship.current." + field);
}
