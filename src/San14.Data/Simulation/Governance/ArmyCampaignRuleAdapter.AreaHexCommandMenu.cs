using San14.Core;

namespace San14.Data;

public enum AreaHexCommandMenuCommand { AreaAdministration, UnitTarget, Strategic, AreaInformation }

public sealed record AreaHexCommandMenuEntry(AreaHexCommandMenuCommand Command, int Payload,
    int CaptionId, AreaAdministrationQuery<bool> SourcePresent, AreaAdministrationQuery<bool> SourceEnabled);

/// <summary>726440's actual Area and its independently admitted tree entries.
/// Null routing fields have not been read; they never stand for City/District0.</summary>
public sealed record AreaHexCommandMenuProjection(int AreaId, int? CityId, int? PlayerDistrictId,
    IReadOnlyList<AreaHexCommandMenuEntry> Entries);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read the ordinary Area-Hex menu from the host's owned World or
    /// held checkpoint. Input, camera and map-hit admission belong to the host.</summary>
    public AreaAdministrationQuery<AreaHexCommandMenuProjection> QueryAreaHexCommandMenu(
        GameState state, int areaId, bool? allowCommands)
    {
        if (areaId is < 0 or > 500 || !state.Areas.ContainsKey(areaId))
            return AreaAdministrationQuery<AreaHexCommandMenuProjection>.Unknown("area-hex-menu.Area_identity");
        var absent = AreaAdministrationQuery<bool>.Known(false);
        AreaHexCommandMenuProjection Restricted(AreaAdministrationQuery<bool> commands) => new(areaId, null, null,
            Array.AsReadOnly(new[]
            {
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.AreaAdministration, 0, 0x4B7, commands, commands),
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.UnitTarget, 1, 0x29A, commands, commands),
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.Strategic, 2, 0xA, commands, commands),
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.AreaInformation, 3, 0x5E,
                    AreaAdministrationQuery<bool>.Known(true), AreaAdministrationQuery<bool>.Known(true))
            }));
        if (allowCommands == false)
            return AreaAdministrationQuery<AreaHexCommandMenuProjection>.Known(Restricted(absent));
        try
        {
            if (allowCommands is null) throw new InvalidDataException("area-hex-menu.allow_commands");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            uint flags = current.ReadWorldTransferServiceFlags()
                ?? throw new InvalidDataException("area-hex-menu.World16A8");
            if ((flags & 0x100) != 0)
                return AreaAdministrationQuery<AreaHexCommandMenuProjection>.Known(Restricted(absent));
            return AreaAdministrationQuery<AreaHexCommandMenuProjection>.Known(
                new EnemyFootholdMenuRead(this, state, current, areaId).ReadAreaHex());
        }
        catch (InvalidDataException missing)
        {
            return AreaAdministrationQuery<AreaHexCommandMenuProjection>.Known(
                Restricted(AreaAdministrationQuery<bool>.Unknown(missing.Message)));
        }
    }

    private sealed partial class EnemyFootholdMenuRead
    {
        private DistrictReference? areaPrimary;

        internal EnemyFootholdMenuRead(ArmyCampaignRuleAdapter adapter, GameState state,
            SourceWorldContext current, int areaId)
        {
            this.adapter = adapter; this.state = state; this.current = current;
            area = areaId; kind = BaseKind.City; camera = null; actor = null;
            var at = current.ReadAreaTableReference(areaId) ?? throw Missing("Area_pointer");
            if (!K(current.ReadAreaNativeValidity(at), "Area_validity")) throw Missing("Area_invalid");
            int rawCity = K(current.ReadAreaCitySlot(at), "Area35");
            foothold = City(rawCity);
            baseId = K(foothold.PhysicalSlot, "Area_City_pointer");
            player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("Area_player_pointer");
            own = K(current.ReadForcePhysicalSlot(player), "Area_player_identity");
        }

        internal AreaHexCommandMenuProjection ReadAreaHex()
        {
            var at = current.ReadAreaTableReference(area) ?? throw Missing("Area_pointer");
            var domesticPresent = Evaluate(() =>
                K(current.ReadAreaCurrentForceSlot(at), "Area_current_Force") == own &&
                K(current.ReadCityDistrictReference(foothold)?.PhysicalSlot, "Area_City_District_pointer") ==
                K(AreaPrimary().PhysicalSlot, "Area_primary_District_pointer"));
            var targetPresent = Evaluate(() => K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, own,
                K(current.ReadAreaCurrentForceSlot(at), "Area_current_Force")), "Area_target_relation2CA650"));
            var strategicPresent = Evaluate(() => AreaAdmission(area, true));
            var entries = new[]
            {
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.AreaAdministration, 0, 0x4B7,
                    domesticPresent, AreaEnabled(domesticPresent, AreaDomesticAvailable)),
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.UnitTarget, 1, 0x29A,
                    targetPresent, AreaEnabled(targetPresent, () => SceneryTargetCommandAvailable(state, current))),
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.Strategic, 2, 0xA,
                    strategicPresent, AreaEnabled(strategicPresent, () =>
                    {
                        _ = AreaPrimary();
                        return StrategicAvailable();
                    })),
                new AreaHexCommandMenuEntry(AreaHexCommandMenuCommand.AreaInformation, 3, 0x5E, Known(true), Known(true))
            };
            return new(area, baseId, areaPrimary?.PhysicalSlot, Array.AsReadOnly(entries));
        }

        private DistrictReference AreaPrimary() => areaPrimary ??= ForcePrimaryDistrictSource.Resolve(current, player);

        private bool AreaDomesticAvailable()
        {
            //6D8600 is the existing complete ordered collector, not an editor.
            //The source then reads the actual primary District's byte14 even
            //when the resulting Person list is empty.
            int count = AreaAdministrationExecution.OrderedCandidates(current, baseId).Count;
            int district = K(AreaPrimary().PhysicalSlot, "Area_primary_District_identity");
            int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining
                ?? throw Missing("Area_primary_District14");
            int cost = K(PersonAssignmentPrecallInputs.ReadAreaAppointmentCost(state), "Area_command_cost");
            return books >= cost && count != 0;
        }

        private static AreaAdministrationQuery<bool> AreaEnabled(AreaAdministrationQuery<bool> present, Func<bool> read)
            => !present.Available ? present : present.Value ? Evaluate(read) : Known(false);
    }
}
