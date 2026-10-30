using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignStoryStringParameterPreparation(GameState State,IReadOnlyList<string> Names);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Prepare248 against the invocation's actual retained current and
    /// message banks. Only the actual shared presentation RNG can advance.</summary>
    internal CampaignStoryStringParameterPreparation PrepareStoryStringParametersCurrent(GameState state,
        SourceWorldContext current, CampaignStoryMessageContext bank,IReadOnlyList<int> roots)
    {
        if(bank.People.Length!=5)throw StoryTextMissing("string_parameters.Person_bank");
        var latest=state;
        int Draw(int bound)
        {
            if(bound<2)return 0;
            var saved=latest.CampaignAppRandom32??throw StoryTextMissing("string_parameters.App32");
            var random=new SharedSourceRandom32(saved.State);
            int result=random.Next(bound);
            latest=latest with {CampaignAppRandom32=new(random.State)};
            return result;
        }
        var abilities=new TacticRemakeCurrentAbilities(current,_scenario,Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var voices=_personnelNarrationVoice is{} settings?new NarrationVoiceRequests(settings,Draw):null;
        var people=new DeploymentNarrationCurrentPeople(state,current,abilities,bank.People[0],bank.People[1],
            panelReader:(force,effect)=>_baseCapacityInputs.ReadPanelModifier(latest,force,effect),
            appearanceRepository:_repository,appearanceScenario:_scenario,appearanceLanguage:Seed.Language,
            personArgument2:bank.People[2],personArgument3:bank.People[3],personArgument4:bank.People[4],voiceRequests:voices);
        ushort? City(int index)=>bank.Cities is{} cities?checked((ushort)cities[index]):null;
        int? Force(int index)=>bank.Forces?[index];
        int? Area(int index)=>bank.Areas?[index];
        var report=new ReportCurrent(this,()=>latest,current,people,
            new(Base0:City(0),Base1:City(1),Base2:City(2),Base3:City(3),Base4:City(4),
                Force0:Force(0),Force1:Force(1),Force2:Force(2),Force3:Force(3),Force4:Force(4),
                Area0:Area(0),Area1:Area(1),Area2:Area(2),Area3:Area(3),Area4:Area(4)),true);
        var context=new StoryTextCurrent(report,bank.Names,bank.Numbers,new(this,()=>latest,current,bank.Items),new(this,()=>latest,current,bank.Provinces));
        var messages=new NarrationResourceMessages(_repository.LoadMessages(Seed.Language,MessageKind.Game).Messages);
        var names=messages.ResolveStoryStringParameters(context,Draw,roots);
        return new(latest,Array.AsReadOnly(names));
    }
}
