using San14.Client.Gameplay;
using San14.Data;

public partial class Main
{
    private CampaignComposition? _campaignComposition;
    private ArmyCampaignRuleAdapter? _ownedArmyCampaign => _campaignComposition?.Army;

    private void PublishCampaignPresentation(CampaignPresentation presentation)
    {
        SetMeta("playable_owned_army_rules", presentation.ArmyRuleVersion is not null);
        if (presentation.ArmyRuleVersion is { } version)
        {
            SetMeta("playable_owned_army_rule_version", version);
            SetMeta("playable_owned_army_source_revision", presentation.ArmySourceRevision!);
        }
        else
        {
            RemoveMeta("playable_owned_army_rule_version");
            RemoveMeta("playable_owned_army_source_revision");
        }
        if (presentation.Income is { } income)
        {
            SetMeta("playable_income_difficulty_rule_version", income.RuleVersion);
            SetMeta("playable_income_player_gold_percent", income.GoldPercent);
            SetMeta("playable_income_player_supplies_percent", income.SuppliesPercent);
        }
        else
        {
            RemoveMeta("playable_income_difficulty_rule_version");
            RemoveMeta("playable_income_player_gold_percent");
            RemoveMeta("playable_income_player_supplies_percent");
        }
    }
}
