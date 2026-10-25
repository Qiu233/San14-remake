using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //42F0D0 tests the saved World16A8 bit8, independently of the optional
    //tutorial transfer counter. The expansion itself was already done by36.
    internal GameState ApplyNormalTurnTerritoryExpansionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw TerritoryExpansionMissing("shared_current_pool");
        uint flags = current.ReadWorldTransferServiceFlags() ?? throw TerritoryExpansionMissing("World16A8");
        if ((flags & 0x100) != 0) return state;
        return new MonthlyCampaignMaintenanceSource(this, state, current, pool).FinalizeTerritoryExpansionCurrent();
    }

    private static InvalidDataException TerritoryExpansionMissing(string field) =>
        new("normal-turn.territory-expansion." + field);

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal GameState FinalizeTerritoryExpansionCurrent()
        {
            var pending = state.TerritoryExpansion?.PendingAssignments
                ?? throw TerritoryExpansionMissing("pending_assignments_owner");
            if (pending.Count == 0) return state;
            current.MaterializeFixedScenery(package._landmarkDefinitions);
            for (int i = 0; i < 101; i++)
                current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
            foreach (var assignment in pending.OrderBy(entry => entry.Key))
            {
                if (assignment.Key is < 0 or >= 48400 || assignment.Value is < 0 or > 51)
                    throw TerritoryExpansionMissing("assignment_domain");
                var hex = current.ReadHexTableReference(assignment.Key)
                    ?? throw TerritoryExpansionMissing("assignment_Hex_pointer");
                //25F1E0 deliberately clears the immediate36 write before
                //185D00 rechecks eligibility and publishes its side effects.
                current.AssignHexForce(hex, 0);
                PublishRestoredHex(hex, null);
                RestoreMonthlyOccupation(hex, assignment.Value, 0,
                    enqueueEncirclement: true, allowType3Override: true);
            }
            //The source does not erase this map here. Its next real clear is
            //25E520 in36; keeping it also preserves the source's saved state.
            return state;
        }

        private bool TerritoryExpansionType3Override(HexReference hex)
        {
            int link = Need(current.ReadHexObjectLink(hex), "expansion_override_Hex1A");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw TerritoryExpansionMissing("override_Object_pointer");
            if (!Need(current.ReadObjectNativeValidity(obj), "expansion_override_Object_validity")) return false;
            int physical = Need(obj.PhysicalSlot, "expansion_override_Object_physical");
            int kind = current.ReadPhysicalObject(physical)?.RawField12
                ?? throw TerritoryExpansionMissing("override_Object12");
            var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0)
                ?? throw TerritoryExpansionMissing("override_definition_pointer");
            return Need(current.ReadObjectDefinitionRaw2C(definition), "expansion_override_definition2C") == 3;
        }
    }
}
