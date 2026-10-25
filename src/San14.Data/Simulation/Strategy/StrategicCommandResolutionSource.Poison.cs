using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class StrategicResolutionCurrent
    {
        private int ResolvePoison()
        {
            int executor=Executor,area=Target;var own=Force(executor);var other=AreaForce(area);
            int result=SavedResult(Trial());
            PoisonHistory(executor,area,own,other,result);
            if(result==1)
            {
                var region=new AreaReference(area);
                int hexId=StrategyNeed(current.ReadAreaHexIndex(region),"poison_Area3A");
                var center=current.ReadHexTableReference(hexId<48400?hexId:0) ?? throw StrategyMissing("poison_center_Hex");
                bool capital=PoisonCityCenter(region,hexId);
                bool port=StrategyNeed(current.ReadHexSurfaceCode(center),"poison_center_surface")==17;
                if(!capital)PoisonAssignCity(area,own);
                ushort governor=StrategyNeed(current.ReadAssignmentArea36(area),"poison_Area36");
                if(governor!=0)
                {
                    current.SetAssignmentArea36(area,0);
                    var removed=PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current,area,governor);
                    if(removed.UnresolvedOwner is string missing)throw StrategyMissing(missing);
                    if(removed.PersonPhysical is int person)
                        State=PersonAssignmentPrecallInputs.PublishDeathTouched(State,current,person,[],publishVector:true);
                }
                current.SetAssignmentArea72(area,0);current.SetAssignmentArea6D(area,0);
                State=PersonAssignmentPrecallInputs.PublishAreaAppointments(State,current,[],[area],[]);
                var native=State.NativeArmySimulation ?? throw StrategyMissing("poison_assignment_owner");
                var parameters=new Dictionary<string,int>(native.Parameters.Int32Values)
                    {[PersonAssignmentPrecallInputs.AreaFieldKey(area,0x6D)]=0};
                State=State with{NativeArmySimulation=native with{Parameters=native.Parameters with{Int32Values=parameters}}};
                if(capital || port)
                {
                    int link=StrategyNeed(current.ReadHexObjectLink(center),"poison_neutral_Hex1A");
                    var target=BaseLookup.Find(link<62?link:0,current);
                    State=package.ApplyStrategicPoisonNeutralBaseTransferCurrent(State,current,target,own,executor,
                        State.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("poison_neutral_source_date"),Effects);
                }
                else
                {
                int force=ForceId(own);
                var changed=new HashSet<int>();
                PoisonTransferHex(center,force,changed);
                var list=current.CreateTemporaryHexList() ?? throw StrategyMissing("poison_radius_list");
                try
                {
                    BaseNeighborhoodInitialization.PopulateRadiusOne(center,list,current);
                    var node=current.ReadHexListHead(list) ?? throw StrategyMissing("poison_radius_head");
                    while(node.Identity is not null)
                    {
                        var hex=current.ReadHexListNodePayload(node) ?? throw StrategyMissing("poison_radius_Hex");
                        int before=StrategyNeed(current.ReadHexForceSlot(hex),"poison_radius_owner");
                        if(before!=force && !new DiplomacyCommandSource(State,current).HasTreaty(before,force) &&
                            GateCaptureTerritorySource.Eligible(State,current,hex,force,requireNoArmy:true))
                            PoisonTransferHex(hex,force,changed);
                        node=current.ReadHexListNodeNext(node) ?? throw StrategyMissing("poison_radius_next");
                    }
                }
                finally{current.ClearTemporaryHexList(list);current.ReleaseTemporaryHexList(list);}
                if(changed.Count!=0)
                {
                    var territory=new Dictionary<int,TerritoryState>(State.Territory);
                    foreach(int hex in changed)
                    {
                        var before=territory.GetValueOrDefault(hex) ?? throw StrategyMissing("poison_Territory_owner");
                        territory[hex]=before with{ForceId=force};
                    }
                    State=State with{Territory=territory};
                }
                //The representative determines the final Area owner. Adjacent
                //centers can also have changed; publish only the reached set.
                var areas=new Dictionary<int,AreaState>(State.Areas);
                foreach(int id in changed.Select(h=>(int)StrategyNeed(current.ReadHexAreaSlot(new(h)),"poison_changed_Area")).Append(area).Distinct())
                {
                    var row=areas.GetValueOrDefault(id) ?? throw StrategyMissing("poison_Area_owner");
                    int owner=StrategyNeed(current.ReadAreaCurrentForceSlot(new(id)),"poison_final_Area_Force");
                    areas[id]=row with{ForceId=owner==0?null:owner};
                }
                State=State with{Areas=areas};
                }
            }
            else if(result==0)
            {
                int trait=Trait(executor,65)>0 ? P("poison-failed-trait",1):0;
                int bonus=DifficultyBonus(own,"poison",[2,2,1,1]);
                int bound=unchecked(bonus+Divide(unchecked(trait*P("poison-failed-trait-weight",20)-
                    P("poison-failed-intelligence-offset",40)+Ability(executor)),P("poison-failed-divisor",25)));
                Security(area,-App(bound));
            }
            if(StrategyNeed(current.ReadForceNativeValidity(other),"poison_old_Force_validity"))
                Relations(own,other,-P(result==1?"poison-success-relation-loss":"poison-failed-relation-loss",result==1?10:5));
            return result;
        }
        private bool PoisonCityCenter(AreaReference area,int representative)
        {
            int cityId=StrategyNeed(current.ReadAreaCitySlot(area),"poison_current_City35");
            var city=current.ReadCityTableReference(cityId<52?cityId:0) ?? throw StrategyMissing("poison_current_City");
            if(!StrategyNeed(current.ReadBaseNativeValidity(city),"poison_current_City_validity"))return false;
            int link=StrategyNeed(current.ReadBaseObjectLink(city),"poison_current_City4E");
            var obj=current.ReadObjectTableReference(link<3001?link:0) ?? throw StrategyMissing("poison_current_City_Object");
            return StrategyNeed(current.ReadObjectNativeValidity(obj),"poison_current_City_Object_validity") &&
                StrategyNeed(current.ReadObjectHexIndex(obj),"poison_current_City_Object_Hex")==representative;
        }
        private void PoisonAssignCity(int area,ForceReference own)
        {
            int cityId=StrategyNeed(current.ReadAreaCitySlot(new(area)),"poison_owner_City35");
            var city=current.ReadCityTableReference(cityId<52?cityId:0) ?? throw StrategyMissing("poison_owner_City");
            int force=ForceId(own),selected=cityId;
            if(StrategyNeed(current.ReadBaseForceSlot(city),"poison_owner_City_Force")!=force)
            {
                var all=current.CreateTemporaryHexList() ?? throw StrategyMissing("poison_neighbor_list");
                var eligible=current.CreateTemporaryHexList() ?? throw StrategyMissing("poison_neighbor_filtered_list");
                try
                {
                    new PersonFreedomHomeSource(package._scenario).FillCities22F430(current,city,all);
                    var at=current.ReadRegistryHead(new(all.Identity)) ?? throw StrategyMissing("poison_neighbor_head");
                    while(at.Identity is not null)
                    {
                        var entry=current.ReadRegistryNodePayload(at) ?? throw StrategyMissing("poison_neighbor_City");
                        int candidate=StrategyNeed(entry.PhysicalSlot,"poison_neighbor_City_physical");
                        var pointer=current.ReadCityTableReference(candidate) ?? throw StrategyMissing("poison_neighbor_City_pointer");
                        if(StrategyNeed(current.ReadBaseForceSlot(pointer),"poison_neighbor_City_Force")==force)
                        {
                            var node=current.AllocateHexListNode(eligible);
                            if(node.Identity is int identity)current.WriteCurrentListPayload(identity,candidate);
                        }
                        at=current.ReadRegistryNodeNext(at) ?? throw StrategyMissing("poison_neighbor_next");
                    }
                    var candidates=ReadIntegerNodes(eligible);
                    int Province(int value)=>StrategyNeed(package._growthQueries.ReadCityState(State,value),"poison_City_provinceCE");
                    StrategySort(candidates,(left,right)=>
                    {
                        int reference=Province(cityId),a=Province(left.Value),b=Province(right.Value);
                        if((a==reference)!=(b==reference))return a==reference;
                        return a!=b?a<b:left.Value<right.Value;
                    });
                    if(candidates.Count!=0)selected=candidates[0].Value;
                    else
                    {
                        var home=BaseLookup.Find(StrategyNeed(current.ReadPersonFirstLocation(Ruler(own)),"poison_ruler_E2"),current);
                        if(StrategyNeed(current.ReadBaseObjectLink(home),"poison_ruler_Base4E")>51)home=CurrentGateMapQueries.FindCity(home,current);
                        selected=StrategyNeed(home.PhysicalSlot,"poison_ruler_City_physical");
                    }
                }
                finally
                {
                    current.ClearTemporaryHexList(eligible);current.ReleaseTemporaryHexList(eligible);
                    current.ClearTemporaryHexList(all);current.ReleaseTemporaryHexList(all);
                }
            }
            var assignment=current.AssignAreaCity(new(area),selected);
            var officers=new Dictionary<int,OfficerState>(State.Officers);
            foreach(int physical in assignment.PersonPhysicalSlotsWritten)
            {
                int id=Id(physical);var person=officers.GetValueOrDefault(id) ?? throw StrategyMissing("poison_free_Person_owner");
                int home=StrategyNeed(current.ReadPersonFirstLocation(physical),"poison_free_Person_E2");
                officers[id]=person with{LocationId=home==0?null:home};
            }
            State=State with{Officers=officers};
            State=PersonReturnDistrictTransferSource.PublishAreaAssignments(State,current,[area],[]);
        }
        private void PoisonTransferHex(HexReference hex,int force,HashSet<int> changed)
        {
            if(!GateCaptureTerritorySource.Eligible(State,current,hex,force,false) ||
                StrategyNeed(current.ReadHexForceSlot(hex),"poison_transfer_oldForce")==force)return;
            //Cityarg0 prevents center reassignment, Army balloons and the
            //encirclement branch. The Area35 store was performed above.
            current.AssignHexForce(hex,force);
            var mode=current.ArmyReturnTransferMode ?? throw StrategyMissing("poison_transfer_mode");
            if(mode.Present && StrategyNeed(mode.Value,"poison_transfer_mode_value")!=0)
                throw StrategyMissing("poison_transfer_mode_counter");
            changed.Add(StrategyNeed(hex.PhysicalSlot,"poison_transferred_Hex"));
        }
        private void PoisonHistory(int executor,int area,ForceReference own,ForceReference other,int result)
        {
            int ownRuler=Ruler(own),otherRuler=Ruler(other);
            int district=StrategyNeed(current.ReadPersonDistrictSlot(executor),"poison_history_District");
            if(Player(own))
                History(result==0?0x3132:0x312D,new(Person0:Id(executor),Person1:Id(ownRuler),Person2:Id(otherRuler),Area0:area),
                    result==0?0x3138:0x3137,new(Person0:Id(executor),Area0:area),district,Id(executor),result!=0);
            else if(Player(other))
                History(result==0?0x313A:0x3139,new(Person0:5012,Person1:Id(otherRuler),Person2:Id(ownRuler),Area0:area),
                    result==0?0x313C:0x313B,new(Person0:Id(ownRuler),Area0:area),Primary(other),5012,false,conditional:true,sound:result==0?6:7);
            else if(result!=0)
            {
                var report=package.FormatStrategicCommandReportCurrent(State,current,0x313D,new(Person0:Id(ownRuler),Area0:area));State=report.State;
                int hex=StrategyNeed(ResolveStrategicTaskPositionCurrent(State,current,task).PhysicalSlot,"poison_history_Hex");
                int day=(package.ReadArmyDestructionSampler(State,current) ?? throw StrategyMissing("poison_history_Day450")).Day450;
                State=package.WriteHistory(State,current,new(4,district,hex,day,report.Text,CampaignHistoryWriter.Supplemental(State),5012,MarkBit0:0,MarkBit4:0),Effects);
            }
        }
    }
}
