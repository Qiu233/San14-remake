using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyDestructionStage
{
    //259360 param3 is a real Base pointer. An ordinary City winner uses its
    //Force/resources; it does not become a synthetic Army or injury Person.
    private static OriginalBaseReference ResolveWinnerBase(OriginalEventInitializationContext current,
        NativeArmyDestructionReceiverReference? receiver, bool winnerArmyValid)
    {
        if (receiver is null) throw Missing("current.winner_Base_pointer");
        if (receiver.PhysicalSlot is null) return new(OriginalBaseKind.City, null);
        if (winnerArmyValid) throw Missing("branch.simultaneous_Army_and_Base_winners");
        if (receiver.Kind != NativeArmyDestructionReceiverKind.City || receiver.PhysicalSlot is < 1 or > 51)
            throw Missing("branch.nonordinary_City_winner");
        var city = new OriginalBaseReference(OriginalBaseKind.City, receiver.PhysicalSlot);
        _ = Known(current.ReadBaseNativeValidity(city), "winner_City_validity");
        return city;
    }
}
