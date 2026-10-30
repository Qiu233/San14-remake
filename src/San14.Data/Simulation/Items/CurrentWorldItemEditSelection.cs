namespace San14.Data;

/// <summary>The current-world editor's own single or batch list. Candidate
/// rows and getter keys share the same captured current operation.</summary>
public sealed record CurrentWorldItemEditSelection(DefinitionInformationPageSet Pages,
    IReadOnlyList<CurrentWorldItemEditRow> Rows,bool Batch,int TitleMessageId,string GuideText)
{
    internal Func<IReadOnlyCollection<int>,bool,AreaAdministrationQuery<DefinitionInformationPage>> ReadSelectionSort { get; init; }=
        (_,_)=>AreaAdministrationQuery<DefinitionInformationPage>.Unknown("current-world.edit.item.selection.sort_owner");
    public AreaAdministrationQuery<DefinitionInformationPage> SortSelection(
        IReadOnlyCollection<int> selectedIds,bool selectedFirst)=>ReadSelectionSort(selectedIds,selectedFirst);
}

public sealed record CurrentWorldItemHolderSelection(OfficerInformationPageSet Pages,
    int TitleMessageId,string GuideText);
