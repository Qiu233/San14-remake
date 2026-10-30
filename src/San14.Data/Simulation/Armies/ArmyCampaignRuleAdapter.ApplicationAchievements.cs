namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Actual generic application achievement tail shared by Ending and rank.
    private static void SetApplicationAchievementAndCompletionCurrent(SourceWorldContext current,
        TransferServiceState profile, int flag)
    {
        if (profile.PlatformPolicy != TransferServicePlatformPolicy.Absent)
            throw ApplicationAchievementMissing("platform_preparation");
        TransferService.SetFlag(flag, current, profile);
        for (int bit = 1; bit <= 37; bit++)
            if (!ApplicationAchievementNeed(profile.ReadPrimaryFlag(bit), "completion_achievement")) return;
        TransferService.SetFlag(0, current, profile);
    }

    private static T ApplicationAchievementNeed<T>(T? value, string owner) where T : struct
        => value ?? throw ApplicationAchievementMissing(owner);
    private static InvalidDataException ApplicationAchievementMissing(string owner)
        => new("application.achievement.current." + owner);
}
