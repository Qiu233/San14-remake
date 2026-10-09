public sealed partial class MapPostEffect3ShaftExecutionOwner
{
    /// <summary>Complete the actual stock pass using this owner's privately
    /// retained World and original frontend setter evaluator.</summary>
    public Task<MapPostEffect3ShaftCompletedFamily> CompleteActualStockPass7FromOwnedSourceAsync()
    {
        var chain=LastComputeChain??throw new InvalidDataException("Actual Shaft compute chain is unavailable.");
        return CompleteActualStockPass7Async(firstWorld.NativeSamplers.CaptureOwnedDrawContext(chain.Prefix));
    }
}
