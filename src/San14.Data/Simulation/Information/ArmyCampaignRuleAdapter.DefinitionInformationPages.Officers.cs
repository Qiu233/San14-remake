using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<OfficerInformationPageSet> PrepareDefinitionOfficerInformationPages(
        GameState state, int playerForceId, InformationListKind kind, int definitionId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            // Each of these original footer callers supplies palette enable0.
            var source = new OfficerInformationCurrent(this, state, playerForceId, rowPaletteEnabled: false);
            var current = source.current;
            var ids = new List<int>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw DefinitionInformationCurrent.Missing("officers.Person_registry"))
            {
                int physical = DefinitionInformationCurrent.Need(entry, "officers.Person_payload");
                byte status = DefinitionInformationCurrent.Need(current.ReadPersonStatus(physical), "officers.Person_status");
                if (!DefinitionInformationCurrent.Need(current.ReadPersonNativeValidity(physical), "officers.Person_validity")) continue;
                int force = DefinitionInformationCurrent.Need(current.ReadPersonForce(physical)?.PhysicalSlot, "officers.Person_force");
                if (force == 51 || status is < 1 or > 6) continue;
                int id = DefinitionInformationCurrent.Need(current.ReadPersonIdentity(physical), "officers.Person_identity");
                if (HasDefinition(id, physical)) ids.Add(id);
            }
            return AreaAdministrationQuery<OfficerInformationPageSet>.Known(source.Pages(ids));

            bool HasDefinition(int id, int physical)
            {
                switch (kind)
                {
                    case InformationListKind.Traits:
                        // Ordinary232E30 uses the shared current nine-slot
                        // qualifier, including original/custom person gates.
                        for (int index = 0; index < 9; index++)
                            if (DefinitionInformationCurrent.Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, index), "officers.trait_slot") == definitionId) return true;
                        return false;
                    case InformationListKind.Tactics:
                        // Four special tactics qualify their actual pair of
                        // original people independently of the ten raw slots.
                        if (definitionId is 107 or 109 or 111 or 113)
                            return unchecked((uint)(id - (5101 + definitionId - 107))) < 2;
                        for (int index = 0; index < 10; index++)
                            if (DeploymentTacticSelectionSource.ReadCurrentSlot(current, id, physical, index).PhysicalSlot == definitionId) return true;
                        return false;
                    case InformationListKind.Doctrines:
                    {
                        var person = state.Officers.GetValueOrDefault(id) ?? throw DefinitionInformationCurrent.Missing("officers.Person_owner");
                        int raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId ?? throw DefinitionInformationCurrent.Missing("officers.Person_doctrine");
                        var at = current.ReadStaticTableReference(0x78CF8, raw < 7 ? raw : 0) ?? throw DefinitionInformationCurrent.Missing("officers.Doctrine_pointer");
                        return at.PhysicalSlot == definitionId;
                    }
                    case InformationListKind.Policies:
                    {
                        var person = state.Officers.GetValueOrDefault(id) ?? throw DefinitionInformationCurrent.Missing("officers.Person_owner");
                        int raw = person.PolicyId ?? (person.PolicyBaseCost.HasValue ? 0 : throw DefinitionInformationCurrent.Missing("officers.Person_policy"));
                        var at = current.ReadStaticTableReference(0x78D50, raw < 121 ? raw : 0) ?? throw DefinitionInformationCurrent.Missing("officers.Policy_pointer");
                        return at.PhysicalSlot == definitionId;
                    }
                    default: throw DefinitionInformationCurrent.Missing("officers.kind."+kind);
                }
            }
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<OfficerInformationPageSet>.Unknown(error.Message); }
    }
}
