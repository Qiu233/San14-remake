using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record TutorialLessonCompletionResult(GameState State,
    ImmutableArray<TutorialLessonCompletionNotice> Notices);

public sealed partial class ArmyCampaignRuleAdapter
{
    //515E50 is called by the actual lesson Exit after the Ending script returns.
    //It records application writes in the same monotone world outbox as Ending.
    internal TutorialLessonCompletionResult CompleteTutorialLessonCurrent(GameState state, int courseIndex)
    {
        if ((uint)courseIndex >= 8) throw new ArgumentOutOfRangeException(nameof(courseIndex));
        if (ReadApplicationGameplay()?.TutorialServicePresent != true)
            throw TutorialCompletionMissing("application_tutorial_service");
        if (TacticRemakeCurrentBattleControl.ReadHeader(state) is null)
            throw TutorialCompletionMissing("controller_header");
        var current = CaptureDeploymentPersonSelectionCurrent(state);
        var notices = new List<(int Root, int? Sound)>();
        int[] rewards = TutorialDefinitions.Rewards[courseIndex];
        GameState working = PrepareApplicationServiceCurrent(state, current, profile =>
        {
            //The source temporarily writes the actual tutorial header to zero
            //before calling3342B0. Preserve its real service presence and all
            //other eligibility inputs rather than guessing an eligible mode.
            var previousMode = current.TransferModeStateReader;
            current.TransferModeStateReader = () => new(true, 0);
            try
            {
                if (Eligible()) { profile.SetTutorialCompleted(courseIndex, true); profile.WriteDirtyFlag(1); }
                bool newReward = false;
                foreach (int nameId in rewards)
                {
                    int index = nameId == 0 ? 0 : nameId is >= 2068 and <= 2077 ? nameId - 2023 :
                        nameId is >= 2083 and <= 2088 ? nameId - 2028 : nameId - 2001;
                    //3EC800 maps raw zero to bonus index0. The caller still
                    //reads/writes that bit; only its two-name notice differs.
                    bool old = profile.ReadBonusOfficerUnlocked(index)
                        ?? throw TutorialCompletionMissing("bonus_unlock_knowledge");
                    if (old) continue;
                    if (Eligible()) { profile.SetBonusOfficerUnlocked(index, true); profile.WriteDirtyFlag(1); }
                    newReward = true; //The caller does not branch on the setter's return.
                }
                if (newReward) notices.Add((rewards[2] == 0 ? 0x9C5C : 0x9C5B, 5));
                for (int index = 0; index < 8; index++)
                    if (!(profile.ReadTutorialCompleted(index) ?? throw TutorialCompletionMissing("all_courses_knowledge")))
                        return profile;
                uint track = profile.ReadEndingTrack(2) ?? throw TutorialCompletionMissing("completion_track_knowledge");
                if (track == 0 && Eligible())
                {
                    profile.SetEndingTrack(2, 1); profile.WriteDirtyFlag(1);
                    notices.Add((0x34EC, null));
                }
                return profile;
            }
            finally
            {
                current.TransferModeStateReader = previousMode;
            }
            bool Eligible() => current.ReadTransferServiceEligibility()
                ?? throw TutorialCompletionMissing("eligibility");
        });
        var prepared = ImmutableArray.CreateBuilder<TutorialLessonCompletionNotice>(notices.Count);
        foreach (var notice in notices)
        {
            var text = FormatNormalTurnReport(working, current, notice.Root,
                new(Person0: rewards[0], Person1: rewards[1], Person2: rewards[2]));
            working = text.State;
            prepared.Add(new(text.Text, notice.Sound));
        }
        return new(working, prepared.ToImmutable());
    }
    private static InvalidDataException TutorialCompletionMissing(string field) => new("tutorial.completion." + field);
}
