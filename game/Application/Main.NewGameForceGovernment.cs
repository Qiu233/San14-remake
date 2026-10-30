using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private NewGameForceGovernmentContent BuildNewGameForceGovernment(
        CampaignComposition composition, GameState world, int forceId)
    {
        //The actual ordinary new-game route has no CUserStrategyState. The
        //Data publication reads only the normal source and reached people.
        var source=composition.PrepareArmyQueries(world)
            .CaptureNewGameForceGovernance(world,forceId,new(false));
        if(source.ForceValid!=true||source.CanEdit!=false||source.Roster.Groups.Count!=5)
            throw new InvalidDataException("New-game government lacks its normal read-only owner.");
        var messages=_repository!.LoadMessages(_ui.SourceLocale,MessageKind.Data).Messages.Sections[0];
        var roster=source.Roster;
        var model=new GovernanceRosterPageModel(world.Forces[forceId].Name,
            roster.Advisor is { } advisor?Person(advisor.OfficerId,advisor.PendingValue):null,
            [],roster.Groups.Select(group=>new GovernanceRosterDepartment(group.Group,
                messages.Records[group.NameMessageId].Text??throw new InvalidDataException("Missing government department name."),
                group.Capacity,group.Expansion,
                group.Members.Select(member=>Person(member.OfficerId,member.PendingValue)).ToArray(),[])
                {SlotMembers=source.OrderedSlots.Where(slot=>slot.Group==group.Group).OrderBy(slot=>slot.Ordinal)
                    .Select(slot=>slot.ReferenceId==0?null:Person(slot.ReferenceId,slot.PendingValue)).ToArray()}).ToArray(),false);
        model=GovernanceRosterPresentation.Enrich(world,forceId,model,composition.PrepareArmyQueries(world),
            composition.DetailForces(world,[forceId]).GetValueOrDefault(forceId),
            reference=>source.PeopleByReference.GetValueOrDefault(reference) is {NativeValid:true} person?person.OfficerId:null,
            composition.Scenario.Snapshot.DoctrineParameters);
        model=model with {Ruler=model.Ruler is {} ruler?ruler with{PendingValue=source.RulerPendingValue}:null};
        return new(model,new(null,null,roster.PolicyCost,roster.CostLimit,null,""),source);

        GovernanceRosterOfficer Person(int reference,int? pending)
        {
            var person=source.PeopleByReference.GetValueOrDefault(reference);
            bool vacant=person?.NativeValid==false;
            return new(reference,vacant?_ui.Get("play_governance_vacant"):person?.Name??_ui.Get("detail_unknown"),
                vacant?null:person?.PolicyName,person?.PolicyCost,pending,false){NativeValid=person?.NativeValid};
        }
    }
}
