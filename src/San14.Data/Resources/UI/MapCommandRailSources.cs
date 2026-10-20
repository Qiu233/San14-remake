using San14.Data.Formats;

namespace San14.Data;

public enum MapCommandCategory { Campaign, Appointment, Personnel, Strategy, Diplomacy, Information, Decisions }
public enum MapMenuCommand
{
    Deploy, WithdrawAll, Policy, Office, Research, CreateDistrict, EditDistrict, MoveGroup, EditGroup,
    Search, Recruit, Employ, Items, Reward, Merit, Award, Expel, Learn, Introduce,
    Poison, Alienate, PlacateArea, Incite, ProvokeWar, Sabotage, Rumor,
    Goodwill, Alliance, CancelAlliance, ReturnCaptives, RequestAttack, DemandSurrender, Exchange,
    Spy, Scout, Patrol, Proposals, Events, CreatedEvents, Trade
}
public sealed record MapMenuEntry(MapMenuCommand Command, string Caption);

/// <summary>Audited Chinese main command layouts and their authored sprite references.</summary>
public sealed class MapCommandRailSources
{
    public MapMarkerLayout Main { get; }
    public MapMarkerLayout Button { get; }
    public MapMarkerLayout Fold { get; }
    public MapMarkerLayout Submenu { get; }
    public MapMarkerLayout SubmenuButton { get; }

    public MapCommandRailSources(GameResourceRepository repository)
    {
        Main = MapMarkerLayout.Load(repository, "00_02_00_mein_offset_20_cn", 0x437465FF, "LCSK9500");
        Button = MapMarkerLayout.Load(repository, "00_02_00_mein_offset_21", 0x4FA29FAA, "LCSK8500");
        // This child has the same audited node/part structure as the existing
        // 6500 map children; the general layout parser remains unchanged.
        Fold = MapMarkerLayout.Load(repository, "00_02_00_mein_offset_22", 0xCD7A5BAB, "LCSK6500");
        Submenu = MapMarkerLayout.Load(repository, "00_02_00_mein_offset_30_cn", 0xCBA4A5DE, "LCSK9500");
        SubmenuButton = MapMarkerLayout.Load(repository, "00_02_00_mein_offset_31", 0xD7D2DF89, "LCSK8500");
    }

    public static IReadOnlyList<MapMenuEntry>[] ReadMenus(GameResourceRepository repository, string locale)
    {
        // Original menu writers select Data section 1. Message indices belong
        // here at the resource boundary, not in command execution.
        (MapMenuCommand, int)[][] rows = [
            [(MapMenuCommand.Deploy,0x34),(MapMenuCommand.WithdrawAll,0x35)],
            [(MapMenuCommand.Policy,0x15),(MapMenuCommand.Office,0x19),(MapMenuCommand.Research,0xA3),
             (MapMenuCommand.CreateDistrict,0x16),(MapMenuCommand.EditDistrict,0x17),(MapMenuCommand.MoveGroup,0x73B),(MapMenuCommand.EditGroup,0x73A)],
            [(MapMenuCommand.Search,0x11),(MapMenuCommand.Recruit,0x12),(MapMenuCommand.Employ,0x859),
             (MapMenuCommand.Items,0x13),(MapMenuCommand.Reward,0x23),(MapMenuCommand.Merit,0x9E),
             (MapMenuCommand.Award,0x9F),(MapMenuCommand.Expel,0x14),(MapMenuCommand.Learn,0x1A),(MapMenuCommand.Introduce,0x7E5)],
            [(MapMenuCommand.Poison,9),(MapMenuCommand.Alienate,8),(MapMenuCommand.PlacateArea,10),
             (MapMenuCommand.Incite,7),(MapMenuCommand.ProvokeWar,6),(MapMenuCommand.Sabotage,0x6F0),(MapMenuCommand.Rumor,0xC5)],
            [(MapMenuCommand.Goodwill,0xB),(MapMenuCommand.Alliance,0xC),(MapMenuCommand.CancelAlliance,0xD),
             (MapMenuCommand.ReturnCaptives,0xE),(MapMenuCommand.RequestAttack,0x10),(MapMenuCommand.DemandSurrender,0xF),(MapMenuCommand.Exchange,0x92)],
            [(MapMenuCommand.Spy,0x9C),(MapMenuCommand.Scout,0x9D),(MapMenuCommand.Patrol,0xC6)],
            [(MapMenuCommand.Proposals,0x1B),(MapMenuCommand.Events,0x1C),(MapMenuCommand.CreatedEvents,0x2EB),(MapMenuCommand.Trade,0x7E)]
        ];
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        return rows.Select(row => (IReadOnlyList<MapMenuEntry>)row.Select(entry =>
            new MapMenuEntry(entry.Item1, messages[entry.Item2].Text ??
                throw new InvalidDataException($"Missing main menu message {entry.Item2} in {locale}."))).ToArray()).ToArray();
    }

    public static string Sprite(MapMarkerLayout layout, int index)
    {
        LcskSpriteNode source = LcskSpriteNodeReader.Read(layout.Layout, layout.Nodes[index]);
        var parts = source.Groups.SelectMany(group => group.Parts).ToArray();
        if (source.NodeType != 1 || parts.Length != 1)
            throw new InvalidDataException($"Expected one main command sprite: {layout.Name}/{index}.");
        return parts[0].SpriteName;
    }
}
