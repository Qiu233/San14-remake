using San14.Core;

internal partial class D3D11MapMarkers
{
    internal bool OwnsArmyTraitPublication(GameState world) => _armyLabels?.OwnsTraitPublication(world) == true;
}
