using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The direct holder store is shared by diplomacy and ordinary events.
    //It does not acquire/register an Item or normalize its independent status.
    private static GameState WriteDirectItemHolderCurrent(GameState state, SourceWorldContext current,
        int physical, int holder, bool clearReservationFlag)
    {
        var items = state.Items ?? throw ItemHolderMissing("inventory");
        var item = items.GetValueOrDefault(physical) ?? throw ItemHolderMissing("item");
        var lifecycle = item.Lifecycle;
        if (clearReservationFlag)
        {
            byte flags = lifecycle?.RetainedFlags ?? throw ItemHolderMissing("reservation_flags");
            lifecycle = lifecycle! with { RetainedFlags = (byte)(flags & 0xF7) };
        }
        int owner = Math.Min(holder, 6000);
        current.SetCaptureItemOwner4A(physical, (ushort)owner);
        return state with { Items = new(items) { [physical] = item with
            { Holder = new(owner == 0 ? null : owner), Lifecycle = lifecycle } } };
    }

    private static InvalidDataException ItemHolderMissing(string owner)
        => new("item.holder.current." + owner);
}
