using San14.Core;

namespace San14.Client.Gameplay;

public sealed record GovernanceSlotChoice(int Id, GovernanceSlotKind Kind);
public sealed record GovernanceOfficerChoice(int Id, string Name, string? PolicyName);

/// <summary>Create snapshots independent display values for the selected governance position.</summary>
public sealed record GovernanceCommandChoices(IReadOnlyList<GovernanceSlotChoice> Slots,
    int? SelectedSlotId, IReadOnlyList<GovernanceOfficerChoice> Officers,
    int? SelectedOfficerId, int ConfiguredCount, int ActiveCount, bool HasPendingChange,
    bool CanConfigure)
{
    public static GovernanceCommandChoices? Create(GameState world, int player, int? previousSlot)
    {
        if (!world.Forces.TryGetValue(player, out ForceState? force) ||
            force.Governance is not GovernanceState governance) return null;
        GovernanceSlot[] slots = governance.OpenSlots.OrderBy(slot => slot.Id).ToArray();
        int? selectedId = previousSlot is int prior && slots.Any(slot => slot.Id == prior)
            ? prior : slots.FirstOrDefault()?.Id;
        GovernanceOfficerChoice[] officers = [];
        int? selectedOfficer = null;
        if (selectedId is int selected)
        {
            GovernanceSlot slot = slots.Single(item => item.Id == selected);
            int? assigned = governance.Configured.Assignments.FirstOrDefault(row => row.SlotId == selected)?.OfficerId;
            officers = world.Officers.Values.Where(officer =>
                    !PlayableOfficerAvailability.IsSourceBusy(world, officer) &&
                    !PlayableDiplomacy.IsOfficerOnMission(world, officer.Id) && (officer.Id == assigned ||
                    (officer.ForceId == player && officer.Presence == OfficerPresence.Present &&
                    !officer.Deployed && world.PreparedOfficerMoves?.ContainsKey(officer.Id) != true &&
                    world.PreparedTalentOrders?.ContainsKey(officer.Id) != true &&
                    !world.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id) &&
                    officer.PolicyId is int policyId && policyId > 0 &&
                    world.Policies.TryGetValue(policyId, out PolicyDefinition? policy) &&
                    policy.DepartmentCode is int code &&
                    (slot.Kind == GovernanceSlotKind.Advisor
                        ? officer.Id != force.RulerId && officer.BaseAbilities?.Intelligence >= 70
                        : slot.DepartmentKey == $"group-{code}"))))
                .OrderBy(officer => officer.Id).Select(officer => new GovernanceOfficerChoice(officer.Id,
                    officer.Name, officer.PolicyId is int id && world.Policies.TryGetValue(id,
                        out PolicyDefinition? definition) ? definition.Name : null)).ToArray();
            selectedOfficer = officers.Any(officer => officer.Id == assigned) ? assigned : null;
        }
        return new(Array.AsReadOnly(slots.Select(slot => new GovernanceSlotChoice(slot.Id, slot.Kind)).ToArray()),
            selectedId, Array.AsReadOnly(officers), selectedOfficer,
            governance.Configured.Assignments.Length, governance.Active.Assignments.Length,
            governance.Pending is not null, world.Phase == TurnPhase.Strategy &&
                PlayableOutcome.Evaluate(world, player) == PlayableResult.Ongoing);
    }
}
