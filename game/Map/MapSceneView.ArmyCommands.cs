using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Func<GameState, int, int, ArmyMapGroupingFrame?, bool>? _ordinaryArmyMapEntry;

    internal void SetOrdinaryArmyMapEntryHandler(Func<GameState, int, int, ArmyMapGroupingFrame?, bool> handler)
        => _ordinaryArmyMapEntry = handler;

    private bool UsesOrdinaryArmyMapEntry => _ordinaryArmyMapEntry is not null &&
        _logicalWorld?.NativeArmySimulation is not null &&
        System.Environment.GetEnvironmentVariable("SAN14_DEVELOPER_WORKSPACE") != "1";

    private bool TryOpenOrdinaryArmyMapEntry(int hex, bool released)
    {
        if (!released || !_embedded || _detailsOpen || _periodMapActive || _commandTargetPicked is not null ||
            _logicalWorld is not { Phase: TurnPhase.Strategy } world || _ordinaryArmyMapEntry is null ||
            _sourceMainCamera?.CurrentRuntime.DirtyCountdownD8 != 0 ||
            _mapPresentation?.SelectionPolicy.Allows(new HexReference(hex)) != true) return false;
        //The click uses exactly the accepted map's World, camera and groups.
        //A later logical successor waits for its normal map publication.
        if (_d3d11PresentedInput is not { SourceFrame: { } frame } input ||
            !ReferenceEquals(input.WorldState, world)) return false;
        int level = MapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
        return _ordinaryArmyMapEntry(world, hex, level, input.ArmyGroupingWorld);
    }
}
