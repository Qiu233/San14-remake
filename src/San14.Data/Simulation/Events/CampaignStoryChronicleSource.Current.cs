using San14.Core;

namespace San14.Data;

// Outer null is unknown. A reference containing a null physical slot is the
// actual reset pointer retained by this invocation, not an unknown selection.
internal sealed record CampaignStoryChronicleInputs(ForceReference? SelectedTargetForce,
    BaseReference? SelectedTargetCity);

internal static partial class CampaignStoryChronicleSource
{
    private sealed class Current(GameState state, SourceWorldContext current,
        CampaignStoryChronicleInputs inputs)
    {
        internal int PersonHomeHex(int rawPersonId) => PersonHome(PersonPointer(rawPersonId));

        internal int SelectedForceRulerHomeHex()
        {
            var force = SelectedForce();
            if (!K(current.ReadForceNativeValidity(force), "selected_Force_validity"))
                throw Missing("selected_Force_ruler_null_dereference");
            return PersonHome(ForceRuler(force));
        }

        internal int SelectedCityHex() => BaseHex(inputs.SelectedTargetCity
            ?? throw Missing("selected_City_pointer"));

        internal int CityHex(int rawCityId) => BaseHex(current.ReadCityTableReference(rawCityId)
            ?? throw Missing("fixed_City_pointer"));

        //DED8 starts the independent eleven-entry Gate table; DEE8 is Gate2.
        internal int GateHex(int rawGateId) => BaseHex(current.ReadGateTableReference(rawGateId)
            ?? throw Missing("fixed_Gate_pointer"));

        //0400E0 reads Force10 directly; it has neither a Force validity gate
        //nor22EE60's special Force0 ruler5014 fallback.
        internal bool PlayerRulerIs(int rawPersonId) => ForceRuler(PlayerForce()) == PersonPointer(rawPersonId);
        internal bool PlayerIsSelectedForce() => PlayerForce() == SelectedForce();

        internal bool PlayerForceOfPerson(int rawPersonId)
        {
            int person = P(PersonPointer(rawPersonId), "fixed_Person_pointer");
            return IsPlayer(current.ReadPersonForce(person) ?? throw Missing("fixed_Person_current_Force"));
        }

        internal bool SelectedForceIsPlayer() => IsPlayer(SelectedForce());
        internal bool PersonExists(int rawPersonId) => PersonPointer(rawPersonId).HasValue;

        internal bool PlayerRelatedToPerson(int rawPersonId)
        {
            int? first = ForceRuler(PlayerForce());
            int? second = PersonPointer(rawPersonId);
            //Keep the exact83D9F0 order and directed relationships. The last
            //predicate validates only its second operand before reading C0.
            return Sworn(first, second) || Partner(first, second) ||
                Kinship(first, second) || Likes(first, second);
        }

        internal bool PlayerKingdomNameIs(byte rawNameId) =>
            K(current.ReadForceKingdomNameIndex(P(PlayerForce().PhysicalSlot, "player_Force_pointer")),
                "player_Force48") == rawNameId;

        //World6CB50 is Area12, not an Army or Object.22C900 reads its
        //representative Hex3A, then Hex1A, clamps that link to0..61 and
        //32C7D0 searches the current registered Bases by word4E.
        internal int AreaCityHex(int rawAreaId)
        {
            var area = current.ReadAreaTableReference(rawAreaId) ?? throw Missing("fixed_Area_pointer");
            _ = P(area.PhysicalSlot, "fixed_Area_pointer");
            int rawHex = K(current.ReadAreaHexIndex(area), "fixed_Area3A");
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                ?? throw Missing("fixed_Area_Hex_pointer");
            _ = P(hex.PhysicalSlot, "fixed_Area_Hex_pointer");
            int link = K(current.ReadHexObjectLink(hex), "fixed_Area_Hex1A");
            return BaseHex(BaseLookup.Find(link < 62 ? link : 0, current));
        }

        //World6D060 is Area174. This source reads3A without a validity gate
        //or clamp; the chronicle writer owns its normal sentinel handling.
        internal int AreaHex(int rawAreaId)
        {
            var area = current.ReadAreaTableReference(rawAreaId) ?? throw Missing("fixed_Area_pointer");
            _ = P(area.PhysicalSlot, "fixed_Area_pointer");
            return K(current.ReadAreaHexIndex(area), "fixed_Area3A");
        }

        private int PersonHome(int? pointer)
        {
            int person = P(pointer, "home_Person_pointer");
            ushort home = K(current.ReadPersonFirstLocation(person), "home_PersonE2");
            return BaseHex(BaseLookup.Find(home, current));
        }

        private int BaseHex(BaseReference basis)
        {
            _ = P(basis.PhysicalSlot, "Base_pointer");
            return current.ReadBaseCurrentHexIndex(basis); //22D880, including48400.
        }

        private ForceReference PlayerForce() => current.ReadCurrentPlayerForce()
            ?? throw Missing("player_Force_pointer");
        private ForceReference SelectedForce() => inputs.SelectedTargetForce
            ?? throw Missing("selected_Force_pointer");

        private bool IsPlayer(ForceReference force)
        {
            int player = K(current.ReadForcePhysicalSlot(PlayerForce()), "player_Force_identity");
            return player != 0 && player == K(current.ReadForcePhysicalSlot(force), "compared_Force_identity");
        }

        private int? ForceRuler(ForceReference force)
        {
            int physical = P(force.PhysicalSlot, "ruler_Force_pointer");
            return PersonPointer(K(current.ReadForceRecord(physical)?.RawRuler10, "ruler_Force10"));
        }

        private int? PersonPointer(int rawPersonId)
        {
            var person = (rawPersonId <= 6000 ? current.ReadPersonLookupReference(rawPersonId) :
                current.ReadTransferPersonTableReference(0)) ?? throw Missing("Person_current_lookup");
            return person.PhysicalSlot;
        }

        private bool Valid(int? person) => person is int physical &&
            K(current.ReadPersonNativeValidity(physical), "relation_Person_validity");
        private ushort Id(int physical) => K(current.ReadPersonIdentity(physical), "relation_Person10");
        private ushort Word(int physical, int field) =>
            K(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, physical, field),
                "relation_Person" + field.ToString("X"));

        private bool Sworn(int? first, int? second)
        {
            if (!Valid(first) || !Valid(second)) return false;
            int a = first!.Value, b = second!.Value;
            if (Id(a) == Id(b)) return false;
            ushort group = Word(a, 0xBC);
            return group != 0 && group == Word(b, 0xBC);
        }

        private bool Partner(int? first, int? second)
        {
            if (!Valid(first) || !Valid(second)) return false;
            int a = first!.Value, b = second!.Value;
            if (Id(a) == Id(b)) return false;
            ushort partner = Word(a, 0xBA);
            return partner != 0 && partner == Id(b);
        }

        private bool Kinship(int? first, int? second) => first is int a && second is int b &&
            PersonKinshipCurrent.Read(state, current, a, b);

        private bool Likes(int? first, int? second)
        {
            if (!Valid(second)) return false;
            int a = P(first, "like_first_Person_pointer");
            for (int i = 0; i < 8; i++)
                if (K(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, a, i), "like_PersonC0") == Id(second!.Value))
                    return true;
            return false;
        }

        private static int P(int? value, string field) => value ?? throw Missing(field);
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("campaign-story-chronicle.current." + field);
    }
}
