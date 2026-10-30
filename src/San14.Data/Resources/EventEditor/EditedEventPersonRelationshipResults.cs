using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EditedEventResultsCurrent
    {
        private int Partner(IReadOnlyList<int> arguments)
        {
            int first = RelationshipPerson(arguments[0]), second = RelationshipPerson(arguments[1]);
            if (!RelationshipValid(first)) return 0;
            if (!RelationshipEditable(first) || !RelationshipValid(second) ||
                !RelationshipEditable(second) || first == second) return 0;
            if ((RelationshipFlags(first) & 0x4000) != 0 || (RelationshipFlags(second) & 0x4000) != 0 ||
                RelationshipUnavailable(first) || RelationshipUnavailable(second)) return 0;
            if (RelationshipFamily(first, second) || RelationshipFamily(second, first)) return 0;
            if (RelationshipSex(first) == RelationshipSex(second)) return 0;

            State = PersonRelationshipSource.SetPartners(State, current, first, second);
            return 1;
        }

        private int Like(IReadOnlyList<int> arguments)
        {
            int first = RelationshipPerson(arguments[0]), second = RelationshipPerson(arguments[1]);
            if (!RelationshipValid(first)) return 0;
            if (!RelationshipEditable(first) || !RelationshipValid(second) ||
                !RelationshipEditable(second) || first == second) return 0;
            if ((RelationshipFlags(first) & 0x4000) != 0 || (RelationshipFlags(second) & 0x4000) != 0) return 0;
            bool room = false;
            for (int i = 0; i < 8; i++)
                if (RelationshipList(first, i, true) == 0) { room = true; break; }
            //The editor wrapper rejects a full list even if the target is
            //already present. It has no status or symmetric-write rule.
            if (!room) return 0;

            State = PersonRelationshipSource.AddLike(State, current, first, second);
            return 1;
        }

        private int Sworn(IReadOnlyList<int> arguments)
        {
            var applied = PersonSwornRelationsSource.Apply(State, current,
                RelationshipPerson(arguments[0]), RelationshipPerson(arguments[1]), RelationshipPerson(arguments[2]));
            State = applied.State;
            return applied.NativeReturn;
        }

        private int RelationshipPerson(int selector) => Selectors.Person(selector);
        private int? RelationshipLookup(int identity)
        {
            var pointer = (identity <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("relationship_Person_pointer");
            return pointer.PhysicalSlot;
        }
        private bool RelationshipValid(int person) => Need(current.ReadPersonNativeValidity(person), "relationship_native_validity");
        private ushort RelationshipId(int person) => Need(current.ReadPersonIdentity(person), "relationship_Person10");
        private ushort RelationshipBoundId(int person) => (ushort)Math.Min((int)RelationshipId(person), 6000);
        private ushort RelationshipWord(int person, int field) =>
            Need(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(State, person, field), "relationship_word_" + field.ToString("X"));
        private ushort RelationshipList(int person, int index, bool like) =>
            Need(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(State, person, index)
                : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(State, person, index), "relationship_list_slot");
        private ushort RelationshipFlags(int person) => Need(current.ReadOwnedReturnPersonFlags(person), "relationship_Person15E");
        private bool RelationshipUnavailable(int person) => Need(current.ReadPersonStatus(person), "relationship_PersonE6") is 6 or 7 or 9;
        private bool RelationshipEditable(int person)
        {
            ushort id = RelationshipId(person);
            if (unchecked((ushort)(id - 4001)) < 1100) return false;
            ushort home = Need(current.ReadPersonFirstLocation(person), "relationship_PersonE2");
            if ((home is < 1 or > 61) && DuelAftermathForce(current, person) is >= 46 and <= 50) return false;
            return !DuelAftermathSpecialPerson(current, person);
        }
        private OfficerState RelationshipOwner(int person)
        {
            int id = RelationshipId(person);
            if (current.ResolveOfficerSource(id) != person || !State.Officers.TryGetValue(id, out var officer))
                throw Missing("relationship_Officer_owner");
            return officer;
        }
        private int RelationshipSex(int person) => (int)Need(RelationshipOwner(person).Sex, "relationship_PersonA9");
        private bool RelationshipFamily(int a, int b)
        {
            if (RelationshipWord(a, 0xB2) == RelationshipWord(b, 0xB2)) return true;
            if (RelationshipLookup(RelationshipWord(a, 0xBA)) == b) return true;
            ushort group = RelationshipWord(a, 0xBC);
            if (group is >= 1 and <= 6000 && group == RelationshipWord(b, 0xBC)) return true;
            return RelationshipLookup(RelationshipWord(a, 0xB4)) == b || RelationshipLookup(RelationshipWord(a, 0xB6)) == b;
        }
        private void RelationshipWrite(int person, int field, ushort value)
            => State = OwnedPersonRelationInputs.WriteCurrentRelationIdentity(State, person, field, value);
    }
}
