using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>1FC6E0's actual neutral City/port branch. The shared26DA10
    /// kernel runs(Base,currentExecutorDistrict,executor,0,0,0,1,0,1,0).
    /// It borrows the reached operation's current and primary list pool.</summary>
    internal GameState ApplyStrategicPoisonNeutralBaseTransferCurrent(GameState state,
        SourceWorldContext current, BaseReference target, ForceReference own,
        int executorPhysical, NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects)
    {
        var pool=current.ReadOwnedCurrentListPool() ?? throw StrategyMissing("poison_Base_pool");
        if(!ReferenceEquals(pool.ArmyReturnOwner,current))throw StrategyMissing("poison_Base_same_current");
        if(!StrategyNeed(current.ReadBaseNativeValidity(target),"poison_Base_validity"))return state;
        int districtRaw=StrategyNeed(current.ReadPersonDistrictSlot(executorPhysical),"poison_Base_executor_E0");
        var destination=current.ReadDistrictTableReference(districtRaw<52?districtRaw:0)
            ?? throw StrategyMissing("poison_Base_executor_District");
        int force=StrategyNeed(current.ReadDistrictForceSlot(destination),"poison_Base_destination_Force");
        if(force!=StrategyNeed(current.ReadForcePhysicalSlot(own),"poison_Base_actor_Force"))
            throw StrategyMissing("poison_Base_actor_affiliation");
        var actor=new TransferPersonReference(executorPhysical);
        var working=ApplyNeutralBaseTransferCurrent(state,current,pool,target,destination,actor,
            operationDate,effects,operation=>TransferStrategicPoisonBaseDistrictCurrent(current,
                target,destination,executorPhysical,operation), PoisonEmperorInputs);
        if (working.ForceTransferEmperorTail is not null) return working;
        return CompleteStrategicPoisonBaseTransferCurrent(working, current, target);
    }

    private GameState CompleteStrategicPoisonBaseTransferCurrent(GameState state,
        SourceWorldContext current, BaseReference target)
    {
        var working = state;
        //The caller independently applies these two virtual resource floors
        //after26DA10. Gate virtual90/A0/98/A8 route to its actual receiving City.
        var receiving=BaseCurrencyAdditionSource.CurrencyCity(target,current);
        Floor(BaseResourceKind.Gold,"poison-minimum-gold",1000);
        Floor(BaseResourceKind.Supplies,"poison-minimum-supplies",10000);
        return working;

        void Floor(BaseResourceKind kind,string parameter,int original)
        {
            int requested=StrategicParameter(working,"resolution."+parameter,original);
            int before=StrategyNeed(current.ReadBaseResource(receiving,kind),"poison_Base_resource");
            if(before>=requested)return;
            var capacity=BasePostLoad.ReadResourceCapacities(receiving,current);
            int after=requested<0?0:Math.Min(requested,kind==BaseResourceKind.Gold?capacity.Gold:capacity.Supplies);
            current.SetBaseResource(receiving,kind,after);
            int cityId=StrategyNeed(receiving.PhysicalSlot,"poison_Base_currency_City");
            var city=working.Cities.GetValueOrDefault(cityId) ?? throw StrategyMissing("poison_Base_currency_owner");
            working=working with{Cities=new(working.Cities){[cityId]=kind==BaseResourceKind.Gold
                ?city with{Gold=after}:city with{Supplies=after}}};
        }
    }

    private static void TransferStrategicPoisonBaseDistrictCurrent(SourceWorldContext current,
        BaseReference target,DistrictReference destination,int actorPhysical,
        ArmyReturnDistrictOperationQueries operation)
    {
        //The existing leaf is the full26D2E0(...,0,1), including actual
        //resident eligibility, City/Gate lists, assignment precalls, primary
        //Gate repair and old-District maintenance. The requested destination
        //is the executor's current E0, rather than an inferred primary.
        var governors=new List<ArmyReturnGovernorPacket>();
        PersonReturnDistrictTransferSource.ApplyRulerCurrent(current,actorPhysical,target,operation,
            operation.Assignment,governors,operation.ChangedAreas,operation.ChangedGates,
            _=>{},requestedDistrict:destination);
    }
}
