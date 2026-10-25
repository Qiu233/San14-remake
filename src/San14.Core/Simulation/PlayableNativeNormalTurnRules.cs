namespace San14.Core;

/// <summary>The normal Turn after all native Army Progress stages and before
/// deployment preparation or period economy. A reached source frontier throws;
/// a real campaign stop returns its saved stop request for reporting.</summary>
public interface IPlayableNativeNormalTurnRules
{
    GameState CompleteNormalTurn(GameState completed);
}
