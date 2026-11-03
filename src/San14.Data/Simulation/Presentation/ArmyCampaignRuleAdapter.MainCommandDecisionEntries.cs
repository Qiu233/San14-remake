using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //5F7300: application restriction precedes tutorial, which precedes the
    //actual edited-event manager publication. Missing later owners do not
    //invalidate a known earlier rejection; an unknown gate cannot be skipped.
    private MapMainCommandEntryPresentation ReadCreatedEventCommandEntry(GameState state,
        bool? restricted, bool? tutorial)
    {
        const MapMenuCommand command = MapMenuCommand.CreatedEvents;
        if (restricted is null) return new(command, null, null, "map.main_command.edited_event_selection");
        if (restricted.Value) return new(command, false, 0x11171);
        if (tutorial is null) return new(command, null, null, "map.main_command.edited_event_tutorial");
        if (tutorial.Value) return new(command, false, 0x3152);
        bool? ready = mainCommandApplicationFacts is { } app && ReferenceEquals(app.World, state)
            ? app.EditedEventsReady : null;
        return ready is bool known ? new(command, known, known ? 0x3152 : 0x3153)
            : new(command, null, null, "map.main_command.edited_event_manager_ready");
    }

    //892170 consumes70EC60's pair, not the normal-turn notification caller's
    //additional second country-list/technology test. Borrow the same current
    //source as the header and never construct a page or recapture the World.
    private MapMainCommandEntryPresentation ReadForeignTradeCommandEntry(GameState state,
        SourceWorldContext current, bool? restricted, Action bindCurrent)
    {
        if (restricted is null) return new(MapMenuCommand.Trade, null, null, "map.main_command.trade_selection");
        if (restricted.Value) return new(MapMenuCommand.Trade, false, 0x11171);
        try
        {
            bindCurrent();
            var force = current.CurrentPlayerForce ?? throw MainCommandMissing("trade_player");
            var district = ForcePrimaryDistrictSource.Resolve(current, force);
            int districtId = MainNeed(district.PhysicalSlot, "trade_primary_district");
            var admission = ReadForeignTradeAdmissionCurrent(state, current, force, district, districtId,
                readNotification: false);
            return new(MapMenuCommand.Trade, admission.Allowed, admission.GameMessageId);
        }
        catch (InvalidDataException error)
        { return new(MapMenuCommand.Trade, null, null, error.Message); }
    }
}
