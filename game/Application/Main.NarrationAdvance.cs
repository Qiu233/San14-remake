public partial class Main
{
    // Each narration also checks its own parent/world identity. These overlays
    // suspend the presentation wait instead of accumulating background time.
    private bool CanAdvanceNarration => !_failed && !_shuttingDown && !IsReturningToTitle &&
        !HasNormalStoryResult && !HasEnvironmentSettings && !HasCampaignSavePage && !HasMapSystemMenu && !HasActionReport &&
        _detailsView is null;
}
