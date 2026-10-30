using Godot;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private void OpenEnemyGateResidents(EnemyFootholdWorkspace owner)
    {
        if(!EnemyFootholdCurrent(owner)||owner.OwnedPage is not GateDetailsView gate||
            owner.ResidentList is not null||_repository is null)return;
        var rules=_campaignComposition!.PrepareArmyQueries(owner.World);
        var membership=rules.QueryGateResidentOfficerIds(owner.World,owner.Id);
        if(!membership.Available||membership.Value is not {} ids)
        {EnemyFootholdUnavailable(membership.UnresolvedInput??"gate-information.residents.current_owner");return;}
        var publication=rules.QueryProvidedOfficerInformation(owner.World,owner.Session.PlayerForceId!.Value,ids);
        if(publication.UnresolvedOwner is {} missing)EnemyFootholdUnavailable(missing);
        var list=new MapInformationListView{Name="GateResidentOfficers",ZIndex=5};owner.ResidentList=list;AddChild(list);
        try
        {
            var columns=publication.Columns.Select(column=>new MapInformationListColumn(column.Key,
                InformationColumnCaption(column),column.Key=="name"?188:200)).ToArray();
            IReadOnlyList<MapInformationListRow> Rows(InformationList value)=>value.Rows.Select(row=>
                new MapInformationListRow(row.Id,publication.Columns.Select(column=>InformationCellText(
                    row.Cells.GetValueOrDefault(column.Key))).ToArray(),row.Detail is not null)).ToArray();
            list.Configure(_repository,_ui,publication.Title,columns,Rows(publication),
                ()=>EnemyFootholdCurrent(owner)&&owner.ResidentList==list&&owner.ResidentDetail is null,
                ()=>RunUiAction(()=>CloseEnemyGateResidents(owner)),
                id=>RunUiAction(()=>InspectEnemyGateResident(owner,id)),CurrentListScrollSpeed,
                (column,ascending)=>Rows(publication.Sort(column,ascending)),
                publication.UnresolvedOwner is null?null:_ui.Get("information_partial"));
            list.RegisterHeader(_sourceHeaders);gate.Hide();
        }
        catch {CloseEnemyGateResidents(owner);throw;}
    }
    private void InspectEnemyGateResident(EnemyFootholdWorkspace owner,int id)
    {
        if(!EnemyFootholdCurrent(owner)||owner.ResidentList is not {} list||owner.ResidentDetail is not null)return;
        var detail=new OfficerDetailsView();owner.ResidentDetail=detail;AddChild(detail);
        detail.BindRetainedSpeechHost(()=>EnemyFootholdCurrent(owner)&&owner.ResidentDetail==detail);
        try
        {
            ConfigureCurrentOfficerDetail(detail,owner.World,id,()=>
            {
                if(owner.ResidentDetail!=detail)return;
                owner.ResidentDetail=null;detail.Hide();detail.QueueFree();
                if(EnemyFootholdCurrent(owner)&&owner.ResidentList==list)list.Show();
                else CloseEnemyFootholdCommands();
            },()=>EnemyFootholdCurrent(owner)&&owner.ResidentDetail==detail,
                next=>owner.World=next);
            detail.RegisterHeader(_sourceHeaders);list.Hide();
        }
        catch
        {
            owner.ResidentDetail=null;detail.Hide();detail.QueueFree();
            if(EnemyFootholdCurrent(owner))list.Show();else CloseEnemyFootholdCommands();
            throw;
        }
    }
    private void CloseEnemyGateResidents(EnemyFootholdWorkspace owner)
    {
        if(!ReferenceEquals(_enemyFootholdCommands,owner))return;
        var detail=owner.ResidentDetail;owner.ResidentDetail=null;detail?.Hide();detail?.QueueFree();
        var list=owner.ResidentList;owner.ResidentList=null;list?.Abandon();list?.QueueFree();
        if(EnemyFootholdCurrent(owner))owner.OwnedPage?.Show();else CloseEnemyFootholdCommands();
    }
}
