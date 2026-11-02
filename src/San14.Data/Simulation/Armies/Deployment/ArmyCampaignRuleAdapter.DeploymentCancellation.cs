using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableDeploymentCancellationRules
{
    /// <summary>One selected at-home receiver is returned immediately. The
    /// command engine supplies the private candidate; this is not a pending
    /// PreparedDeployment cancellation and does not reconstruct a deployment.</summary>
    public CommandResult CancelDeployment(GameState state, ForceState force,
        CancelCityArmyDeploymentOrder order, CommandContext context)
    {
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var pool = world.Pool;
            var target = current.ReadCityTableReference(order.CityId)
                ?? throw DeploymentCancellationMissing("City_pointer");
            var arena = state.ReadArmyLifecycle()
                ?? throw DeploymentCancellationMissing("Army_lifecycle");
            var selected = arena.ReceiverSlots().FirstOrDefault(slot => slot.ReceiverId == order.Army);
            if (selected is null || !IsCityDeploymentCancellationCandidateCurrent(state, current, target, selected.PhysicalSlot))
                return CommandResult.Reject("The selected army is no longer awaiting departure at this city.", "deployment.cancellation_selection");

            var army = new ArmyUnitReference(selected.PhysicalSlot);
            var operation = FacilityOperation.Playable(state);
            //772200 retains the commander before29F220, then clears his
            //return bit. The existing ordinary wrapper implements both in
            //that order, borrowing this operation's exact current and pool.
            var working = ReturnCampaignArmyAtBaseCurrent(state, current, pool, army, target, operation,
                out int retainedPerson);
            if (working.ArmyReturnCaptiveContinuation is { } pending)
                return CommandResult.Accept(working with { ArmyReturnCaptiveContinuation = pending with
                {
                    Caller = ArmyReturnCaptiveCaller.OrdinaryEntry,
                    DeploymentCancellationTail = new(order.CityId, retainedPerson,
                        operation.ReadDate(), operation.Revision, operation.ReadCampaignDay(working))
                } });
            return CommandResult.Accept(FinishCancelledDeploymentReturnCurrent(working, current, pool,
                army, target, operation));
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable("rules.deployment_cancellation", error.Message); }
    }

    private GameState FinishCancelledDeploymentReturnCurrent(GameState working, SourceWorldContext current,
        WorldIndexContext pool, ArmyUnitReference army, BaseReference target, FacilityOperation operation)
    {
        //The transport branch is tested after29F220 and before28E6D0.
        //Its receiver remains alive until the actual pending cleanup.
        if (DeploymentCancellationKnown(current.ReadArmyReturnUnitStatus10(army), "Army_status10") == 2)
        {
            ushort commander = DeploymentCancellationKnown(current.ReadArmyUnitCommanderId(army), "Army_Commander12");
            var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                ?? throw DeploymentCancellationMissing("Army_Commander_pointer");
            int physical = DeploymentCancellationKnown(person.PhysicalSlot, "Army_Commander_physical");
            byte forceSlot = DeploymentCancellationKnown(current.ReadPersonOldForceSlot(physical), "Army_Force22C990");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
                ?? throw DeploymentCancellationMissing("player_Force");
            if (DeploymentCancellationKnown(current.ReadForceNativeValidity(player), "player_validity") &&
                player.PhysicalSlot == forceSlot &&
                DeploymentCancellationKnown(current.ReadBaseObjectLink(target), "City4E") < 52)
                working = RefreshCancelledDeploymentFoodWarningCurrent(working, current, target, operation);
        }

        working = ConsumeArmyLifecyclePendingCurrent(working, current, pool)
            ?? throw DeploymentCancellationMissing("pending70_cleanup");

        //The return can reorganize a ruler's District, so resolve the
        //City's current v80 only now. Refund one book, not the troop fee.
        var district = current.ReadBaseDistrictReference(target)
            ?? throw DeploymentCancellationMissing("refund_City_District");
        if (DeploymentCancellationKnown(current.ReadDistrictNativeValidity(district), "refund_District_validity"))
        {
            int districtId = DeploymentCancellationKnown(district.PhysicalSlot, "refund_District_physical");
            var domain = working.Districts.GetValueOrDefault(districtId)
                ?? throw DeploymentCancellationMissing("refund_District_owner");
            //Ordinary command spending is owned by DistrictState. The
            //force-reset window can predate those debits; it is not a
            //second current book balance. Publish all three views below.
            int before = domain.OrdersRemaining
                ?? throw DeploymentCancellationMissing("refund_District14");
            //2D9930 is the existing current leader-politics limit, not
            //the saved UI budget or a guessed constant command capacity.
            int limit = ReadDistrictCommandReturnLimitCurrent(working, current, districtId);
            int after = Math.Clamp(Math.Min(unchecked(before + 1), limit), 0, 200);
            current.SetDistrictRawByte(districtId, 0x14, (byte)after);
            var saved = working.NativeForceUnregisterCurrent
                ?? throw DeploymentCancellationMissing("refund_District_saved_fields");
            var fields = saved.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray()
                ?? throw DeploymentCancellationMissing("refund_District_saved_window");
            fields[4] = (byte)after;
            working = working with
            {
                Districts = new(working.Districts) { [districtId] = domain with { OrdersRemaining = after } },
                NativeForceUnregisterCurrent = saved with
                { DistrictWindows10 = new(saved.DistrictWindows10) { [districtId] = fields } }
            };
        }

        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        working = OwnedArmyLifecycleTargets.PublishCurrent(working, current);
        working = BaseCaptureCurrentInputs.PublishCurrent(working, current);
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
            { Indexes = pool.ExportOwnedState() });
    }

    private GameState RefreshCancelledDeploymentFoodWarningCurrent(GameState state,
        SourceWorldContext current, BaseReference city, FacilityOperation operation)
    {
        //2FE2B0 changes the persistent warning bit and its existing map
        //tooltip consumer. It neither spends food nor posts an income report.
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        var working = state;
        _ = CityCaptureTerritorySource.ApplyCityFoodWarning(working, current, city,
            () =>
            {
                var quote = ReadCityCaptureFoodQuoteCurrent(working, current, city);
                int result = quote.Value ?? throw DeploymentCancellationMissing(quote.UnresolvedOwner ?? "food_expense");
                working = quote.State ?? working;
                return result;
            },
            () => CityCaptureFoodWarningSource.TryReadPredictedFoodIncome(working, current, city,
                operation.ReadDate(), CreateArmyDestructionAbilities(current), FoodIncomeSceneries).Value
                ?? throw DeploymentCancellationMissing("predicted_food_income"));
        return BaseCaptureCurrentInputs.PublishCurrent(working, current);
    }

    private static T DeploymentCancellationKnown<T>(T? value, string field) where T : struct =>
        value ?? throw DeploymentCancellationMissing(field);
}
