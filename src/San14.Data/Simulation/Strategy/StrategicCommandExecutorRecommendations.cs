using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //796550 refreshes recommendation and duration when target selection
    //changes. Its pure sampler neither advances App randomness nor writes a Person.
    private IReadOnlyList<StrategicCommandCandidate> ReadStrategicExecutorRecommendationsCurrent(
        StrategicCommandRules rules,int? targetId,int? secondForceId)
    {
        var current=rules.Current;var state=rules.State;
        int accuracy=0;
        int advisorRaw=StrategyNeed(current.ReadAssignmentForceAdvisor52(rules.ForceId),"recommend_Force52");
        var advisor=current.ReadPersonLookupReference(advisorRaw<=6000?advisorRaw:0) ?? throw StrategyMissing("recommend_Advisor_pointer");
        int advisorPhysical=StrategyNeed(advisor.PhysicalSlot,"recommend_Advisor_physical");
        var abilities=SearchAbilities(current);
        var abilityCache=new Dictionary<int,int>();
        int Intelligence(int identity)
        {
            if(!abilityCache.TryGetValue(identity,out int value))
                abilityCache[identity]=value=StrategyNeed(abilities.ResolveDimension(state,identity,2,
                    (force,effect)=>_baseCapacityInputs.ReadPanelModifier(state,force,effect)),"recommend_Intelligence");
            return value;
        }
        if(StrategyNeed(current.ReadPersonNativeValidity(advisorPhysical),"recommend_Advisor_validity"))
        {
            int ability=Intelligence(StrategyNeed(current.ReadPersonIdentity(advisorPhysical),"recommend_Advisor_identity"));
            accuracy=Math.Clamp(unchecked(60+Math.Max(0,ability-70)+Math.Max(0,ability-90)),0,100);
        }
        var target=rules.Targets.FirstOrDefault(row=>row.Id==targetId);
        bool complete=target is not null && (rules.Kind!=10 || secondForceId is not null);
        int salt=complete ? target!.Id:0;
        var sampler=ReadArmyDestructionSampler(state,current) ?? throw StrategyMissing("recommend_sampler");
        var result=new List<StrategicCommandCandidate>();
        var journeys=new Dictionary<int,int>();
        foreach(var person in rules.Executors)
        {
            try
            {
                bool accurate=TacticEffectSourceSamplerMath.Sample(100,10,person.Id,salt,0,0,sampler)<accuracy;
                bool predicted=complete && ResolveStrategicCommandTrialCurrent(state,current,rules.Kind,
                    person.PhysicalSlot,target!.PhysicalSlot,secondForceId,0,0)!=0;
                int days=0;
                if(target is not null)
                {
                    int home=StrategyNeed(current.ReadPersonFirstLocation(person.PhysicalSlot),"recommend_executor_E2");
                    if(!journeys.TryGetValue(home,out days))
                    {
                        var identity=new StrategicCommandTarget(rules.Kind switch {
                            10=>StrategicCommandTargetKind.Force,11=>StrategicCommandTargetKind.City,
                            12 or 24=>StrategicCommandTargetKind.Person,_=>StrategicCommandTargetKind.Area},target.Id);
                        days=StrategicCommandTravelSource.Resolve(state,current,person.PhysicalSlot,identity,secondForceId,
                            state.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("recommend_date")).TravelDays;
                        journeys[home]=days;
                    }
                }
                result.Add(person with {Recommended=predicted==accurate,TravelDays=days});
            }
            catch(InvalidDataException error){result.Add(person with{Recommended=null,TravelDays=null,UnresolvedOwner=error.Message});}
        }
        StrategySort(result,(left,right)=>
        {
            if(left.Recommended!=right.Recommended)return left.Recommended==true;
            if(left.TravelDays!=right.TravelDays)return left.TravelDays<right.TravelDays;
            int a=Intelligence(left.Id),b=Intelligence(right.Id);return a!=b?a>b:left.Id<right.Id;
        });
        return result.AsReadOnly();
    }
}
