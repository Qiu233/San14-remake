using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseOriginAutomaticApplication(GameState State, bool Activated, ushort Word22)
{
    internal BaseAutomaticTacticExecutionContinuation? Continuation { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //172EB0's Base actor has one candidate, no Army chain, and a genuine
    //Base4C waiting write. The governor never acquires a synthetic Army ID.
    private BaseOriginAutomaticApplication CompleteBorrowedBaseOriginAutomatic(GameState state,
        SourceWorldContext current, BaseReference origin, NormalCombatMixedPair pair,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, long revision)
    {
        var context = Context(current, sampler);
        var region = new BattleRemakeCurrentRegionFactor(_regionSeed, context, current, Revision);
        var selection = context.ResolveBaseOriginAutomaticCurrent(state, origin, pair, region);
        if (selection?.Candidate is not { } selected) return new(state, false, 0);
        return ApplyBaseOriginAutomaticExecution(state, current, origin, pair, selection,
            new(selected.TacticSlot, selected.Program, selected.Instructions.Select(vector =>
                vector.OrderedTargets.Select(node => node.HexId).ToArray()).ToArray()),
            sampler, date, effects, revision, null);
    }

    private BaseOriginAutomaticApplication ResumeBaseOriginAutomaticExecution(GameState state,
        SourceWorldContext current, NormalCombatMixedPair pair, BaseAutomaticTacticExecutionContinuation saved,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        var bytes = new byte[8];
        foreach (var row in saved.Instructions)
        {
            bytes[row.Index] = row.Opcode; bytes[2 + row.Index] = row.Raw64;
            bytes[4 + row.Index] = row.EffectValue; bytes[6 + row.Index] = row.Raw68;
        }
        var source = BaseLookup.Find(saved.CasterBaseLocationId, current);
        int governor = current.ReadPersonLookupReference(saved.GovernorOfficerId)?.PhysicalSlot
            ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("resume_governor_pointer");
        var selection = new BaseOriginAutomaticSelection(source, saved.SourceForceId,
            governor, saved.GovernorOfficerId, saved.SourceHexId, null, null);
        return ApplyBaseOriginAutomaticExecution(state, current, source, pair, selection,
            new(saved.TacticId, S14TacticEffectProgramReader.Read(saved.TacticId, bytes),
                saved.Instructions.OrderBy(row => row.Index).Select(row => row.OrderedHexIds.ToArray()).ToArray()),
            sampler, date, effects, revision, saved);
    }

    private BaseOriginAutomaticApplication ApplyBaseOriginAutomaticExecution(GameState state,
        SourceWorldContext current, BaseReference origin, NormalCombatMixedPair pair,
        BaseOriginAutomaticSelection selection, BorrowedAutomaticExecutionPlan selected,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, long revision, BaseAutomaticTacticExecutionContinuation? resume)
    {
        var context = Context(current, sampler);
        var source = _catalog.Rows[selected.TacticSlot];
        var sourceRules = new TacticEffectSourceRules(_catalog, context, new(new HexTravelMap(_map)));
        var working = state;
        var ledger = resume is null ? new AutomaticBaseTacticDamageLedger()
            : AutomaticBaseTacticDamageLedger.Restore(resume.DamageContributions);
        var results = new[] { resume?.FirstInstructionResults.ToList() ?? new List<TacticResultPresentation>(),
            resume?.SecondInstructionResults.ToList() ?? new List<TacticResultPresentation>() };
        var details = resume?.Details.ToList() ?? new List<TacticReportEntry>();
        var presentationEffects = resume?.ArmyPresentationEffects.ToList() ?? new List<TacticEffectPresentation>();
        int sourceLocation = Need(current.ReadBaseObjectLink(origin), "source_Base4E");
        foreach (var instruction in selected.Program.Instructions)
        {
            if (resume is not null && instruction.Index < resume.InstructionIndex) continue;
            bool resumingInstruction = resume is not null && instruction.Index == resume.InstructionIndex;
            BaseOriginAutomaticTroopPrefix? troop = null;
            BaseOriginAutomaticModifierPrefix? modifier = null;
            BaseOriginAutomaticFirePrefix? fire = null;
            int? abnormalSourceHex = null;
            TacticEffectScaleSourceInput? scale = null;
            switch (instruction.Opcode)
            {
                case 0 or 18: continue; //177610's actual Army-only entry gate.
                case 1:
                    troop = resumingInstruction
                        ? new(resume!.TroopPrefix!.SourceKind, resume.TroopPrefix.SourceId,
                            resume.TroopPrefix.SourceTroops, resume.TroopPrefix.Object14,
                            resume.TroopPrefix.SourceForce, resume.TroopPrefix.StageProduct)
                        : context.BeginBaseOriginAutomaticTroops(working, origin);
                    break;
                case 2 or 3 or 4 or 23:
                    abnormalSourceHex = context.ReadBaseOriginAutomaticAbnormalPrefix(working, origin, instruction);
                    break;
                case >= 5 and <= 14:
                case 21 or 22:
                    modifier = BeginBaseOriginAutomaticModifiersCurrent(working, current, context, origin, instruction);
                    break;
                case 15 or 20: scale = context.ResolveBaseOriginAutomaticScale(working, origin, source, instruction); break;
                case 16:
                    //177990's stage/waiting prefix precedes even an empty list.
                    _ = TacticRemakeCurrentContext.OriginParameter(working, SimulationParameterKeys.Tactics.EffectStageScale);
                    break;
                case 17:
                    fire = BeginBaseOriginAutomaticFireCurrent(working, current, context, origin, source, instruction);
                    break;
                case 19: break;
                default: throw Missing("opcode" + instruction.Opcode + "_Base_actor_writer");
            }
            var vector = selected.HexVectors[instruction.Index];
            for (int targetIndex = resumingInstruction ? resume!.TargetIndex : 0; targetIndex < vector.Length; targetIndex++)
            {
                int nodeHex = vector[targetIndex];
                if (resumingInstruction && targetIndex == resume!.TargetIndex)
                {
                    //The target body and the real259360/295EE0 prefix already
                    //ran. Its successor answer resumes only this node's tail.
                    working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                    FinishArmyNode(resume.TargetUnitId, resume.TargetArmyPhysicalSlot, resume.TargetReceiverId,
                        nodeHex, instruction, resume.RequestedLoss);
                    continue;
                }
                if (instruction.Opcode == 17)
                {
                    //177C60 addresses the actual Hex before any occupant
                    //dispatch. Ground ignition is not an invalid Army target.
                    var applied = ApplyBaseOriginAutomaticFireHexCurrent(working, current, fire!, nodeHex,
                        pair.Target.HexId, date, revision, effects);
                    working = applied.State;
                    if (applied.TargetResultMarker)
                        AddMarker(applied.MarkerTarget ?? throw Missing("fire_marker_receiver"), nodeHex, instruction.Index, 0);
                    continue;
                }
                var read = FieldHexOccupantSource.ReadCurrent(working, current, nodeHex);
                var target = read.Receipt ?? throw Missing(read.UnresolvedOwner ?? "live_target");
                if (target.NativeKind == 255) continue;
                //The support wrappers dispatch only their actual Base/Army
                //receivers; a changed live Object has no write or marker.
                if ((instruction.Opcode is 15 or 16 or 19 or 20) && target.NativeKind is not (5 or 6 or 27)) continue;
                ushort marker = 0;
                bool mark = false;
                if (instruction.Opcode is >= 5 and <= 14 or 21 or 22)
                {
                    var applied = ApplyBaseOriginAutomaticModifiersCurrent(working, current, context, origin,
                        target, source, instruction, modifier!, sampler, date, effects);
                    working = applied.State;
                    if (applied.TargetResultMarker) AddMarker(target, nodeHex, instruction.Index, 0);
                    continue;
                }
                if (instruction.Opcode is 2 or 3 or 4 or 23)
                {
                    var applied = context.ApplyBaseOriginAutomaticAbnormalCurrent(working, current, origin,
                        target, source, instruction, abnormalSourceHex!.Value, sampler.Read(working));
                    working = ApplyBaseOriginAutomaticAbnormalReportsCurrent(applied.State, current, origin,
                        target, source, instruction, applied, sampler, date, effects);
                    if (target.NativeKind == 27)
                    {
                        var receiver = working.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId;
                        foreach (var effect in applied.ArmyEffects)
                        {
                            presentationEffects.Add(new(instruction.Index, receiver, effect.Kind, effect.Amount,
                                effect.HealingResult, effect.NativeAbnormalAfter, effect.CurrentMoraleAfter));
                            Detail(target.GameplayArmyId, null, nodeHex, instruction.Index, effect.Kind, effect.Amount);
                        }
                    }
                    if (applied.TargetResultMarker) AddMarker(target, nodeHex, instruction.Index, 0);
                    continue;
                }
                if (target.NativeKind == 27)
                {
                    int id = target.GameplayArmyId ?? throw Missing("target_ArmyId");
                    var army = working.Units.GetValueOrDefault(id) ?? throw Missing("target_Army_image");
                    var reference = new ArmyUnitReference(target.NativePhysicalSlot);
                    if (!Need(current.ReadArmyUnitNativeValidity(reference), "target_Army_validity")) continue;
                    if (instruction.Opcode == 1)
                    {
                        int amount = context.ResolveBaseOriginAutomaticTroopDamage(working, origin, target, source, instruction, troop!);
                        if (nodeHex != pair.Target.HexId)
                        {
                            var applied = ApplyBaseOriginAutomaticArmyDamage(working, current, origin, id, amount, sampler, date, revision, effects);
                            working = applied.State;
                            if (working.ArmyDefeatContinuation is not null)
                                return Suspend(instruction, targetIndex, id, target.NativePhysicalSlot,
                                    working.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId, nodeHex, amount, troop);
                            if (applied.NativeReturn) working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                        }
                        FinishArmyNode(id, target.NativePhysicalSlot, working.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId,
                            nodeHex, instruction, amount);
                        continue;
                    }
                    if (instruction.Opcode == 16)
                    {
                        int amount = context.ResolveBaseOriginAutomaticMoraleLoss(working, origin, target, source, instruction);
                        var routed = ArmyMoraleZeroRoutingCurrent.Apply(working, current, reference, amount,
                            TrapMobility(), ImmediateMorale, new(selection.SourceHex));
                        working = routed.State ?? throw Missing(routed.UnresolvedOwner ?? "Army_2709A0");
                        if (routed.NativeReturn is not int native) throw Missing("Army_2709A0_return");
                        if (working.ArmyDefeatContinuation is not null)
                            return Suspend(instruction, targetIndex, id, target.NativePhysicalSlot,
                                working.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId, nodeHex, 0, null);
                        if (native != 0) working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                        mark = true;
                    }
                    else if (instruction.Opcode is 15 or 19 or 20)
                    {
                        var resolution = instruction.Opcode == 19
                            ? sourceRules.ResolveCapturedArmyClearTarget(working, army, (reached, recipient) =>
                                context.ArmyValid(reached, recipient) == true && recipient.HexId is >= 0 and < 48400
                                    ? reached.RandomState : null)
                            : instruction.Opcode == 15
                            ? context.ResolveAutomaticArmyMoraleTarget(working, army, scale!)
                            : context.ResolveAutomaticArmyHealingTarget(working, army, scale!);
                        if (resolution is null) throw Missing("Army_opcode" + instruction.Opcode + "_target");
                        foreach (var effect in resolution.Effects)
                        {
                            army = TacticEffectSourceRules.ApplyReceipt(army, effect);
                            if (army.Troops <= 0 || army.MoraleState.CurrentValue is not > 0) throw Missing("support_unexpected_retirement");
                            var receiver = working.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId;
                            presentationEffects.Add(new(instruction.Index, receiver, effect.Kind, effect.Amount,
                                effect.HealingResult, effect.NativeAbnormalAfter, effect.CurrentMoraleAfter));
                            Detail(id, null, nodeHex, instruction.Index, effect.Kind, effect.Amount);
                            if (instruction.Opcode == 20) marker = unchecked((ushort)effect.Amount);
                        }
                        working = working with { Units = new(working.Units) { [id] = army }, RandomState = resolution.RandomState,
                            ResourceState = resolution.ResourceStateAfter ?? working.ResourceState };
                        if (instruction.Opcode == 19)
                        {
                            var runtime = army.NativeTacticRuntime ?? throw Missing("clear_Army_counters");
                            var raw = new RegistryObjectReference(RegistryKind.Army, target.NativePhysicalSlot);
                            current.SetTacticAbnormalRawByte(raw, 0x23, runtime.Abnormal23);
                            current.SetTacticAbnormalRawByte(raw, 0x24, runtime.Abnormal24);
                            current.SetTacticAbnormalRawByte(raw, 0x25, runtime.Abnormal25);
                            current.SetArmyCommandOverride(target.NativePhysicalSlot, ArmyOverrideTargetSourceAdapter.Read(working, army));
                        }
                        else
                        {
                            var quantities = current.ReadArmyReturnResourceFields(reference) ?? throw Missing("support_Army_quantities");
                            bool healing = instruction.Opcode == 20;
                            quantities = healing ? quantities with { Ready16 = checked((ushort)army.Troops), Wounded18 = checked((ushort)army.WoundedTroops!.Value) }
                                : quantities with { Morale1A = ArmyMoraleSourceAdapter.Read(army) };
                            current.BindArmyReturnResourceFields(target.NativePhysicalSlot, quantities);
                            current.AcknowledgeArmyReturnNumerics(target.NativePhysicalSlot, wounded: healing, morale: !healing);
                        }
                        mark = resolution.Effects.Length != 0;
                    }
                    else throw Missing("Army_opcode" + instruction.Opcode + "_application");
                }
                else if (target.NativeKind is 5 or 6)
                {
                    var reference = new BaseReference(target.NativeKind == 5 ? BaseKind.City : BaseKind.Gate, target.NativePhysicalSlot);
                    if (!Need(current.ReadBaseNativeValidity(reference), "target_Base_validity")) continue;
                    int location = target.BaseLocationId ?? throw Missing("target_Base_location");
                    if (instruction.Opcode == 1)
                    {
                        var numeric = context.ResolveBaseOriginAutomaticBaseTroopDamage(working, origin, target, source, instruction, troop!);
                        if (!numeric.TargetValid || !numeric.TargetPermitted) continue;
                        int amount = numeric.Amount ?? throw Missing("Base_damage_amount");
                        if (nodeHex != pair.Target.HexId)
                        {
                            var applied = ApplyBaseOriginAutomaticBaseDamageCurrent(working, current, origin, reference, amount,
                                sampler, (reached, recipient, district) => ApplyBaseOriginFatalCaptureCurrent(reached, current,
                                    recipient, district, sampler, date, revision, effects));
                            working = applied.State;
                            if (applied.NativeReturn) working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                        }
                        AddMarker(target, nodeHex, instruction.Index, unchecked((ushort)amount));
                        if (Need(current.ReadBaseNativeValidity(reference), "post_marker_Base_validity")) ledger.Accumulate(reference, amount);
                        Detail(null, location, nodeHex, instruction.Index, PlayableTacticEffectKind.ArmyDamage, unchecked((ushort)amount));
                        continue;
                    }
                    if (instruction.Opcode is 15 or 16 or 19 or 20)
                    {
                        var applied = ApplyBaseOriginAutomaticBaseSupportCurrent(working, current, context, origin,
                            target, source, instruction, scale);
                        working = applied.State;
                        if (applied.EffectKind is { } effect)
                            Detail(null, location, nodeHex, instruction.Index, effect, applied.EffectAmount);
                        if (applied.TargetResultMarker)
                            AddMarker(target, nodeHex, instruction.Index, applied.MarkerAmount);
                        continue;
                    }
                    throw Missing("Base_target_opcode" + instruction.Opcode + "_application");
                }
                else throw Missing("target_kind" + target.NativeKind);
                if (mark) AddMarker(target, nodeHex, instruction.Index, marker);
            }
        }
        //No16DF00 for Base actors and no178990 Army commander contribution.
        var tactic = current.ReadSkillTacticReference(selected.TacticSlot) ?? throw Missing("waiting_tactic");
        int waiting = Need(current.ReadSkillTacticRawByte(tactic, 0x42), "waiting42_low") |
            Need(current.ReadSkillTacticRawByte(tactic, 0x43), "waiting42_high") << 8;
        int capWaiting = TacticRemakeCurrentContext.OriginParameter(working, SimulationParameterKeys.SubdayStatus.BaseCooldownUpper);
        var finalFields = current.ReadCaptureBaseThreatFields(origin) ?? throw Missing("waiting_Base_fields");
        current.SetCaptureBaseThreatFields(origin, finalFields with { Word4C = unchecked((ushort)Math.Min(waiting, capWaiting)) });
        working = CaptureBaseThreatInputs.PublishCurrent(working, current);
        var used = Report("used", pair.Target.UnitId, null, pair.Target.HexId, null, 0);
        working = working with { TacticReports = [.. working.TacticReports ?? [], used, .. details] };
        var manager = working.ManualTacticIntervention ?? throw Missing("manager168C40");
        if (manager.Effects.Length < 563)
        {
            var ordered = results.Select(row => row.ToArray()).ToArray();
            var armies = ordered.Select(row => row.Where(result => result.TargetKind == TacticResultTargetKind.ArmyReceiver).Select(result => result.TargetId).ToArray()).ToArray();
            var actor = new TacticActorPresentation(new(0), selected.TacticSlot, new(ManualTacticTargetKind.Hex, pair.Target.HexId), armies)
            { BaseCasterLocationId = sourceLocation, InstructionResults = ordered, Effects = presentationEffects.ToArray() };
            working = working with { ManualTacticIntervention = manager.Enqueue(TacticPresentationKind.OrdinaryEffects, [actor]) };
        }
        var frame22 = ledger.ConsumeFrame22(current, pair.Target.Kind, checked((ushort)pair.Target.PhysicalSlot));
        working = BaseCaptureCurrentInputs.PublishCurrent(working, current);
        return new(working, true, frame22.Word22After);

        GameState ImmediateMorale(GameState value, UnitDefeatRequest request)
            => ApplyNormalCombatImmediateDefeatCurrent(value, current, request,
                sampler.Read(value) ?? throw Missing("morale_immediate_sampler"), date, revision, effects);
        void FinishArmyNode(int id, int physical, ArmyReceiverId receiver, int hex,
            S14TacticEffectInstruction instruction, int request)
        {
            ushort amount = instruction.Opcode == 1 ? unchecked((ushort)request) : (ushort)0;
            results[instruction.Index].Add(new(TacticResultTargetKind.ArmyReceiver, receiver.Value, hex, amount));
            if (instruction.Opcode != 1) return;
            if (Need(current.ReadArmyUnitNativeValidity(new(physical)), "post_marker_Army_validity"))
                ledger.AccumulateArmy(new(physical), request);
            Detail(id, null, hex, instruction.Index, PlayableTacticEffectKind.ArmyDamage, amount);
        }
        BaseOriginAutomaticApplication Suspend(S14TacticEffectInstruction instruction, int targetIndex,
            int targetId, int physical, ArmyReceiverId receiver, int hex, int request, BaseOriginAutomaticTroopPrefix? prefix)
        {
            var saved = new BaseAutomaticTacticExecutionContinuation(sourceLocation, selection.GovernorId,
                selection.SourceForce, selection.SourceHex, selected.TacticSlot,
                selected.Program.Instructions.Select(row => new AutomaticTacticInstructionSnapshot(row.Index,
                    row.Opcode, row.Raw64, row.EffectValue, row.Raw68,
                    selected.HexVectors[row.Index].ToImmutableArray())).ToImmutableArray(),
                instruction.Index, targetIndex, targetId, physical, receiver, hex, request,
                ledger.Capture(), details.ToImmutableArray(), results[0].ToImmutableArray(), results[1].ToImmutableArray(),
                presentationEffects.ToImmutableArray())
                { TroopPrefix = prefix is null ? null : new(prefix.SourceKind, prefix.SourceId, prefix.SourceTroops,
                    prefix.Object14, prefix.SourceForce, prefix.StageProduct) };
            working = BaseCaptureCurrentInputs.PublishCurrent(working, current);
            return new(working, true, 0) { Continuation = saved };
        }
        void AddMarker(FieldHexOccupantReceipt target, int hex, int instruction, ushort amount)
        {
            TacticResultTargetKind kind;
            int id;
            if (target.NativeKind == 27)
            {
                kind = TacticResultTargetKind.ArmyReceiver;
                id = working.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId.Value;
            }
            else if (target.NativeKind is 5 or 6)
            {
                kind = TacticResultTargetKind.BaseLocation;
                id = target.BaseLocationId ?? throw Missing("marker_Base_location");
            }
            else if (target.NativeKind == 11)
            {
                kind = TacticResultTargetKind.MapObjectReceiver;
                int physical = target.CapturedObject?.PhysicalSlot ?? throw Missing("marker_Object_pointer");
                id = (current.CurrentMapObjectDirectory ?? throw Missing("marker_Object_directory"))
                    .ReceiverAtPhysical(physical).Value;
            }
            else throw Missing("marker_kind" + target.NativeKind);
            results[instruction].Add(new(kind, id, hex, amount));
        }
        TacticReportEntry Report(string kind, int? targetArmy, int? instruction, int hex, PlayableTacticEffectKind? effect, int amount)
            => new(kind, checked(state.PeriodNumber + 1), selection.SourceForce, 0, selection.GovernorId,
                selected.TacticSlot, targetArmy, effect, amount)
            { SourceBaseLocationId = sourceLocation, NativeUpdateOrdinal = state.NativeClock!.UpdateOrdinal,
                TargetHexId = hex, InstructionIndex = instruction };
        void Detail(int? targetArmy, int? location, int hex, int instruction, PlayableTacticEffectKind effect, int amount)
            => details.Add(Report("effect", targetArmy, instruction, hex, effect, amount) with { TargetBaseLocationId = location });
        T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        InvalidDataException Missing(string name) => TacticRemakeCurrentContext.OriginAutomaticMissing(name);
    }

    private AutomaticArmyTroopApplication ApplyBaseOriginAutomaticArmyDamage(GameState state, SourceWorldContext current,
        BaseReference origin, int targetId, int requested, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        TacticRemakeCurrentContext Latest(GameState value)
        { OwnedArmyDestructionCurrent.BindOwnedCurrentContext(value, current); return Context(current, sampler); }
        _baseCapacityInputs.BindCurrent(state, current);
        var body = TryApplyBorrowedBaseArmyNumbers(state, origin, targetId, requested, Latest, current);
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved || body.Receipt is not { } receipt)
            throw TacticRemakeCurrentContext.OriginAutomaticMissing(body.MissingPhase ?? "Army_body");
        var working = body.PendingCleanupState ?? body.State;
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.NativeTargetSkipped) return new(working, false);
        var target = working.Units.GetValueOrDefault(targetId) ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Army_body_image");
        int physical = target.NativeArmyOrigin?.NativeSlotIndex ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Army_body_physical");
        bool killed = receipt.Killed ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Army_body_return");
        var feedback = new ArmyTraitFeedback(working, current, date, effects, () => working,
            speechConsumer: CreateArmyTraitSpeechConsumer(current, () => working, next => working = next));
        if (!killed)
        {
            var personality = ApplyBorrowedNormalPersonality(working, current, Latest(working), targetId);
            if (personality.Outcome == NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
                throw TacticRemakeCurrentContext.OriginAutomaticMissing(personality.MissingOwner ?? "personality");
            working = personality.State;
            foreach (var activation in personality.Activations) feedback.RefreshAndFeedback(new(physical), new(activation.PersonalityPhysicalSlot));
        }
        feedback.Refresh(new(physical)); working = feedback.Publish(working);
        if (killed)
        {
            int location = current.ReadBaseObjectLink(origin) ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("winner_Base4E");
            var admission = OwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(working, current, physical, null, location);
            if (admission.RequiresImmediateSettlement)
            {
                var winner = ArmyDestructionQueue.ResolveBase(current, origin)
                    ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("immediate_winner_Base");
                working = ApplyNormalCombatImmediateDefeatCurrent(working, current, new(targetId, null, winner),
                    sampler.Read(working) ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("immediate_sampler"),
                    date, revision, effects);
            }
            else if (admission.IsPending && admission.State is { } queued) working = queued;
            else throw TacticRemakeCurrentContext.OriginAutomaticMissing(admission.UnresolvedOwner ?? "Army1745B0");
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(working, current);
        }
        return new(working, killed);
    }
}
