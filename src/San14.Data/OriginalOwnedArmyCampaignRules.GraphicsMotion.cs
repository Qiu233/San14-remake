using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyGraphicsCurrentInputs(ushort Commander12,
    ushort CurrentHex2A, byte Direction36, bool ArmyValid, byte Flags26,
    byte Abnormal23, float Mobility2B0660);

public sealed partial class OriginalOwnedArmyCampaignRules
{
    OriginalArmyMobilityCurrentProducer? graphicsMotionMobility;

    /// <summary>Current source-created ordinary Army inputs. Reuses this
    /// campaign's loaded tables/seed and the existing 2B0660 consumer; it creates
    /// no campaign, resources or source image reads. Legacy absence stays unknown.</summary>
    public OriginalArmyGraphicsCurrentInputs? CaptureGraphicsMotionInputs(
        GameState state, UnitState army)
    {
        if (army.NativeArmyOrigin is not { NativeSlotIndex: int physical } ||
            army.NativeTacticRuntime is not { } runtime ||
            army.NativeCurrentHex2A is not ushort currentHex || army.NativeDirection36 is not byte direction)
            return null;

        ushort commander;
        if (state.NativeArmyLifecycle is { } arena)
        {
            if ((uint)physical >= arena.Slots.Length ||
                arena.Slots[physical] is not { } slot || slot.PhysicalSlot != physical ||
                slot.ActiveUnitId != army.Id || slot.Commander12 is not ushort actualCommander)
                return null;
            commander = actualCommander;
        }
        else
        {
            // The proved ordinary creator writes its packet commander to12;
            // the canonical OfficerId is that packet ID, admitted only1..6000.
            if (army.OfficerId is < 1 or > 6000)
                return null;
            commander = (ushort)army.OfficerId;
        }

        graphicsMotionMobility ??= new(this, _mobilitySeed, _map, _terrain, _catalog);
        var mobility = graphicsMotionMobility.Resolve(state, army);
        return mobility.Receipt is { } receipt
            ? new(commander, currentHex, direction, true, runtime.Flags26, runtime.Abnormal23, receipt.Value)
            : null;
    }
}
