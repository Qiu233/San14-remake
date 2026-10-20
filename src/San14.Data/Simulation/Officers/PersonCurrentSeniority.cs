using San14.Core;

namespace San14.Data;

/// <summary>The bare current2C9A20 predicate. Caller-specific validity gates
/// stay with the narration wrapper and the actual selector respectively.</summary>
internal sealed class PersonCurrentSeniority(GameState state, SourceWorldContext current)
{
    internal bool IsSenior(int candidate, int ruler)
        => IsSenior(candidate, new TransferPersonReference(ruler));

    internal bool IsSenior(int candidate, TransferPersonReference rulerPointer)
    {
        if (Status(candidate) == 1 && K(current.ReadMoraleWaveForceRank47(Force(candidate)), "Force47") == 1) return true;
        if (Id(candidate) == K(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654")) return true;
        int ruler = K(rulerPointer.PhysicalSlot, "ruler_pointer");
        if (Word(candidate, 0xB2) == Word(ruler, 0xB2))
        {
            if (Generation(candidate) < Generation(ruler)) return true;
            if (Generation(candidate) == Generation(ruler))
            {
                int first = Age(candidate), second = Age(ruler);
                if (second <= first) return true;
            }
        }
        if (Sworn(candidate, ruler) && Word(ruler, 0xBC) != Id(ruler))
        {
            int first = Age(candidate), second = Age(ruler);
            if (second <= first) return true;
        }
        //The source's inlined spouse expression repeats these native tests.
        if (Valid(candidate) && Valid(ruler) &&
            (!Valid(candidate) || !Valid(ruler) || Id(candidate) != Id(ruler)) &&
            Word(candidate, 0xBA) != 0 && Word(candidate, 0xBA) == Id(ruler) &&
            K(Owner(ruler).Sex, "ruler_A9") == OfficerSex.Female) return true;
        if (K(Owner(candidate).DialogueStyle, "candidate_150") == 15) return true;
        int firstForce = Force(candidate);
        if (firstForce is >= 1 and <= 51)
        {
            int freshFirst = Force(candidate), second = Force(ruler);
            if (freshFirst == second && Status(candidate) == 1) return true;
        }
        if (Mother(ruler, candidate)) return true;
        if (!Valid(MaternalPointer(ruler))) return false;
        if (Father(MaternalPointer(ruler), candidate)) return true;
        if (Mother(MaternalPointer(ruler), candidate)) return true;
        return Siblings(MaternalPointer(ruler), candidate);
    }

    private bool Sworn(int a, int b) => Pair(a, b) && Word(a, 0xBC) != 0 && Word(a, 0xBC) == Word(b, 0xBC);
    private bool Father(int a, int b) => Pair(a, b) && Word(a, 0xB4) != 0 && Word(a, 0xB4) == Id(b);
    private bool Mother(int a, int b) => Pair(a, b) && Word(a, 0xB6) != 0 && Word(a, 0xB6) == Id(b);
    private bool Siblings(int a, int b) => Pair(a, b) &&
        (Word(a, 0xB4) != 0 && Word(a, 0xB4) == Word(b, 0xB4) ||
         Word(a, 0xB6) != 0 && Word(a, 0xB6) == Word(b, 0xB6));
    private bool Pair(int a, int b)
    {
        if (!Valid(a) || !Valid(b)) return false;
        return !(Valid(a) && Valid(b) && Id(a) == Id(b));
    }
    private bool Father(TransferPersonReference a, int b) => a.PhysicalSlot is int physical && Father(physical, b);
    private bool Mother(TransferPersonReference a, int b) => a.PhysicalSlot is int physical && Mother(physical, b);
    private bool Siblings(TransferPersonReference a, int b) => a.PhysicalSlot is int physical && Siblings(physical, b);
    private TransferPersonReference MaternalPointer(int ruler)
    {
        ushort raw = Word(ruler, 0xB6);
        var pointer = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("maternal_lookup");
        return pointer;
    }
    private int Age(int person)
    {
        int mode = state.CampaignEventWorld?.ScenarioMode ?? throw Missing("age_world_mode");
        if (mode is 2 or 3) throw Missing("special_mode_age");
        int year = state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("age_current_year");
        return Math.Max(0, unchecked((short)year - K(Owner(person).BirthYear, "age_current_birth") + 1));
    }
    private byte Generation(int person) => K(Owner(person).FamilyGeneration, "generation_B8");
    private OfficerState Owner(int physical)
    {
        ushort id = Id(physical);
        if (current.ResolveOfficerSource(id) != physical || !state.Officers.TryGetValue(id, out var person))
            throw Missing("current_person_owner");
        return person;
    }
    private ushort Word(int person, int field) => K(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "relation_" + field.ToString("X"));
    private ushort Id(int person) => K(current.ReadPersonIdentity(person), "ID10");
    private byte Status(int person) => K(current.ReadPersonStatus(person), "status_E6");
    private int Force(int person) => K(current.ReadForcePhysicalSlot(current.ReadPersonForce(person)
        ?? throw Missing("Force_pointer")), "Force_physical");
    private bool Valid(int person) => K(current.ReadPersonNativeValidity(person), "native_validity");
    private bool Valid(TransferPersonReference person) => person.PhysicalSlot is int physical && Valid(physical);
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new("person.current-seniority." + owner);
}
