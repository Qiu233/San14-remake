using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7CC870 mode1 publishes resource5757 after Yes and before
    /// the ordered packet commit. The caller retains this one publication
    /// throughout the real drawn-page and advance lifecycle.</summary>
    public DeploymentNarrationQuery QueryGeneralUnitTargetNarration(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows,string locale,Func<int,int> appPresentationRandom)
        => QueryDeploymentNarrationCurrent(state,rows,locale,appPresentationRandom,true);
}
