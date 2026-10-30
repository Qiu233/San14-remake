using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IIntelligenceCommandRules
{
    public CommandResult DispatchIntelligence(GameState state,ForceState force,DispatchIntelligenceOrder order,CommandContext context)
        => StartIntelligenceMissionCurrent(state,force,order.TargetForceId,order.OfficerId,order.Duration,PersonnelIntelligenceKind.Away,order.DistrictId);

    public CommandResult InspectIntelligence(GameState state,ForceState force,InspectIntelligenceOrder order,CommandContext context)
        => StartIntelligenceMissionCurrent(state,force,order.TargetCityId,order.OfficerId,order.Level,PersonnelIntelligenceKind.Inspect,order.DistrictId);

    private CommandResult StartIntelligenceMissionCurrent(GameState state,ForceState force,int targetId,int officerId,int option,PersonnelIntelligenceKind kind,int? commandDistrict)
    {
        try
        {
            bool away=kind==PersonnelIntelligenceKind.Away;int mode=away?1:2;
            if(away?option is not(5 or 10 or 15):option is not(10 or 20 or 30))
                return CommandResult.Reject("The intelligence duration or inspection level is invalid.","intelligence.option");
            var world=CaptureOwnedArmyReturnWorld(state);var current=world.Context;
            BindIntelligencePageCurrent(state,current);
            int goldCost,bookCost,militaryCost;
            if(commandDistrict is null)
            {
                //The ordinary player selection retains its original page scope.
                var inputs=CreateIntelligenceDraftInputs(state,current,force.Id,mode);
                if(!inputs.Targets.Any(row=>row.Id==targetId&&row.Enabled)||!inputs.People.Any(row=>row.Id==officerId&&row.Enabled))
                    return CommandResult.Reject("The intelligence target or officer is unavailable.","intelligence.selection");
                var quote=inputs.Preview(targetId,officerId,option);
                if(quote.Missing is string missing)throw IntelligenceMissing(missing);
                if(quote.Gold<quote.GoldCost||quote.Books<quote.BookCost||quote.MilitaryIntelligence<quote.MilitaryCost)
                    return CommandResult.Reject("There are insufficient intelligence resources.","intelligence.resources");
                goldCost=quote.GoldCost;bookCost=quote.BookCost;militaryCost=quote.MilitaryCost;
            }
            else
            {
                //A District command uses the actual native packet and payer
                //owners. It never borrows the primary-District UI candidate list.
                if(!ValidateDistrictIntelligenceMissionPacketCurrent(state,current,commandDistrict.Value,
                    targetId,officerId,option,kind,out int sourceOwner)||sourceOwner!=force.Id)
                    return CommandResult.Reject("The District intelligence packet is invalid.","intelligence.district_packet");
                goldCost=IntelligenceParameter(state,(away?"away-gold-":"inspect-gold-")+option);
                bookCost=IntelligenceParameter(state,away?"away-book":"inspect-book");
                militaryCost=away?0:IntelligenceParameter(state,"inspect-military-"+option);
            }
            int actor=IntelligenceNeed(current.ResolveOfficerSource(officerId),"creation_Person");
            int home=IntelligenceNeed(current.ReadPersonFirstLocation(actor),"creation_E2");
            var source=BaseLookup.Find(home,current);
            if(!IntelligenceNeed(current.ReadBaseNativeValidity(source),"creation_Home_validity"))
                return CommandResult.Reject("The intelligence officer has no valid home.","intelligence.home");
            int district=IntelligenceNeed(current.ReadBaseDistrictSlot(source),"creation_District");
            int sourceForce=IntelligenceNeed(current.ReadBaseForceSlot(source),"creation_source_Force");
            if(sourceForce!=force.Id)return CommandResult.Reject("The intelligence officer's ownership changed.","intelligence.ownership");
            var own=current.ReadForceTableReference(force.Id)??throw IntelligenceMissing("creation_Force");
            var reportingDistrict=commandDistrict is int scoped
                ?current.ReadDistrictTableReference(scoped)??throw IntelligenceMissing("creation_reporting_District")
                :ForcePrimaryDistrictSource.Resolve(current,own);
            var date=state.NativeMapSeason?.ThirdDate??throw IntelligenceMissing("creation_date");
            var travel=StrategicCommandTravelSource.Resolve(state,current,actor,
                new(away?StrategicCommandTargetKind.Force:StrategicCommandTargetKind.City,targetId),null,date);
            int targetForce=away?targetId:IntelligenceNeed(current.ReadBaseForceSlot(
                current.ReadCityTableReference(targetId)??throw IntelligenceMissing("creation_City")),"creation_target_Force");
            var task=new PersonnelIntelligenceTask(0,0,[officerId],kind,sourceForce,home,district,targetId,targetForce,
                travel.StartHexId,travel.EndHexId,date,
                PersonReturnCommands.DateFromOrdinal(unchecked(PersonReturnCommands.CalendarOrdinal(date)+travel.TravelDays+option)),
                option,-1,false,false)
            {ReportToPlayer=commandDistrict is null?TalentDistrictIsPlayerRulerCurrent(state,current,reportingDistrict)
                :!away&&TalentDistrictIsPlayerRulerCurrent(state,current,reportingDistrict)};
            var working=PersonReturnCommands.RegisterIntelligenceCurrent(state,current,task,actor,date);
            int id=(current.ReadOwnedPersonCommandManager()??throw IntelligenceMissing("creation_registered_owner")).NextTaskId-1;
            if(away)
            {
                var advice=ResolveIntelligenceCreationAdviceCurrent(working,current,force.Id,actor,targetId,option);
                working=advice.State;
                var manager=current.ReadOwnedPersonCommandManager()??throw IntelligenceMissing("creation_advice_owner");
                task=(manager.Intelligences??throw IntelligenceMissing("creation_task_owner"))[id] with {SavedResult=advice.SavedResult};
                working=StoreIntelligenceTaskCurrent(working,current,task);
            }
            working=SpendStrategicCommandBooksCurrent(working,current,district,bookCost);
            //The ordinary selected-user accounting service is independently
            //gated; neither opening the editor nor previewing spends books.
            if(away&&HasCommandAccountingUser(working)&&TalentDistrictIsPlayerRulerCurrent(working,current,reportingDistrict))
                working=ApplyCommandBookUsageCurrent(working,current,bookCost);
            var treasury=away?new DiplomacyCommandSource(working,current).Capital(force.Id):ReadPersonnelHireTreasuryCurrent(current,district);
            working=BaseCurrencyAdditionSource.Add(working,current,treasury,-goldCost,
                city=>BasePostLoad.ReadResourceCapacities(city,current).Gold).State;
            if(!away)working=WriteIntelligenceBudgetCurrent(working,current,district,
                unchecked(ReadDiplomacyDistrictBudget(current,district)-militaryCost));
            //334A50 reaches the same current governor repair as the other
            //ordinary single-person task initializers, after association.
            if(IntelligenceNeed(current.ReadArmyReturnBaseGovernor2E(source),"creation_source_governor")==officerId)
                _=ApplyPersonFreedomGovernorCurrent(working,current,source);
            working=OwnedArmyReturnCurrentFields.PublishCurrent(working,current);
            working=PersonReturnCommands.PublishCurrent(working,current);
            //5C46/5D0C are the player terminal's extra preparation narration.
            //Native node callers stop after the shared writer and its real advice.
            if(commandDistrict is not null)
                return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with {Indexes=world.Pool.ExportOwnedState()}));
            var operands=new HistoryMessageOperands(Person0:officerId,Person1:TalentRulerCurrent(current,own),
                Force0:away?targetId:null,Base0:away?null:checked((ushort)targetId));
            var text=FormatReportMessage(working,current,away?0x5C46:0x5D0C,operands,
                ReportMessageOrigin.IntelligenceCommands,null,_personnelNarrationVoice);
            var request=CreateSingleOfficerPresentation(current,state,officerId,text,
                TalentCommandPresentationOperation.ConditionalOfficerText,"intelligence.preparation");
            working=text.State with {CampaignEffectRequests=(text.State.CampaignEffectRequests??throw IntelligenceMissing("creation_effect_owner"))
                .Append(CampaignEffectSource.IntelligenceCommands,text.State.NativeMapSeason!.AdvancedCalendarSteps,[request])};
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with {Indexes=world.Pool.ExportOwnedState()}));
        }
        catch(InvalidDataException error){return CommandResult.RuleUnavailable(error.Message,error.Message);}
    }

    private bool ValidateDistrictIntelligenceMissionPacketCurrent(GameState state, SourceWorldContext current,
        int district, int targetId, int officerId, int option, PersonnelIntelligenceKind kind, out int force)
    {
        force = 0;
        if (Seed.Language != "TC") throw IntelligenceMissing("district_mission_SC_source_contract");
        bool away = kind == PersonnelIntelligenceKind.Away;
        if (away ? option is not (5 or 10 or 15) : option is not (10 or 20 or 30)) return false;
        int person = IntelligenceMissionPersonCurrent(current, officerId);
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(person), "packet_Person_validity")) return false;
        if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "packet_District_validity") ||
            IntelligenceActorDistrict(current, person) != district) return false;
        force = IntelligenceNeed(current.ReadDistrictForceId(district), "packet_District_Force");
        if (away && IntelligenceNeed(current.ReadDistrictRawByte(district, 0x14), "packet_away_books") <
            StrategicParameter(state, "intelligence.away-node-book", 1)) return false;
        if (!away && (IntelligenceNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "packet_Person161") >= 2 ||
            (IntelligenceNeed(current.ReadArmyReturnPersonFlags196(person), "packet_Person15E") & 1) != 0 ||
            DuelAftermathSpecialPerson(current, person))) return false;
        var targetForce = away ? current.ReadForceTableReference((uint)targetId < 52 ? targetId : 0) : null;
        var targetCity = away ? null : current.ReadCityTableReference((uint)targetId < 52 ? targetId : 0);
        if (away)
        {
            if (targetForce is null || !IntelligenceNeed(current.ReadForceNativeValidity(targetForce), "packet_target_Force_validity"))
                return false;
        }
        else if (targetCity is null || !IntelligenceNeed(current.ReadBaseNativeValidity(targetCity), "packet_target_City_validity") ||
            IntelligenceNeed(current.ReadBaseDistrictSlot(targetCity), "packet_target_City_District") != district)
            return false;
        var home = BaseLookup.Find(IntelligenceNeed(current.ReadPersonFirstLocation(person), "packet_PersonE2"), current);
        if (!IntelligenceNeed(current.ReadBaseNativeValidity(home), "packet_Home_validity"))
            throw IntelligenceMissing("mission_home_receiver_contract");
        if (IntelligenceNeed(current.ReadBaseDistrictSlot(home), "packet_Home_District") != district ||
            IntelligenceNeed(current.ReadBaseForceSlot(home), "packet_Home_Force") != force)
            throw IntelligenceMissing("mission_home_district_contract");
        //The source SpyAway validator has no busy gate. This engine cannot
        //replace a live associated command with a second concurrent owner.
        if (PersonCurrentBusySource.Read(current, person, IntelligenceMissing))
            throw IntelligenceMissing("mission_busy_association_contract");
        return true;
    }

    private static int IntelligenceMissionPersonCurrent(SourceWorldContext current, int id) =>
        (id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw IntelligenceMissing("mission_Person_lookup");

    //2F39D0(kind21) is evaluated even when its formatted advice is not
    //displayed. Function3 consumes the shared App stream at this real point.
    private (GameState State,int SavedResult) ResolveIntelligenceCreationAdviceCurrent(GameState state,SourceWorldContext current,
        int forceId,int actor,int target,int duration)
    {
        int advisorId=IntelligenceNeed(current.ReadAssignmentForceAdvisor52(forceId),"creation_advisor");
        int advisor=IntelligenceNeed((advisorId<=6000?current.ReadPersonLookupReference(advisorId):
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"creation_advisor_pointer");
        if(!IntelligenceNeed(current.ReadPersonNativeValidity(advisor),"creation_advisor_validity"))return(state,-1);
        var abilities=SearchAbilities(current);
        int Ability()=>IntelligenceNeed(abilities.ResolveDimension(state,advisorId,2,
            (f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e)),"creation_advisor_Intelligence");
        int accuracy=Math.Clamp(unchecked(60+Math.Max(0,Ability()-70)+Math.Max(0,Ability()-90)),0,100);
        int actorId=IntelligenceNeed(current.ReadPersonIdentity(actor),"creation_actor_identity");
        var sampler=ReadArmyDestructionSampler(state,current)??throw IntelligenceMissing("creation_sampler");
        bool correct=TacticEffectSourceSamplerMath.Sample(100,10,actorId,target,0,0,sampler)<accuracy;
        var trial=ResolveIntelligenceAwayTrialCurrent(state,current,actor,target,duration);
        var own=current.ReadForceTableReference(forceId)??throw IntelligenceMissing("creation_advice_Force");
        var other=current.ReadForceTableReference(target)??throw IntelligenceMissing("creation_advice_target");
        var text=FormatReportMessage(state,current,trial.Succeeded==correct?0x3409:0x3419,
            new(Person0:advisorId,Person1:TalentRulerCurrent(current,own),Person2:actorId,Person3:TalentRulerCurrent(current,other)),
            ReportMessageOrigin.IntelligenceCommands,null,_personnelNarrationVoice);
        return(text.State,accuracy<100?-1:trial.Succeeded?1:0);
    }
}
