using San14.Core;

namespace San14.Data;

public enum StrategicSelectionRole { Executor, Target, SecondForce }

/// <summary>Exactly one original entity list, with the current command's
/// retained candidates. The modal owns its own sortable order.</summary>
public sealed record StrategicSelectionPage(int Kind,StrategicSelectionRole Role,
    int TitleMessageId,string GuideText,IReadOnlyDictionary<int,StrategicCommandTargetChoice> Eligibility)
{
    public OfficerInformationPageSet? Officers { get; init; }
    public ForceInformationPageSet? Forces { get; init; }
    public CityInformationPageSet? Cities { get; init; }
    public AreaInformationPageSet? Areas { get; init; }
    public IReadOnlyDictionary<int,AreaAdministrationQuery<bool>>? AlteredHealth { get; init; }
    public IReadOnlyDictionary<int,AreaAdministrationQuery<int?>>? LeadingAreaIcons { get; init; }
    public Func<bool,AreaAdministrationQuery<AreaInformationListPage>>? SortLeadingAreaIcon { get; init; }
    public StrategicTargetMapQuery? MapSelection { get; init; }
}

public sealed partial class StrategicCommandDraft
{
    public AreaAdministrationQuery<StrategicSelectionPage> ReadExecutorSelection()=>ReadSelection(StrategicSelectionRole.Executor);
    public AreaAdministrationQuery<StrategicSelectionPage> ReadTargetSelection(bool second=false)=>
        ReadSelection(second?StrategicSelectionRole.SecondForce:StrategicSelectionRole.Target);
    private AreaAdministrationQuery<StrategicSelectionPage> ReadSelection(StrategicSelectionRole role)
    {
        if(selection is null||readSelection is null)
            return AreaAdministrationQuery<StrategicSelectionPage>.Unknown("strategic.selection.owner");
        if(role==StrategicSelectionRole.SecondForce&&Kind!=10)
            return AreaAdministrationQuery<StrategicSelectionPage>.Unknown("strategic.selection.second_force_kind");
        try{return AreaAdministrationQuery<StrategicSelectionPage>.Known(readSelection(role,TargetId,SecondForceId,selection));}
        catch(InvalidDataException error){return AreaAdministrationQuery<StrategicSelectionPage>.Unknown(error.Message);}
    }
}
