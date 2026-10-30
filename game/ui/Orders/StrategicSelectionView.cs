using Godot;
using San14.Data;

/// <summary>The strategic caller keeps its own mode/role publication while
/// borrowing the same original single-selection list controls as other commands.</summary>
internal partial class StrategicSelectionView : Control
{
    private IntelligenceSelectionView? selection;
    internal bool IsMapMode=>selection?.IsMapMode==true;
    internal IReadOnlyList<int> SelectedIds=>selection?.SelectedIds??[];
    internal bool CanChooseTarget(int id)=>selection?.CanChooseTarget(id)==true;
    internal void Configure(GameResourceRepository repository,PreviewText text,StrategicSelectionPage publication,
        Func<bool> admitted,Action<int?> closed,Func<int> scrollSpeed,IntelligenceSelectionView.MapMode? mapMode=null)
    {
        Name="StrategicSelection";ZIndex=5;MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        selection=new();AddChild(selection);
        selection.Configure(repository,text,StrategicSelectionContent.Create(publication,text.Get("detail_unknown")),admitted,closed,scrollSpeed,mapMode);
        selection.Name="StrategicSourceList";
        SetMeta("strategic_kind",publication.Kind);SetMeta("strategic_selection_role",publication.Role.ToString());
    }
    internal void PickMapTarget(int id)=>selection?.PickMapTarget(id);
    internal void ApplySelectionAdmission(Func<int,bool>? allowed,bool canReturn)=>selection?.ApplySelectionAdmission(allowed,canReturn);
    internal void RequestCancel()=>selection?.RequestCancel();
    internal void Abandon(){selection?.Abandon();Hide();}
}
