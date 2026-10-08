using San14.Core;

namespace San14.Data;

/// <summary>Actual16EB20 occupant, then ordinary170900 target admission.
/// One definition view belongs to this Army's current movement pass.</summary>
public sealed class OriginalPlayableArmyContact(OriginalOwnedArmyCampaignRules campaign) : IPlayableArmyContactRules
{
    public Func<int, PlayableArmyContact?> CaptureCurrentArmyContact(GameState state)
    {
        var current = campaign.CaptureCurrentDefinitions(state);
        return hex =>
        {
            if (current is null) return null;
            var read = OriginalFieldHexOccupantSource.ReadCurrent(state, current, hex);
            if (read.Receipt is not { } selected) return null;
            if (selected.NativeKind != 27) return new(null);
            return OriginalFieldHexOccupantSource.ReadOrdinaryFieldArmyEligibility(state, current, selected) switch
            {
                true => new(selected.GameplayArmyId), false => new(null), _ => null
            };
        };
    }

    public bool? CanAttackFromCurrent(GameState state, UnitState actor)
    {
        var current = campaign.CaptureCurrentDefinitions(state);
        if (current is null || actor.NativeArmyOrigin?.NativeSlotIndex is not int physical) return null;
        bool? ordinary = OriginalFieldHexOccupantSource.ReadOrdinaryFieldArmyEligibility(state, current,
            new(27, physical, null, actor.Id, null));
        if (ordinary != true) return ordinary;
        //170240 actor gates are additional to170900 target admission. In
        //particular a confused target remains selectable by another Army.
        if (actor.Troops == 0) return false;
        if (actor.NativeTacticRuntime is not { } native) return null;
        if (native.Abnormal23 != 0) return false;
        if (actor.NativeArmyConfiguration?.Action37 is not byte action) return null;
        //2C9070 immediately returns0 for non16 actions or positive current
        //23/24/25. The remaining16 destination/path owner is not replaced
        //by the Core retreat plan. Actor23 already vetoed above.
        return action != 0x10 || native.Abnormal24 != 0 || native.Abnormal25 != 0 ? true : null;
    }
}
