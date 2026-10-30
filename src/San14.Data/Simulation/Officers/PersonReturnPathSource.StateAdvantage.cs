using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    //2C6550(kind,0,1): the wildcard fourth argument deliberately ignores
    //magnitude. Its two tutorial exemptions require magnitude46/4, not zero.
    internal static bool HasQualifiedStateAdvantage(GameState state, SourceWorldContext current,
        ForceReference force, int effect)
    {
        try
        {
            if (!Known(current.ReadForceNativeValidity(force), "2C6550 Force validity") || effect is not (1 or 2)) return false;
            var temporary = Known(current.CreateTemporaryHexList(), "2C6550 State descriptor");
            try
            {
                current.ClearTemporaryHexList(temporary);
                var candidates = new List<int>();
                CollectAdvantageStates(state, current, effect, temporary, candidates);
                foreach (int province in candidates)
                    if (QualifiedAdvantage(state, current, force, province).HasValue) return true;
                return false;
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
        catch (MissingOwner missing) { throw new InvalidDataException(missing.Message, missing); }
    }
}
