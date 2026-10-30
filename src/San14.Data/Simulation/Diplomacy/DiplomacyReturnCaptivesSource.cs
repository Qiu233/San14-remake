using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record DiplomacyCaptiveChoice(int OfficerId, string Name, int RansomGold);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadDiplomacyCaptiveCountCurrent(SourceWorldContext current,int ownForce,int targetForce)
        =>ReadDiplomacyCaptivePeopleCurrent(current,ownForce,targetForce).Count;

    private static IReadOnlyList<int> ReadDiplomacyCaptivePeopleCurrent(SourceWorldContext current,int ownForce,int targetForce)
    {
        var people=new List<int>();
        foreach(var entry in current.ReadRegistryReferences(RegistryKind.Person)??throw CaptiveDiplomacyMissing("Person_registry"))
        {
            int person=CaptiveDiplomacyNeed(entry?.PhysicalSlot,"Person_receiver");
            if(!IsDiplomacyCaptiveHeldCurrent(current,person,targetForce))continue;
            int raw=CaptiveDiplomacyNeed(current.ReadPersonDistrictSlot(person),"Person_E0");
            var district=current.ReadDistrictTableReference(raw<52?raw:0)??throw CaptiveDiplomacyMissing("Person_District");
            int allegiance=CaptiveDiplomacyNeed(current.ReadDistrictNativeValidity(district),"Person_District_validity")
                ?CaptiveDiplomacyNeed(current.ReadDistrictForceId(CaptiveDiplomacyNeed(district.PhysicalSlot,"District_receiver")),"District10"):0;
            if(allegiance==ownForce || CaptiveDiplomacyNeed(current.ReadOfficerAftermath(person)?.SourceKnowledge?.PreviousForceCode,
                "Person165_former_Force")==ownForce)people.Add(person);
        }
        return people;
    }

    internal IReadOnlyList<int> SortReturnCaptivesTargetsCurrent(GameState state,SourceWorldContext current,
        int ownForce,IReadOnlyList<int> targets,Func<int,int> count)
        =>targets.Select(id=>new {Id=id,Count=count(id),Relation=DRelations(state,ownForce)[id]})
            .OrderByDescending(row=>row.Count).ThenByDescending(row=>row.Relation).ThenBy(row=>row.Id).Select(row=>row.Id).ToArray();

    //6DE260: native registry order, current status/custody, then6A0AC0's
    //effective five-ability sum descending and actual identity ascending.
    internal IReadOnlyList<DiplomacyCaptiveChoice> ReadDiplomacyCaptivesCurrent(
        GameState state, SourceWorldContext current, int ownForce, int targetForce)
    {
        var result = new List<(DiplomacyCaptiveChoice Row, int Ability)>();
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        foreach (int person in ReadDiplomacyCaptivePeopleCurrent(current,ownForce,targetForce))
        {
            int id = CaptiveDiplomacyNeed(current.ReadPersonIdentity(person), "Person10");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw CaptiveDiplomacyMissing("Person_owner");
            int sum=0;
            for(int dimension=0;dimension<5;dimension++)
                sum=unchecked(sum+CaptiveDiplomacyNeed(abilities.ResolveDimension(state,id,dimension,
                    (force,effect)=>_baseCapacityInputs.ReadPanelModifier(state,force,effect)),"Person_effective_ability"));
            result.Add((new(id, owner.Name, ReadDiplomacyCaptiveRansomCurrent(state, current, person)), sum));
        }
        return result.OrderByDescending(x => x.Ability).ThenBy(x => x.Row.OfficerId).Select(x => x.Row).ToArray();
    }

    private static bool IsDiplomacyCaptiveHeldCurrent(SourceWorldContext current, int person, int targetForce)
    {
        if (!CaptiveDiplomacyNeed(current.ReadPersonNativeValidity(person), "Person_validity") ||
            CaptiveDiplomacyNeed(current.ReadPersonStatus(person), "PersonE6") != 6) return false;
        var home = BaseLookup.Find(CaptiveDiplomacyNeed(current.ReadPersonFirstLocation(person), "PersonE2"), current);
        if (CaptiveDiplomacyNeed(current.ReadBaseForceSlot(home), "holding_Base_Force") != targetForce) return false;
        var position = PersonPositionRepair.DecodeSecondLocation(CaptiveDiplomacyNeed(current.ReadPersonSecondLocation(person), "PersonE4"));
        return position.Kind is 5 or 6 or 27;
    }

    internal void RequireReturnCaptivesSubmissionCurrent(GameState state, SourceWorldContext current, PrepareDiplomaticOrder order)
    {
        var ids = order.RequestedCaptiveOfficerIds;
        if (ids.IsDefault || ids.Length is < 1 or > 3 || ids.Distinct().Count() != ids.Length)
            throw DiplomacyCommandSource.Invalid("return_captives_selection");
        int gold = 0;
        foreach (int id in ids)
        {
            int person=DiplomacyLookupPerson(current,id);
            if(!IsDiplomacyCaptiveHeldCurrent(current,person,order.TargetForceId))
                throw DiplomacyCommandSource.Invalid("return_captives_candidate");
            int district=CaptiveDiplomacyNeed(current.ReadPersonDistrictSlot(person),"selected_Person_E0");
            var reference=current.ReadDistrictTableReference(district<52?district:0)??throw CaptiveDiplomacyMissing("selected_District");
            int allegiance=CaptiveDiplomacyNeed(current.ReadDistrictNativeValidity(reference),"selected_District_validity")
                ?CaptiveDiplomacyNeed(current.ReadDistrictForceId(CaptiveDiplomacyNeed(reference.PhysicalSlot,"selected_District_receiver")),"selected_District10"):0;
            if(allegiance!=order.ForceId && CaptiveDiplomacyNeed(current.ReadOfficerAftermath(person)?.SourceKnowledge?.PreviousForceCode,
                "selected_Person165")!=order.ForceId)throw DiplomacyCommandSource.Invalid("return_captives_allegiance");
            gold=unchecked(gold+ReadDiplomacyCaptiveRansomCurrent(state,current,person));
        }
        //6CE190 discards the editable gift field and writes this actual sum
        //into the initializer's gold operand. The order carries that quote;
        //the formal writer verifies it against the same current calculation.
        if (order.GiftGold != gold || order.GiftSupplies != 0 || order.GiftItemId is not null || order.GiftAreaId is not null)
            throw DiplomacyCommandSource.Invalid("return_captives_ransom");
    }

    internal PersonnelDiplomacyTask RetainReturnCaptivesCurrent(SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        //1E81E0 removes invalid payloads in place. It does not reapply the
        //initial allegiance gate or substitute a newly found captive.
        return task with { RequestedCaptiveOfficerIds = task.RequestedCaptiveOfficerIds
            .Where(id => IsDiplomacyCaptiveHeldCurrent(current, DiplomacyLookupPerson(current,id),task.TargetForceId)).ToImmutableArray() };
    }

    //26B070's bare ability bytes, qualified tactics, first-five personality
    //slots and personal policy definition. No UI rank or effective ability
    //is substituted for its raw five-byte cost basis.
    internal int ReadDiplomacyCaptiveRansomCurrent(GameState state, SourceWorldContext current, int person)
    {
        int id = CaptiveDiplomacyNeed(current.ReadPersonIdentity(person), "ransom_Person10");
        var owner = state.Officers.GetValueOrDefault(id) ?? throw CaptiveDiplomacyMissing("ransom_Person_owner");
        var values = owner.BaseAbilities ?? throw CaptiveDiplomacyMissing("ransom_raw_abilities");
        int[] raw = [values.Leadership, values.Strength, values.Intelligence, values.Politics, values.Charm];
        if (raw.Any(value => value is < 0 or > 255)) throw CaptiveDiplomacyMissing("ransom_raw_byte_domain");
        int a = raw.Count(value => value >= ReturnRule(state,"ransom-high-threshold"));
        int b = raw.Count(value => value >= ReturnRule(state,"ransom-middle-threshold"));
        int c = raw.Count(value => value >= ReturnRule(state,"ransom-low-threshold"));
        bool unique = false;
        for (int index = 0; index < 10; index++)
        {
            var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current,id,person,index);
            if (!CaptiveDiplomacyNeed(current.ReadSkillTacticValidity(tactic),"ransom_Tactic_validity")) continue;
            int physical = CaptiveDiplomacyNeed(tactic.PhysicalSlot,"ransom_Tactic_receiver");
            if (physical is >=31 and <=130 or >=140 and <=200) { unique = true; break; }
        }
        int traits = 0, releaseTrait = 0;
        for (int index = 0; index < 5; index++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current,person,index)
                ?? throw CaptiveDiplomacyMissing("ransom_Personality_lookup");
            if (!CaptiveDiplomacyNeed(current.ReadSkillPersonalityValidity(trait),"ransom_Personality_validity")) continue;
            if (CaptiveDiplomacyNeed(current.ReadPersonalityByte(trait,0xBE),"ransom_Personality_category") == 3) traits++;
            for (int pair = 0; pair < 2; pair++)
            {
                if (CaptiveDiplomacyNeed(current.ReadPersonalityByte(trait,pair==0?0xB6:0xBA),"ransom_Personality_effect") != 15) continue;
                releaseTrait = unchecked(releaseTrait + Math.Clamp((int)unchecked((short)CaptiveDiplomacyNeed(
                    current.ReadPersonalityWord(trait,pair==0?0xB8:0xBC),"ransom_Personality_value")),-100,2000));
                break;
            }
        }
        int policyRaw = owner.PolicyId ?? (owner.PolicyBaseCost.HasValue ? 0 : throw CaptiveDiplomacyMissing("ransom_personal_policy"));
        int policy = CaptiveDiplomacyNeed(current.ReadStaticTableReference(0x78D50,policyRaw <121?policyRaw:0)?.PhysicalSlot,"ransom_policy_receiver");
        int policyBonus = 0;
        if (policy != 0)
        {
            informationPolicies ??= _repository.LoadPolicies(_scenario.FixedDataSource.SourcePath);
            var row = informationPolicies.Catalog.Policies[policy];
            //23F160 serializes A0,A1,A2 in order after its eight effect bytes.
            //The normal fixed reader retains these four bytes at144..147.
            if (row.RawTailBytes.Length < 2) throw CaptiveDiplomacyMissing("ransom_policy_flags");
            if ((row.RawTailBytes[1]&1)!=0) policyBonus=ReturnRule(state,"ransom-special-policy-addition");
        }
        int sum = raw.Aggregate(0,(total,value)=>unchecked(total+value));
        sum = unchecked(sum+a*ReturnRule(state,"ransom-high-addition")+b*ReturnRule(state,"ransom-middle-addition")+
            c*ReturnRule(state,"ransom-low-addition")+traits*ReturnRule(state,"ransom-trait-addition")+policyBonus+
            (unique?ReturnRule(state,"ransom-unique-tactic-addition"):0));
        int cost = Math.Max(ReturnRule(state,"ransom-minimum"),unchecked(sum*ReturnRule(state,"ransom-scale"))/10);
        return unchecked(cost+(releaseTrait!=0?ReturnRule(state,"ransom-release-trait-addition"):0));
    }

    internal GameState ApplyReturnCaptivesNumericCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, bool accepted, List<CampaignEffectRequest> effects,
        NativeMapSeasonDate? operationDate)
    {
        var q = new DiplomacyQuery(this,state,current,task);
        if (!accepted)
        {
            state=ApplyDiplomacyRelationCurrent(state,current,q.OwnOrdinal,q.OtherOrdinal,ReturnRule(state,"failure-adjustment"),effects);
            return DApplyFailureCooldown(state,current,q,"return-captives-failure-cooldown-days");
        }
        var sampler=ReadArmyDestructionSampler(state,current)??throw CaptiveDiplomacyMissing("release_sampler");
        foreach(int id in task.RequestedCaptiveOfficerIds)
            state=ApplyCaptiveReleaseCurrent(state,current,DiplomacyLookupPerson(current,id),operationDate,
                ()=>sampler.Day450,effects,sampler,reportRelease:false);
        int initial=q.OfferValue(task.InitialOffer),final=q.OfferValue(offer);
        int scale=ReturnRule(state,"relation-value-scale");
        if(scale==0)throw CaptiveDiplomacyMissing("relation_value_divisor");
        int addition=ReturnRule(state,"success-relation-addition");
        //The two native integer comparisons are intentionally independent;
        //their narrow neutral interval must not be replaced by a ratio float.
        if(unchecked(initial*100)/scale<=final)
        {
            int boundary=unchecked(initial*scale)/100;
            addition=final>boundary?ReturnRule(state,"success-relation-addition"):0;
        }
        int before=q.Relation(q.OwnOrdinal,q.OtherOrdinal);
        int after=Math.Max(ReturnRule(state,"success-relation-floor"),unchecked(before+addition));
        return ApplyDiplomacyRelationCurrent(state,current,q.OwnOrdinal,q.OtherOrdinal,unchecked(after-before),effects);
    }

    private sealed partial class DiplomacyQuery
    {
        internal ImmutableArray<int> RequestedCaptives { get; set; }=[];
        private int ReturnCaptivesCost()
        {
            int cost=0;
            foreach(int id in RequestedCaptives)
            {
                int person=DiplomacyLookupPerson(Current,id);
                if(Valid(person))cost=unchecked(cost+package.ReadDiplomacyCaptiveRansomCurrent(State,Current,person));
            }
            int relation=Relation(OwnOrdinal,OtherOrdinal);
            int threshold=ReturnRule(State,"cost-relation-threshold")+ReturnRule(State,"success-relation-floor");
            if(relation<threshold)
                cost=unchecked(cost+unchecked((int)(((long)threshold-relation)*
                    ((long)Parameter("politics-baseline")-Politics(true))/10)));
            return cost;
        }
        internal int ReturnCaptivesProbability(DiplomacyOffer offer)
        {
            int baseValue=OfferValue(offer),value=unchecked((Trait()+100)*baseValue)/100;
            int policy=ForcePolicyScratchCurrent.ReadBeforeFreedom(State,Current,Own,0x17,true).Value;
            if(policy>0)value=unchecked(value+Math.Max(1,unchecked(policy*baseValue*Parameter("policy-offer-scale"))/100));
            if(value==0)return 0;
            int cost=ReturnCaptivesCost();
            return DDiv(unchecked((value-DDiv(unchecked(cost*ReturnRule(State,"rate-cost-multiplier")),
                ReturnRule(State,"rate-cost-divisor"),"return_captives_rate_cost"))*ReturnRule(State,"rate-scale")),value,"return_captives_rate_value");
        }
    }

    private static int ReturnRule(GameState state,string name)=>ReadExtendedDiplomacyRuleCurrent(state,"return-captives-"+name);
    private static T CaptiveDiplomacyNeed<T>(T? value,string field)where T:struct=>value??throw CaptiveDiplomacyMissing(field);
    private static InvalidDataException CaptiveDiplomacyMissing(string field)=>new("diplomacy.return-captives."+field);
}
