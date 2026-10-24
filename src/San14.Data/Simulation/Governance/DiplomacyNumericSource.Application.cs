using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //81E4C0 dispatches the mission writer before settling the signed final
    //offer against the original escrow. The caller owns publication/export.
    public GameState ApplyDiplomacyNumericCurrent(GameState state,SourceWorldContext current,
        PersonnelDiplomacyTask task,DiplomacyOffer offer,bool accepted,List<CampaignEffectRequest> effects,
        NativeMapSeasonDate? operationDate = null)
    {
        RequireDiplomacyItemOfferCurrent(state,current,task,offer);
        var q=new DiplomacyQuery(this,state,current,task);
        var sourceHome=DRulerHome(q,q.Own,out int ownRuler);var targetHome=DRulerHome(q,q.Other,out int otherRuler);
        //The independent native result helper repeats both ruler/home getters.
        _=DRulerHome(q,q.Own);_=DRulerHome(q,q.Other);
        GameState working=state;

        if(task.NativeKind==6)
            working=ApplyDiplomacyCancelAllianceCurrent(working,current,task,effects);
        if(task.NativeKind==4)
        {
            int delta;
            if(accepted)
            {
                int politics=q.Politics(false),value=q.OfferValue(offer),relation=q.Relation(q.OwnOrdinal,q.OtherOrdinal);
                int cost=q.GoodwillCost();
                int extra=Math.Min(q.Parameter("goodwill-extra-gain-cap"),DDiv(unchecked(Math.Max(0,unchecked(value-cost))*10),
                    unchecked(q.Parameter("politics-baseline")-politics),"goodwill_gain_politics"));
                //ASM822A97/822AC6 explicitly supply default100 to both8 queries.
                var own=PersonReturnPathSource.ReadForceAdvantage(working,current,q.Own,8,100);
                int ownValue=DNeed(own.Value,own.UnresolvedOwner??"goodwill_own_Advantage8");
                var other=PersonReturnPathSource.ReadForceAdvantage(working,current,q.Other,8,100);
                int otherValue=DNeed(other.Value,other.UnresolvedOwner??"goodwill_other_Advantage8");
                float factor=(own.QualifiedStates.Length!=0?ownValue:otherValue)/100f;
                int flat=q.Parameter("goodwill-flat-gain");
                if(q.OtherOrdinal is >=46 and <=50 && relation<q.Parameter("goodwill-special-relation-threshold"))
                {
                    int threshold=DDiv(unchecked(relation+q.Parameter("goodwill-special-base")),q.Parameter("goodwill-special-relation-divisor"),"special_relation");
                    extra=Math.Min(q.Parameter("goodwill-special-extra-cap"),DDiv(unchecked(Math.Max(0,threshold)*value),q.Parameter("goodwill-special-gift-divisor"),"special_gift"));
                    factor=MathF.Ceiling(factor);flat=q.Parameter("goodwill-special-base");
                }
                int gain=DCvtt(flat*factor);
                delta=unchecked(Math.Max(flat,unchecked(unchecked(gain+extra)+relation))-relation);
            }
            else delta=q.Parameter("goodwill-failure-adjustment"); //Current BSS, never virtual zero.
            working=ApplyDiplomacyRelationCurrent(working,current,q.OwnOrdinal,q.OtherOrdinal,delta,effects);
            if(!accepted)working=DApplyFailureCooldown(working,current,q,"goodwill-failure-cooldown-days");
        }
        else if(task.NativeKind==5)
        {
            if(accepted)
            {
                int before=ReadReportForceRelationByte(working,q.Own,q.OtherOrdinal,DiplomacyMissing)&0x7f;
                working=DSetAllianceMonths(working,q.OwnOrdinal,q.OtherOrdinal,unchecked(before+task.AllianceMonths));
                int relation=q.Relation(q.OwnOrdinal,q.OtherOrdinal);
                int next=Math.Max(q.Parameter("alliance-relation-floor"),unchecked(relation+q.Parameter("alliance-success-extra-relation")));
                working=ApplyDiplomacyRelationCurrent(working,current,q.OwnOrdinal,q.OtherOrdinal,unchecked(next-relation),effects);
                if(DAllianceDistrictObjectives(working,current,q.OwnOrdinal,q.OtherOrdinal))
                    working=WriteDiplomacyDistrictObjectiveNoticeCurrent(working,current,effects);
            }
            else
            {
                working=ApplyDiplomacyRelationCurrent(working,current,q.OwnOrdinal,q.OtherOrdinal,q.Parameter("alliance-failure-adjustment"),effects);
                working=DApplyFailureCooldown(working,current,q,"alliance-failure-cooldown-days");
            }
        }
        else if(task.NativeKind==9)
        {
            //1EE4F0 is read on both success and failure.
            var attack=q.AttackBase();
            if(accepted)
            {
                var target=working.Forces.GetValueOrDefault(q.OtherOrdinal)??throw DiplomacyNumericMissing("attack_recipient_domain");
                var objective=target.CombatObjective??throw DiplomacyNumericMissing("attack_recipient_objective");
                int location=DNeed(current.ReadBaseObjectLink(attack),"attack_Base4E");
                int months=q.Parameter("attack-duration-months");
                int alliance=ReadReportForceRelationByte(working,q.Own,q.OtherOrdinal,DiplomacyMissing)&0x7f;
                if(alliance!=0)months=Math.Min(months,alliance);
                working=working with {Forces=new(working.Forces){[target.Id]=target with
                {CombatObjective=objective with {RequestingForceId=Math.Clamp(q.OwnOrdinal,0,51),
                    TargetLocationId=Math.Min(location,61),RemainingMonths=Math.Clamp(months,0,60),Progress=0}}}};
                int relation=q.Relation(q.OwnOrdinal,q.OtherOrdinal);
                int next=Math.Max(q.Parameter("attack-relation-floor"),unchecked(relation+q.Parameter("attack-relation-gain")));
                working=ApplyDiplomacyRelationCurrent(working,current,q.OwnOrdinal,q.OtherOrdinal,unchecked(next-relation),effects);
            }
            else
            {
                working=ApplyDiplomacyRelationCurrent(working,current,q.OwnOrdinal,q.OtherOrdinal,q.Parameter("attack-failure-adjustment"),effects);
                working=DApplyFailureCooldown(working,current,q,"attack-failure-cooldown-days");
            }
        }
        if(accepted)
        {
            working=DApplyAcceptedDiplomaticItemHead(working,current,task,offer,ownRuler,otherRuler);
            working=DAddDiplomacyResource(working,current,targetHome,BaseResourceKind.Gold,offer.Gold);
            working=DAddDiplomacyResource(working,current,sourceHome,BaseResourceKind.Gold,unchecked(task.InitialOffer.Gold-offer.Gold));
            working=DAddDiplomacyResource(working,current,targetHome,BaseResourceKind.Supplies,offer.Supplies);
            working=DAddDiplomacyResource(working,current,sourceHome,BaseResourceKind.Supplies,unchecked(task.InitialOffer.Supplies-offer.Supplies));
            working=DApplyAcceptedDiplomaticItemRefund(working,current,task,offer,ownRuler);
            working=ApplyDiplomacyAreaOfferSettlementCurrent(working,current,task,offer,true,effects,operationDate);
        }
        else
        {
            working=DApplyRejectedDiplomaticItem(working,current,task,ownRuler);
            working=DAddDiplomacyResource(working,current,sourceHome,BaseResourceKind.Gold,task.InitialOffer.Gold);
            working=DAddDiplomacyResource(working,current,sourceHome,BaseResourceKind.Supplies,task.InitialOffer.Supplies);
        }
        return working;
    }
    private static BaseReference DRulerHome(DiplomacyQuery q,ForceReference force)
        =>BaseLookup.Find(DNeed(q.Current.ReadPersonFirstLocation(q.Ruler(force)),"ruler_homeE2"),q.Current);
    private static BaseReference DRulerHome(DiplomacyQuery q,ForceReference force,out int rulerId)
    {
        int person=q.Ruler(force);rulerId=q.Id(person);
        return BaseLookup.Find(DNeed(q.Current.ReadPersonFirstLocation(person),"ruler_homeE2"),q.Current);
    }

    private static GameState DSetAllianceMonths(GameState state,int first,int second,int months)
    {
        var native=state.NativeArmySimulation??throw DiplomacyNumericMissing("alliance_native_owner");
        var forces=new Dictionary<int,NativeArmyForceState>(native.Forces);
        foreach(var (self,other) in new[]{(first,second),(second,first)})
        {
            var owner=forces.GetValueOrDefault(self)??throw DiplomacyNumericMissing("alliance_Force_owner");
            var values=owner.RelationsEA is {Length:52} ea?ea.ToArray():throw DiplomacyNumericMissing("alliance_RelationsEA");
            values[other]=(byte)((values[other]&0x80)|Math.Clamp(months,0,48));
            forces[self]=owner with{RelationsEA=values};
        }
        return state with{NativeArmySimulation=native with{Forces=forces}};
    }
    private static GameState DApplyFailureCooldown(GameState state,SourceWorldContext current,DiplomacyQuery q,string daysKey)
    {
        var player=OwnedCurrentPlayerForce.ReadCurrentReference(state,current)??throw DiplomacyNumericMissing("cooldown_player");
        int playerId=DNeed(current.ReadForcePhysicalSlot(player),"cooldown_player60");
        if(playerId==0 || playerId!=q.OtherOrdinal)return state;
        var date=state.NativeMapSeason?.ThirdDate??throw DiplomacyNumericMissing("cooldown_WorldDate34");
        int ordinal=unchecked(PersonReturnCommands.CalendarOrdinal(date)+q.Parameter(daysKey));
        short year=unchecked((short)(ordinal/360));int remaining=Math.Max(0,unchecked(ordinal-year*360));
        var after=new NativeMapSeasonDate(year,unchecked((sbyte)(remaining/30+1)),unchecked((sbyte)(remaining%30+1)),0);
        var native=state.NativeArmySimulation??throw DiplomacyNumericMissing("cooldown_native_owner");
        var owner=native.Forces.GetValueOrDefault(q.OwnOrdinal)??throw DiplomacyNumericMissing("cooldown_Force_owner");
        return state with{NativeArmySimulation=native with{Forces=new(native.Forces){[q.OwnOrdinal]=owner with{DiplomacyCooldownDate=after}}}};
    }
    private static GameState DAddDiplomacyResource(GameState state,SourceWorldContext current,BaseReference home,BaseResourceKind resource,int amount)
    {
        //027300/169890: each Gate capacity/read/set virtual independently
        //routes to22A940's current City. Gate troop/morale stocks stay local.
        var capacityCity=BaseCurrencyAdditionSource.CurrencyCity(home,current);
        int firstCap=resource==BaseResourceKind.Gold?BasePostLoad.ReadResourceCapacities(capacityCity,current).Gold:BasePostLoad.ReadResourceCapacities(capacityCity,current).Supplies;
        int before=ReadDiplomacyBaseResourceCurrent(current,home,resource);
        int requested=Math.Max(0,Math.Min(unchecked(before+amount),firstCap));
        var target=BaseCurrencyAdditionSource.CurrencyCity(home,current);
        int setterCap=resource==BaseResourceKind.Gold?BasePostLoad.ReadResourceCapacities(target,current).Gold:BasePostLoad.ReadResourceCapacities(target,current).Supplies;
        int after=Math.Max(0,Math.Min(requested,setterCap));
        int id=DNeed(target.PhysicalSlot,"settlement_City_pointer");
        var city=target.Kind==BaseKind.City?state.Cities.GetValueOrDefault(id):null;
        if(city is null)throw DiplomacyNumericMissing("settlement_City_owner");
        current.SetBaseResource(target,resource,after);
        return state with{Cities=new(state.Cities){[id]=resource==BaseResourceKind.Gold?city with{Gold=after}:city with{Supplies=after}}};
    }
    private static bool DAllianceDistrictObjectives(GameState state,SourceWorldContext current,int first,int second)
    {
        bool notice=false;
        foreach(var entry in current.ReadArmyReturnRegisteredDistricts()??throw DiplomacyNumericMissing("alliance_District_registryD0"))
        {
            var district=entry??throw DiplomacyNumericMissing("alliance_District_payload");
            int id=DNeed(district.PhysicalSlot,"alliance_District_pointer");
            int owner=DNeed(current.ReadDistrictForceId(id),"alliance_District10");
            if(owner!=first&&owner!=second)continue;
            int kind=DNeed(current.ReadDistrictRawByte(id,0x19),"alliance_District19");
            bool match=false;
            if(kind is 7 or 2 or 5 or 6)
            {
                int target=DNeed(current.ReadDistrictRawByte(id,0x18),"alliance_District18");
                if(kind==7)match=target==first||target==second;
                else
                {
                    BaseReference location=kind==2?BaseLookup.Find(target,current):kind==5?
                        current.ReadCityTableReference(target<52?target:0)??throw DiplomacyNumericMissing("alliance_target_City"):
                        current.ReadGateTableReference(target<11?target:0)??throw DiplomacyNumericMissing("alliance_target_Gate");
                    int force=DNeed(current.ReadBaseForceSlot(location),"alliance_target_BaseB0");
                    match=force==first||force==second;
                }
            }
            if(!match)continue;
            GateCaptureRoutingSource.Clear2EE1F0(current,id);
            //233880 means player-owned District, not native validity.
            var player=OwnedCurrentPlayerForce.ReadCurrentReference(state,current)??throw DiplomacyNumericMissing("alliance_player_Force");
            if(!DNeed(current.ReadForceNativeValidity(player),"alliance_player_validity"))continue;
            int playerId=DNeed(current.ReadForcePhysicalSlot(player),"alliance_player60");
            if(owner!=playerId)continue;
            ushort leader=DNeed(current.ReadDistrictLeader(id),"alliance_District12");
            int person=DNeed((current.ReadPersonLookupReference(leader<=6000?leader:0)??throw DiplomacyNumericMissing("alliance_District_leader_pointer")).PhysicalSlot,"alliance_District_leader");
            if(!DNeed(current.ReadPersonNativeValidity(person),"alliance_District_leader_validity")||
                DNeed(current.ReadPersonStatus(person),"alliance_District_leaderE6")!=1)notice=true;
        }
        return notice;
    }
    private static int DCvtt(float value)=>!float.IsFinite(value)||value<int.MinValue||value>=2147483648f?int.MinValue:(int)value;
}
