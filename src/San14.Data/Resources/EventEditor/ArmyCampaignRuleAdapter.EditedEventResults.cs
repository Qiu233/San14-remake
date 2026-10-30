using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Apply actual business result instructions to one detached current.
    /// Returned State is the complete candidate; no occurrence or Story ACK is
    /// written here. A missing later input publishes none of the candidate.</summary>
    public EditedEventResultApplication ApplyEditedEventResultsCurrent(GameState state,
        S14EventExecutionProgram program,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        CampaignStorySelectionContext? selections = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(dispatchInputs);
        EditedEventResultExecution.RequireImplemented(program.DispatchProgram);
        var current = CaptureCurrentCampaignEventQueries(state);
        return ApplyEditedEventResultProgramCurrent(state, current, program, dispatchInputs, selections);
    }

    private EditedEventResultApplication ApplyEditedEventResultProgramCurrent(GameState state,
        SourceWorldContext current, S14EventExecutionProgram program,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        CampaignStorySelectionContext? selections = null)
    {
        _baseCapacityInputs.BindCurrent(state, current);
        var runtime = new EditedEventResultsCurrent(this, state, current, dispatchInputs, selections);
        var saved = state.EditedEventResultCursor;
        var checkpoint = saved is null ? null : new S14EventExecutionCheckpoint(saved.Frames.Select(frame =>
            new S14EventExecutionFrame(frame.Begin, frame.End, frame.All, frame.Depth, frame.OrDepth,
                frame.Cursor, frame.Fold, frame.PendingGroup, frame.IfPhase, frame.ThenMarker,
                frame.ElseMarker, frame.SelectedThen)).ToImmutableArray(), saved.SuccessCount, saved.EvaluatedCount, saved.RootFold);
        var slice = S14EventConditionEvaluator.EvaluateUntilYield(program.DispatchProgram, runtime,
            checkpoint, () => false, isLeafIncomplete: () => runtime.IsLeafIncomplete);
        bool paused = !slice.Checkpoint.Frames.IsEmpty;
        var cursor = paused ? new EditedEventResultCursor(slice.Checkpoint.Frames.Select(frame =>
            new CampaignStoryDispatchFrame(frame.Begin, frame.End, frame.All, frame.Depth, frame.OrDepth,
                frame.Cursor, frame.Fold, frame.PendingGroup, frame.IfPhase, frame.ThenMarker,
                frame.ElseMarker, frame.SelectedThen)).ToImmutableArray(), slice.Checkpoint.SuccessCount,
                slice.Checkpoint.EvaluatedCount, slice.Checkpoint.RootFold) : null;
        return new(runtime.CaptureResultCursor(cursor), slice.Result) { IsPaused = paused };
    }

    private sealed partial class EditedEventResultsCurrent(ArmyCampaignRuleAdapter rules, GameState state, SourceWorldContext current,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        CampaignStorySelectionContext? selections) : IS14EventConditionRuntime
    {
        internal GameState State { get; private set; } = state;
        internal bool IsLeafIncomplete => State.EditedEventForceAbsorptionTail is not null;
        internal GameState CaptureResultCursor(EditedEventResultCursor? cursor) =>
            State = State with { EditedEventResultCursor = cursor };
        private EditedEventResultSelectors? selectors;
        private EditedEventResultSelectors Selectors => selectors ??= new(current, selections,
            () => rules.BindCampaignStoryInputs(State, current, false));
        public bool ConditionBlocked => false;
        public bool TryGetBinding(int code, out S14ConditionBinding binding)
            => EventExecutionPermissionsData.TryGet(code, out binding);
        public S14ConditionDispatchInputs ReadInputs(S14ConditionContext context)
        {
            BindCurrentInputs();
            return dispatchInputs(context);
        }

        public int InvokeLeaf(S14ConditionContext context)
        {
            BindCurrentInputs();
            var arguments = context.Operation.RawArguments;
            if (arguments.Count != 8) throw Missing("statement_arguments");
            return context.Operation.Code switch
            {
                388 or 389 or 391 or 392 or 393 or 394 or 399 => ForceResult(context.Operation.Code, arguments),
                400 or 401 => MovePerson(context.Operation.Code, arguments),
                403 => Parameter(arguments),
                405 => Death(arguments),
                407 => Partner(arguments),
                408 => Illness(arguments),
                409 => Like(arguments),
                419 => Sworn(arguments),
                429 => Credit(arguments),
                395 => Resource(arguments, BaseResourceKind.Gold),
                396 => Resource(arguments, BaseResourceKind.Supplies),
                397 => Resource(arguments, BaseResourceKind.Troops),
                398 => Endurance(arguments),
                404 => Loyalty(arguments),
                _ => throw Missing(EditedEventResultExecution.UnresolvedOwner(context.Operation.Code)
                    ?? "unexpected_structure_callback")
            };
        }

        private void BindCurrentInputs()
        {
            //Later statements consume this transaction's latest semantic
            //owners, while the same mutable current retains earlier writes.
            rules._baseCapacityInputs.BindCurrent(State, current);
            rules._personRelationInputs.BindCurrent(() => State, current);
            current.ConditionLoyaltyReader = physical =>
                ArmyDestructionLoyalty.ReadCurrentRawE8(State, current, physical, rules._governorInputs);
            current.ConditionBaseAbilityReader = (physical, index) =>
            {
                if (current.ReadPersonIdentity(physical) is not ushort id ||
                    current.ResolveOfficerSource(id) != physical ||
                    State.Officers.GetValueOrDefault(id)?.BaseAbilities is not { } ability) return null;
                int value = index switch
                {
                    0 => ability.Leadership, 1 => ability.Strength, 2 => ability.Intelligence,
                    3 => ability.Politics, 4 => ability.Charm, _ => -1
                };
                return value is >= 0 and <= 255 ? (byte)value : null;
            };
        }

        private int MovePerson(int code, IReadOnlyList<int> arguments)
        {
            var changed = rules.ApplyEditedEventPersonMovementResultCurrent(State, current, code, arguments, Selectors);
            State = changed.State;
            return changed.NativeReturn;
        }

        private int Death(IReadOnlyList<int> arguments)
        {
            var changed = rules.ApplyEditedEventPersonDeathCurrent(State, current, arguments, Selectors);
            State = changed.State;
            return changed.NativeReturn;
        }

        private BaseReference Base(int selector) => Selectors.Base(selector);

        private int Resource(IReadOnlyList<int> arguments, BaseResourceKind kind)
        {
            var receiver = Base(arguments[0]);
            if (!Need(current.ReadBaseNativeValidity(receiver), "resource_Base_validity")) return 0;
            if (kind is BaseResourceKind.Gold or BaseResourceKind.Supplies)
            {
                if (Need(current.ReadBaseObjectLink(receiver), "resource_Base4E") >= 52) return 0;
            }
            else if (Need(current.ReadBaseForceSlot(receiver), "troops_Base_virtualB0") == 0) return 0;
            int before = Need(current.ReadBaseResource(receiver, kind), "resource_before");
            int requested = EditedEventResultExecution.Change(before, arguments[1], arguments[2]);
            int first = Capacity();
            int bounded = EditedEventResultExecution.Bound(requested, first);
            //Gold/food setters read capacity again; troop source stores after
            //its explicit second virtualC8 call. These are the same rule kernel.
            int after = EditedEventResultExecution.Bound(bounded, Capacity());
            current.SetBaseResource(receiver, kind, after);
            int physical = Need(receiver.PhysicalSlot, "resource_Base_receiver");
            if (receiver.Kind == BaseKind.City)
            {
                var owner = State.Cities.GetValueOrDefault(physical) ?? throw Missing("resource_City_owner");
                var changed = kind switch
                {
                    BaseResourceKind.Gold => owner with { Gold = after },
                    BaseResourceKind.Supplies => owner with { Supplies = after },
                    _ => owner with { Troops = after }
                };
                State = State with { Cities = new(State.Cities) { [physical] = changed } };
            }
            else
            {
                int id = checked(physical + 51);
                var owner = State.Gates.GetValueOrDefault(id) ?? throw Missing("resource_Gate_owner");
                if (kind != BaseResourceKind.Troops) throw Missing("resource_noncity_currency_owner");
                State = State with { Gates = new(State.Gates) { [id] = owner with { Troops = after } } };
            }
            return 1;

            int Capacity() => kind switch
            {
                BaseResourceKind.Gold => BasePostLoad.ReadGoldCapacity(receiver, current),
                BaseResourceKind.Supplies => BasePostLoad.ReadSuppliesCapacity(receiver, current),
                _ => BasePostLoad.ReadTroopCapacity(receiver, current)
            };
        }

        private int Endurance(IReadOnlyList<int> arguments)
        {
            var receiver = Base(arguments[0]);
            if (!Need(current.ReadBaseNativeValidity(receiver), "endurance_Base_validity")) return 0;
            int before = BasePostLoad.ReadLinkedField14(receiver, current);
            int requested = EditedEventResultExecution.Change(before, arguments[1], arguments[2]);
            int bounded = EditedEventResultExecution.Bound(requested,
                BasePostLoad.ReadCapacities(receiver, current).Field46);
            //241140/1F4440 and2AA9B0 are the existing actual object setter
            //and damage-tier kernels, including the valid linked receiver gate.
            BasePostLoad.WriteLinkedField14(receiver, bounded, current);
            byte tier = BasePostLoad.ReadFlag66(receiver, current);
            current.SetBasePostLoadFlag66(receiver, tier);
            int physical = Need(receiver.PhysicalSlot, "endurance_Base_receiver");
            int after = BasePostLoad.ReadLinkedField14(receiver, current);
            if (receiver.Kind == BaseKind.City)
            {
                var owner = State.Cities.GetValueOrDefault(physical) ?? throw Missing("endurance_City_owner");
                State = State with { Cities = new(State.Cities) { [physical] = owner with
                    { Durability = after, DamageTier = tier } } };
            }
            else
            {
                int id = checked(physical + 51);
                var owner = State.Gates.GetValueOrDefault(id) ?? throw Missing("endurance_Gate_owner");
                State = State with { Gates = new(State.Gates) { [id] = owner with
                    { Durability = after, DamageTier = tier } } };
            }
            return 1;
        }

        private int Loyalty(IReadOnlyList<int> arguments)
        {
            int physical = Selectors.Person(arguments[0]);
            if (!Need(current.ReadPersonNativeValidity(physical), "loyalty_Person_validity")) return 0;
            ushort id = Need(current.ReadPersonIdentity(physical), "loyalty_Person10");
            if (unchecked((ushort)(id - 4001)) <= 1099) return 0;
            //232530 applies only outside a real Base1..61;233240 has the
            //opposite home gate and its own special-ID rule.
            ushort home = Need(current.ReadPersonFirstLocation(physical), "loyalty_PersonE2");
            if ((home is < 1 or > 61) && (DuelAftermathForce(current, physical) is >= 46 and <= 50)) return 0;
            if (DuelAftermathSpecialPerson(current, physical)) return 0;
            ushort flags = Need(current.ReadOwnedReturnPersonFlags(physical), "loyalty_Person15E");
            if ((flags & 0x4000) != 0) return 0;
            byte status = Need(current.ReadPersonStatus(physical), "loyalty_PersonE6");
            if (status == 1 || status is 6 or 7 or 9) return 0;
            //The source writes the low byte, except bit13 forces raw255.
            byte after = (flags & 0x2000) != 0 ? byte.MaxValue : unchecked((byte)arguments[1]);
            current.SetPersonDestructionLoyaltyE8(physical, after);
            State = ArmyDestructionLoyalty.PublishCurrentRawE8(State, current, physical, after);
            return 1;
        }

        private static T Need<T>(T? value, string owner) where T : struct
            => value ?? throw Missing(owner);
        private static InvalidDataException Missing(string owner)
            => new("edited_event.results.current." + owner);
    }
}
