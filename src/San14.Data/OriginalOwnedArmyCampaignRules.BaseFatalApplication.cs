using San14.Core;

namespace San14.Data;

public sealed record OriginalBaseFatalAdmissionReceipt(OriginalBaseReference Target, int AttackerArmyId,
    int AttackerPhysicalSlot, OriginalDistrictReference CurrentAttackerDistrict, int NativeReturn31F160,
    int? AllocatedNode, bool Duplicate, bool ImmediateFallback);
public sealed record OriginalNormalBaseFatalApplicationReceipt(int LocationId, OriginalBaseKind Kind,
    int RequestedLoss, int AppliedLoss, OriginalPersonNormalArmyRewardResult AttackerReward,
    OriginalNormalCityCapacityWriteReceipt? TroopDebit, OriginalNormalCityWoundedReceipt? Wounded,
    OriginalNormalArmyCombatMoraleReceipt? MoraleLoss, OriginalNormalCityMoraleWriteReceipt? MoraleWrite,
    bool NativeReturn, OriginalBaseFatalAdmissionReceipt? Admission);
public sealed record OriginalNormalBaseFatalApplicationResult(OriginalNormalCityApplicationOutcome Outcome,
    GameState State, OriginalNormalBaseFatalApplicationReceipt? Receipt, string? UnresolvedOwner);

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>Actual ordinary Army→Base178EC0, including a reached fatal
    ///1745B0 admission after wounded/morale. Returntrue is the native branch,
    ///not proof of allocation or capture. This operation does not runstage19.</summary>
    public OriginalNormalBaseFatalApplicationResult TryApplyNativeNormalBaseFatalApplication(GameState input,
        int attackerArmyId, int locationId, int requestedLoss,
        OriginalNativeArmyCombatCycleSampler? operationSampler = null)
    {
        OriginalBaseKind kind = locationId <= 51 ? OriginalBaseKind.City : OriginalBaseKind.Gate;
        var result = TryApplyNativeNormalBaseApplication(input, attackerArmyId, kind, locationId,
            requestedLoss, operationSampler, allowFatal: true);
        var r = result.Receipt;
        return new(result.Outcome, result.State, r is null ? null : new(r.LocationId, kind,
            r.RequestedLoss, r.AppliedLoss, r.AttackerReward, r.TroopDebit, r.Wounded,
            r.MoraleLoss, r.MoraleWrite, r.NativeReturn, r.FatalAdmission), result.UnresolvedOwner);
    }

    private static OriginalBaseFatalAdmissionReceipt ApplyOrdinaryBaseFatalAdmission(GameState state,
        OriginalEventInitializationContext current, UnitState attacker, OriginalBaseReference target)
    {
        var army = new OriginalArmyUnitReference(attacker.NativeArmyOrigin?.NativeSlotIndex);
        int physical = army.PhysicalSlot ?? throw new InvalidDataException("current.Base1745B0.attacker_pointer");
        if (current.ReadArmyUnitNativeValidity(army) != true)
            throw new InvalidDataException("current.Base1745B0.attacker_validity");
        int commanderId = current.ReadArmyUnitCommanderId(army)
            ?? throw new InvalidDataException("current.Base1745B0.Commander12");
        int commander = current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)?.PhysicalSlot
            ?? throw new InvalidDataException("current.Base1745B0.Commander_pointer");
        byte districtRaw = current.ReadPersonDistrictSlot(commander)
            ?? throw new InvalidDataException("current.Base1745B0.CommanderE0");
        var district = current.ReadDistrictTableReference(districtRaw <= 51 ? districtRaw : 0)
            ?? throw new InvalidDataException("current.Base1745B0.saved_District22BB10");
        //22ECB0 formats the Army's Commander name into the text service;
        //the unused presentation result has no gameplay owner here.
        var queue = current.OwnedDestructionQueue
            ?? throw new InvalidDataException("current.Base1745B0.manager140");
        var payload = new NativeArmyDestructionQueuePayload(new(NativeArmyDestructionReceiverKind.Army, null),
            new(target.Kind == OriginalBaseKind.City ? NativeArmyDestructionReceiverKind.City : NativeArmyDestructionReceiverKind.Gate,
                target.PhysicalSlot), new(NativeArmyDestructionReceiverKind.Army, physical),
            new(NativeArmyDestructionReceiverKind.City, null), 0, 0, 4, null);
        var admitted = OriginalArmyDestructionQueue.Admit31F160(queue, payload, OriginalArmyDestructionQueue.BindCurrent(current));
        int nativeReturn = admitted.NativeReturn ?? throw new InvalidDataException(admitted.UnresolvedOwner ?? "current.Base1745B0.queue_admission");
        current.BindOwnedDestructionQueue(admitted.State);
        return new(target, attacker.Id, physical, district, nativeReturn, admitted.AllocatedNode,
            admitted.Duplicate, nativeReturn == 0);
    }
}
