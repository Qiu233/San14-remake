using San14.Core;

namespace San14.Data;

/// <summary>Read-only quotes over the same current owners used at execution.
/// Offices use the native batch writer instead of the item quote protocol.</summary>
internal sealed partial class OwnedPersonnelCommandRules(ArmyCampaignRuleAdapter army) : IPersonnelItemTransferCommandRules,
    IOfficerOfficeCommandRules, ICommanderSelectionCommandRules, IPersonnelAdministrationCommandRules,
    IPersonnelDevelopmentCommandRules, ICommanderAutomaticTacticsCommandRules
{
    public CommandResult ApplyHire(GameState state, ForceState force, int districtId, int destinationCityId,
        IReadOnlyList<int> ids, CommandContext context) => army.ApplyPersonnelHire(state, force, districtId, destinationCityId, ids, context);
    public CommandResult ApplyLearn(GameState state, ForceState force, int officerId, int gemId,
        int? replacedTacticId, CommandContext context) => army.ApplyPersonnelLearn(state, force, officerId, gemId, replacedTacticId, context);
    public CommandResult ApplyMediation(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> ids, CommandContext context) => army.ApplyPersonnelMediation(state, force, districtId, ids, context);
    public CommandResult ApplyReward(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> ids, CommandContext context) => army.ApplyPersonnelReward(state, force, districtId, ids, context);
    public CommandResult ApplyCreditGive(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> ids, CommandContext context) => army.ApplyPersonnelCreditGive(state, force, districtId, ids, context);
    public CommandResult ApplyCreditRankup(GameState state, ForceState force, int districtId,
        IReadOnlyList<OfficerCreditChoice> choices, CommandContext context) => army.ApplyPersonnelCreditRankup(state, force, districtId, choices, context);
    public CommandResult ApplyBanish(GameState state, ForceState force,
        IReadOnlyList<int> ids, CommandContext context) => army.ApplyPersonnelBanish(state, force, ids, context);

    public CommandResult ToggleCommanderAutomaticTactics(GameState state, ForceState force, int officerId, CommandContext context)
        => army.ToggleCommanderAutomaticTactics(state, force, officerId, context);

    public CommandResult ApplyCommanderSelection(GameState state, ForceState force,
        IReadOnlyList<int> officerIds, CommandContext context)
        => army.ApplyCommanderSelection(state, force, officerIds, context);

    public CommandResult ApplyOffices(GameState state, ForceState force,
        IReadOnlyList<OfficerOfficeAssignment> assignments, CommandContext context)
        => army.ApplyOfficerOffices(state, force, assignments, context);

    public PersonnelEvaluation Evaluate(GameState state, ForceState force, PersonnelChange change, CommandContext actor)
    {
        if (change.Kind == PersonnelActionKind.SetOffice)
            return UnresolvedPersonnelRules.Instance.Evaluate(state, force, change, actor);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || actor.DelegatedDistrictId is not null)
            return new(false, Reason: "Personnel administration requires direct strategy control.");
        if (!state.Officers.TryGetValue(change.OfficerId, out var person) || person.ForceId != force.Id)
            return new(false, Reason: "The target does not belong to this force.");
        if (change.ItemId is not int itemId || state.Items?.GetValueOrDefault(itemId) is not { } item ||
            item.Holder?.OfficerId is not int holder || !state.Officers.TryGetValue(holder, out var owner) ||
            owner.ForceId != force.Id || item.IsPresent != true)
            return new(false, Reason: "The item is not held by this force.");
        if (force.RulerId is not int ruler ||
            (change.Kind == PersonnelActionKind.GrantItem ? holder == person.Id : holder != person.Id))
            return new(false, Reason: "The item's current holder does not match the command.");
        if (change.PreviousHolderId != holder || change.NextHolderId !=
            (change.Kind == PersonnelActionKind.GrantItem ? person.Id : ruler))
            return new(false, Reason: "The item's holder changed since selection.");
        if (item.Lifecycle?.RetainedFlags is not byte flags)
            return new(null, MissingRuleKey: "personnel.item_flags", Reason: "Current item flags are unresolved.");
        return EvaluateItemTransfer(state, force, change, actor, flags);
    }

    private sealed class CurrentItems(ArmyCampaignRuleAdapter army, SourceWorldContext? suppliedCurrent = null) : IPersonnelCommandContext
    {
        public PersonnelCommandContext? Resolve(GameState state, ForceState force, PersonnelChange change)
        {
            try
            {
                var current = suppliedCurrent ?? army.CaptureOwnedArmyReturnWorld(state).Context;
                int person = current.ResolveOfficerSource(change.OfficerId) ?? throw Missing();
                bool isRuler = Need(current.ReadPersonStatus(person)) == 1;
                bool? hired = OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)) is bool ordinary ? !ordinary : null;
                bool? maximum = OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person));
                if (isRuler)
                    return Inputs(new(true, null, null, null, maximum));
                ushort rulerId = current.ReadForceRecord(force.Id)?.RawRuler10 ?? throw Missing();
                int ruler = current.ReadPersonLookupReference(rulerId)?.PhysicalSlot ?? throw Missing();
                bool related = Related(person, ruler);
                bool? d0 = related ? InList(person, ruler, 0xD0) : null;
                bool? c0 = related && d0 == false ? null : InList(person, ruler, 0xC0);
                return Inputs(new(false, related, d0, c0, maximum));

                PersonnelCommandContext Inputs(PersonnelLoyaltyGuards guards) => new(
                    PersonnelCommandParameters.Read(state, PersonnelCommandParameters.ItemCommandBooks),
                    PersonnelCommandParameters.Read(state, PersonnelCommandParameters.ConfiscationPenalty),
                    hired, guards, PersonnelCommandParameters.Revision)
                {
                    ChargedDistrictId = ForcePrimaryDistrictSource.Resolve(current, new(force.Id)).PhysicalSlot
                        ?? throw Missing()
                };
                ushort Id(int p) => Need(current.ReadPersonIdentity(p));
                bool Valid(int p) => Need(current.ReadPersonNativeValidity(p));
                ushort Word(int p, int field) => state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
                    values.GetValueOrDefault(OwnedPersonRelationInputs.VersionKey) == 1 &&
                    values.TryGetValue(OwnedPersonRelationInputs.PersonFieldKey(p, field), out int raw) && raw is >= 0 and <= 65535
                        ? (ushort)raw : throw Missing();
                bool InList(int a, int b, int field)
                {
                    if (!Valid(b)) return false;
                    ushort id = Id(b);
                    for (int i = 0; i < 8; i++) if (Word(a, field + i * 2) == id) return true;
                    return false;
                }
                bool Related(int a, int b) => PersonKinshipCurrent.Read(state, current, a, b);
            }
            catch (InvalidDataException) { return null; }
        }
        private static T Need<T>(T? value) where T : struct => value ?? throw Missing();
        private static InvalidDataException Missing() => new("Current personnel relation/identity owner is unresolved.");
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public IPersonnelCommandRules CreatePersonnelCommandRules() => new OwnedPersonnelCommandRules(this);
}
