using San14.Core;

internal partial class D3D11ArmyMapLabels
{
    internal bool OwnsTraitPublication(GameState world) => ReferenceEquals(_capturedWorld, world);
}
