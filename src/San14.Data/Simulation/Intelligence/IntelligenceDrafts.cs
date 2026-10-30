using San14.Core;

namespace San14.Data;

public sealed record IntelligenceTargetChoice(int Id,string Name,bool Enabled,string? Missing=null);
public sealed record IntelligencePreview(int Gold,int GoldCost,int GoldAfter,int Books,int BookCost,int BooksAfter,
    int MilitaryIntelligence,int MilitaryCost,int MilitaryIntelligenceAfter,int? TravelDays,int? ChancePercent,string? Missing=null);

internal sealed record IntelligenceDraftInputs(GameState World,int ForceId,
    IReadOnlyList<IntelligenceTargetChoice> Targets,IReadOnlyList<IntelligenceTargetChoice> People,
    Func<int,int,int,IntelligencePreview> Preview,
    Func<int,AreaAdministrationQuery<AreaAdministrationOfficerCard>> Card,
    Func<int,int,IReadOnlyList<IntelligenceTargetChoice>>? SortedPeople,
    Func<IReadOnlyList<IntelligenceTargetChoice>,int,int,IntelligencePersonSelection> PersonSelection,
    Func<IReadOnlyList<IntelligenceTargetChoice>,IntelligenceForceSelection> ForceSelection,
    Func<IReadOnlyList<IntelligenceTargetChoice>,IntelligenceCitySelection> CitySelection);

public sealed class IntelligenceHomeDraft
{
    readonly IntelligenceDraftInputs source;
    readonly IntelligenceAssignment[] original;
    readonly Func<IReadOnlyList<IntelligenceAssignment>,IReadOnlyList<IntelligenceAssignment>> auto;
    readonly Func<IReadOnlyList<IntelligenceAssignment>,AreaAdministrationQuery<int>> monthlyGoldCost;
    readonly int bookUnit;
    IntelligenceAssignment[] assignments;
    internal IntelligenceHomeDraft(IntelligenceDraftInputs source,IReadOnlyList<IntelligenceAssignment> assignments,
        int bookUnit,Func<IReadOnlyList<IntelligenceAssignment>,IReadOnlyList<IntelligenceAssignment>> auto,
        Func<IReadOnlyList<IntelligenceAssignment>,AreaAdministrationQuery<int>> monthlyGoldCost)
    {
        this.source=source;original=assignments.ToArray();this.assignments=assignments.ToArray();
        this.bookUnit=bookUnit;this.auto=auto;this.monthlyGoldCost=monthlyGoldCost;
    }
    public GameState World=>source.World;
    public int ForceId=>source.ForceId;
    public IReadOnlyList<IntelligenceTargetChoice> Forces=>source.Targets.Select(row=>row with
        {Enabled=row.Enabled&&HasSlot(row.Id)}).ToArray();
    public IReadOnlyList<IntelligenceAssignment> Assignments=>Array.AsReadOnly(assignments);
    public int Books=>source.Preview(0,0,0).Books;
    public int CurrentGold=>source.Preview(0,0,0).Gold;
    public AreaAdministrationQuery<int> MonthlyGoldCost=>monthlyGoldCost(Assignments);
    public int BookCost=>source.Targets.Count(force=>Person(original,force.Id)!=Person(assignments,force.Id))*bookUnit;
    public bool CanConfirm=>BookCost>0&&BookCost<=Books;
    public IntelligenceHomeEditorDraft BeginEditor(int targetForceId)
    {
        if(!Forces.Any(row=>row.Id==targetForceId&&row.Enabled))throw new InvalidOperationException("The intelligence target is unavailable.");
        return new(this,targetForceId,Person(assignments,targetForceId),source);
    }
    public bool AdoptEditor(IntelligenceHomeEditorDraft editor)
    {
        if(!ReferenceEquals(editor.Owner,this)||!editor.CanConfirm)return false;
        var copy=assignments.ToArray();
        int slot=Array.FindIndex(copy,row=>row.TargetForceId==editor.SelectedTargetId);
        if(slot<0)slot=Array.FindIndex(copy,row=>row.OfficerId==0);
        if(slot<0)return false;
        for(int i=0;i<copy.Length;i++)if(editor.SelectedOfficerId is int id&&id!=0&&copy[i].OfficerId==id)
            copy[i]=new(0,0);
        copy[slot]=editor.SelectedOfficerId is int selected&&selected!=0?new(editor.SelectedTargetId,selected):new(0,0);
        assignments=copy;return true;
    }
    public bool Reset(){bool changed=assignments.Any(row=>row.OfficerId!=0);assignments=[new(0,0),new(0,0),new(0,0)];return changed;}
    public bool AutoAssign(){assignments=auto(Assignments).ToArray();return true;}
    public SetIntelligenceAssignmentsOrder BuildOrder()=>CanConfirm?new(ForceId,Assignments):
        throw new InvalidOperationException("The intelligence assignments cannot be confirmed.");
    internal bool HasSlot(int target)=>assignments.Any(row=>row.TargetForceId==target||row.OfficerId==0);
    static int Person(IEnumerable<IntelligenceAssignment> rows,int force)=>rows.FirstOrDefault(row=>row.TargetForceId==force)?.OfficerId??0;
}

public sealed partial class IntelligenceHomeEditorDraft
{
    readonly IntelligenceDraftInputs source;
    readonly int original;
    internal IntelligenceHomeDraft Owner{get;}
    internal IntelligenceHomeEditorDraft(IntelligenceHomeDraft owner,int target,int person,IntelligenceDraftInputs source)
    {Owner=owner;SelectedTargetId=target;SelectedOfficerId=person==0?null:person;original=person;this.source=source;}
    public int SelectedTargetId{get;}
    public int? SelectedOfficerId{get;private set;}
    public IReadOnlyList<IntelligenceTargetChoice> People=>source.People;
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> OfficerCard=>source.Card(SelectedOfficerId??0);
    public IntelligencePreview Preview=>source.Preview(SelectedTargetId,SelectedOfficerId??0,0);
    public bool CanConfirm=>(SelectedOfficerId??0)!=original&&Owner.HasSlot(SelectedTargetId)&&
        (SelectedOfficerId is null||People.Any(row=>row.Id==SelectedOfficerId&&row.Enabled));
    public bool SetPerson(int? id)
    {if(id is not null&&!People.Any(row=>row.Id==id&&row.Enabled))return false;SelectedOfficerId=id;return true;}
}

public abstract partial class IntelligenceMissionDraft
{
    private protected readonly IntelligenceDraftInputs source;
    int option;
    private protected IntelligenceMissionDraft(IntelligenceDraftInputs source,int option)
    {this.source=source;this.option=option;}
    public GameState World=>source.World;
    public int ForceId=>source.ForceId;
    public IReadOnlyList<IntelligenceTargetChoice> Targets=>source.Targets;
    public IReadOnlyList<IntelligenceTargetChoice> People=>source.SortedPeople?.Invoke(SelectedTargetId??0,option)??source.People;
    public int? SelectedTargetId{get;private set;}
    public int? SelectedOfficerId{get;private set;}
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> OfficerCard=>source.Card(SelectedOfficerId??0);
    public IntelligencePreview Preview=>source.Preview(SelectedTargetId??0,SelectedOfficerId??0,option);
    public bool CanSelectOption(int value)=>source.Preview(SelectedTargetId??0,SelectedOfficerId??0,value) is var p&&p.GoldCost<=p.Gold;
    public bool CanConfirm=>SelectedTargetId is not null&&SelectedOfficerId is not null&&Preview is {Missing:null} p&&
        p.GoldCost<=p.Gold&&p.BookCost<=p.Books&&p.MilitaryCost<=p.MilitaryIntelligence;
    public bool SelectTarget(int id){if(!Targets.Any(row=>row.Id==id&&row.Enabled))return false;SelectedTargetId=id;return true;}
    public bool SelectPerson(int id){if(!People.Any(row=>row.Id==id&&row.Enabled))return false;SelectedOfficerId=id;return true;}
    private protected int Option=>option;
    private protected void SetOption(int value)=>option=value;
}

public sealed partial class IntelligenceAwayDraft: IntelligenceMissionDraft
{
    internal IntelligenceAwayDraft(IntelligenceDraftInputs source):base(source,5){}
    public int Duration=>Option;
    public bool SetDuration(int value){if(value is not(5 or 10 or 15))return false;SetOption(value);return true;}
    public DispatchIntelligenceOrder BuildOrder()=>CanConfirm?new(ForceId,SelectedTargetId!.Value,SelectedOfficerId!.Value,Duration):
        throw new InvalidOperationException("The intelligence mission cannot be confirmed.");
}

public sealed partial class IntelligenceInspectDraft: IntelligenceMissionDraft
{
    internal IntelligenceInspectDraft(IntelligenceDraftInputs source):base(source,10){}
    public int Level=>Option;
    public bool SetLevel(int value){if(value is not(10 or 20 or 30))return false;SetOption(value);return true;}
    public InspectIntelligenceOrder BuildOrder()=>CanConfirm?new(ForceId,SelectedTargetId!.Value,SelectedOfficerId!.Value,Level):
        throw new InvalidOperationException("The inspection cannot be confirmed.");
}
