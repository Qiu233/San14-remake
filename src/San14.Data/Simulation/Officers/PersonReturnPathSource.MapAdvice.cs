using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    // Ordinary map advice reaches the selected-Province 2C6820 leaf, not
    // the wildcard aggregate. Share its existing current rules and owners.
    internal static bool HasQualifiedProvinceAdvantage(GameState state,SourceWorldContext current,
        ForceReference force,int province)
    {
        try
        {
            return Known(current.ReadForceNativeValidity(force),"advice_Force_validity") &&
                QualifiedAdvantage(state,current,force,province).HasValue;
        }
        catch(MissingOwner missing){throw new InvalidDataException(missing.Message,missing);}
    }
}
