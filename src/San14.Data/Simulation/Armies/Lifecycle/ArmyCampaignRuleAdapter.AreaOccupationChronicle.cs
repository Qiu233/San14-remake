using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2725E0's event199/kind25 is a source-time8C8D50 chronology write.
    //Its labels do not enter ArmyMapSpeech, CRT admission or a voice player.
    private GameState AppendAreaOccupationCompletionChronicleCurrent(GameState state,SourceWorldContext current,
        ArmyUnitReference army,int areaId,byte choice,int day,int hex)
    {
        var force=current.ReadArmyUnitForceReference(army)??throw MaintenanceMissing("occupation_chronicle_Force");
        string forceName=HistoryWriter.CustomFirstName(state,current,force,(person,full)=>
            HeadHistory(full?person.Name??throw MaintenanceMissing("occupation_chronicle_ruler_name"):
                person.CurrentSurname??throw MaintenanceMissing("occupation_chronicle_ruler_surname")));
        int color=MaintenanceKnown(current.ReadForceReportColor(force),"occupation_chronicle_color");
        string choiceName=ReadHistoryChoiceName(state,choice);
        string armyName=ReadHistoryArmyName(state,current,army);
        string areaName=HeadHistory(state.Areas.GetValueOrDefault(areaId)?.Name
            ??throw MaintenanceMissing("occupation_chronicle_Area_name"));
        return new CampaignChronicleWriter(HistoryWriter).Write(state,current,
            new(199,1,day,forceName,color,25,ImmutableArray.Create(choiceName,armyName,areaName),
                ImmutableArray.Create(hex,48400,48400,48400)),
            ReadApplicationGameplay()?.UserSelectionServicePresent);
    }
}
