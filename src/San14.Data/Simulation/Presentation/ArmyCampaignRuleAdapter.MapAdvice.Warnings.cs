using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //67D670 reuses the report producers, but does not run the end-of-turn
    //preparation or touch its saved history/Area flags. This is an explicit
    //world-change query, never a paint-time or per-frame operation.
    private void AppendMapAdviceWarningsCurrent(GameState state,SourceWorldContext current,
        ForceReference player,DistrictReference primary,int speakerId,int rulerId,
        NarrationResourceMessages formatter,TacticRemakeCurrentAbilities abilities,
        List<MapAdviceEntry> entries,List<string> missing,Action<GameState>? publishReached=null)
    {
        var pool=current.ReadOwnedCurrentListPool()??throw MapAdviceMissing("warnings_current_pool");
        var speaker=current.ReadPersonLookupReference(speakerId)??throw MapAdviceMissing("warnings_speaker");
        var records=new ActionReportLocalRecords();
        var latest=state;
        int serial=0;
        void Read(string key,Action read)
        {try{read();}catch(InvalidDataException error){missing.Add(key+":"+error.Message);}}
        string Render(string key,int root,HistoryMessageOperands operands,int? city=null,int? area=null)
        {
            try
            {
                var people=new DeploymentNarrationCurrentPeople(state,current,abilities,operands.Person0,operands.Person1,
                    area:operands.Area0,panelReader:(f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e),
                    cityArgument:operands.Base0,appearanceRepository:_repository,appearanceScenario:_scenario,
                    appearanceLanguage:Seed.Language,forceArgument:operands.Force0);
                var context=new ReportCurrent(this,()=>latest,current,people,operands);
                int Draw(int bound)
                {
                    if(bound<2)return 0;
                    if(publishReached is null)throw MapAdviceMissing("warning_App32_submission_owner");
                    var saved=latest.CampaignAppRandom32??throw MapAdviceMissing("warning_App32_state");
                    var random=new SharedSourceRandom32(saved.State);
                    int value=random.Next(bound);
                    latest=latest with{CampaignAppRandom32=new(random.State)};
                    publishReached(latest);
                    return value;
                }
                string body=root switch
                {
                    0x5024=>formatter.ResolveActionArmyTargetReport(context,Draw),
                    0x502b=>formatter.ResolveActionBaseNearbyReport(context,Draw),
                    0x5046=>formatter.ResolveActionAllianceExpiryReport(context,Draw),
                    0x50c8=>formatter.ResolveActionAttackRequestExpiryReport(context,Draw),
                    0x50c1=>formatter.ResolveActionForceObjectiveReport(context,Draw),
                    0x500f or 0x53cd or 0x5016=>formatter.ResolveActionAreaReport(context,Draw,root),
                    _=>throw MapAdviceMissing("warning_message_root")
                };
                entries.Add(new(key+":"+serial++,body,0,city,area,root));
                return body;
            }
            catch(InvalidDataException error){missing.Add(key+":"+error.Message);return "";}
        }
        //The formatter callback is an explicit, nonmutating alternative to
        //the ordinary report's App32/publish callback. All filtering, order,
        //target normalization and deduplication remain in the report owner.
        Read("army-target",()=>new ArmyTargetReportContentOperation(this,state,current,pool,input=>
            Render("army-target",0x5024,new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Force0:input.EnemyForceOperand0,Base0:input.TargetBaseOperand0)))
            .Produce(speaker,records,3));
        Read("nearby-enemy",()=>new BaseNearbyEnemyReportContentOperation(this,state,current,pool,input=>
            Render("nearby-enemy",0x502b,new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Base0:input.FootholdBaseOperand0))).Produce(speaker,records,3));
        Read("alliance-expiry",()=>new ForceAllianceExpiryReportContentOperation(this,state,current,pool,input=>
            Render("alliance-expiry",0x5046,new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Force0:input.OtherForceOperand0,Integers:[input.RemainingAllianceCounterInteger0])))
            .Produce(speaker,records,2));
        //The parent passes two; 676C80 itself can append at most one request.
        Read("attack-request-expiry",()=>new ForceAttackRequestExpiryReportContentOperation(this,state,current,pool,input=>
            Render("attack-request-expiry",0x50c8,new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Force0:input.RequestingForceOperand0,Integers:[input.RemainingMonthsInteger0])))
            .Produce(speaker,records));
        Read("force-objective",()=>new ForceObjectiveReportContentOperation(this,state,current,pool,input=>
            Render("force-objective",0x50c1,new(Person0:input.SpeakerPersonOperand0,Person1:input.RulerPersonOperand1,
                Force0:input.OtherForceOperand0,Base0:input.ObjectiveBaseOperand0))).Produce(speaker,records,1));
        Read("area-discovery",()=>
        {
            var app=ReadApplicationGameplay()??throw MapAdviceMissing("warnings_Application");
            if(MapAdviceNeed(app.TutorialBlocksAreaReports,"warnings_tutorial_blocks_area"))return;
            var operation=new AreaReportContentOperation(this,state,current,pool,
                new(()=>app.TutorialBlocksAreaReports,()=>app.SpecialModeSuppressesSceneryReport));
            bool locked=MapAdviceNeed(current.ReadRegistryLockEnabled(),"warnings_pool_lock");
            HexListHandle output;
            if(locked)current.EnterRegistryLock();
            try{output=current.CreateTemporaryHexList()??throw MapAdviceMissing("warnings_area_temporary");}
            finally{if(locked)current.ExitRegistryLock();}
            try
            {
                var selected=operation.Select(output);
                if(selected.Area is not {} area||!MapAdviceNeed(current.ReadAreaNativeValidity(area),"warnings_area_validity"))return;
                byte citySlot=MapAdviceNeed(current.ReadAreaCitySlot(area),"warnings_Area35");
                var city=current.ReadCityTableReference(citySlot<52?citySlot:0)??throw MapAdviceMissing("warnings_area_city");
                int root;
                if(MapAdviceNeed(current.ReadBaseNativeValidity(city),"warnings_area_city_validity")&&selected.First!=0)root=0x500f;
                else if(selected.Second!=0)
                {
                    if(MapAdviceNeed(app.SpecialModeSuppressesSceneryReport,"warnings_scenery_special_mode"))return;
                    root=0x53cd;
                }
                else if(selected.Third!=0)root=0x5016;
                else return;
                ushort base0=MapAdviceNeed(current.ReadBaseObjectLink(city),"warnings_area_Base4E");
                Render("area-discovery",root,new(Person0:speakerId,Person1:rulerId,Base0:base0),citySlot,area.PhysicalSlot);
            }
            finally
            {
                if(locked)current.EnterRegistryLock();
                try{current.ClearTemporaryHexList(output);current.ReleaseTemporaryHexList(output);}
                finally{if(locked)current.ExitRegistryLock();}
            }
        });
    }
}
