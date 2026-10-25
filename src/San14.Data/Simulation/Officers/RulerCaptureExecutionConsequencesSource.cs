using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC27F820(Person, executionForce,0). The live registered Person chain
    //supplies the order; relation categories take the source's first match.
    internal GameState ApplyRulerExecutionConsequencesCurrent(GameState state,
        SourceWorldContext current, int victimPhysical, ForceReference executionForce)
    {
        if (!Need(current.ReadPersonNativeValidity(victimPhysical), "victim_validity") ||
            !Need(current.ReadForceNativeValidity(executionForce), "execution_Force_validity")) return state;
        int victimId = Identity(victimPhysical);
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
        {
            int person = Need(entry, "Person_pointer");
            int id = Identity(person);
            if (id == victimId) continue;
            int kind;
            int sworn = Relation(person, 0xBC);
            if (ValidId(sworn) && sworn == Relation(victimPhysical, 0xBC)) kind = 1;
            else if (ValidId(Relation(person, 0xBA)) && Relation(victimPhysical, 0xBA) == id) kind = 2;
            else if (Parent(victimPhysical, id, 0xB4) || Parent(victimPhysical, id, 0xB6) ||
                Parent(person, victimId, 0xB4) || Parent(person, victimId, 0xB6)) kind = 3;
            else if (OfficeRelatedCurrent(state, current, person, victimPhysical) &&
                !OfficeRelationList(state, current, person, victimPhysical, 0xD0)) kind = 4;
            else if (OfficeRelationList(state, current, person, victimPhysical, 0xC0)) kind = 5;
            else continue;

            int force = Need(current.ReadForcePhysicalSlot(executionForce), "execution_Force60");
            if (Need(current.ReadPersonOldForceSlot(person), "Person_Force22CF60") == force)
            {
                //232460 followed by233780(Person,1): only serving2..4.
                if (Need(current.ReadPersonStatus(person), "PersonE6") is < 2 or > 4) continue;
                byte loyalty = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "PersonE8");
                int penalty = kind switch { 1 => 50, 2 or 3 => 30, 4 or 5 => 20, _ => 0 };
                byte after = (byte)Math.Clamp(loyalty - penalty, 0, 255);
                //2F5EA0 preserves255 for source15E bit13 after subtraction.
                if ((Need(current.ReadArmyReturnPersonFlags196(person), "Person15E") & 0x2000) != 0) after = 255;
                current.SetPersonDestructionLoyaltyE8(person, after);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
            }
            else
            {
                int target = Math.Clamp((int)Need(current.ReadForceRecord(force)?.RawRuler10, "execution_Force_ruler10"), 0, 6000);
                byte intensity = kind switch { 1 => 36, 2 or 3 => 24, 4 or 5 => 12, _ => 0 };
                var aftermath = current.ReadOfficerAftermath(person) ?? throw Missing("Person_aftermath_owner");
                bool mapped = state.Officers.ContainsKey(target);
                current.SetOfficerAftermath(person, aftermath with
                {
                    Resentment = new(mapped ? target : null, intensity),
                    SourceKnowledge = (aftermath.SourceKnowledge ?? new(id, null, null, null)) with
                        { UnmappedResentmentTarget = mapped ? null : target }
                });
                state = current.PublishOfficerAftermath(state, id);
            }
        }
        return state;

        int Identity(int person) => Need(current.ReadPersonIdentity(person), "Person10");
        int Relation(int person, int field) => Need(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "Person_relation_" + field.ToString("X"));
        bool Parent(int person, int otherId, int field)
        {
            int value = Relation(person, field);
            return ValidId(value) && value == otherId;
        }
        bool ValidId(int id) => id is >= 1 and <= 6000;
        T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("ruler-execution.consequences." + field);
    }
}
