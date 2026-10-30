using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IGeneralUnitTargetExecution
{
    /// <summary>7ED5B0 mode1's ordered1EC980(packet,1) loop. The outer
    /// command publishes only the final accepted immutable candidate; a late
    /// failure cannot expose previously created packets to the live session.</summary>
    CommandResult IGeneralUnitTargetExecution.Deploy(GameState state,ForceState force,
        DeployArmiesOrder order,CommandContext context)
    {
        try
        {
            var scope = CaptureDeploymentPersonSelectionCurrent(state);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state,scope) ?? throw GeneralTargetMissing("commit_player_Force");
            if (player.PhysicalSlot != force.Id || context.DelegatedDistrictId is null &&
                ForcePrimaryDistrictSource.Resolve(scope,player).PhysicalSlot != order.DistrictId)
                return CommandResult.Reject("The selected district is outside current strategy control.","deployment.district_scope");
            var working = state;
            foreach (var packet in order.Armies)
            {
                var current = CaptureDeploymentPersonSelectionCurrent(working);
                var home = BaseLookup.Find(packet.SourceLocationId,current);
                if (!GeneralTargetNeed(current.ReadBaseNativeValidity(home),"commit_Foothold_validity"))
                    return CommandResult.Reject("The selected deployment source is no longer valid.","deployment.source_missing");
                int owner = GeneralTargetNeed(current.ReadBaseForceSlot(home),"commit_Foothold_Force");
                int district = GeneralTargetNeed(current.ReadBaseDistrictSlot(home),"commit_Foothold_District");
                if (owner != force.Id || district != order.DistrictId)
                    return CommandResult.Reject("The deployment source is outside the selected district.","deployment.source_control");
                int troops = GeneralTargetNeed(current.ReadBaseResource(home,BaseResourceKind.Troops),"commit_Foothold_troops");
                if (packet.Unit.Troops > troops)
                    return CommandResult.Reject("A deployment source lacks soldiers.","deployment.insufficient_troops");
                int books = working.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw GeneralTargetMissing("commit_District_books");
                if (books < 1)
                    return CommandResult.Reject("The deployment district lacks command books.","deployment.insufficient_orders");
                var source = new DeploymentSource(packet.SourceLocationId,owner,district,
                    current.ReadBaseCurrentHexIndex(home),troops);
                var result = CommitDeploymentUnitsCurrent(working,source,[packet.Unit],context,debitCommandBook:true);
                if (result.State is not { } candidate) return result;
                working = candidate;
            }
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("deployment.general_execution",error.Message); }
    }
}
