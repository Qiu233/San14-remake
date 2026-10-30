using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICurrentWorldEditExecution
{
    /// <summary>The editing pages use the same original single-number
    /// caption expansion as the ordinary current information pages.</summary>
    public string FormatCurrentWorldEditNumberText(int messageId, int value, int section = 1) =>
        FormatSceneryPanelNumber(ReadHistoryDataText(section, messageId), value);

    /// <summary>The actual host supplies its transient pending-operation fact.
    /// This query neither copies nor edits the world.</summary>
    public AreaAdministrationQuery<CurrentWorldEditAdmission> QueryCurrentWorldEditAdmission(
        GameState state, bool hasPendingOperation)
    {
        try
        {
            uint flags = WorldEditNeed(OwnedArmyReturnServiceInputs.ReadCurrent(state), "world_flags");
            if ((flags & 0x100) != 0) return Blocked(0x5F12);
            bool tutorial = WorldEditNeed(ReadApplicationTutorialActive(), "tutorial_controller");
            if (tutorial) return Blocked(0x5F10);
            var application = ReadApplicationGameplay() ?? throw WorldEditMissing("application_controller");
            bool special = WorldEditNeed(application.SpecialModeServicePresent, "special_mode_controller");
            if (special && application.Mode == ApplicationGameplayMode.Challenge) return Blocked(0x11171);
            if (state.Phase != TurnPhase.Strategy || hasPendingOperation ||
                state.EditedEventPlayback is not null || state.CampaignEventCommandPlayback is not null ||
                state.AutomaticEmperorCapture is not null || state.NativeNormalTurnContinuation is not null ||
                state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null)
                return Blocked(0x5F13);
            // This already-adopted exact3342B0 predicate uses the same saved
            // flags and real tutorial service. Only true requests Game5F2C.
            var confirmation = QueryManualEditedEventConfirmation(state);
            if (!confirmation.Available) throw WorldEditMissing(confirmation.UnresolvedInput ?? "confirmation_eligibility");
            return AreaAdministrationQuery<CurrentWorldEditAdmission>.Known(new(true, null, confirmation.Value));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldEditAdmission>.Unknown(error.Message); }

        static AreaAdministrationQuery<CurrentWorldEditAdmission> Blocked(int message) =>
            AreaAdministrationQuery<CurrentWorldEditAdmission>.Known(new(false, message, false));
    }

    public AreaAdministrationQuery<bool> QueryCurrentWorldEdited(GameState state)
    {
        uint? flags = OwnedArmyReturnServiceInputs.ReadCurrent(state);
        return flags.HasValue ? AreaAdministrationQuery<bool>.Known((flags.Value & 0x78) != 0)
            : AreaAdministrationQuery<bool>.Unknown("current-world-edit.world_flags");
    }

    CommandResult ICurrentWorldEditExecution.ApplyCurrentWorldEdit(GameState state, ForceState actorForce,
        CurrentWorldEditOrder order, CommandContext context)
    {
        try
        {
            // GameSimulation.Apply has admitted its no-pending-operation
            // boundary before invoking normal IOrderRules. The transient host
            // is not reconstructed from World or retained in an edit order.
            var admission = QueryCurrentWorldEditAdmission(state, false);
            if (!admission.Available) throw WorldEditMissing(admission.UnresolvedInput ?? "admission");
            if (!admission.Value!.Allowed)
                return CommandResult.Reject("Current-world editing is not admitted now.",
                    "world_edit.blocked_" + admission.Value.BlockedGameMessageId);
            return order switch
            {
                EditCurrentWorldAreaOrder area => ApplyCurrentWorldAreaEditCurrent(state, area),
                EditCurrentWorldCityOrder city => ApplyCurrentWorldCityEditCurrent(state, city),
                EditCurrentWorldForceOrder force => ApplyCurrentWorldForceEditCurrent(state, force),
                _ => ApplyCurrentWorldItemEditCurrent(state, actorForce, order, context)
            };
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.current_world_edit", error.Message); }
    }

    //33BDF0 sets the selected City/Force/Area bit; Item's source writes bit6
    //directly. The saved bits are also the actual service eligibility inputs.
    //The original transient World1650 has no independent project consumer.
    internal static GameState MarkCurrentWorldEdited(GameState state, CurrentWorldEditKind kind)
    {
        if (kind is not (CurrentWorldEditKind.City or CurrentWorldEditKind.Force or
            CurrentWorldEditKind.Area or CurrentWorldEditKind.Item)) throw WorldEditMissing("kind");
        uint before = WorldEditNeed(OwnedArmyReturnServiceInputs.ReadCurrent(state), "mark_world_flags");
        uint after = before | (1u << (int)kind);
        if (after == before) return state;
        var native = state.NativeArmySimulation ?? throw WorldEditMissing("mark_parameter_owner");
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with
        { Int32Values = new(native.Parameters.Int32Values)
            { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)after) } } } };
    }

    private static T WorldEditNeed<T>(T? value, string field) where T : struct => value ?? throw WorldEditMissing(field);
    private static InvalidDataException WorldEditMissing(string field) => new("current-world-edit." + field);
}
