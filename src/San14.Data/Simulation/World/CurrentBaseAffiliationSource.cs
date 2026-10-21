using San14.Core;

namespace San14.Data;

/// <summary>Current Base ownership is bound by the operation capture.
/// A saved initial byte must never overwrite an explicit current unknown.</summary>
internal static class CurrentBaseAffiliationSource
{
    internal static int? ReadForce(GameState state, SourceWorldContext current, BaseReference target) =>
        current.ReadBaseForceSlot(target);
}
