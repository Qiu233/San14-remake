using San14.Core;

namespace San14.Data;

internal static class FacilityConstructionCost
{
    internal static int Read(GameState state, SourceWorldContext current, ForceReference force,
        FacilityConstructionDefinition definition, OwnedBaseCapacityInputs panels)
    {
        if (!FacilityConstructionDefinitions.K(current.ReadForceNativeValidity(force), "cost_Force_validity"))
            return definition.BaseGoldCost;
        int physical = FacilityConstructionDefinitions.K(force.PhysicalSlot, "cost_Force_pointer");
        int modifier = FacilityConstructionDefinitions.K(panels.ReadPanelModifier(state, physical, 63), "cost_panel63");
        return unchecked(definition.BaseGoldCost * (100 - modifier)) / 100;
    }
}
