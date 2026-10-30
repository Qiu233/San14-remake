using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public CommandResult ToggleCommanderAutomaticTactics(GameState state, ForceState force, int officerId, CommandContext context)
    {
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Commander tactics require direct force control.", "commander-automatic.scope");
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var selection = ReadCommanderSelectionCurrent(state, current, force.Id);
            if (!selection.Storage.Take(selection.Count).Any(row => row.OfficerId == officerId))
                return CommandResult.Reject("The officer is not a current commander.", "commander-automatic.selection");
            int physical = current.ResolveOfficerSource(officerId) ?? throw CommanderHudMissing("automatic_Person");
            if (!CommanderHudNeed(current.ReadPersonNativeValidity(physical), "automatic_Person_validity") ||
                current.ReadPersonIdentity(physical) != officerId || !state.Officers.TryGetValue(officerId, out var officer))
                return CommandResult.Reject("The commander is no longer valid.", "commander-automatic.person");
            //892C60 changes only bit20, including a selected Person who has no Army.
            ushort flags = (ushort)(CommanderHudNeed(current.ReadArmyReturnPersonFlags196(physical), "automatic_Person_flags") ^ 0x20);
            return CommandResult.Accept(state with { Officers = new(state.Officers) { [officerId] = officer with
                { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(officer.Behavior, flags) } } });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.commander_automatic", error.Message); }
    }
}
