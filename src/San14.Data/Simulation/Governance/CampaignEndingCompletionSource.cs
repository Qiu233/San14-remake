using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record CampaignEndingCompletionResult(GameState State,
    ImmutableArray<CampaignEndingCompletionNotice> Notices);

public sealed partial class ArmyCampaignRuleAdapter
{
    //The Ending calls these guarded achievement leaves on entering its actual
    //branches (3E6FB0/3E8FA0/3E8EE0/3E9060/3E6F00), before final completion.
    internal GameState PrepareEndingBranchProgressCurrent(GameState state,
        SourceWorldContext current, int stage)
    {
        if (stage == 7) return state;
        int flag = stage switch
        {
            6 => 2, 8 => 39, 9 => 38, 10 => 40, 11 or 12 => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
        return PrepareApplicationServiceCurrent(state, current, profile =>
        {
            PrepareEndingAchievementCurrent(current, profile, flag);
            return profile;
        });
    }

    //44FBD0's completion body, reached only by the admitted Ending owner.
    //Profile writes remain an operation outbox until the host adopts this state.
    internal CampaignEndingCompletionResult CompleteCampaignEndingProgressCurrent(
        GameState state, SourceWorldContext current, CampaignEndingAdmission admission,
        int endingCompletionTrack)
    {
        var notices = new List<(int Root, int Person, int? Sound)>();
        var working = PrepareApplicationServiceCurrent(state, current, profile =>
        {
            if (admission.FromEvent900)
            {
                //44FCC0 passes literal third argument zero to 3EC600: its
                //return is zero on this path, so every source branch uses3236.
                notices.Add((0x3236, 0, null));
                if (EndingProfileEligible(current)) UnlockScenarioBonus();
                return profile;
            }

            if (endingCompletionTrack is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(endingCompletionTrack));
            uint oldTrack = EndingProfileNeed(profile.ReadEndingTrack(endingCompletionTrack), "track");
            if (oldTrack == 0 && EndingProfileEligible(current))
            {
                profile.WriteDirtyFlag(1);
                profile.SetEndingTrack(endingCompletionTrack, 1);
                notices.Add((endingCompletionTrack == 0 ? 0x3234 : 0x3235, 0, null));
            }
            //333F00 observes World16A8 bit8 after the track write/notice.
            if ((EndingProfileNeed(current.ReadWorldTransferServiceFlags(), "observer_flags") & 0x100) != 0)
                return profile;

            int scenario = Scenario();
            //3F0A70 can establish this one bit without reading the other199.
            if ((uint)scenario < 200 && EndingProfileEligible(current))
            {
                profile.WriteDirtyFlag(1);
                profile.SetScenarioCompleted(scenario, true);
            }
            //3EA990 retains the generic all-scenarios check even when the
            //scenario has no individual achievement or bonus mapping.
            if (scenario is >= 1 and <= 8)
                PrepareEndingAchievementCurrent(current, profile, scenario + 3);
            if (EndingProfileEligible(current)) UnlockScenarioBonus();
            if (EndingProfileEligible(current))
            {
                bool all = true;
                for (int bit = 4; bit <= 11; bit++)
                    if (!EndingProfileNeed(profile.ReadPrimaryFlag(bit), "scenario_achievement"))
                    { all = false; break; }
                if (all) SetEndingAchievementAndCompletion(current, profile, 3);
            }
            return profile;

            int Scenario() => EndingProfileNeed(current.ReadScenarioId(), "scenario_id");
            void UnlockScenarioBonus()
            {
                //3F4910 maps scenario identity to a resource person name.
                int name = Scenario() switch
                {
                    1 => 2007, 2 => 2005, 3 => 2008, 4 => 2009,
                    5 => 2012, 6 => 2017, 7 => 2010, 8 => 2016, _ => 0
                };
                if (name == 0) return;
                int index = name - 2001;
                if (EndingProfileNeed(profile.ReadBonusOfficerUnlocked(index), "bonus_officer")) return;
                //3E6D70 emits its sound/message after an old-false bit even
                //if the repeated eligibility gate prevents the actual write.
                if (EndingProfileEligible(current))
                {
                    profile.WriteDirtyFlag(1);
                    profile.SetBonusOfficerUnlocked(index, true);
                }
                notices.Add((0x323C, name, 5));
            }
        });
        var prepared = ImmutableArray.CreateBuilder<CampaignEndingCompletionNotice>(notices.Count);
        foreach (var notice in notices)
        {
            var formatted = FormatNormalTurnReport(working, current, notice.Root,
                new(Person0: notice.Person));
            working = formatted.State;
            prepared.Add(new(formatted.Text, notice.Sound));
        }
        return new(working, prepared.ToImmutable());
    }

    private static void PrepareEndingAchievementCurrent(SourceWorldContext current,
        TransferServiceState profile, int flag)
    {
        if (EndingProfileEligible(current)) SetEndingAchievementAndCompletion(current, profile, flag);
    }

    private static void SetEndingAchievementAndCompletion(SourceWorldContext current,
        TransferServiceState profile, int flag) => SetApplicationAchievementAndCompletionCurrent(current, profile, flag);

    private static bool EndingProfileEligible(SourceWorldContext current) =>
        EndingProfileNeed(current.ReadTransferServiceEligibility(), "eligibility");
    private static T EndingProfileNeed<T>(T? value, string name) where T : struct =>
        value ?? throw EndingProfileMissing(name);
    private static InvalidDataException EndingProfileMissing(string name) =>
        new("campaign.ending.profile." + name);
}
