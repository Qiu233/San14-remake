using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    //The same2C6820 selected-Province leaf used by special personnel. A
    //known unqualified Province is distinct from an unknown current input.
    internal static (int? Value, string? UnresolvedOwner) ReadQualifiedAdvantageForProvinceCurrent(
        GameState state, SourceWorldContext current, ForceReference force, int province)
    {
        try { return (QualifiedAdvantage(state, current, force, province), null); }
        catch (MissingOwner missing) { return (null, missing.Message); }
    }
}
