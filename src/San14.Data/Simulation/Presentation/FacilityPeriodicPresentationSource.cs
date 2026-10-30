using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Func<ArmyReceiverId,FacilityArmyViewSnapshot?>? facilityPeriodicArmyViews;

    /// <summary>The map's immutable current Army-view positions. A known
    /// missing view is distinct from a provider which has not been bound.</summary>
    public void ConfigureFacilityPeriodicArmyViews(Func<ArmyReceiverId,FacilityArmyViewSnapshot?>? read)
        =>facilityPeriodicArmyViews=read;

    internal FacilityArmyViewSnapshot CaptureFacilityPeriodicArmyViewCurrent(GameState state,SourceWorldContext current,ArmyUnitReference army)
    {
        if(!(current.ReadArmyUnitNativeValidity(army)??throw FacilityPresentationMissing("Army_validity")))
            return new(false,0,0,0);
        int physical=army.PhysicalSlot??throw FacilityPresentationMissing("Army_pointer");
        var life=state.ReadArmyLifecycle()??throw FacilityPresentationMissing("Army_lifecycle");
        ArmyReceiverId receiver=life.Slots[physical].ReceiverId;
        var view=facilityPeriodicArmyViews?.Invoke(receiver)??throw FacilityPresentationMissing("Army_view_position_owner");
        //16EA80 initializes zero and copies view+10 only when a view exists.
        return view.Present?view:new(false,0,0,0);
    }

    private static InvalidDataException FacilityPresentationMissing(string field)=>new("facility.presentation."+field);
}
