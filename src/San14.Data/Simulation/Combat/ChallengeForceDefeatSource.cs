using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Every successfully initialized tutorial paragraph writes its own exit
    //paragraph index and clears the failure latch. This is an application
    //producer entry, not a fresh-campaign or missing-save default.
    internal static GameState InitializeTutorialForceDefeatCurrent(GameState state, int exitParagraphIndex)
    {
        if (exitParagraphIndex < 0) throw TutorialDefeatMissing("exit_paragraph_index");
        return state with { TutorialForceDefeat = new(exitParagraphIndex, false) };
    }

    //27A6B0 reaches4FAE50 only for an existing, active570850 tutorial
    //controller and no remaining player Force. It is not the selected-user
    //task conclusion or the independent service-stop dword consumed at Turn63.
    internal GameState ApplyTutorialForceDefeatCurrent(GameState state, SourceWorldContext current,
        List<CampaignEffectRequest> effects)
    {
        var mode = current.ReadTransferModeState() ?? throw TutorialDefeatMissing("tutorial_service");
        if (!mode.Present || (mode.Value ?? throw TutorialDefeatMissing("tutorial_active")) == 0)
            throw TutorialDefeatMissing("inactive_tutorial_entry");
        var failure = state.TutorialForceDefeat ?? throw TutorialDefeatMissing("paragraph_failure_owner");
        if (!failure.FailureHandled)
        {
            //4A98D0 sets its latch before pausing input or formatting either
            //message. A resumed result therefore cannot repeat the dialogue.
            state = state with { TutorialForceDefeat = failure with { FailureHandled = true } };
            Present(TutorialForceDefeatPresentationOperation.SuspendControls);
            var source = state;
            var speech = FormatNormalTurnReport(state, current, 0x9C55, new());
            state = speech.State;
            effects.Add(CreateSingleOfficerPresentation(current, source, 952, speech,
                TalentCommandPresentationOperation.ConditionalOfficerText, "tutorial.force-defeat"));
            var announcement = FormatNormalTurnReport(state, current, 0x9C56, new());
            state = announcement.State;
            if (announcement.Voices.Count != 0) throw TutorialDefeatMissing("announcement_resource_voice");
            effects.Add(new(null, null) { TalentCommandPresentation = new(
                TalentCommandPresentationOperation.Announcement, null, null, null, null, announcement.Text) });
            Present(TutorialForceDefeatPresentationOperation.ResumeControls);
            if (failure.ExitParagraphIndex < 0) throw TutorialDefeatMissing("saved_exit_paragraph_index");
            Present(TutorialForceDefeatPresentationOperation.SelectExitParagraph, failure.ExitParagraphIndex);
        }
        //4C65F0 is outside the one-shot latch: it closes the tutorial controls,
        //requests the host's input restriction and stops the world even when
        //the earlier failure dialogue was already handled. These are pending
        //presentation requests; no control lifetime or playback is claimed.
        Present(TutorialForceDefeatPresentationOperation.CloseControlsAndStop);
        return state with { CampaignTurnStop = new(CampaignTurnStopReason.TutorialForceDefeat, null, false) };

        void Present(TutorialForceDefeatPresentationOperation operation, int? exit = null)
            => effects.Add(new(null, null) { TutorialForceDefeatPresentation = new(operation, exit) });
    }

    private static InvalidDataException TutorialDefeatMissing(string field) => new("tutorial.force-defeat." + field);
}
