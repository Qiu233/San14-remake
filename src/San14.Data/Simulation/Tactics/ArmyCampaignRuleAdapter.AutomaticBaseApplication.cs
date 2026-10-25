using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BorrowedBaseAutomaticApplication(GameState State, bool Activated, ushort? PrimaryTroopDamageWord22)
{
    internal AutomaticTacticExecutionContinuation? Continuation { get; init; }
}
internal sealed record BorrowedAutomaticSelectedApplication(GameState State,
    NormalArmyCombatTacticFrame22Receipt? Frame22, TacticReportEntry[] Reports)
{
    internal AutomaticTacticExecutionContinuation? Continuation { get; init; }
}
internal sealed record BorrowedAutomaticExecutionPlan(int TacticSlot,
    S14TacticEffectProgram Program, int[][] HexVectors);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>172EB0's real Base-primary selection and finite zero-chain
    /// 178990 writers, borrowing the stage13 world. The two original
    /// Hex vectors stay ordered, while each effect resolves its live receiver.
    /// No manual permission, fake primary Army or complete-world copy is used.</summary>
    private BorrowedBaseAutomaticApplication CompleteBorrowedArmyBaseAutomatic(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, NormalCombatMixedPair pair,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate, long operationRevision,
        List<CampaignEffectRequest> effects)
    {
        var candidates = context.ResolveAutomaticBaseCurrentCandidates(state, pair)
            ?? throw BaseAutomaticMissing("current_candidates");
        var region = new BattleRemakeCurrentRegionFactor(_regionSeed, context, current, Revision);
        var selection = context.ResolveAutomaticBaseCurrentSelection(state, pair, candidates, operationDate, region);
        if (selection.Status is AutomaticTacticSelectionStatus.NoEffectTargets or AutomaticTacticSelectionStatus.RateRejected)
            return new(state, false, 0);
        var selected = selection.SelectedCandidate ?? throw BaseAutomaticMissing("selected_tactic");
        var applied = ApplyBorrowedSelectedAutomatic(state, current, context, candidates.SourceArmyId,
            pair.Target, selected, sampler, operationDate, effects, operationRevision);
        return new(applied.State, true, applied.Frame22?.Word22After) { Continuation = applied.Continuation };
    }

    //The caller supplies the already selected program and its original ordered
    //Hex vectors. Army and Base primaries share178990's actual effect writers;
    //this entry never reruns available tactics, target selection or rate trials.
    internal BorrowedAutomaticSelectedApplication ApplyBorrowedSelectedAutomatic(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        NormalCombatParticipant primary, AutomaticTacticBaseCandidateSource selected,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        List<CampaignEffectRequest> effects, long? immediatePauseRevision = null)
        => ApplyBorrowedAutomaticExecution(state, current, context, casterId, primary,
            new(selected.TacticSlot, selected.Program, selected.Instructions.Select(vector =>
                vector.OrderedTargets.Select(node => node.HexId).ToArray()).ToArray()),
            sampler, operationDate, effects, null, immediatePauseRevision);

    internal BorrowedAutomaticSelectedApplication ResumeBorrowedSelectedAutomatic(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, NormalCombatParticipant primary,
        AutomaticTacticExecutionContinuation saved, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate operationDate, long revision, List<CampaignEffectRequest> effects)
    {
        var bytes = new byte[8];
        foreach (var instruction in saved.Instructions)
        {
            bytes[instruction.Index] = instruction.Opcode;
            bytes[2 + instruction.Index] = instruction.Raw64;
            bytes[4 + instruction.Index] = instruction.EffectValue;
            bytes[6 + instruction.Index] = instruction.Raw68;
        }
        var plan = new BorrowedAutomaticExecutionPlan(saved.TacticId,
            S14TacticEffectProgramReader.Read(saved.TacticId, bytes),
            saved.Instructions.OrderBy(row => row.Index).Select(row => row.OrderedHexIds.ToArray()).ToArray());
        return ApplyBorrowedAutomaticExecution(state, current, context, saved.CasterUnitId, primary,
            plan, sampler, operationDate, effects, saved, revision);
    }

    private BorrowedAutomaticSelectedApplication ApplyBorrowedAutomaticExecution(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        NormalCombatParticipant primary, BorrowedAutomaticExecutionPlan selected,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        List<CampaignEffectRequest> effects, AutomaticTacticExecutionContinuation? resume,
        long? immediatePauseRevision)
    {
        var actor = state.Units[casterId];
        //16DF00 precedes all instruction effects. This capture is only a
        //genuinely empty actual neighbor chain, not an empty replacement list.
        var chain = resume is null
            ? context.CaptureAutomaticEmptyChain(state, actor, selected.TacticSlot)
                ?? throw BaseAutomaticMissing("linked_actor_chain16DF00")
            : context.RestoreAutomaticEmptyChain(actor.Id, selected.TacticSlot,
                resume.ChainWaitingContribution, resume.ChainRandomState);
        var source = _catalog.Rows[selected.TacticSlot];
        var sourceRules = new TacticEffectSourceRules(_catalog, context, new(new HexTravelMap(_map)));
        var details = resume?.Details.ToList() ?? new List<TacticReportEntry>();
        var results = new[] { resume?.FirstInstructionResults.ToList() ?? new List<TacticResultPresentation>(),
            resume?.SecondInstructionResults.ToList() ?? new List<TacticResultPresentation>() };
        var armyPresentationEffects = resume?.ArmyPresentationEffects.ToList() ?? new List<TacticEffectPresentation>();
        var troopLedger = resume is null ? new AutomaticBaseTacticDamageLedger()
            : AutomaticBaseTacticDamageLedger.Restore(resume.DamageContributions);
        var working = state;
        foreach (var instruction in selected.Program.Instructions)
        {
            if (resume is not null && instruction.Index < resume.InstructionIndex) continue;
            bool resumingInstruction = resume is not null && instruction.Index == resume.InstructionIndex;
            actor = working.Units.GetValueOrDefault(casterId) ?? throw BaseAutomaticMissing("instruction_actor");
            var vector = selected.HexVectors[instruction.Index];
            //Every native wrapper is reached in program order, even for an
            //empty vector. Unsupported numeric prefixes cannot become no-op.
            TacticEffectScaleSourceInput? instructionScale = null;
            int? sourceHex = null;
            AutomaticArmyBaseTroopDamagePrefix? troopPrefix = null;
            AutomaticBaseWallTacticRequest? wallPrefix = null;
            AutomaticArmySingleAbnormalPrefix? singleAbnormalPrefix = null;
            AutomaticArmyAllModifierPrefix? modifierPrefix = null;
            AutomaticArmyMoraleDamagePrefix? moraleDamagePrefix = null;
            switch (instruction.Opcode)
            {
                case 0: continue;
                case 1:
                    troopPrefix = resumingInstruction
                        ? new(casterId, resume!.TroopPrefix!.SourceArmyPhysicalSlot, resume.TroopPrefix.SourceTroops,
                            resume.TroopPrefix.SourceForceSlot, resume.TroopPrefix.StageProduct)
                        : context.BeginAutomaticArmyBaseTroopDamage(working, actor, 0);
                    break;
                case 18:
                    wallPrefix = context.ResolveAutomaticBaseWallTacticRequest(working, actor, source, instruction, 0);
                    break;
                case 15:
                    instructionScale = context.ResolveBorrowedBaseTacticMoraleScale(working, actor,
                        source, instruction, 0);
                    _ = TacticEffectSourceMath.Scale(instructionScale);
                    break;
                case 16:
                    moraleDamagePrefix = resumingInstruction
                        ? new(casterId, new(resume!.MoralePrefix!.SourceArmyPhysicalSlot),
                            new(resume.MoralePrefix.SourceHexId), resume.MoralePrefix.StageFactor, resume.MoralePrefix.WaitingFactor)
                        : BeginBorrowedAutomaticArmyMoraleDamage(working, current, context, casterId, 0);
                    break;
                case 22:
                    modifierPrefix = BeginBorrowedAutomaticArmyAllModifiers(working, current,
                        context, casterId, 0);
                    break;
                case 19: break;
                case 20:
                    //177ED0 shares16B6D0, stage and16D900 with1763B0,
                    //including for an empty vector. Its conversion is in the
                    //per-Army healing formula, not15's amount conversion.
                    instructionScale = context.ResolveBorrowedBaseTacticMoraleScale(working, actor,
                        source, instruction, 0);
                    break;
                case 2 or 3 or 4:
                    singleAbnormalPrefix = context.BeginAutomaticArmySingleAbnormal(working, actor, 0);
                    sourceHex = singleAbnormalPrefix.SourceHex;
                    break;
                case 23:
                    sourceHex = ArmyMarchSourceAdapter.ReadCurrentHex(working, actor)
                        ?? throw BaseAutomaticMissing("instruction_source_Hex2A");
                    //175FF0/178500 capture source Hex/Force and stage at entry.
                    //Their ability/selector queries are inside each valid
                    //receiver's gate, not an empty-vector prerequisite.
                    context.ReadAutomaticAbnormalInstructionPrefix(working, actor, instruction);
                    break;
                default:
                    throw BaseAutomaticMissing("instruction" + instruction.Index + "_opcode" + instruction.Opcode + "_numeric_writer");
            }

            for (int targetIndex = resumingInstruction ? resume!.TargetIndex : 0; targetIndex < vector.Length; targetIndex++)
            {
                int nodeHex = vector[targetIndex];
                if (resumingInstruction && targetIndex == resume!.TargetIndex)
                {
                    //The actual numeric callback and its immediate destruction
                    //are complete. Keep the original target and signed request.
                    working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                    FinishArmyDamageNode(resume.TargetUnitId, resume.TargetArmyPhysicalSlot,
                        resume.TargetReceiverId, nodeHex, instruction, resume.RequestedLoss);
                    continue;
                }
                //177610 independently reads Hex1A -> Object.16EB20's Army
                //priority must not hide an Object occupying this same Hex.
                if (instruction.Opcode == 18)
                {
                    var applied = ApplyBorrowedAutomaticHexWallTactic(working, current, context, casterId,
                        nodeHex, source, instruction, 0, sampler, effects, wallPrefix!);
                    working = applied.State;
                    if (applied.TargetResultMarker)
                    {
                        var marked = FieldHexOccupantSource.ReadCurrent(working, current, nodeHex);
                        var marker = marked.Receipt ?? throw BaseAutomaticMissing(marked.UnresolvedOwner ?? "wall_marker_receiver");
                        AddCurrentMarker(marker, nodeHex, instruction.Index, unchecked((ushort)wallPrefix!.Request));
                        if (applied.BaseLocationId is int wallLocation && applied.Wall is { } wall)
                            working = working with { NativeSubdayReports = [.. working.NativeSubdayReports ?? [],
                                new("tactic_city_wall_damage", working.NativeClock!.UpdateOrdinal, casterId,
                                    working.Units[casterId].ForceId, wallLocation, wall.AppliedLoss)] };
                    }
                    continue;
                }
                var target = FieldHexOccupantSource.ReadCurrent(working, current, nodeHex);
                var receiver = target.Receipt ?? throw BaseAutomaticMissing(target.UnresolvedOwner ?? "effect_Hex_receiver");
                if (receiver.NativeKind == 255) continue;
                actor = working.Units.GetValueOrDefault(casterId) ?? throw BaseAutomaticMissing("effect_actor");
                if (receiver.NativeKind is 5 or 6)
                {
                    int location = receiver.BaseLocationId ?? throw BaseAutomaticMissing("effect_Base_location");
                    var reference = new BaseReference(receiver.NativeKind == 5 ? BaseKind.City : BaseKind.Gate,
                        receiver.NativePhysicalSlot);
                    if (instruction.Opcode == 1)
                    {
                        var numeric = context.ResolveAutomaticArmyBaseTroopDamage(working, actor,
                            reference, source, instruction, troopPrefix!);
                        if (!numeric.TargetValid || !numeric.TargetPermitted) continue;
                        int request = numeric.Amount ?? throw BaseAutomaticMissing("Base_opcode1_amount");
                        //The ordinary frame+30 is0. Only a different captured
                        //node Hex applies178EC0 now; receiver equality is not
                        //the native deferral test.
                        if (nodeHex != primary.HexId)
                        {
                            var applied = ApplyBorrowedNormalBaseFatalApplication(working, casterId,
                                location, request, sampler, current);
                            if (applied.Outcome == NormalCityApplicationOutcome.CurrentInputsUnresolved || applied.Receipt is null)
                                throw BaseAutomaticMissing(applied.UnresolvedOwner ?? "Base_opcode1_application");
                            working = applied.State;
                            if (applied.Receipt.NativeReturn)
                                working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                        }
                        AddBaseEffect(location, nodeHex, instruction, PlayableTacticEffectKind.ArmyDamage,
                            unchecked((ushort)request), unchecked((ushort)request));
                        //176C10 records the original full signed request after
                        //its marker, whether applied now or deferred above.
                        if (current.ReadBaseNativeValidity(reference)
                            ?? throw BaseAutomaticMissing("Base_opcode1_post_application_validity"))
                            troopLedger.Accumulate(reference, request);
                    }
                    else if (instruction.Opcode == 15)
                    {
                        var applied = ApplyBorrowedBaseTacticMorale(working, current, context, casterId,
                            source, reference, instruction, 0, sampler, instructionScale!);
                        working = applied.State;
                        if (applied.Applied && applied.Receipt is { } receipt)
                            AddBaseEffect(location, nodeHex, instruction, PlayableTacticEffectKind.NativeMoraleChange,
                                receipt.MoraleAfter!.Value - receipt.MoraleBefore!.Value);
                    }
                    else if (instruction.Opcode == 19)
                    {
                        var applied = ApplyBorrowedBaseTacticClear(working, current, context, casterId,
                            source, reference, instruction, 0, sampler);
                        working = applied.State;
                        //A valid unchanged receiver still has its real marker.
                        if (applied.Applied) AddBaseEffect(location, nodeHex, instruction,
                            PlayableTacticEffectKind.ClearAbnormalStatuses, 0);
                    }
                    else if (instruction.Opcode is 2 or 3 or 4 or 23)
                    {
                        var applied = context.ApplyAutomaticBaseAbnormal(working, actor, reference,
                            source, instruction, 0, sourceHex!.Value, sampler.Read(working));
                        working = applied.State;
                        if (applied.Receipt.TargetResultMarker)
                        {
                            int amount = applied.Receipt.Counters.Sum(counter => counter.CounterWritten
                                ? counter.CounterAfter!.Value - counter.CounterBefore!.Value : 0);
                            AddBaseEffect(location, nodeHex, instruction,
                                PlayableTacticEffectKind.NativeAbnormalChange, amount);
                        }
                    }
                    else throw BaseAutomaticMissing("instruction20_selected_Base");
                }
                else if (receiver.NativeKind == 27)
                {
                    int id = receiver.GameplayArmyId ?? throw BaseAutomaticMissing("effect_Army_identity");
                    var army = working.Units.GetValueOrDefault(id) ?? throw BaseAutomaticMissing("effect_Army_image");
                    var targetReceiverId = working.ReadArmyLifecycle()!.Slots[receiver.NativePhysicalSlot].ReceiverId;
                    if (instruction.Opcode == 1)
                    {
                        var numeric = context.ResolveAutomaticArmyTroopDamage(working, actor, army,
                            source, instruction, troopPrefix!);
                        if (!numeric.TargetValid) continue;
                        int request = numeric.Amount ?? throw BaseAutomaticMissing("Army_opcode1_amount");
                        if (nodeHex != primary.HexId)
                        {
                            var applied = ApplyBorrowedAutomaticArmyTroopDamage(working, current, casterId,
                                id, request, sampler, operationDate, effects, immediatePauseRevision);
                            working = applied.State;
                            if (working.ArmyDefeatContinuation is not null)
                                return Suspend(instruction, targetIndex, id, receiver.NativePhysicalSlot, targetReceiverId, nodeHex,
                                    request, troopPrefix, null);
                            if (applied.NativeReturn) working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                        }
                        FinishArmyDamageNode(id, receiver.NativePhysicalSlot, targetReceiverId,
                            nodeHex, instruction, request);
                        continue;
                    }
                    if (instruction.Opcode is 16 or 22)
                    {
                        bool marked;
                        if (instruction.Opcode == 16)
                        {
                            var applied = ApplyBorrowedAutomaticArmyMoraleDamage(working, current, context,
                                casterId, id, source, instruction, moraleDamagePrefix!, sampler, operationDate, effects, immediatePauseRevision);
                            working = applied.State; marked = applied.TargetResultMarker;
                            if (working.ArmyDefeatContinuation is not null)
                                return Suspend(instruction, targetIndex, id, receiver.NativePhysicalSlot, targetReceiverId, nodeHex,
                                    0, null, moraleDamagePrefix);
                            if (applied.NativeReturn) working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                        }
                        else
                        {
                            var applied = ApplyBorrowedAutomaticArmyAllModifiers(working, current, context,
                                casterId, id, source, instruction, modifierPrefix!, sampler, operationDate, effects);
                            working = applied.State; marked = applied.TargetResultMarker;
                        }
                        if (marked)
                        {
                            AddCurrentMarker(receiver, nodeHex, instruction.Index, 0);
                            details.Add(new("effect", checked(state.PeriodNumber + 1), actor.ForceId,
                                actor.Id, actor.OfficerId, selected.TacticSlot, id)
                            { NativeUpdateOrdinal = state.NativeClock!.UpdateOrdinal, TargetHexId = nodeHex,
                                InstructionIndex = instruction.Index });
                        }
                        continue;
                    }
                    //Use the qualified TC strength query. Its existing status
                    //loop remains shared; the older SC/public strength entry
                    //is neither replaced nor implicitly qualified by this use.
                    TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>? strength = instruction.Opcode == 23
                        ? context.ResolveTcAutomaticArmyAbnormalStrength(working, actor, army, source, instruction, 0) : null;
                    AutomaticArmySingleAbnormalResolution? singleAbnormal = instruction.Opcode is 2 or 3 or 4
                        ? context.ResolveAutomaticArmySingleAbnormal(working, actor, army, source,
                            instruction, singleAbnormalPrefix!) : null;
                    if (singleAbnormal?.Receipt.FailureFeedbackKind is int failedKind)
                    {
                        var feedback = new ArmyTraitFeedback(working, current, operationDate, effects, () => working,
                            speechConsumer: CreateArmyTraitSpeechConsumer(current, () => working, next => working = next));
                        feedback.NoAbnormalApplied(new(receiver.NativePhysicalSlot), failedKind);
                        working = feedback.Publish(working);
                    }
                    var resolution = singleAbnormal is not null ? singleAbnormal.Resolution
                        : instruction.Opcode == 15
                        ? context.ResolveAutomaticArmyMoraleTarget(working, army, instructionScale!)
                        : instruction.Opcode == 20
                        ? context.ResolveAutomaticArmyHealingTarget(working, army, instructionScale!)
                        : sourceRules.ResolveCapturedInstructionTarget(working, actor, army, source, instruction, strength);
                    if (resolution is null) throw BaseAutomaticMissing("Army_instruction" + instruction.Opcode + "_inputs");
                    foreach (var effect in resolution.Effects)
                    {
                        var before = army;
                        army = TacticEffectSourceRules.ApplyReceipt(army, effect);
                        if (army.Troops <= 0 || army.MoraleState.CurrentValue is not > 0)
                            throw BaseAutomaticMissing("Army_effect_retirement");
                        int amount = effect.Kind == PlayableTacticEffectKind.ClearAbnormalStatuses
                            ? before.TacticRuntime!.Statuses.Length - army.TacticRuntime!.Statuses.Length : effect.Amount;
                        var receiverId = working.ReadArmyLifecycle()!.Slots[receiver.NativePhysicalSlot].ReceiverId;
                        armyPresentationEffects.Add(new(instruction.Index, receiverId, effect.Kind, effect.Amount,
                            effect.HealingResult, effect.NativeAbnormalAfter, effect.CurrentMoraleAfter));
                        details.Add(new("effect", checked(state.PeriodNumber + 1), actor.ForceId,
                            actor.Id, actor.OfficerId, selected.TacticSlot, army.Id, effect.Kind, amount)
                        { NativeUpdateOrdinal = state.NativeClock!.UpdateOrdinal, TargetHexId = nodeHex,
                            InstructionIndex = instruction.Index });
                    }
                    working = working with { Units = new(working.Units) { [id] = army },
                        ResourceState = resolution.ResourceStateAfter ?? working.ResourceState,
                        RandomState = resolution.RandomState };
                    int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw BaseAutomaticMissing("effect_Army_slot");
                    if (resolution.Effects.Any(effect => effect.NativeAbnormalAfter is not null))
                    {
                        var abnormal = army.NativeTacticRuntime ?? throw BaseAutomaticMissing("effect_Army_counters");
                        var reference = new RegistryObjectReference(RegistryKind.Army, physical);
                        current.SetTacticAbnormalRawByte(reference, 0x23, abnormal.Abnormal23);
                        current.SetTacticAbnormalRawByte(reference, 0x24, abnormal.Abnormal24);
                        current.SetTacticAbnormalRawByte(reference, 0x25, abnormal.Abnormal25);
                        current.SetArmyCommandOverride(physical, ArmyOverrideTargetSourceAdapter.Read(working, army));
                    }
                    if (singleAbnormal is not null)
                    {
                        var receipt = singleAbnormal.Receipt;
                        if (receipt.IncreasingFeedbackKind is int increasedKind)
                        {
                            var feedback = new ArmyTraitFeedback(working, current, operationDate, effects, () => working,
                                speechConsumer: CreateArmyTraitSpeechConsumer(current, () => working, next => working = next));
                            feedback.AbnormalIncreased(new(physical), increasedKind);
                            working = feedback.Publish(working);
                        }
                        //2EED60 reports immunity, or the real zero->positive
                        //unclamped transition, before178500's result marker.
                        //The report owner repeats the original player gate.
                        if (receipt.NotificationDistrict is int district && receipt.Status is { } status &&
                            (!status.CounterWritten || receipt.StartsStatus))
                            working = ApplyAutomaticArmyAbnormalReport(working, current,
                                working.Units[casterId], working.Units[id], district, receipt.NativeIndex,
                                status.CauseCode, source.Name,
                                () => sampler.Read(working)?.Day450 ?? throw BaseAutomaticMissing("abnormal_report_World450"),
                                effects);
                    }
                    //2/3/4/15/19/23 wrappers use result amount0;20 uses its
                    //actual signed healing amount truncated to the result word.
                    if (singleAbnormal?.TargetResultMarker ?? (resolution.Effects.Length != 0))
                        AddCurrentMarker(receiver, nodeHex, instruction.Index, instruction.Opcode == 20
                            ? unchecked((ushort)resolution.Effects.Single().Amount) : (ushort)0);
                    bool healing = resolution.Effects.Any(effect => effect.Kind == PlayableTacticEffectKind.ArmyHealing);
                    bool morale = resolution.Effects.Any(effect => effect.Kind == PlayableTacticEffectKind.NativeMoraleChange);
                    if (healing || morale)
                    {
                        var quantities = current.ReadArmyReturnResourceFields(new(physical))
                            ?? throw BaseAutomaticMissing("effect_Army_resources");
                        if (healing) quantities = quantities with { Ready16 = checked((ushort)army.Troops),
                            Wounded18 = checked((ushort)army.WoundedTroops!.Value) };
                        if (morale) quantities = quantities with { Morale1A = ArmyMoraleSourceAdapter.Read(army) };
                        current.BindArmyReturnResourceFields(physical, quantities);
                        //No deferred image publisher may replay an earlier value.
                        current.AcknowledgeArmyReturnNumerics(physical, wounded: healing, morale: morale);
                    }
                }
                else throw BaseAutomaticMissing("effect_target_kind" + receiver.NativeKind);
            }
        }
        actor = working.Units.GetValueOrDefault(casterId) ?? throw BaseAutomaticMissing("tail_actor");
        //178990 suppresses this contribution tail whenever either original
        //opcode is1/18, even when that instruction's vector is empty.
        if (!selected.Program.Instructions.Any(instruction => instruction.Opcode is 1 or 18))
        {
            var commander = context.ResolveOrdinaryActivationCommanderTail(working, actor, selected.TacticSlot,
                (reached, person, amount) => _pendingMerit.TryApplyWithinArmyOperation(reached, current, person, amount))
                ?? throw BaseAutomaticMissing("commander_merit_tail1695C0");
            working = working with { Officers = new(working.Officers) { [commander.Id] = commander } };
        }
        var waiting = context.ResolveAutomaticActivationWaiting(working, actor, selected.TacticSlot, chain)
            ?? throw BaseAutomaticMissing("waiting_after_effects");
        var native = actor.NativeTacticRuntime ?? throw BaseAutomaticMissing("actor_waiting");
        var runtime = actor.TacticRuntime ?? throw BaseAutomaticMissing("actor_tactic_runtime");
        var ready = new Dictionary<int, int>(runtime.ReadyAtPeriod);
        ready.Remove(selected.TacticSlot);
        actor = actor with { NativeTacticRuntime = native with { Waiting20 = waiting.Waiting20,
            Waiting21 = waiting.Waiting21, Waiting22 = waiting.Waiting22,
            PrimaryWaitingReadyAtUpdateOrdinal = waiting.Waiting20 == 0 ? working.NativeClock!.UpdateOrdinal : null },
            TacticRuntime = runtime with { ReadyAtPeriod = ready } };
        int? primaryLocation = primary.Kind == 5 ? primary.PhysicalSlot
            : primary.Kind == 6 ? primary.PhysicalSlot + 51 : null;
        var used = new TacticReportEntry("used", checked(state.PeriodNumber + 1), actor.ForceId,
            actor.Id, actor.OfficerId, selected.TacticSlot, primary.Kind == 27 ? primary.UnitId : null)
        { NativeUpdateOrdinal = state.NativeClock!.UpdateOrdinal, TargetBaseLocationId = primaryLocation,
            TargetHexId = primary.HexId };
        working = working with { Units = new(working.Units) { [actor.Id] = actor },
            TacticReports = [.. working.TacticReports ?? [], used, .. details] };
        working = EnqueueBorrowedAutomaticBasePresentation(working, current, context, actor, selected.TacticSlot,
            primary.HexId, results, armyPresentationEffects);
        var frame22 = troopLedger.ConsumeFrame22(current, primary.Kind, checked((ushort)primary.PhysicalSlot));
        return new(working, frame22, [used, .. details]);

        BorrowedAutomaticSelectedApplication Suspend(S14TacticEffectInstruction instruction, int targetIndex,
            int targetId, int targetPhysical, ArmyReceiverId targetReceiver, int targetHex, int request,
            AutomaticArmyBaseTroopDamagePrefix? troop, AutomaticArmyMoraleDamagePrefix? morale)
        {
            if (immediatePauseRevision is null || primary.Kind is not (5 or 6 or 27))
                throw BaseAutomaticMissing("immediate_activation_outer_owner");
            var saved = new AutomaticTacticExecutionContinuation(casterId, selected.TacticSlot,
                selected.Program.Instructions.Select(row => new AutomaticTacticInstructionSnapshot(row.Index,
                    row.Opcode, row.Raw64, row.EffectValue, row.Raw68,
                    selected.HexVectors[row.Index].ToImmutableArray())).ToImmutableArray(),
                instruction.Index, targetIndex, targetId, targetPhysical,
                targetReceiver, targetHex, request,
                chain.SumOfNativeWaitingContributions, chain.RandomState, troopLedger.Capture(),
                details.ToImmutableArray(), results[0].ToImmutableArray(), results[1].ToImmutableArray(),
                armyPresentationEffects.ToImmutableArray())
            {
                TroopPrefix = troop is null ? null : new(troop.SourceArmyPhysicalSlot, troop.SourceTroops,
                    troop.SourceForceSlot, troop.StageProduct),
                MoralePrefix = morale is null ? null : new(morale.SourceArmy.PhysicalSlot!.Value,
                    morale.SourceHex.PhysicalSlot!.Value, morale.StageFactor, morale.WaitingFactor)
            };
            return new(working, null, []) { Continuation = saved };
        }

        void FinishArmyDamageNode(int targetId, int targetPhysical, ArmyReceiverId receiver,
            int hex, S14TacticEffectInstruction instruction, int request)
        {
            results[instruction.Index].Add(new(TacticResultTargetKind.ArmyReceiver, receiver.Value, hex,
                instruction.Opcode == 1 ? unchecked((ushort)request) : (ushort)0));
            if (instruction.Opcode == 1 && (current.ReadArmyUnitNativeValidity(new(targetPhysical))
                ?? throw BaseAutomaticMissing("Army_opcode1_post_application_validity")))
                troopLedger.AccumulateArmy(new(targetPhysical), request);
            details.Add(new("effect", checked(state.PeriodNumber + 1), actor.ForceId, actor.Id, actor.OfficerId,
                selected.TacticSlot, targetId, instruction.Opcode == 1 ? PlayableTacticEffectKind.ArmyDamage : null,
                instruction.Opcode == 1 ? unchecked((ushort)request) : 0)
            { NativeUpdateOrdinal = state.NativeClock!.UpdateOrdinal, TargetHexId = hex, InstructionIndex = instruction.Index });
        }

        void AddBaseEffect(int location, int hex, S14TacticEffectInstruction instruction,
            PlayableTacticEffectKind effect, int amount, ushort markerAmount = 0)
        {
            details.Add(new("effect", checked(state.PeriodNumber + 1), actor.ForceId, actor.Id,
                actor.OfficerId, selected.TacticSlot, Effect: effect, Amount: amount)
                { NativeUpdateOrdinal = state.NativeClock!.UpdateOrdinal, TargetBaseLocationId = location,
                    TargetHexId = hex, InstructionIndex = instruction.Index });
            results[instruction.Index].Add(new(TacticResultTargetKind.BaseLocation, location, hex, markerAmount));
        }

        void AddCurrentMarker(FieldHexOccupantReceipt marker, int hex, int instruction, ushort amount)
        {
            if (marker.NativeKind == 255) return;
            TacticResultPresentation result = marker.NativeKind switch
            {
                5 or 6 => new(TacticResultTargetKind.BaseLocation,
                    marker.BaseLocationId ?? throw BaseAutomaticMissing("marker_Base_location"), hex, amount),
                27 => new(TacticResultTargetKind.ArmyReceiver,
                    working.ReadArmyLifecycle()!.Slots[marker.NativePhysicalSlot].ReceiverId.Value, hex, amount),
                11 => new(TacticResultTargetKind.MapObjectReceiver,
                    (current.CurrentMapObjectDirectory ?? throw BaseAutomaticMissing("marker_Object_directory"))
                        .ReceiverAtPhysical(marker.NativePhysicalSlot).Value, hex, amount),
                _ => throw BaseAutomaticMissing("marker_kind" + marker.NativeKind)
            };
            results[instruction].Add(result);
        }
    }

    private static InvalidDataException BaseAutomaticMissing(string owner) =>
        new($"ordinary_army_combat_cut.automatic_Base.{owner}");
}

public sealed partial class TacticRemakeCurrentContext
{
    private sealed record AutomaticEmptyChainCapture(int ActorUnitId, int TacticId,
        TacticRemakeChainResolution Chain);
    private AutomaticEmptyChainCapture? _automaticEmptyChain;

    internal TacticRemakeChainResolution? CaptureAutomaticEmptyChain(GameState state, UnitState actor, int tacticSlot)
    {
        var chain = ResolveChain(state, actor, _catalog.Rows[tacticSlot]);
        if (chain is not { ChainCount: 0 } || chain.RandomState != state.RandomState) return null;
        _automaticEmptyChain = new(actor.Id, tacticSlot, chain);
        return chain;
    }

    internal TacticRemakeChainResolution RestoreAutomaticEmptyChain(int actorId, int tacticId,
        int waitingContribution, ulong randomState)
    {
        var chain = new TacticRemakeChainResolution(0, waitingContribution, randomState);
        _automaticEmptyChain = new(actorId, tacticId, chain);
        return chain;
    }

    internal void ReadAutomaticAbnormalInstructionPrefix(GameState state, UnitState caster,
        S14TacticEffectInstruction instruction)
    {
        int physical = caster.NativeArmyOrigin?.NativeSlotIndex
            ?? throw new InvalidDataException("ordinary_army_combat_cut.automatic_Base.abnormal_source_slot");
        var army = _definitions.ReadArmyUnitTableReference(physical)
            ?? throw new InvalidDataException("ordinary_army_combat_cut.automatic_Base.abnormal_source_pointer");
        if (instruction.Opcode == 23)
        {
            //22C990 is the Force pointer getter. It does not require native
            //Force validity here and no ability query belongs to this prefix.
            _ = _definitions.ReadArmyUnitForceReference(army)
                ?? throw new InvalidDataException("ordinary_army_combat_cut.automatic_Base.abnormal_source_Force_pointer");
        }
        else _ = _definitions.ReadArmyUnitRawForceSlot(army); //178500:22CD80.
        if (Parameter(state, SimulationParameterKeys.Tactics.EffectStageScale) is null)
            throw new InvalidDataException("ordinary_army_combat_cut.automatic_Base.abnormal_stage_scale");
    }

    internal TacticTargetResolution? ResolveAutomaticArmyMoraleTarget(GameState state,
        UnitState target, TacticEffectScaleSourceInput scale)
    {
        if (ArmyValid(state, target) != true || ArmyMoraleSourceAdapter.Read(target) is not byte morale) return null;
        int? first = ForceMoraleMaximum(state, target.ForceId), second = ForceMoraleMaximum(state, target.ForceId),
            third = ForceMoraleMaximum(state, target.ForceId);
        if (first is null || second is null || third is null) return null;
        int after = TacticEffectSourceMath.ArmyMoraleAfter(new(scale, morale, first.Value, second.Value, third.Value));
        if (after is < 1 or > 255) return null;
        return new([new(PlayableTacticEffectKind.NativeMoraleChange, after - morale)
            { CurrentMoraleAfter = after }], state.RandomState);
    }

    internal TacticTargetResolution? ResolveAutomaticArmyHealingTarget(GameState state,
        UnitState target, TacticEffectScaleSourceInput scale)
    {
        if (ArmyValid(state, target) != true || target.WoundedTroops is not int wounded) return null;
        int? capacity = ReadArmyHealingSelectionCapacity(state, target);
        int? floor = Parameter(state, SimulationParameterKeys.Tactics.HealingMinimum);
        if (capacity is null || floor is null) return null;
        //Actual20 target selection rejects all Bases. Its carried wounded
        //write limit therefore remains the original Army literal20000.
        var healed = TacticEffectSourceMath.ArmyHealing(new(scale, target.Troops, wounded,
            capacity.Value, floor.Value, 20000));
        if (healed.TroopsAfter < 1) return null;
        return new([new(PlayableTacticEffectKind.ArmyHealing, healed.TroopsAfter - target.Troops)
            { HealingResult = new(healed.TroopsAfter, healed.WoundedAfter) }], state.RandomState);
    }
}
