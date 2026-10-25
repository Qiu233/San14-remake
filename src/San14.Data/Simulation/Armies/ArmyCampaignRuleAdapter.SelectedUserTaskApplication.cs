using San14.Core;

namespace San14.Data;

/// <summary>Actual host scene lifetimes at the selected task's conclusion.
/// These are independent of the scenario, user slot and gameplay mode.</summary>
public sealed record SelectedUserTaskApplicationFacts(bool ProgressScenePresent, bool GameScenePresent);

public sealed partial class ArmyCampaignRuleAdapter
{
    private SelectedUserTaskApplicationFacts? selectedUserTaskApplication;

    /// <summary>Configure before entering the private worker. A missing
    /// snapshot remains unknown until a task actually reaches its conclusion.</summary>
    public void ConfigureSelectedUserTaskApplication(SelectedUserTaskApplicationFacts? facts)
        => selectedUserTaskApplication = facts;

    //428180→1D36E0: selected tasks retain the current elapsed score on every
    //turn. A deadline or a confirmed victory/defeat condition then requests
    //the original task-result scene, rather than ordinary unification.
    private GameState RefreshNormalTurnSelectedUserCurrent(GameState state)
    {
        var world = state.CampaignEventWorld ?? throw ClosingMissing("user_World");
        int selected = ClosingNeed(world.CurrentUserIndex, "user_WorldBC");
        if (selected == -1) return state;
        var progress = state.SelectedUserTaskProgress ?? throw ClosingMissing("selected_user_progress");
        var season = state.NativeMapSeason ?? throw ClosingMissing("selected_user_calendar");
        uint elapsed = unchecked((uint)(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods));
        byte score = (byte)Math.Min(elapsed, 255u);
        var updated = progress with { ScorePeriods = score };
        var deadline = progress.Deadline ?? throw ClosingMissing("selected_user_deadline");
        bool reached = TaskDateOrdinal(season.ThirdDate) >= TaskDateOrdinal(deadline);
        if (!reached && progress.DefeatConditionStatus <= 2 && progress.VictoryConditionStatus <= 2)
            return state with { SelectedUserTaskProgress = updated };

        bool success = progress.VictoryConditionStatus >= 3;
        if (!success) updated = updated with { ScorePeriods = 0 };
        var application = selectedUserTaskApplication ?? throw ClosingMissing("selected_user_application_scene_facts");
        //Absent scenes take the original no-writer branches. In particular an
        //absent game scene does not manufacture a victory/defeat transition.
        var request = application.ProgressScenePresent || application.GameScenePresent
            ? new SelectedUserTaskConclusionRequest(selected,
                application.GameScenePresent ? success : null, application.ProgressScenePresent)
            : null;
        return state with { SelectedUserTaskProgress = updated,
            PendingSelectedUserTaskConclusion = request ?? state.PendingSelectedUserTaskConclusion };
    }

    //31D1F0 compares the original thirty-day calendar, including hour. No
    //DateTime conversion, host wall clock or Gregorian day count participates.
    private static int TaskDateOrdinal(NativeMapSeasonDate date) => unchecked(
        date.Hour + (date.Day - 31 + (date.Month + date.Year * 12) * 30) * 24);
}
