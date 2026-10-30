using San14.Core;

namespace San14.Data;

public sealed record DiplomacyTrialResult(bool Accepted, int Probability, int? Sample, string Branch);
public sealed record DiplomacyRelationCurrent(int Relation, int Level, int AllianceMonths, bool UpperFlag);

public sealed partial class ArmyCampaignRuleAdapter
{
    //322B80 is the unbounded baseline. Preview is deliberately separate from
    //3254D0: a computed100 still samples, while its explicit shortcuts do not.
    public int ReadDiplomacyProbabilityCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer)
        => new DiplomacyQuery(this,state,current,task).Probability(offer);

    public DiplomacyTrialResult ResolveDiplomacyTrialCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, Func<TacticSourceSamplerWorld> readSampler)
        => ResolveDiplomacyTrialQuery(new DiplomacyQuery(this,state,current,task),offer,null,readSampler);

    //3254D0(param7=1). The advisor already evaluated322B80 before this
    //shortcut pass.807BD0's separate budget writer is not part of this query.
    private DiplomacyTrialResult ResolveDiplomacyTrialQuery(DiplomacyQuery query,DiplomacyOffer offer,
        int? initialProbability,Func<TacticSourceSamplerWorld> readSampler)
    {
        if(query.NativeKind==6)return new(true,100,null,"cancel-alliance");
        if(query.NativeKind==4 && query.OtherOrdinal is >=46 and <=50)
            return new(true,100,null,"special-force-goodwill");
        if(query.NativeKind==9 && query.AttackBlockReason()!=0)
            return new(false,0,null,"request-attack-blocked");
        if(query.Level(query.OwnOrdinal,query.OtherOrdinal)==5)
            return new(true,100,null,"maximum-relation");
        int probability=initialProbability ?? query.Probability(offer);
        int sample=TacticEffectSourceSamplerMath.Sample(100,1,query.OwnOrdinal,query.ExecutorId,
            query.OtherOrdinal,probability,readSampler());
        return new(sample<probability,probability,sample,"baseline-trial");
    }

    //3254D0(param7=0), after the negotiation UI has supplied its final offer.
    //No second shortcut pass and no App32 draw belong to this sampler.
    public DiplomacyTrialResult DrawDiplomacyFinalOfferCurrent(GameState state,SourceWorldContext current,
        PersonnelDiplomacyTask task,DiplomacyOffer offer,Func<TacticSourceSamplerWorld> readSampler)
    {
        var query=new DiplomacyQuery(this,state,current,task);
        if(task.NativeKind==6)return new(true,100,null,"cancel-alliance");
        int probability=query.Probability(offer);
        return DrawDiplomacyFinalProbabilityCurrent(state,current,task,probability,readSampler);
    }
    public DiplomacyTrialResult DrawDiplomacyFinalProbabilityCurrent(GameState state,SourceWorldContext current,
        PersonnelDiplomacyTask task,int probability,Func<TacticSourceSamplerWorld> readSampler)
    {
        var query=new DiplomacyQuery(this,state,current,task);
        if(task.NativeKind==6)return new(true,100,null,"cancel-alliance");
        int sample=TacticEffectSourceSamplerMath.Sample(100,1,query.OwnOrdinal,query.ExecutorId,query.OtherOrdinal,
            probability,readSampler());
        return new(sample<probability,probability,sample,"negotiated-trial");
    }

    public DiplomacyRelationCurrent ReadDiplomacyRelationCurrent(GameState state,
        SourceWorldContext current,int first,int second)
    {
        var relation=DRelations(state,first);
        var force=current.ReadForceTableReference(first is >=0 and <=51?first:0)
            ?? throw DiplomacyNumericMissing("relation_Force_pointer");
        int index=second is >=0 and <=51?second:0;
        int ea=ReadReportForceRelationByte(state,force,index,DiplomacyMissing);
        int raw=relation[index];
        return new(raw,DLevel(state,raw),ea&0x7f,(ea&0x80)!=0);
    }
    private static byte[] DRelations(GameState state,int physical)
        => (physical==0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelations13 :
            state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.NormalAftermath?.Relations13)
            is {Length:52} values ? values : throw DiplomacyNumericMissing("Force_Relations13_"+physical);
    private static int DLevel(GameState state,int relation)
    {
        var bank=state.NativeArmySimulation?.Parameters ?? throw DiplomacyNumericMissing("relation_threshold_bank");
        for(int level=1;level<=5;level++)
        {
            string key=SimulationParameterKeys.ForceRelations.LevelThreshold(level);
            if(bank.Float32Values.ContainsKey(key) || !bank.Int32Values.TryGetValue(key,out int threshold))
                throw DiplomacyNumericMissing("relation_threshold_"+level);
            if(relation<threshold)return level-1;
        }
        return 5;
    }

    private sealed partial class DiplomacyQuery
    {
        private readonly ArmyCampaignRuleAdapter package;
        internal readonly GameState State;
        internal readonly SourceWorldContext Current;
        internal readonly int NativeKind,AllianceMonths;
        internal readonly int? TargetCityId;
        internal readonly int Executor;
        internal readonly ForceReference Own,Other;
        internal int OwnOrdinal=>Ordinal(Own);
        internal int OtherOrdinal=>Ordinal(Other);
        internal int ExecutorId=>Id(Executor);
        internal DiplomacyQuery(ArmyCampaignRuleAdapter package,GameState state,SourceWorldContext current,PersonnelDiplomacyTask task)
            :this(package,state,current,task.MemberIds.FirstOrDefault(),task.TargetForceId,task.NativeKind,
                task.AllianceMonths,task.TargetCityId) { RequestedCaptives=task.RequestedCaptiveOfficerIds; }
        internal DiplomacyQuery(ArmyCampaignRuleAdapter package,GameState state,SourceWorldContext current,
            int executorId,int targetForceId,int nativeKind,int allianceMonths,int? targetCityId)
        {
            this.package=package;State=state;Current=current;
            NativeKind=nativeKind;AllianceMonths=allianceMonths;TargetCityId=targetCityId;
            var parameters=state.NativeArmySimulation?.Parameters;
            if(parameters is null || parameters.Language!=package.Seed.Language ||
                !HasDiplomacyParameterProfile(parameters.Language,parameters.RuleVersion))
                throw DiplomacyNumericMissing("numeric_owner_profile");
            if(nativeKind is not (4 or 5 or 6 or 7 or 8 or 9 or 19))throw DiplomacyNumericMissing("mission_kind");
            Executor=P(current.ReadPersonLookupReference(executorId<=6000?executorId:0) ?? throw DiplomacyNumericMissing("executor_pointer"));
            Own=current.ReadPersonForce(Executor) ?? throw DiplomacyNumericMissing("executor_Force22CB40");
            Other=current.ReadForceTableReference(targetForceId is >=0 and <=51?targetForceId:0)
                ?? throw DiplomacyNumericMissing("target_Force_pointer");
        }
        internal int Parameter(string name)=>name.StartsWith("return-captives-",StringComparison.Ordinal)
            ?ReadExtendedDiplomacyRuleCurrent(State,name):DParameter(State,name);
        internal int Ordinal(ForceReference force)=>DNeed(Current.ReadForcePhysicalSlot(force),"Force_ordinal60");
        internal static int P(TransferPersonReference person)=>DNeed(person.PhysicalSlot,"Person_pointer");
        internal int Id(int person)=>DNeed(Current.ReadPersonIdentity(person),"Person_ID10");
        internal bool Valid(int person)=>DNeed(Current.ReadPersonNativeValidity(person),"Person_validity");
        internal int Relation(int first,int second)=>second<52?DRelations(State,first)[second]:0;
        internal int Level(int first,int second)=>DLevel(State,Relation(first,second));
        internal int CityCount(ForceReference force)=>DNeed(PersonArmyCaptureSource.ReadOwnedCityCount(Current,force),"Force_City_count22AB60");
        internal int Ruler(ForceReference force)
        {
            ushort id=DNeed(Current.ReadForceRecord(Ordinal(force))?.RawRuler10,"Force_ruler10");
            return P((id<=6000?Current.ReadPersonLookupReference(id):Current.ReadTransferPersonTableReference(0))
                ?? throw DiplomacyNumericMissing("ruler_Person_pointer"));
        }
        internal bool RawHit(int first,int second,bool like)
        {
            if(!Valid(second))return false;
            int id=Id(second);
            for(int i=0;i<8;i++)
                if(DNeed(like?OwnedPersonRelationInputs.ReadCurrentLikeIdentity(State,first,i):
                    OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(State,first,i),"Person_raw_relationship")==id)return true;
            return false;
        }
        internal bool Spouse(int first,int second)=>Valid(first)&&Valid(second)&&Id(first)!=Id(second)&&
            Word(first,0xBA)!=0&&Word(first,0xBA)==Id(second);
        internal bool Sworn(int first,int second)=>Valid(first)&&Valid(second)&&Id(first)!=Id(second)&&
            Word(first,0xBC)!=0&&Word(first,0xBC)==Word(second,0xBC);
        internal int Word(int person,int field)=>DNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(State,person,field),"Person_relation_"+field.ToString("X"));
        internal int Politics(bool traits)
        {
            if(!Valid(Executor))return 0;
            int ability=DNeed(new TacticRemakeCurrentAbilities(Current,package._scenario,package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language)).ResolveDimension(State,ExecutorId,3,
                    (force,effect)=>package._baseCapacityInputs.ReadPanelModifier(State,force,effect)),"executor_current_politics");
            return traits?unchecked(ability+unchecked(unchecked(Trait()*ability)*Parameter("politics-trait-scale"))/100):ability;
        }
        internal int Trait()=>!Valid(Executor)?0:DNeed(TacticRemakeCurrentTraits.CommanderEffectSum(Current,Executor,0x40),"executor_trait64");
        internal int OfferValue(DiplomacyOffer offer)
        {
            if(offer.Gold>0)return unchecked((int)((long)offer.Gold*Parameter("gold-value-percent")/100));
            if(offer.Supplies>0)return unchecked((int)((long)offer.Supplies*10/Parameter("supplies-value-divisor")));
            if(offer.ItemId is >=1 and <=201)
            {
                var items=State.Items ?? throw DiplomacyNumericMissing("offer_Item_inventory");
                var item=items.GetValueOrDefault(offer.ItemId.Value) ?? throw DiplomacyNumericMissing("offer_Item");
                int value=State.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.LoyaltyValue ?? throw DiplomacyNumericMissing("offer_Item49");
                return unchecked(Math.Min(value,50)*Parameter("item-value-multiplier"));
            }
            if(offer.AreaId is >=1 and <=500)
            {
                var area=Current.ReadAreaTableReference(offer.AreaId.Value) ?? throw DiplomacyNumericMissing("offer_Area");
                int count=DNeed(Current.ReadAreaRawField68(area),"offer_Area68");
                int hex=DNeed(Current.ReadAreaHexIndex(area),"offer_Area3A");
                var at=Current.ReadHexTableReference(hex<48400?hex:0) ?? throw DiplomacyNumericMissing("offer_Area_Hex");
                bool port=DNeed(Current.ReadHexSurfaceCode(at),"offer_Area_Hex10")==18;
                return unchecked(Parameter("area-value-base")+count*Parameter(port?"port-area-value-cell-multiplier":"area-value-cell-multiplier"));
            }
            return 0;
        }
        internal int GoodwillCost()
        {
            int politics=Politics(true),ownRuler=Ruler(Own),otherRuler=Ruler(Other);
            int dislike=RawHit(otherRuler,ownRuler,false)||RawHit(otherRuler,Executor,false)?Parameter("goodwill-disliked-relation-divisor"):1;
            int relation=Math.Min(DDiv(Relation(OwnOrdinal,OtherOrdinal),dislike,"dislike"),100);
            int cities=CityCount(Other);
            int hostility=Level(OwnOrdinal,OtherOrdinal)==0?Parameter("goodwill-hostile-cost-scale"):100;
            int discount=Spouse(Executor,otherRuler)?Parameter("goodwill-spouse-reduction"):
                Sworn(Executor,otherRuler)?Parameter("goodwill-sworn-reduction"):
                RawHit(otherRuler,Executor,true)?Parameter("goodwill-liked-reduction"):0;
            long delta=(long)Parameter("goodwill-relation-maximum")-relation;
            int term=unchecked((int)(delta*delta/Parameter("goodwill-relation-square-divisor")*hostility/100));
            int inner=unchecked((Parameter("goodwill-city-base")-DDiv(politics,Parameter("goodwill-politics-divisor"),"politics"))*cities);
            int cost=unchecked((int)(((long)inner+term)*(100-discount)/100));
            int rounding=Parameter("goodwill-cost-rounding");
            return unchecked(DDiv(cost,rounding,"goodwill_rounding")*rounding);
        }
        internal BaseReference AttackBase()=>BaseLookup.Find(TargetCityId ?? throw DiplomacyNumericMissing("attack_target_Base4E"),Current);
        internal ForceReference BaseForce(BaseReference location)=>Current.ReadForceTableReference(DNeed(Current.ReadBaseForceSlot(location),"Base_ForceB0"))??throw DiplomacyNumericMissing("Base_Force_pointer");
        internal int RelationCost(bool alliance)
        {
            int relation=Relation(OwnOrdinal,OtherOrdinal),politics=Politics(true);
            int offset=Parameter(alliance?"alliance-relation-offset":"attack-relation-offset"),floor=Parameter(alliance?"alliance-relation-floor":"attack-relation-floor");
            return relation<unchecked(offset+floor)?unchecked(unchecked(offset-relation+floor)*unchecked(Parameter("politics-baseline")-politics))/10:0;
        }
        internal int Probability(DiplomacyOffer offer)
        {
            if(NativeKind==6)return 100;
            if(NativeKind==8)return ReturnCaptivesProbability(offer);
            if(NativeKind is 7 or 19)return 0; //322B80 has no percentage branch for these independent trials.
            int baseValue=OfferValue(offer),value=baseValue;
            if(NativeKind==4)value=unchecked(unchecked((Trait()+100)*baseValue)/100);
            int policy=ForcePolicyScratchCurrent.ReadBeforeFreedom(State,Current,Own,0x17,true).Value;
            if(policy>0)value=unchecked(value+Math.Max(1,unchecked(unchecked(Parameter("policy-offer-scale")*policy)*baseValue)/100));
            int cost;
            string prefix;
            if(NativeKind==4){cost=GoodwillCost();prefix="goodwill";}
            else if(NativeKind==5)
            {
                int ownCities=CityCount(Own),otherCities=CityCount(Other);
                if(ownCities<otherCities)otherCities=unchecked(otherCities*2-ownCities);
                cost=Math.Max(Parameter("alliance-cost-base"),unchecked(unchecked((AllianceMonths-Parameter("alliance-months-baseline"))*Parameter("alliance-month-cost"))+unchecked(Parameter("alliance-city-cost")*otherCities)+Parameter("alliance-cost-base")));
                cost=unchecked(cost+RelationCost(true));prefix="alliance";
            }
            else
            {
                int cities=CityCount(Other),victim=CityCount(BaseForce(AttackBase()));
                int difference=cities<victim?unchecked(victim-cities):1;
                cost=unchecked(unchecked(difference*difference)*Parameter("attack-city-square-cost")+Parameter("attack-cost-base"));
                _=OtherOrdinal;cost=unchecked(cost+RelationCost(false));prefix="attack";
            }
            if(value==0)return 0;
            return DDiv(unchecked(unchecked(value-DDiv(unchecked(Parameter(prefix+"-rate-cost-multiplier")*cost),Parameter(prefix+"-rate-cost-divisor"),"rate_cost"))*Parameter(prefix+"-rate-scale")),value,"rate_value");
        }
        //3247B0. Its total-troops call explicitly sets R8D=0 at324936.
        internal int AttackBlockReason()
        {
            int ownToOther=Relation(OwnOrdinal,OtherOrdinal);
            var attack=AttackBase();var victim=BaseForce(attack);
            int otherToVictim=Relation(OtherOrdinal,Ordinal(victim));
            int cities=CityCount(Other),foodScale=Parameter("attack-forbid-food-per-army"),troopScale=Parameter("attack-forbid-armies-per-city"),difference=Parameter("attack-forbid-relation-difference");
            int requiredMonths=Parameter("attack-minimum-alliance-months");
            int alliance=ReadReportForceRelationByte(State,Own,OtherOrdinal,DiplomacyMissing)&0x7f;
            int food=0;
            foreach(var location in ForceBases(Other))food=unchecked(food+ReadDiplomacyBaseResourceCurrent(Current,location,BaseResourceKind.Supplies));
            int months=State.Forces.GetValueOrDefault(OtherOrdinal)?.CombatObjective?.RemainingMonths ?? throw DiplomacyNumericMissing("request_Force161");
            if(months!=0)return 0x34A3;
            if(Level(OtherOrdinal,Ordinal(victim))==5)return 0x34A4;
            if(unchecked(otherToVictim-ownToOther)>=difference)return 0x34A5;
            int troops=TotalTroops(Other);
            if(troops<=unchecked(troopScale*cities)||food<=unchecked(unchecked(foodScale*troopScale)*cities))return 0x34A6;
            return alliance<=requiredMonths?0x34A7:0;
        }
        internal IEnumerable<BaseReference> ForceBases(ForceReference force)
        {
            int ordinal=Ordinal(force);
            if(ordinal is <1 or >51)yield break;
            foreach(var location in Current.ReadRegisteredBases()??throw DiplomacyNumericMissing("Force_foothold_registry80"))
            {
                var at=location??throw DiplomacyNumericMissing("Force_foothold_payload");
                if(DNeed(Current.ReadBaseForceSlot(at),"Force_foothold_B0")==ordinal)yield return at;
            }
        }
        internal int TotalTroops(ForceReference force)
        {
            if(!DNeed(Current.ReadForceNativeValidity(force),"total_troops_Force_validity"))return 0;
            int result=0,ordinal=Ordinal(force);
            foreach(var location in ForceBases(force))result=unchecked(result+DNeed(Current.ReadBaseResource(location,BaseResourceKind.Troops),"Force_foothold_ready3C"));
            foreach(var item in Current.ReadRegistryReferences(RegistryKind.Army)??throw DiplomacyNumericMissing("Force_Army_registryC0"))
            {
                var entry=item??throw DiplomacyNumericMissing("Force_Army_payload");
                int physical=DNeed(entry.PhysicalSlot,"Force_Army_pointer");var army=new ArmyUnitReference(physical);
                if(Current.ReadArmyUnitRawForceSlot(army)!=ordinal)continue;
                if(DNeed(Current.ReadOwnedArmyExclusion233560(entry),"Force_Army_233560"))continue;
                result=unchecked(result+DNeed(Current.ReadArmyReturnResourceFields(army)?.Ready16,"Force_Army_ready16"));
            }
            return result;
        }
    }
}
