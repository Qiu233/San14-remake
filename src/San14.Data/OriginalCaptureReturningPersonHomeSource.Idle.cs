using San14.Core;

namespace San14.Data;

public sealed partial class OriginalCaptureReturningPersonHomeSource
{
    //232370 after matching E2/E4. Only its reached associated virtual98
    //matters; Return's actual1EDB40 returns15, so it is not busy16.
    private static void RequireIdle232370(OriginalEventInitializationContext current, int person)
    {
        var associated = current.ReadPersonLinkedEntity(person) ?? throw Missing("current.idle232370_associated22AC00");
        if (associated.Identity is not int receiver) return;
        if (!Need(current.ReadLinkedEntityNativeValidity(associated), "current.idle232370_associated_validity")) return;
        if (current.ReadOwnedPersonCommandManager()?.Commands.GetValueOrDefault(receiver)?.Kind != "return")
            throw Missing("current.idle232370_associated_virtual98");
        //Known Return v98=15 continues busy0. Other receiver kinds need
        //their actual virtual, rather than an assumed busy0 result.
    }

    private static void RequireIdleNoCommanderArmy(OriginalEventInitializationContext current)
    {
        var army = current.ReadArmyUnitTableReference(0) ?? throw Missing("current.idle22A200_Army0_pointer");
        _ = Need(current.ReadArmyUnitCommanderId(army), "current.idle22A200_Army0_commander12");
        if (Need(current.ReadArmyUnitNativeValidity(army), "current.idle22A200_Army0_validity"))
            throw Missing("branch.idle22A200_Army0_valid_prisoner_home_writer");
    }

    private static OriginalPersonReturnCommandResult ApplyIdleReturn(GameState state,
        OriginalEventInitializationContext current, int person, NativeMapSeasonDate? operationThirdDate)
    {
        var force = current.ReadPersonForce(person) ?? throw Missing("current.idle2EA9A0_Person_Force");
        if (!Need(current.ReadForceNativeValidity(force), "current.idle2EA9A0_Force_validity"))
            throw Missing("branch.idle2EA9A0_265250_invalid_Force");
        var home = OriginalBaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "current.idle2EA9A0_E2"), current);
        if (!Need(current.ReadBaseNativeValidity(home), "current.idle2EA9A0_Home_validity") ||
            Need(current.ReadBaseForceSlot(home), "current.idle2EA9A0_Home_Force") != force.PhysicalSlot)
            throw Missing("branch.idle2EA9A0_265250_changed_Home");

        var associated = current.ReadPersonLinkedEntity(person) ?? throw Missing("current.idle2EA9A0_associated22AC00");
        bool liveCommand = associated.Identity.HasValue &&
            Need(current.ReadLinkedEntityNativeValidity(associated), "current.idle2EA9A0_associated_validity");
        int start;
        if (liveCommand)
            start = Need(OriginalPersonReturnCommands.ReadPersonCurrentHex(state, current, person,
                operationThirdDate ?? state.NativeMapSeason?.ThirdDate ?? throw Missing("current.idle2EA9A0_World_third_date")).PhysicalSlot,
                "current.idle2EA9A0_start_Hex");
        else
            //22D6C0→22DEF0: an ordinary idle resident still has its old
            //physical Base in E4 even though260B44 already changed E2.
            start = current.ReadBaseCurrentHexIndex(OriginalBaseLookup.Find(
                Need(current.ReadPersonSecondLocation(person), "current.idle2EA9A0_E4"), current));
        if (start == current.ReadBaseCurrentHexIndex(home))
        {
            current.WritePersonSecondLocation(person,
                Math.Min(Need(current.ReadBaseObjectLink(home), "current.idle2EA9A0_Home4E"), (ushort)1061));
            return new(state, 0, null, null, null, null);
        }
        var date = operationThirdDate ?? state.NativeMapSeason?.ThirdDate ?? throw Missing("current.idle2EA9A0_World_third_date");
        var prepared = OriginalPersonReturnCommands.Prepare(state, current, person, date);
        var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "current.idle2EA9A0_Return_prepare");
        return OriginalPersonReturnCommands.Apply(state, current, packet);
    }

    private static GameState PublishIdleReturnFields(GameState state, OriginalEventInitializationContext current,
        int officerId, int person)
    {
        var officer = state.Officers[officerId];
        var ability = officer.NativeAbilityRuntime ?? throw Missing("current.idle_Home_Person_ability_owner");
        var location = officer.NativeLocationRuntime ?? throw Missing("current.idle_Home_Person_location_owner");
        return state with { Officers = new(state.Officers) { [officerId] = officer with
        {
            NativeAbilityRuntime = ability with { Flags196 = Need(current.ReadArmyReturnPersonFlags196(person), "current.idle_Home_Person15E") },
            NativeLocationRuntime = location with { Second11C = Need(current.ReadPersonSecondLocation(person), "current.idle_Home_PersonE4") }
        } } };
    }
}
