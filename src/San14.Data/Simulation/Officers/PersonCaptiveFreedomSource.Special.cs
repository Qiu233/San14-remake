using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The bit14 early-return of265250. It borrows the enclosing
    /// operation and leaves actual template reclamation to Turn action53.</summary>
    private GameState ApplySpecialCaptiveFreedomTailCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, NativeMapSeasonDate? date,
        Func<int> readDay, Func<TacticSourceSamplerWorld> readSampler, List<CampaignEffectRequest> effects)
    {
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
            ?? throw CaptiveFreedomMissing("special_22A200_Army");
        if (CaptiveFreedomNeed(current.ReadArmyUnitNativeValidity(army), "special_Army_validity"))
        {
            int physical = CaptiveFreedomNeed(army.PhysicalSlot, "special_Army_physical");
            bool excluded = current.ReadOwnedArmyExclusion233560(new(RegistryKind.Army, physical))
                ?? throw CaptiveFreedomMissing("special_Army233560");
            if (!excluded)
            {
                //Only the same259360(mode0,cause4) kernel is shared here.
                //No2CD1C0 tribal recall/status7 operation is performed.
                var removed = new OwnedArmyDestructionStage(this).ApplySpecialPersonnelRecallCause4Current(
                    state, current, pool, physical, date ?? throw CaptiveFreedomMissing("special_cause4_date"),
                    effects, readSampler());
                state = removed.State ?? throw CaptiveFreedomMissing(removed.UnresolvedOwner ?? "special_259360_cause4");
            }
        }
        //The nested destructor can create a Return association. Read the
        //actual current association and status again, in the native order.
        var linked = current.ReadPersonLinkedEntity(person) ?? throw CaptiveFreedomMissing("special_linked22AC00");
        if (CaptiveFreedomNeed(current.ReadLinkedEntityNativeValidity(linked), "special_linked_validity"))
        {
            var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(state, current, person);
            state = state with { PersonnelCommands = detached.Owner
                ?? throw CaptiveFreedomMissing(detached.UnresolvedOwner ?? "special_228890") };
        }
        if (CaptiveFreedomNeed(current.ReadPersonStatus(person), "special_fresh_status5") == 5) return state;
        return QueuePersonnelHireReleaseCurrent(state, current, person, readDay, effects);
    }

    /// <summary>32BC50's existing hire-manager producer. Duplicate pointer
    /// admission precedes every Force and Person write. No reset is done here.</summary>
    private GameState QueuePersonnelHireReleaseCurrent(GameState state, SourceWorldContext current,
        int person, Func<int> readDay, List<CampaignEffectRequest> effects)
    {
        var owner = state.PersonnelHire ?? throw CaptiveFreedomMissing("special_hire_manager");
        var pending = owner.PendingReleaseOfficerIds ?? throw CaptiveFreedomMissing("special_hire_pending_owner");
        foreach (int queued in pending)
            if (Pointer(queued) == person) return state;

        var force = current.ReadPersonForce(person) ?? throw CaptiveFreedomMissing("special_fresh_Force_pointer");
        if (force.PhysicalSlot is null ||
            !CaptiveFreedomNeed(current.ReadForceNativeValidity(force), "special_fresh_Force_validity")) return state;
        int forceId = CaptiveFreedomNeed(current.ReadForcePhysicalSlot(force), "special_fresh_Force_identity");
        var roster = owner.Forces.GetValueOrDefault(forceId) ?? throw CaptiveFreedomMissing("special_Force260_owner");
        if (roster.HiredOfficerIds.IsDefault) throw CaptiveFreedomMissing("special_Force260_knowledge");
        var hired = roster.HiredOfficerIds;
        for (int index = 0; index < hired.Length; index++)
            if (Pointer(hired[index]) == person) { hired = hired.RemoveAt(index); break; }
        state = state with { PersonnelHire = owner with
        { Forces = owner.Forces.SetItem(forceId, roster with { HiredOfficerIds = hired }) } };

        current.WriteFormerForceKnowledge(person, 0);
        current.WritePersonStatus(person, 5);
        var assignments = new List<PersonAssignmentPrecallResult>();
        PersonDistrictTransfer.Apply(person, 0,
            ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments, result =>
                state = ApplyPersonAssociationRemovalReports(state, current, person, result, readDay, effects)));
        state = PersonAssignmentPrecallInputs.PublishDeathTouched(state, current, person, assignments);
        //240A30(Person,kind5,index0) clears Home/E2. It does not touch E4.
        current.WritePersonFirstLocation(person, 0);
        ushort id = CaptiveFreedomNeed(current.ReadPersonIdentity(person), "special_release_Person10");
        var officer = state.Officers.GetValueOrDefault(id) ?? throw CaptiveFreedomMissing("special_release_Person_owner");
        var native = state.NativeArmySimulation ?? throw CaptiveFreedomMissing("special_release_saved_owner");
        byte district = CaptiveFreedomNeed(current.ReadPersonDistrictSlot(person), "special_release_E0");
        var status = OfficerServiceStatusSourceAdapter.Decode(CaptiveFreedomNeed(current.ReadPersonStatus(person), "special_release_E6"));
        state = state with { Officers = new(state.Officers) { [id] = officer with
        {
            ForceId = null, DistrictId = district == 0 ? null : district, LocationId = null,
            ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, officer.Role),
            LocationState = current.ReadOfficerLocation(id) ?? throw CaptiveFreedomMissing("special_release_location"),
            Behavior = current.ReadPersonBehavior(person)
        } }, NativeArmySimulation = native with { Parameters = native.Parameters with
        { Int32Values = new(native.Parameters.Int32Values)
            { [OwnedArmyLifecycleDetachedInputs.Key("Person", person, "E0")] = district,
              [OwnedArmyLifecycleDetachedInputs.Key("Person", person, "E2")] = 0 } } } };
        state = OfficerLocationSourceAdapter.PublishPosition(state, id, current);
        state = current.PublishOfficerAftermath(state, id);
        //Use the latest hire owner: shared callbacks may have changed their
        //own fields. Contracts/capacity and the Person registration stay alive.
        owner = state.PersonnelHire ?? throw CaptiveFreedomMissing("special_release_latest_manager");
        var latestPending = owner.PendingReleaseOfficerIds ?? throw CaptiveFreedomMissing("special_release_latest_pending");
        return state with { PersonnelHire = owner with { PendingReleaseOfficerIds = latestPending.Add(id) } };

        int Pointer(int officerId) => current.ResolveOfficerSource(officerId)
            ?? throw CaptiveFreedomMissing("special_hire_Person_pointer:" + officerId);
    }

    internal GameState ApplyCapturedSpecialPersonReleaseCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, NativeMapSeasonDate? date, TacticSourceSamplerWorld? sampler,
        List<CampaignEffectRequest> effects)
    {
        TacticSourceSamplerWorld ReadSampler() => sampler ?? ReadArmyDestructionSampler(state, current)
            ?? throw CaptiveFreedomMissing("forced_special_sampler");
        return ApplyCaptiveFreedomCurrent(state, current, pool, person, date,
            () => ReadSampler().Day450, ReadSampler, effects);
    }
}
