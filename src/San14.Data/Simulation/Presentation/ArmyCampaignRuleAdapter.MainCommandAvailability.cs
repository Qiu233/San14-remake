using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The completed entries share the header's predicate/help
    /// publication. Trade alone retains its additional notification gate.</summary>
    private bool ReadMainCommandAvailabilityCurrent(GameState state, SourceWorldContext current, MapMenuCommand command)
    {
        var force = current.CurrentPlayerForce ?? throw MainCommandMissing("availability_player");
        var district = ForcePrimaryDistrictSource.Resolve(current, force);
        int districtId = MainNeed(district.PhysicalSlot, "availability_primary_district");
        if (!MainNeed(current.ReadDistrictNativeValidity(district), "availability_district_validity")) return false;
        //70EC60's menu pair is already published separately. This caller's
        //second actual country list and technology-progress test is a notice
        //rule, so it must not be replaced by the menu's broader admission.
        if (command != MapMenuCommand.Trade) throw MainCommandMissing("availability_command_" + command);
        return NormalTurnForeignTradeAvailable(state, current, force, district, districtId);
    }

    private static T MainNeed<T>(T? value, string field) where T : struct
        => value ?? throw MainCommandMissing(field);
}
