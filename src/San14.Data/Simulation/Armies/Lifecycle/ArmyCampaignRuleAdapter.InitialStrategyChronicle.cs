using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //44B070 reaches43BB70 before the independent first-strategy events/proposals.
    //Only its actual tutorial, elapsed-time and application load gates select
    //the initial row. Existing chronology contents never stand in for a gate.
    private GameState AppendInitialStrategyChronicleCurrent(GameState state)
    {
        bool tutorial = ReadApplicationTutorialActive()
            ?? throw InitialChronicleMissing("tutorial_service");
        if (tutorial) return state;
        var current = CaptureCurrentCampaignEventQueries(state);
        int elapsed = current.ReadPastGameTime()
            ?? throw InitialChronicleMissing("past_game_time");
        if (elapsed != 0) return state;
        var application = applicationGameplay
            ?? throw InitialChronicleMissing("application_lifecycle");
        if (application.StrategyLoadResume) return state;

        //32EFA0 reads saved World3A, with its real Force-table overflow rule.
        //8EE7F0 prepares the main label;22EE20 separately prepares Label0.
        var force = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw InitialChronicleMissing("current_player_Force");
        int day = (ReadArmyDestructionSampler(state, current)
            ?? throw InitialChronicleMissing("World450")).Day450;
        string forceName = HistoryWriter.CustomFirstName(state, current, force, PersonName);
        int color = current.ReadForceReportColor(force)
            ?? throw InitialChronicleMissing("Force_color");
        string label = HistoryWriter.FallbackName(state, current, force, PersonName);
        return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
            new(2003, 3, day, forceName, color, 82,
                ImmutableArray.Create(label, "", ""),
                ImmutableArray.Create(48400, 48400, 48400, 48400), Primary: false),
            ReadApplicationGameplay()?.UserSelectionServicePresent);

        static string PersonName(OfficerState person, bool full) => HeadHistory(full
            ? person.Name ?? throw InitialChronicleMissing("ruler_name")
            : person.CurrentSurname ?? throw InitialChronicleMissing("ruler_surname"));
    }

    private static InvalidDataException InitialChronicleMissing(string field) =>
        new("initial-strategy-chronicle." + field);
}
