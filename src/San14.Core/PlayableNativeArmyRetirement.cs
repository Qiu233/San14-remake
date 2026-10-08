using System.Text.Json;

namespace San14.Core;

/// <summary>Preserves the complete current native receiver when the provisional
/// world removes its active Army projection. This does not execute an original
/// return, destructor, pending70 admission, pool release or Hex/header writer.</summary>
public static class PlayableNativeArmyRetirement
{
    public const string RuleId = "san14:provisional-native-army-retirement-v1";

    public static GameState PreservePendingImage(GameState postRemoval, UnitState retired)
    {
        ArgumentNullException.ThrowIfNull(postRemoval);
        ArgumentNullException.ThrowIfNull(retired);
        if (postRemoval.NativeArmyLifecycle is not { } lifecycle || retired.NativeArmyOrigin is not { } origin)
            return postRemoval;
        if (postRemoval.Units.ContainsKey(retired.Id) || origin.NativeSlotIndex is not int physical ||
            physical < 0 || physical >= lifecycle.Slots.Length)
            throw new InvalidDataException("Native Army retirement has no removed projection and exact saved receiver.");
        NativeArmyLifecycleSlot slot = lifecycle.Slots[physical];
        if (slot.ActiveUnitId != retired.Id || slot.PendingImage is not null ||
            slot.Commander12 != retired.OfficerId || slot.Status10 != retired.NativeTacticRuntime?.Status10)
            throw new InvalidDataException("Native Army retirement does not match its current saved slot/header.");
        //Keep the receiver detached from transient live Army arrays. Historical
        //ready troops are the settled quantity, including zero after combat;
        //independent native fields remain exactly as their owners last wrote.
        //The removed projection cannot execute a queued playable tactic, even
        //when retirement occurs during strategy (for example immediate disband).
        //Do not change native waiting, abnormal counters or other source fields.
        UnitState terminal = retired with { TacticRuntime = retired.TacticRuntime is { } runtime
            ? runtime with { Pending = null } : null };
        UnitState image = JsonSerializer.Deserialize<UnitState>(JsonSerializer.SerializeToUtf8Bytes(terminal))
            ?? throw new InvalidDataException("Native Army retirement image could not be preserved.");
        var slots = lifecycle.Slots.ToArray();
        slots[physical] = slot with { ActiveUnitId = null, PendingImage = image };
        return postRemoval with { NativeArmyLifecycle = lifecycle with { Slots = slots } };
    }
}
