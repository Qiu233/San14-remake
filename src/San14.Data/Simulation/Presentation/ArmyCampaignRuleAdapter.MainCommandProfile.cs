using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public long MainCommandPresentationRevision { get; private set; }

    /// <summary>450AE0's actual child-state launch, after the host has accepted
    /// that launch. Category expansion does not mark these first-use bits.</summary>
    public bool MarkMainCommandFeatureSeen(GameState state, MapMenuCommand command)
    {
        int? bit = command switch
        {
            MapMenuCommand.Research => 0, MapMenuCommand.Employ => 1,
            MapMenuCommand.Merit => 2, MapMenuCommand.Award => 3,
            MapMenuCommand.Spy => 4, MapMenuCommand.Scout => 5,
            MapMenuCommand.Patrol => 12, MapMenuCommand.Rumor => 11, _ => null
        };
        return bit is int index && MarkMainCommandSeenCurrent(state, index);
    }

    /// <summary>8448D0 acknowledges Events only at the actual category2 tab.</summary>
    public bool MarkMainCommandEventCategorySeen(GameState state, int category)
        => category == 2 && MarkMainCommandSeenCurrent(state, 6);

    private bool MarkMainCommandSeenCurrent(GameState state, int index)
    {
        if (ReadApplicationTutorialActive() ?? throw MainCommandMissing("seen_tutorial_controller")) return false;
        if (ReadApplicationInformationRestriction(state) ?? throw MainCommandMissing("seen_selected_user")) return false;
        var store = applicationProfileStore ?? throw MainCommandMissing("seen_application_profile");
        var next = store.Current;
        if (next.ReadMainCommandSeen(index) == true) return false;
        next.SetMainCommandSeen(index, true);
        //Native DE0/6F0 are working/backing mirrors of this same owner. The
        //atomic store replaces it only after persistence succeeds. No Steam,
        //dirty, World, RNG or invented presentation receipt is touched.
        var saved = store.Save(next);
        applicationProfileOperation = new(saved);
        MainCommandPresentationRevision = unchecked(MainCommandPresentationRevision + 1);
        return true;
    }
}
