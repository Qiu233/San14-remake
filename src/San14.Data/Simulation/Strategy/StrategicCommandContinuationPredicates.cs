using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private bool ReadStrategicPoisonContinuationCurrent(SourceWorldContext current, AreaReference area)
    {
        int representative = StrategyNeed(current.ReadAreaHexIndex(area), "journey_poison_Area3A");
        var hex = current.ReadHexTableReference(representative < 48400 ? representative : 0)
            ?? throw StrategyMissing("journey_poison_Hex");
        if (unchecked((short)StrategyNeed(current.ReadHexRaw1C(hex), "journey_poison_Hex1C")) is >= 1 and <= 500)
            return false;
        int rawCity = StrategyNeed(current.ReadAreaCitySlot(area), "journey_poison_Area35");
        var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
            ?? throw StrategyMissing("journey_poison_City");
        bool center = false;
        if (StrategyNeed(current.ReadBaseNativeValidity(city), "journey_poison_City_validity"))
        {
            int link = StrategyNeed(current.ReadBaseObjectLink(city), "journey_poison_City4E");
            var obj = current.ReadObjectTableReference(link < 3001 ? link : 0)
                ?? throw StrategyMissing("journey_poison_City_Object");
            center = StrategyNeed(current.ReadObjectNativeValidity(obj), "journey_poison_Object_validity") &&
                StrategyNeed(current.ReadObjectHexIndex(obj), "journey_poison_Object_Hex") == representative;
        }
        if (!center && StrategyNeed(current.ReadHexSurfaceCode(hex), "journey_poison_Surface") != 17)
            return true;
        ushort objectSlot = StrategyNeed(current.ReadHexObjectLink(hex), "journey_poison_Hex1A");
        var home = BaseLookup.Find(objectSlot < 62 ? objectSlot : 0, current);
        return !StrategyNeed(current.ReadBaseNativeValidity(home), "journey_poison_Base_validity") ||
            StrategyNeed(current.ReadBaseForceSlot(home), "journey_poison_Base_virtualB0") == 0;
    }

    private bool ReadStrategicRumorContinuationCurrent(GameState state, SourceWorldContext current,
        int person, ForceReference actor, int originalForce)
        => new StrategicPersonTargetsCurrent(this, state, current,
            StrategyNeed(current.ReadForcePhysicalSlot(actor), "journey_rumor_actor_Force"))
            .RumorContinuation(person, originalForce);
}
