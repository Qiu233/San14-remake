using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public IntelligenceHomeDraft CreateIntelligenceHomeDraft(GameState state,int forceId)
    {
        var current=CaptureOwnedArmyReturnWorld(state).Context;
        BindIntelligencePageCurrent(state,current);
        var inputs=CreateIntelligenceDraftInputs(state,current,forceId,0);
        var original=ReadIntelligenceAssignmentsCurrent(current,forceId);
        return new(inputs,original,IntelligenceParameter(state,"home-book"),
            selected=>AutoAssignIntelligenceCurrent(state,current,forceId,original,selected,inputs),
            selected=>ReadIntelligenceHomeMonthlyGoldCostCurrent(state,current,forceId,selected));
    }

    // The Home header sums fees for valid draft pairs. Unlike the monthly
    // expense forecast, it has no treasury, gain or intelligence-capacity gate.
    private AreaAdministrationQuery<int> ReadIntelligenceHomeMonthlyGoldCostCurrent(GameState state,
        SourceWorldContext current,int forceId,IReadOnlyList<IntelligenceAssignment> assignments)
    {
        try
        {
            var own=current.ReadForceTableReference(forceId)??throw IntelligenceMissing("monthly_own_Force");
            int total=0;
            foreach(var assignment in assignments)
            {
                var target=current.ReadForceTableReference(assignment.TargetForceId is >=0 and <=51?
                    assignment.TargetForceId:0)??throw IntelligenceMissing("monthly_target_Force");
                int person=HireAssociationPerson(current,assignment.OfficerId);
                if(!IntelligenceNeed(current.ReadForceNativeValidity(target),"monthly_target_validity")||
                    !IntelligenceNeed(current.ReadPersonNativeValidity(person),"monthly_Person_validity"))continue;
                total=unchecked(total+ReadHireAssociationFee(state,current,own,person));
            }
            return AreaAdministrationQuery<int>.Known(total);
        }
        catch(InvalidDataException error){return AreaAdministrationQuery<int>.Unknown(error.Message);}
    }

    public IntelligenceAwayDraft CreateIntelligenceAwayDraft(GameState state,int forceId)
    {
        var current=CaptureOwnedArmyReturnWorld(state).Context;BindIntelligencePageCurrent(state,current);
        return new(CreateIntelligenceDraftInputs(state,current,forceId,1));
    }

    public IntelligenceInspectDraft CreateIntelligenceInspectDraft(GameState state,int forceId)
    {
        var current=CaptureOwnedArmyReturnWorld(state).Context;BindIntelligencePageCurrent(state,current);
        return new(CreateIntelligenceDraftInputs(state,current,forceId,2));
    }

    private void BindIntelligencePageCurrent(GameState state,SourceWorldContext current)
    {
        _baseCapacityInputs.BindCurrent(state,current);
        _personAssignmentPrecallInputs.BindCurrent(state,current);
        PersonReturnCommands.BindCurrent(state,current);
    }

    internal IReadOnlyList<IntelligenceAssignment> ReadIntelligenceAssignmentsCurrent(SourceWorldContext current,int forceId)
    {
        var force=current.ReadForceTableReference(forceId)??throw IntelligenceMissing("assignment_Force");
        return Enumerable.Range(0,3).Select(slot=>new IntelligenceAssignment(
            IntelligenceNeed(current.ReadForceAssociationTargetSlot(force,slot),"assignment_target"),
            IntelligenceNeed(current.ReadForceAssociationPersonId(force,slot),"assignment_Person"))).ToArray();
    }

    private IntelligenceDraftInputs CreateIntelligenceDraftInputs(GameState state,SourceWorldContext current,int forceId,int mode)
    {
        var force=current.ReadForceTableReference(forceId)??throw IntelligenceMissing("page_Force");
        if(!IntelligenceNeed(current.ReadForceNativeValidity(force),"page_Force_validity"))throw IntelligenceMissing("page_invalid_Force");
        var district=ForcePrimaryDistrictSource.Resolve(current,force);
        int districtId=IntelligenceNeed(district.PhysicalSlot,"page_District");
        var payer=new DiplomacyCommandSource(state,current).Capital(forceId);
        int gold=IntelligenceNeed(current.ReadBaseResource(payer,BaseResourceKind.Gold),"page_Gold");
        int books=IntelligenceNeed(state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining,"page_books")-
            checked((int)CommandBookAvailability.Reserved(state,districtId));
        int military=ReadDiplomacyDistrictBudget(current,districtId);
        var targets=ReadIntelligenceTargetsCurrent(state,current,forceId,mode);
        var people=ReadIntelligencePeopleCurrent(state,current,forceId,mode);
        var cards=new AreaAdministrationOfficerCardSource(state,current,_definitions,_scenario,Seed.Language,_baseCapacityInputs,_repository);
        var travel=new Dictionary<(int Target,int Person),StrategicCommandTravelReceipt>();
        var previews=new Dictionary<(int,int,int),IntelligencePreview>();
        var cardCache=new Dictionary<int,AreaAdministrationQuery<AreaAdministrationOfficerCard>>();
        var abilities=SearchAbilities(current);
        var selectionTravel = new Dictionary<(int Target,int Home),int>();
        return new(state,forceId,targets,people,Preview,Card,SortedPeople,
            (rows,target,option)=>ReadIntelligencePersonSelectionCurrent(state,current,forceId,mode,rows,
                id=>SelectionDays(target,id,option)),
            rows=>ReadIntelligenceForceSelectionCurrent(state,current,forceId,rows),
            rows=>ReadIntelligenceCitySelectionCurrent(state,current,forceId,rows));

        int SelectionDays(int target,int person,int option)
        {
            int physical=IntelligenceNeed(current.ResolveOfficerSource(person),"selection.travel_person");
            int home=IntelligenceNeed(current.ReadPersonFirstLocation(physical),"selection.travel_home");
            if(!selectionTravel.TryGetValue((target,home),out int days))
                selectionTravel[(target,home)]=days=ReadIntelligenceSelectionTravelDaysCurrent(state,current,forceId,mode,target,home);
            return unchecked(days+option);
        }

        AreaAdministrationQuery<AreaAdministrationOfficerCard> Card(int id)
        {
            if(cardCache.TryGetValue(id,out var found))return found;
            try
            {
                int physical=id==0?0:IntelligenceNeed(current.ResolveOfficerSource(id),"page_card_Person");
                return cardCache[id]=AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(
                    cards.ReadCity(0,physical,id!=0,mode==0?18:mode==1?19:21));
            }
            catch(InvalidDataException error){return cardCache[id]=AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(error.Message);}
        }
        StrategicCommandTravelReceipt Travel(int target,int person)
        {
            if(travel.TryGetValue((target,person),out var found))return found;
            int physical=IntelligenceNeed(current.ResolveOfficerSource(person),"page_travel_Person");
            var date=state.NativeMapSeason?.ThirdDate??throw IntelligenceMissing("page_date");
            return travel[(target,person)]=StrategicCommandTravelSource.Resolve(state,current,physical,
                new(mode==1?StrategicCommandTargetKind.Force:StrategicCommandTargetKind.City,target),null,date);
        }
        IntelligencePreview Preview(int target,int person,int option)
        {
            if(previews.TryGetValue((target,person,option),out var result))return result;
            int fee=mode==0?0:IntelligenceParameter(state,(mode==1?"away-gold-":"inspect-gold-")+option);
            int militaryCost=mode==2?IntelligenceParameter(state,"inspect-military-"+option):0;
            int book=mode==0?0:IntelligenceParameter(state,mode==1?"away-book":"inspect-book");
            int after=military;int? days=null,chance=null;string? missing=null;
            try
            {
                if(target!=0&&person!=0)
                {
                    int physical=IntelligenceNeed(current.ResolveOfficerSource(person),"preview_Person");
                    if(mode==0)
                    {
                        fee=ReadHireAssociationFee(state,current,force,physical);
                        if(fee<=gold)after=unchecked(military+ReadHireAssociationGain(state,current,force,
                            current.ReadForceTableReference(target)??throw IntelligenceMissing("preview_target_Force"),physical));
                    }
                    else days=unchecked(Travel(target,person).TravelDays+option);
                    if(mode==1&&fee<=gold)
                    {
                        int gain=ReadIntelligenceAwayGainCurrent(state,current,forceId,target,physical,option);
                        int value=unchecked(military+gain);
                        after=value<military?military:Math.Min(value,ReadHireAssociationBudgetCapacity(state,current,district));
                        chance=ResolveIntelligenceAwayTrialCurrent(state,current,physical,target,option).ChancePercent;
                    }
                }
                if(mode==2)after=Math.Max(0,unchecked(military-militaryCost));
            }
            catch(InvalidDataException error){missing=error.Message;}
            return previews[(target,person,option)]=new(gold,fee,unchecked(gold-fee),books,book,unchecked(books-book),
                military,militaryCost,after,days,chance,missing);
        }
        IReadOnlyList<IntelligenceTargetChoice> SortedPeople(int target,int option)
        {
            if(mode==0||target==0)return people;
            return people.Select(row=>(Row:row,Days:Preview(target,row.Id,option).TravelDays))
                .OrderBy(row=>row.Days??int.MaxValue)
                .ThenByDescending(row=>IntelligenceNeed(abilities.ResolveDimension(state,row.Row.Id,2,
                    (f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e)),"people_Intelligence"))
                .ThenBy(row=>row.Row.Id).Select(row=>row.Row).ToArray();
        }
    }

    internal IReadOnlyList<IntelligenceTargetChoice> ReadIntelligencePeopleCurrent(GameState state,SourceWorldContext current,int forceId,int mode)
    {
        var force=current.ReadForceTableReference(forceId)??throw IntelligenceMissing("people_Force");
        int district=IntelligenceNeed(ForcePrimaryDistrictSource.Resolve(current,force).PhysicalSlot,"people_District");
        var result=new List<IntelligenceTargetChoice>();
        foreach(int? row in current.ReadRegisteredPersonPhysicalSlots()??throw IntelligenceMissing("people_registry"))
        {
            int person=IntelligenceNeed(row,"people_pointer");
            if(IntelligenceNeed(current.ReadPersonDistrictSlot(person),"people_E0")!=district||
                IntelligenceNeed(current.ReadPersonStatus(person),"people_E6") is not(>=1 and <=4))continue;
            int id=IntelligenceNeed(current.ReadPersonIdentity(person),"people_identity");
            int home=IntelligenceNeed(current.ReadPersonFirstLocation(person),"people_E2");
            if(BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id)&&home is >=1 and <=61&&forceId is not(>=46 and <=50))continue;
            if(mode!=0&&(IntelligenceNeed(OfficerHealthSourceAdapter.ReadByte(current,person),"people_health")>=2||
                (IntelligenceNeed(current.ReadArmyReturnPersonFlags196(person),"people_flags")&1)!=0||
                PersonCurrentBusySource.Read(current,person,IntelligenceMissing)))continue;
            result.Add(new(id,state.Officers.GetValueOrDefault(id)?.Name??throw IntelligenceMissing("people_Name"),true));
        }
        var abilities=SearchAbilities(current);
        return result.OrderByDescending(row=>IntelligenceNeed(abilities.ResolveDimension(state,row.Id,2,
            (f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e)),"people_Intelligence")).ThenBy(row=>row.Id).ToArray();
    }

    internal IReadOnlyList<IntelligenceTargetChoice> ReadIntelligenceTargetsCurrent(GameState state,SourceWorldContext current,int forceId,int mode)
    {
        var rows=new List<IntelligenceTargetChoice>();
        if(mode==2)
        {
            int district=IntelligenceNeed(ForcePrimaryDistrictSource.Resolve(current,new(forceId)).PhysicalSlot,"targets_District");
            foreach(var entry in current.ReadRegisteredBases()??throw IntelligenceMissing("targets_Base_registry"))
            {
                var city=entry??throw IntelligenceMissing("targets_Base_pointer");
                if(city.Kind!=BaseKind.City||!IntelligenceNeed(current.ReadBaseNativeValidity(city),"targets_City_validity")||
                    IntelligenceNeed(current.ReadBaseDistrictSlot(city),"targets_City_District")!=district)continue;
                int id=IntelligenceNeed(city.PhysicalSlot,"targets_City");
                if(HasIntelligenceInspectCommandCurrent(state,current,city))continue;
                rows.Add(new(id,state.Cities.GetValueOrDefault(id)?.Name??throw IntelligenceMissing("targets_City_Name"),true));
            }
            return rows;
        }
        foreach(var entry in current.ReadRegistryReferences(RegistryKind.Force)??throw IntelligenceMissing("targets_Force_registry"))
        {
            int id=IntelligenceNeed(entry?.PhysicalSlot,"targets_Force_pointer");
            if(id==forceId||id is >=46 and <=50)continue;
            var target=current.ReadForceTableReference(id)??throw IntelligenceMissing("targets_Force");
            if(!IntelligenceNeed(current.ReadForceNativeValidity(target),"targets_Force_validity"))continue;
            if(mode==0&&!(_eventCityNeighborhoods??=new(_scenario)).AreForceCitiesAdjacent(current,id,forceId))continue;
            if(mode==1&&HasIntelligenceAwayMarkerCurrent(state,current,
                current.ReadForceTableReference(forceId)??throw IntelligenceMissing("targets_own_Force"),target))continue;
            byte relation=ReadReportForceRelationByte(state,target,forceId,IntelligenceMissing);
            byte flags=IntelligenceNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(id)?.Flags12,"targets_Force_flags");
            byte ownFlags=IntelligenceNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags12,"targets_own_flags");
            bool enabled=(relation&127)==0&&!((flags&2)!=0&&(ownFlags&2)!=0);
            rows.Add(new(id,state.Forces.GetValueOrDefault(id)?.Name??throw IntelligenceMissing("targets_Force_Name"),enabled));
        }
        //The overview retains native registry order; auto-selection has its own
        //explicit advisor comparator below, independent of this table ordering.
        return rows;
    }

    private IReadOnlyList<IntelligenceAssignment> AutoAssignIntelligenceCurrent(GameState state,SourceWorldContext current,int forceId,
        IReadOnlyList<IntelligenceAssignment> original,IReadOnlyList<IntelligenceAssignment> selected,IntelligenceDraftInputs source)
    {
        var copy=selected.ToArray();var abilities=SearchAbilities(current);
        int Ability(int id)=>IntelligenceNeed(abilities.ResolveDimension(state,id,2,(f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e)),"auto_Intelligence");
        int Advisor(int target)
        {
            int id=IntelligenceNeed(current.ReadAssignmentForceAdvisor52(target),"auto_advisor");
            int physical=IntelligenceNeed((id<=6000?current.ReadPersonLookupReference(id):current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"auto_advisor_pointer");
            return IntelligenceNeed(current.ReadPersonNativeValidity(physical),"auto_advisor_validity")?Ability(id):0;
        }
        bool Trait(int id)
        {
            int physical=IntelligenceNeed(current.ResolveOfficerSource(id),"auto_Person");
            return Enumerable.Range(0,9).Any(slot=>IntelligenceNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,physical,slot),"auto_trait")==211);
        }
        var targets=source.Targets.Where(row=>row.Enabled&&!copy.Any(item=>item.TargetForceId==row.Id&&item.OfficerId!=0))
            .OrderBy(row=>Advisor(row.Id)).ThenBy(row=>row.Id).ToArray();
        var people=source.People.Where(row=>!copy.Any(item=>item.OfficerId==row.Id)&&Available(row.Id))
            .OrderByDescending(row=>Trait(row.Id)).ThenByDescending(row=>Ability(row.Id)).ThenBy(row=>row.Id).ToArray();
        int count=Math.Min(targets.Length,people.Length);
        for(int i=0;i<count;i++)
        {
            int cost=source.Targets.Count(row=>original.FirstOrDefault(item=>item.TargetForceId==row.Id)?.OfficerId!=
                copy.FirstOrDefault(item=>item.TargetForceId==row.Id)?.OfficerId)*IntelligenceParameter(state,"home-book");
            if(cost>=source.Preview(0,0,0).Books)break;
            int slot=Array.FindIndex(copy,item=>item.OfficerId==0||item.TargetForceId==0);if(slot<0)break;
            copy[slot]=new(targets[i].Id,people[i].Id);
        }
        return copy;
        bool Available(int id)
        {
            int physical=IntelligenceNeed(current.ResolveOfficerSource(id),"auto_Person");
            if(IntelligenceNeed(current.ReadAssignmentPersonAreaVector(physical)?.ActiveCount1E8,"auto_Area_tasks")>=1)return false;
            var home=BaseLookup.Find(IntelligenceNeed(current.ReadPersonFirstLocation(physical),"auto_E2"),current);
            if(home.Kind!=BaseKind.City||!IntelligenceNeed(current.ReadBaseNativeValidity(home),"auto_Base_validity"))return true;
            int city=IntelligenceNeed(home.PhysicalSlot,"auto_City");
            return IntelligenceNeed(current.ReadAssignmentCityCA(city),"auto_recruit")!=id&&
                IntelligenceNeed(current.ReadAssignmentCityCC(city),"auto_training")!=id;
        }
    }
}
