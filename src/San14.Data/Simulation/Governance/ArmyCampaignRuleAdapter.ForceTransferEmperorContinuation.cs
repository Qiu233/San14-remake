using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Resume after actual26DA10's completed threat write. Never
    /// repeats District transfer, resources, administration, Army or land work.</summary>
    internal GameState ResumeForceTransferEmperorCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, AutomaticEmperorCaptureInputs? inputs = null,
        bool suppressPrimary = false, bool eventPlaybackPresent = false)
    {
        var tail = state.ForceTransferEmperorTail ?? throw AbsorptionBaseMissing("Emperor_resume_tail");
        if (state.AutomaticEmperorCapture is not { } progress) throw AbsorptionBaseMissing("Emperor_resume_progress");
        if (progress.PendingDecision is not null || AutomaticEmperorCaptureLifecycle.IsPresentationPending(state)) return state;
        var working = AutomaticEmperorCaptureLifecycle.ConsumeCompletedPresentation(state);
        _ = BorrowOwnedArmyReturnWorld(working, current, pool);
        _personRelationInputs.BindCurrent(() => working, current);
        current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        var advanced = AdvanceAutomaticEmperorCaptureCurrent(working, current, pool,
            working.AutomaticEmperorCapture!, inputs);
        working = advanced.State with { AutomaticEmperorCapture = advanced.Progress };
        var effects = advanced.Effects.ToList();
        working = AppendForceTransferEmperorEffects(working, effects, advanced.NeedsPresentationReceipt);
        if (advanced.Progress.Phase != AutomaticEmperorCapturePhase.Completed || advanced.NeedsPresentationReceipt)
            //Emperor stages publish their own RNG/history/loyalty/relation
            //owners. Only their balanced temporary pool needs a role snapshot;
            //do not rebuild every City/Area/Hex after each dialogue receipt.
            return working.WithArmyLifecycle((working.ReadArmyLifecycle()
                ?? throw AbsorptionBaseMissing("Emperor_phase_lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });

        var target = new BaseReference(BaseKind.City, tail.CityPhysicalSlot);
        var areas = RestoreForceTransferAreaPayloads(current, tail);
        var previousReader = current.TransferModeStateReader;
        try
        {
            current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
            CityCaptureTerritorySource.ClearTargetRouting(current, target);
            var date = tail.OperationDate ?? throw AbsorptionBaseMissing("Emperor_resume_date");
            working = ApplyCityCaptureStateAdvantageCurrent(working, current, areas, date, effects,
                suppressPrimary: suppressPrimary, eventPlaybackPresent: eventPlaybackPresent);
            CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, target,
                () => Quote(ReadCityCaptureFoodQuoteCurrent(working, current, target)),
                () => Quote(CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, target,
                    date, CreateArmyDestructionAbilities(current), FoodIncomeSceneries)));
        }
        finally
        {
            current.TransferModeStateReader = previousReader;
            CityCaptureTerritorySource.ReleaseRelatedAreas(current, areas);
        }
        working = working with { AutomaticEmperorCapture = null, ForceTransferEmperorTail = null };
        working = AppendForceTransferEmperorEffects(working, effects);
        return BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);

        int Quote(CityCaptureFoodQuote quote)
        {
            int value = quote.Value ?? throw AbsorptionBaseMissing(quote.UnresolvedOwner ?? "Emperor_resume_food_quote");
            working = quote.State ?? working;
            return value;
        }
    }

    private static ImmutableArray<int> ReadForceTransferAreaPayloads(SourceWorldContext current, HexListHandle list)
    {
        var values = ImmutableArray.CreateBuilder<int>();
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw AbsorptionBaseMissing("Emperor_Area_head");
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node);
            if (value is not { Kind: RegistryKind.Area, PhysicalSlot: int physical })
                throw AbsorptionBaseMissing("Emperor_Area_payload");
            values.Add(physical);
            node = current.ReadRegistryNodeNext(node) ?? throw AbsorptionBaseMissing("Emperor_Area_next");
        }
        return values.ToImmutable();
    }

    private static CityCaptureAreaScope RestoreForceTransferAreaPayloads(SourceWorldContext current, ForceTransferEmperorTail tail)
    {
        var list = current.CreateNearbyTemporary();
        try
        {
            foreach (int physical in tail.RelatedAreaPhysicalSlots)
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw AbsorptionBaseMissing("Emperor_Area_restore_node");
                if (!node.Identity.HasValue) throw AbsorptionBaseMissing("Emperor_Area_restore_allocation");
                current.WriteRegistryNodePayload(node, new(RegistryKind.Area, physical));
            }
            return new(new(BaseKind.City, tail.CityPhysicalSlot), tail.OldForcePhysicalSlot,
                tail.NewForcePhysicalSlot, new(tail.CapturedOldDistrictPhysicalSlot), list);
        }
        catch { current.ClearNearbyTemporary(list, true); throw; }
    }

    private static GameState AppendForceTransferEmperorEffects(GameState state,
        List<CampaignEffectRequest> effects, bool markPresentation = false)
    {
        if (effects.Count == 0) return state;
        var queue = (state.CampaignEffectRequests ?? throw AbsorptionBaseMissing("Emperor_effect_queue"))
            .Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw AbsorptionBaseMissing("Emperor_effect_revision"), effects.ToArray());
        effects.Clear();
        return state with { CampaignEffectRequests = queue, AutomaticEmperorCapture = markPresentation
            ? (state.AutomaticEmperorCapture ?? throw AbsorptionBaseMissing("Emperor_effect_progress")) with
                { NeedsPresentationReceipt = true, AwaitingEffectBatchSequence = queue.LastBatchSequence }
            : state.AutomaticEmperorCapture };
    }
}
