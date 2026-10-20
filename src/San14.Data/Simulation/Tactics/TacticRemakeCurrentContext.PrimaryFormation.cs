using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>22B220 selects the current land/naval formation, then06FE50
    /// reads its current A6 scalar or vector. This is not the range override.</summary>
    internal byte? ReadOrdinaryFormationBits(GameState state, UnitState army)
    {
        if (CurrentSurfaceCode(army) is not byte surface || !ArmyConfigurationSourceAdapter.HasConfiguration(army)) return null;
        int terrainSlot = surface <= 30 ? surface : 0;
        byte? formation = (_terrain.Catalog.Types[terrainSlot].RawField2C & 2) != 0
            ? ArmyConfigurationSourceAdapter.ReadNaval(state, army) : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army);
        return formation is byte selected ? TacticRemakeCurrentFormations.ReadParameter(state, selected <= 20 ? selected : 0, 0xA6) : null;
    }
}
