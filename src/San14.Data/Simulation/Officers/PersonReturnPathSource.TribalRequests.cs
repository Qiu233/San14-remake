using San14.Core;

namespace San14.Data;

public static partial class PersonReturnPathSource
{
    //282020 uses the same actual2B5190 State-list producer as the existing
    //special-personnel path. It then reads only the first effect1 magnitude.
    //The allocator and source State order remain owned by that producer.
    internal static int? ReadTribalRequestProvinceCurrent(GameState state,
        SourceWorldContext current, int tribalForcePhysical)
    {
        var temporary = Known(current.CreateTemporaryHexList(), "tribal-request State descriptor");
        try
        {
            var provinces = new List<int>();
            CollectAdvantageStates(state, current, 1, temporary, provinces);
            foreach (int province in provinces)
            {
                int raw = AdvantageByte(state, PersonGrowthCurrentQueries.StateAdvantageKey(province));
                var pointer = Known(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0),
                    "tribal-request Advantage pointer");
                int type = Known(pointer.PhysicalSlot, "tribal-request Advantage receiver");
                int magnitude = 0;
                for (int index = 0; index < 2; index++)
                {
                    if (AdvantageByte(state, PersonGrowthCurrentQueries.EffectKey(type, index)) != 1) continue;
                    magnitude = AdvantageByte(state, PersonGrowthCurrentQueries.MagnitudeKey(type, index));
                    break;
                }
                if (magnitude == tribalForcePhysical) return province;
            }
            return null;
        }
        catch (MissingOwner missing)
        {
            throw new InvalidDataException("story.tribal-request." + missing.Message, missing);
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
    }
}
