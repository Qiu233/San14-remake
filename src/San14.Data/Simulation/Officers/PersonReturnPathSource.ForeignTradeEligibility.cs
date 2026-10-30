using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    //2C6550(kind2,fourth0), sharing the actual Province/Advantage iterator
    //with the special-personnel predicate instead of substituting ownership.
    internal static bool ReadForeignTradeEligibility(GameState state, SourceWorldContext current,
        ForceReference force, int country, ApplicationGameplayContext? application)
    {
        try { return ReadForeignTradeEligibilityCore(state, current, force, country, application); }
        catch (MissingOwner missing) { throw new InvalidDataException("normal-turn.foreign-trade." + missing.Message); }
    }

    private static bool ReadForeignTradeEligibilityCore(GameState state, SourceWorldContext current,
        ForceReference force, int country, ApplicationGameplayContext? application)
    {
        if (!Known(current.ReadForceNativeValidity(force), "trade Force validity")) return false;
        var app = Known(application, "trade application");
        if (Known(app.TutorialServicePresent, "trade tutorial service") && Known(app.TutorialActive, "trade tutorial active"))
        {
            var player = Known(OwnedCurrentPlayerForce.ReadCurrentReference(state, current), "trade player");
            if (Known(current.ReadForcePhysicalSlot(player), "trade player ordinal") != 0 &&
                player == force && country == 4)
            {
                //570840 and17A470 share the controller bitmap; its actual
                //byte6.bit7 is projected by the attached tutorial session.
                if (Known(app.TutorialAllowsForeignTrade, "trade special-mode byte6 bit7")) return true;
            }
        }
        var list = Known(current.CreateTemporaryHexList(), "trade State temporary");
        try
        {
            var provinces = new List<int>();
            CollectAdvantageStates(state, current, 2, list, provinces);
            foreach (int province in provinces)
            {
                int raw = AdvantageByte(state, PersonGrowthCurrentQueries.StateAdvantageKey(province));
                var advantage = Known(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0), "trade Advantage");
                if (advantage.PhysicalSlot is not int type || type == 0) continue;
                int selected = 0;
                for (int index = 0; index < 2; index++)
                    if (AdvantageByte(state, PersonGrowthCurrentQueries.EffectKey(type, index)) == 2)
                    { selected = AdvantageByte(state, PersonGrowthCurrentQueries.MagnitudeKey(type, index)); break; }
                if (selected == country && QualifiedAdvantage(state, current, force, province).HasValue) return true;
            }
            return false;
        }
        finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }
}
