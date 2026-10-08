using San14.Core;

namespace San14.Data;

/// <summary>29F220's type2/bit20 call to2EA9A0(Person,0,0), before Army
/// pending/26. Ordinary status4 Commander, friendly City Home, no preexisting
/// associated command. It writes only the enclosing private current world;
/// returned command State must be adopted before merit and final publication.</summary>
public static class OriginalTransportPersonReturnSource
{
    public static OriginalPersonReturnCommandResult TryApplyCurrent(GameState state,
        OriginalEventInitializationContext current, int personPhysical,
        NativeMapSeasonDate? operationThirdDate = null)
    {
        try
        {
            ushort id = Need(current.ReadPersonIdentity(personPhysical), "Person10");
            if (!Need(current.ReadPersonNativeValidity(personPhysical), "Person_validity") ||
                current.ReadPersonStatus(personPhysical) != 4)
                return Missing("branch.transport_return_ordinary_status4");
            var force = current.ReadPersonForce(personPhysical) ?? throw Unknown("Person_Force");
            if (force.PhysicalSlot is not (>= 1 and <= 45) ||
                !Need(current.ReadForceNativeValidity(force), "Person_Force_validity"))
                return Missing("branch.transport_return_265250_lost_Force");
            var home = OriginalBaseLookup.Find(Need(current.ReadPersonFirstLocation(personPhysical), "PersonE2"), current);
            if (home.Kind != OriginalBaseKind.City || home.PhysicalSlot is not (>= 1 and <= 51) ||
                !Need(current.ReadBaseNativeValidity(home), "Home_validity") ||
                Need(current.ReadBaseForceSlot(home), "Home_Force") != force.PhysicalSlot)
                return Missing("branch.transport_return_265250_changed_Home");

            var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Unknown("associated22AC00");
            if (linked.Identity.HasValue && Need(current.ReadLinkedEntityNativeValidity(linked), "associated_validity"))
                return Missing("branch.transport_return_existing_associated_receiver");
            ushort second = Need(current.ReadPersonSecondLocation(personPhysical), "PersonE4");
            if (second is < 62 or > 561) return Missing("branch.transport_return_not_current_CommanderArmy");
            var army = current.ReadArmyUnitTableReference(second - 61) ?? throw Unknown("Army_pointer");
            if (!Need(current.ReadArmyUnitNativeValidity(army), "Army_validity") ||
                Need(current.ReadArmyUnitCommanderId(army), "Army12") != id ||
                Need(current.ReadArmyReturnUnitStatus10(army), "Army10") != 2)
                return Missing("branch.transport_return_not_current_type2_Commander");
            int currentHex = Need(current.ReadArmyUnitRawHex2A(army), "Army2A");
            if (currentHex == current.ReadBaseCurrentHexIndex(home))
            {
                //True2EA9A0 NativeReturn0: onlyE4. No271320, service,
                //Governor update, command allocation or current date query.
                current.WritePersonSecondLocation(personPhysical,
                    Math.Min(Need(current.ReadBaseObjectLink(home), "Home4E"), (ushort)1061));
                return new(state, 0, null, null, null, null);
            }
            var date = operationThirdDate ?? state.NativeMapSeason?.ThirdDate ?? throw Unknown("World34_ThirdDate");
            var prepared = OriginalPersonReturnCommands.Prepare(state, current, personPhysical, date);
            if (prepared.Packet is not { } packet) return Missing(prepared.UnresolvedOwner ?? "current.transport_return_Prepare");
            return OriginalPersonReturnCommands.Apply(state, current, packet);
        }
        catch (InvalidDataException error) { return Missing(error.Message); }
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Unknown(field);
    private static InvalidDataException Unknown(string field) => new("current.transport_return_" + field);
    private static OriginalPersonReturnCommandResult Missing(string field) => new(null, null, null, null, null, field);
}
