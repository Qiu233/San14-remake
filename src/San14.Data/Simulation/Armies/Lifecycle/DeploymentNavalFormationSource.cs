using San14.Core;

namespace San14.Data;

/// <summary>Normal summary's independent Person naval choice. Explicit
/// confirmation packets retain their chosen value instead of rerunning this.</summary>
internal static class DeploymentNavalFormationSource
{
    internal static int Resolve(GameState state, SourceWorldContext current, int id, int physical)
        // Preserve the existing caller signature; the native predicate reads
        // the actual current Person identity, not a separately supplied label.
        => PersonNavalFormationSelection.Resolve(state, current, physical);
}
