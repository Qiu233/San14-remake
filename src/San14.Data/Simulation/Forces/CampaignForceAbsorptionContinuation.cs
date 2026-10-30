using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Resume the existing2A2760 after its actual automatic emperor
    /// wait. Its relationship/item/object/captive/Army prefix is already done.</summary>
    internal GameState ResumeNormalTurnForceAbsorptionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, AutomaticEmperorCaptureInputs? emperorInputs = null,
        bool? eventPlaybackPresent = null)
    {
        var saved = state.ForceAbsorptionContinuation ?? throw ForceAbsorptionMissing("resume_owner");
        //These are actual saved VM caller tails, not generic Story activity.
        //Normal coalition/demand/NPC callers have no CEventPlaybackState.
        bool playback = eventPlaybackPresent ?? (state.StoryForceAbsorptionTail is not null ||
            state.EditedEventForceAbsorptionTail is not null);
        //Only the proven non-playback constructor/retired history-manager+10
        //is0. Story and edited callers retain their actual supplied/unknown
        //invocation input; never borrow their independent +C/1B0 gates.
        if (!playback && emperorInputs is null) emperorInputs = new(null, null, new(null, null, 0));
        var working = ResumeForceTransferEmperorCurrent(state, current, pool, emperorInputs,
            suppressPrimary: true, eventPlaybackPresent: playback);
        if (working.AutomaticEmperorCapture is not null) return working;
        var effects = new List<CampaignEffectRequest>();
        var from = new ForceReference(saved.OriginalForcePhysicalSlot);
        var to = new ForceReference(saved.RecipientForcePhysicalSlot);
        var primary = new DistrictReference(saved.PrimaryDistrictPhysicalSlot);
        HexListHandle? districts = null, bases = null;
        try
        {
            districts = current.CreatePersonInjuryTemporary();
            foreach (int district in saved.DistrictPhysicalSlots)
                AbsorptionAppend(current, districts, new(RegistryKind.District, district), requireAllocation: true);
            bases = current.CreatePersonInjuryTemporary();
            foreach (var previous in saved.ProcessedBases)
                AbsorptionAppendBase(current, bases, ForceAbsorptionCurrentBase(previous), requireAllocation: true);
            for (int index = saved.BaseIndex; index < saved.RemainingBases.Length; index++)
            {
                var target = ForceAbsorptionCurrentBase(saved.RemainingBases[index]);
                //The source tests current ownership just before each element.
                //A saved suffix is not a saved claim that every Base qualifies.
                if (saved.OriginalForcePhysicalSlot is < 1 or > 51 ||
                    AbsorptionK(current.ReadBaseForceSlot(target), "resume_Base_Force") != saved.OriginalForcePhysicalSlot) continue;
                AbsorptionAppendBase(current, bases, target);
                var ruler = current.ReadPersonLookupReference(saved.RecipientRulerId <= 6000 ? saved.RecipientRulerId : 0)
                    ?? throw ForceAbsorptionMissing("resume_recipient_ruler");
                working = ApplyForceAbsorptionBaseTransferCurrent(working, current, pool, target, primary, ruler,
                    saved.OperationDate, effects, emperorInputs, playback);
                if (working.ForceTransferEmperorTail is not null)
                {
                    working = working with { ForceAbsorptionContinuation = saved with
                    {
                        ProcessedBases = AbsorptionBases(current, bases).Select(at => ForceAbsorptionSavedBase(current, at)).ToImmutableArray(),
                        BaseIndex = index + 1
                    } };
                    return AppendForceTransferEmperorEffects(working, effects);
                }
            }
            return CompleteNormalTurnForceAbsorptionTailCurrent(working, current, pool, from, to, primary,
                saved.OperationDate, saved.SuppressEliminationPresentation, bases, effects);
        }
        finally
        {
            if (bases is not null) current.ClearPersonInjuryTemporary(bases, true);
            if (districts is not null) current.ClearPersonInjuryTemporary(districts, true);
        }
    }

    private GameState CompleteNormalTurnForceAbsorptionTailCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference from, ForceReference to,
        DistrictReference primary, NativeMapSeasonDate date, bool suppressEliminationPresentation,
        HexListHandle bases, List<CampaignEffectRequest> effects)
    {
        var working = state;
        int original = AbsorptionK(from.PhysicalSlot, "tail_original_Force");
        int recipient = AbsorptionK(to.PhysicalSlot, "tail_recipient_Force");
        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current)
            ?? throw ForceAbsorptionMissing("tail_World450_45C");
        int Day() => Sampler().Day450;
        GameState PublishEffects(GameState value) => AppendForceTransferEmperorEffects(value, effects);
            //Two ordered full Hex traversals are native business writers, not
            //online correctness scans. Their distinct timing preserves a live
            //third-party Army's later restoration of its occupied cells.
            for (int hex = 0; hex < 48400; hex++)
                if (AbsorptionK(current.ReadHexForceSlot(new(hex)), "source_Hex_Force") == original)
                    working = TransferAbsorptionHexCurrent(working, current, new(hex), recipient, 0);
            for (int hex = 0; hex < 48400; hex++)
            {
                var tile = new HexReference(hex);
                if (AbsorptionK(current.ReadHexForceSlot(tile), "recipient_Hex_Force") != recipient) continue;
                int rawArmy = AbsorptionK(current.ReadHexRaw1C(tile), "Hex_Army");
                var army = current.ReadArmyUnitTableReference(rawArmy is >= 1 and <= 500 ? rawArmy : 0)
                    ?? throw ForceAbsorptionMissing("Hex_Army_pointer");
                if (!AbsorptionK(current.ReadArmyUnitNativeValidity(army), "foreign_Army_validity")) continue;
                int physical = AbsorptionK(army.PhysicalSlot, "foreign_Army_pointer");
                if (AbsorptionK(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, physical), "foreign_Army_excluded")) continue;
                var force = current.ReadArmyUnitForceReference(army);
                int other = AbsorptionK(current.ReadForcePhysicalSlot(force), "foreign_Army_Force");
                if (!AbsorptionK(current.ReadForceNativeValidity(force), "foreign_Force_validity") || other == original || other == recipient ||
                    new DiplomacyCommandSource(working, current).HasTreaty(recipient, other)) continue;
                var mapped = CurrentGateMapQueries.FindCity(current.FindArmyUnitCurrentBase(army), current);
                working = TransferAbsorptionHexCurrent(working, current, tile, other,
                    AbsorptionK(mapped.PhysicalSlot, "foreign_Army_mapped_City"));
            }

            foreach (int person in AbsorptionForcePeople(current, original))
            {
                byte status = AbsorptionK(current.ReadPersonStatus(person), "transfer_Person_status");
                if (status != 6)
                {
                    if (status is not (>= 1 and <= 4)) continue;
                    if ((AbsorptionK(current.ReadOwnedReturnPersonFlags(person), "transfer_Person_flags") & 0x4000) == 0)
                    {
                        var home = BaseLookup.Find(AbsorptionK(current.ReadPersonFirstLocation(person), "transfer_Person_home"), current);
                        working = ApplyForceAbsorptionPersonJoinCurrent(working, current, person, primary, home, date, Day, effects).State;
                    }
                    else working = ApplyCaptiveFreedomCurrent(working, current, pool, person, date, Day, Sampler, effects);
                }
                else
                {
                    current.WriteFormerForceKnowledge(person, 0);
                    var assignments = new List<PersonAssignmentPrecallResult>();
                    PersonDistrictTransfer.Apply(person, 0, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current,
                        assignments, result => working = ApplyPersonAssociationRemovalReports(working, current, person, result, Day, effects)));
                    if (assignments.Count != 0) working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, person, assignments);
                    byte loyalty = (AbsorptionK(current.ReadOwnedReturnPersonFlags(person), "captive_loyalty_flags") & 0x2000) != 0 ? (byte)255 : (byte)0;
                    current.SetPersonDestructionLoyaltyE8(person, loyalty);
                    working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, person, loyalty);
                    var native = working.NativeArmySimulation ?? throw ForceAbsorptionMissing("captive_service_year_owner");
                    working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with
                    { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyGovernorComparatorInputs.PersonWord166Key(person)] = 0 } } } };
                    working = current.PublishOfficerAftermath(working, AbsorptionK(current.ReadPersonIdentity(person), "captive_identity"));
                }
            }
            foreach (var target in AbsorptionBases(current, bases))
            {
                int location = AbsorptionK(current.ReadBaseObjectLink(target), "tail_Base_location");
                var governor = current.ReadArmyReturnBaseGovernor2E(target) ?? throw ForceAbsorptionMissing("tail_Base_governor");
                int person = AbsorptionPerson(current, governor);
                if (AbsorptionK(current.ReadPersonNativeValidity(person), "tail_governor_validity")) current.WritePersonStatus(person, 3);
            }
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            var unregistered = new ForceUnregisterSource(this).TryApplyAbsorptionBorrowedCurrent(working, original, current, pool,suppressEliminationPresentation,effects);
            working = unregistered.State ?? throw ForceAbsorptionMissing(unregistered.UnresolvedOwner ?? "Force_unregister");
            if(unregistered.Receipt?.Terminal==true)return PublishEffects(working with { ForceAbsorptionContinuation = null });
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            //Non-serving people may retain the old E0 when293BD0 resets its
            //District. Their derived Force changes even though E0 did not.
            Dictionary<int, OfficerState>? derivedPeople = null;
            foreach (var person in working.Officers.Values)
            {
                if (person.ForceId != original) continue;
                int physical = current.ResolveOfficerSource(person.Id) ?? throw ForceAbsorptionMissing("tail_Person_pointer");
                var actual = current.ReadPersonForce(physical) ?? throw ForceAbsorptionMissing("tail_Person_Force");
                int slot = AbsorptionK(current.ReadForcePhysicalSlot(actual), "tail_Person_Force_slot");
                int? forceId = slot == 0 ? null : slot;
                if (forceId == person.ForceId) continue;
                derivedPeople ??= new(working.Officers);
                derivedPeople[person.Id] = person with { ForceId = forceId };
            }
            if (derivedPeople is not null) working = working with { Officers = derivedPeople };
            return PublishEffects(working with { ForceAbsorptionContinuation = null });
    }

    private static SettlementBase ForceAbsorptionSavedBase(SourceWorldContext current, BaseReference target)
        => new(target.Kind == BaseKind.City ? SettlementBaseKind.City : SettlementBaseKind.Gate,
            AbsorptionK(current.ReadBaseObjectLink(target), "resume_Base4E"));
    private static BaseReference ForceAbsorptionCurrentBase(SettlementBase saved)
        => new(saved.Kind == SettlementBaseKind.City ? BaseKind.City : BaseKind.Gate,
            saved.Kind == SettlementBaseKind.City ? saved.Id : saved.Id - 51);
    private static ImmutableArray<int> ReadForceAbsorptionDistrictPayloads(SourceWorldContext current, HexListHandle list)
    {
        var values = ImmutableArray.CreateBuilder<int>();
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw ForceAbsorptionMissing("resume_District_head");
        while (node.Identity.HasValue)
        {
            if (current.ReadRegistryNodePayload(node) is not { Kind: RegistryKind.District, PhysicalSlot: int district })
                throw ForceAbsorptionMissing("resume_District_payload");
            values.Add(district);
            node = current.ReadRegistryNodeNext(node) ?? throw ForceAbsorptionMissing("resume_District_next");
        }
        return values.ToImmutable();
    }
}
