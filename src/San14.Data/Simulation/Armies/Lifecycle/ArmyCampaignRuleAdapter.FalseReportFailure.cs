using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC2E35D0's failed281AC0 branch, borrowing the enclosing current/pool.
    //The caller still owns destination reset, common finish and one batch.
    private GameState FApplyFailure(GameState state, SourceWorldContext current,
        int actorPhysical, int targetPhysical, NativeArmyProgressStageMoment moment,
        List<CampaignEffectRequest> requests, out bool backfired)
    {
        backfired = false;
        var working = state;
        var changedPeople = new HashSet<int>();
        var feedback = new ArmyTraitFeedback(state, current, moment, requests, () => working);
        var actor = new ArmyUnitReference(actorPhysical);
        var target = new ArmyUnitReference(targetPhysical);
        TacticRemakeCurrentAbilities? abilities = null;

        AdjustMorale(target, FP(working, "target-morale"));
        AdjustMorale(actor, FP(working, "actor-morale"));
        Visual(targetPhysical, ArmyFalseReportVisualEvent.TargetResisted);
        Visual(actorPhysical, ArmyFalseReportVisualEvent.InitiatorFailed);
        backfired = BackfireTrial();
        if (backfired) ApplyConfusion();
        PublishPeople();
        return feedback.Publish(working);

        void AdjustMorale(ArmyUnitReference receiver, int adjustment)
        {
            var applied = ArmyMoraleZeroRoutingCurrent.ApplyImmediate(working, current,
                receiver, adjustment, TrapMobility(), Immediate);
            working = applied.State ?? throw FM(applied.UnresolvedOwner ?? "failure_morale2709A0");
        }

        GameState Immediate(GameState value, UnitDefeatRequest request)
        {
            working = value;
            PublishPeople();
            var pool = current.ReadOwnedCurrentListPool() ?? throw FM("failure_immediate_pool");
            if (current.ReadOwnedPersonCommandManager() is null)
            {
                if (working.PersonnelCommands is null) throw FM("failure_immediate_personnel_manager");
                PersonReturnCommands.BindCurrent(working, current);
            }
            working = working with { PersonnelCommands = current.ReadOwnedPersonCommandManager()! };
            BindActorlessDestructionInputs(working, current);
            working = new OwnedArmyDestructionStage(this).ApplyBattleDefeatCurrent(working,
                current, pool, request, FSampler(working, moment), moment.PreWriterThirdDate,
                requests, feedback.Refresh, person => changedPeople.Add(person));
            PublishPeople();
            return working;
        }

        void Visual(int physical, ArmyFalseReportVisualEvent kind)
        {
            //The source reads its retained Army pointer even after an immediate
            //defeat. It does not require that Army to remain in state.Units.
            ushort raw = FK(current.ReadArmyUnitRawHex2A(new(physical)), "failure_visual_Army2A");
            var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0)
                ?? throw FM("failure_visual_Hex_pointer");
            int at = FK(current.ReadHexPhysicalSlot(hex), "failure_visual_Hex22DEF0");
            var lifecycle = working.ReadArmyLifecycle() ?? throw FM("failure_visual_lifecycle");
            requests.Add(new(null, null) { ArmyFalseReportVisual = new(kind,
                lifecycle.Slots[physical].ReceiverId, at) });
        }

        bool BackfireTrial()
        {
            //276520 runs after both2709A0 calls and both separate view intents.
            if (!FK(current.ReadArmyUnitNativeValidity(actor), "backfire_actor_validity") ||
                !FK(current.ReadArmyUnitNativeValidity(target), "backfire_target_validity")) return false;
            int percent = FP(working, "backfire-intelligence-percent");
            int difference = unchecked(Intelligence(target) - Intelligence(actor));
            float factor = (float)percent / 100f;
            float scaled = (float)difference * factor;
            int threshold = scaled is >= int.MinValue and < 2147483648f ? (int)scaled : int.MinValue;
            threshold = Math.Max(threshold, FP(working, "backfire-minimum"));
            threshold = Math.Min(threshold, FP(working, "backfire-maximum"));
            int commander = Commander(actor);
            ushort personId = FK(current.ReadPersonIdentity(commander), "backfire_sampler_Person10");
            ushort targetHex = FK(current.ReadArmyUnitRawHex2A(target), "backfire_sampler_target2A");
            ushort actorHex = FK(current.ReadArmyUnitRawHex2A(actor), "backfire_sampler_actor2A");
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1, targetHex, actorHex,
                personId, 0, FSampler(working, moment));
            return sample <= threshold;
        }

        int Intelligence(ArmyUnitReference receiver)
        {
            int person = Commander(receiver);
            if (!FK(current.ReadPersonNativeValidity(person), "backfire_ability_Person_validity")) return 0;
            int id = FK(current.ReadPersonIdentity(person), "backfire_ability_Person10");
            abilities ??= new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            return abilities.ResolveDimension(working, id, 2) ?? throw FM("backfire_current_intelligence");
        }

        int Commander(ArmyUnitReference receiver)
        {
            ushort raw = FK(current.ReadArmyUnitCommanderId(receiver), "failure_Commander12");
            var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw)
                : current.ReadTransferPersonTableReference(0)) ?? throw FM("failure_Commander_pointer");
            return FK(person.PhysicalSlot, "failure_Commander_physical");
        }

        void ApplyConfusion()
        {
            int duration = FP(working, "confusion-duration");
            if (!FK(current.ReadArmyUnitNativeValidity(actor), "backfire_status_Army_validity")) return;
            //Null source, index0, empty-string report operand and param7=0.
            bool general = FK(TacticRemakeCurrentTraits.CommanderImmune(current, Commander(actor), 21),
                "backfire_immunity21");
            bool? specific = general ? null : FK(TacticRemakeCurrentTraits.CommanderImmune(
                current, Commander(actor), 22), "backfire_immunity22");
            TacticAbnormalSourceInput input;
            if (general || specific == true)
                input = new(0, duration, 0, true, general, specific,
                    null, null, null, null, null, null, null, null);
            else
            {
                var cached = current.ReadArmyReturnTraitFields(actor) ?? throw FM("backfire_traits36");
                int[] traits = TacticRemakeCurrentTraits.ArmyContributions(current, Commander(actor), cached.Traits64)
                    ?? throw FM("backfire_target_traits127");
                int generalRate = FK(ArmyDestructionParameterSeed.ReadCurrent(working,
                    SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate), "backfire_general_rate");
                int specificRate = FK(ArmyDestructionParameterSeed.ReadCurrent(working,
                    SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate), "backfire_type0_rate");
                //Both zeros are real initialized null-source locals. The source
                //does not read the trait98 global or any caster for this branch.
                input = new(0, duration, 0, true, false, false, traits[9], traits[10],
                    0, generalRate, specificRate, 0, null, null);
            }
            //CurrentCounter is not consumed by ResolveAdjustment. Read it only
            //after the original immunity/trait and reached233A90 queries.
            var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input)
                ?? throw FM("backfire_adjustment");
            if (!adjustment.CounterWritten) return; //Empty report operand suppresses2F29B0.
            if (adjustment.HasTraitAdjustment)
            {
                int person = Commander(actor);
                byte rawDistrict = FK(current.ReadPersonNativeValidity(person), "backfire_District_Person_validity")
                    ? FK(current.ReadPersonDistrictSlot(person), "backfire_District_PersonE0") : (byte)0;
                var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                    ?? throw FM("backfire_District_pointer");
                //This query is reached even with an empty history operand.
                //Its attribution code is not subsequently used by this caller.
                _ = BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district,
                    field => FM("backfire_233A90_" + field));
            }
            var pointer = new RegistryObjectReference(RegistryKind.Army, actorPhysical);
            var image = FArmy(working, actorPhysical);
            var runtime = image.NativeTacticRuntime ?? throw FM("backfire_counter_image");
            byte before = runtime.Abnormal23;
            int upper = FK(ArmyDestructionParameterSeed.ReadCurrent(working,
                SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "backfire_counter_cap");
            var result = TacticEffectSourceAbnormal.Resolve(input with
                { CurrentCounter = before, CurrentUpperBound = upper }, null)
                ?? throw FM("backfire_counter_result");
            current.SetTacticAbnormalRawByte(pointer, 0x23, result.CounterAfter);
            var changed = image with { NativeTacticRuntime = runtime with { Abnormal23 = result.CounterAfter } };
            if (working.Units.ContainsKey(image.Id))
                working = working with { Units = new(working.Units) { [image.Id] = changed } };
            else
            {
                var lifecycle = working.ReadArmyLifecycle() ?? throw FM("backfire_pending_lifecycle");
                var receiver = lifecycle.ReceiverSlots()[actorPhysical].Receiver;
                if (receiver.PendingImage?.Id != image.Id) throw FM("backfire_pending_image");
                working = working.WithArmyLifecycle(lifecycle with
                    { Receivers = lifecycle.Receivers.With(receiver with { PendingImage = changed }) });
            }
            //2EED60 tests the unclamped sum, not byte delta or a zero->nonzero
            //transition. It emits real native23 with its zero context packet.
            if (result.IncreasingNotification) feedback.AbnormalIncreased(actor, 23);
            //param6 is the actual empty-string sentinel: neither immunity nor
            //started/trait-adjusted paths invoke2F29B0 for this source call.
        }

        void PublishPeople()
        {
            if (changedPeople.Count == 0) return;
            var people = new Dictionary<int, OfficerState>(working.Officers);
            foreach (int physical in changedPeople)
            {
                int id = FK(current.ReadPersonIdentity(physical), "failure_changed_Person10");
                var person = people.GetValueOrDefault(id) ?? throw FM("failure_changed_Person");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (person.TraitActivation is not { DurationSlotCount: 9, ActiveSlots: not null } ||
                    activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                    throw FM("failure_changed_full_activation");
                people[id] = person with { TraitActivation = activation };
            }
            working = working with { Officers = people };
            changedPeople.Clear();
        }
    }
}
