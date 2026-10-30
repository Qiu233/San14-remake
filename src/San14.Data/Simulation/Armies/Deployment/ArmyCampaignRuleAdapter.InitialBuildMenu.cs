using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class UnitTargetMenuReader
    {
        //7C70D0 supplies the selected draft Persons, not any fabricated Army.
        //Only the actual menu open needs construction inputs; pointer/hover
        //queries keep their existing capture and never search definitions.
        private void AddInitialConstruction() => Add("build", 0x10B, 0x56A4, InitialConstructionIncluded, null);

        private bool InitialConstructionIncluded()
        {
            if (!(Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army ||
                Kind() == UnitTargetMenuKind.Trap && Relation() == UnitTargetMenuRelation.Other) ||
                Relation() == UnitTargetMenuRelation.Friendly || SameSite()) return false;
            package.BindFacilityConstructionInputs(state, current);
            var people = current.CreateNearbyTemporary();
            try
            {
                foreach (int id in selectedOfficerIds)
                {
                    var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                        ?? throw Missing("initial_build_Person_pointer");
                    bool valid = person.PhysicalSlot is int physical &&
                        Need(current.ReadPersonNativeValidity(physical), "initial_build_Person_validity");
                    //The source appends a null pointer for a known-invalid
                    //receiver; Person0 is not a substitute for that pointer.
                    current.AppendConstructionPersonTemporary(people, valid ? person.PhysicalSlot : null);
                }
                string? missing = null;
                for (int slot = 0; slot < 31; slot++)
                {
                    try
                    {
                        var definition = current.ReadObjectDefinitionReference(slot)
                            ?? throw Missing("initial_build_definition_pointer");
                        if (!Need(current.ReadObjectDefinitionValidity(definition), "initial_build_definition_validity") ||
                            Need(current.ReadObjectDefinitionRaw2C(definition), "initial_build_definition_kind") == 1) continue;
                        if (FacilityConstructionLegality.Check(state, current, people, hex, definition)) return true;
                    }
                    catch (InvalidDataException error) { missing ??= error.Message; }
                }
                if (missing is not null) throw new InvalidDataException(missing);
                return false;
            }
            finally { current.ClearNearbyTemporary(people, true); }
        }
    }
}
