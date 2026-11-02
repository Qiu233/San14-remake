using System.Globalization;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static readonly MapSpritePalettes FacilitySummaryPalettes = MapSpritePalettes.Create();
    /// <summary>The ordinary Facility summary's two real children. Borrows
    /// the summary manager's current; no full detail or scene capture.</summary>
    internal MapSummaryPage QueryFacilityMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        var root = new MapSummaryPage(4, receiver.ReceiverId, "00_82_07_sum_architect", null,
            new Dictionary<int, string>());
        try
        {
            if (receiver.Slot != 4) throw FacilitySummaryMissing("receiver_slot");
            var directory = current.CurrentMapObjectDirectory ?? throw FacilitySummaryMissing("Object_directory");
            var obj = new ObjectReference(directory.PhysicalOf(new(receiver.ReceiverId)));
            if (!FacilitySummaryNeed(current.ReadObjectNativeValidity(obj), "Object_validity"))
                return root with { ReceiverValid = false };
            root = root with { ReceiverValid = true };

            var nameText = new Dictionary<int, string>();
            var namePresets = new Dictionary<int, int>();
            var nameVisible = new Dictionary<int, bool>();
            var nameColors = new Dictionary<int, MapCornerColors>();
            var nameImages = new List<MapSummaryImage>();
            var nameMissing = new Dictionary<int, string>();
            Read(nameMissing, nameVisible, [2], () => nameText[2] = ReadFacilityArrivalObjectName(state, current, obj));
            Read(nameMissing, nameVisible, [2, 3], () =>
            {
                bool enabled = FacilitySummaryNeed(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "Object_enabled16");
                namePresets[2] = enabled ? 0 : 75;
                nameVisible[3] = !enabled;
            });
            Read(nameMissing, nameVisible, [4, 5, 6], () =>
            {
                int kind = (current.ReadPhysicalObject(FacilitySummaryNeed(obj.PhysicalSlot, "Object_physical"))
                    ?? throw FacilitySummaryMissing("Object_fields")).RawField12;
                var background = FacilitiesInfoSource.IconNames(kind, false);
                var outline = FacilitiesInfoSource.IconNames(kind, true);
                string? texturePack = kind is 15 or 17 ? "texture_pk_common" : null;
                nameImages.Add(new(6, background.Fill, texturePack));
                nameImages.Add(new(5, background.Outline, texturePack));
                nameImages.Add(new(4, outline.Outline, texturePack));
            });
            ForceReference Force()
            {
                int raw = FacilitySummaryNeed(current.ReadObjectForceSlot13(obj), "Object_Force13");
                return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw FacilitySummaryMissing("Force_pointer");
            }
            Read(nameMissing, nameVisible, [6], () =>
            {
                var force = Force();
                var color = FacilitySummaryNeed(current.ReadForceNativeValidity(force), "Force_validity")
                    ? MapSpritePalettes.ForceFill(FacilitySummaryNeed(current.ReadForceReportColor(force), "Force_color49"))
                    : MapSpritePalettes.FromAuthored(uint.MaxValue);
                nameColors[6] = Solid(color);
            });
            Read(nameMissing, nameVisible, [4, 5], () =>
            {
                var force = Force();
                var player = current.ReadCurrentPlayerForce() ?? throw FacilitySummaryMissing("Player_Force_pointer");
                int playerId = FacilitySummaryNeed(current.ReadForceNativeValidity(player), "Player_Force_validity")
                    ? FacilitySummaryNeed(player.PhysicalSlot, "Player_Force_identity") : 0;
                MapCornerColors color;
                if ((ReadReportForceRelationByte(state, force, playerId, FacilitySummaryMissing) & 0x7F) != 0)
                    color = FacilitySummaryPalettes[7];
                else
                {
                    int id = FacilitySummaryNeed(force.PhysicalSlot, "Force_identity");
                    byte flags = state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(id, out var reset) == true
                        ? FacilitySummaryNeed(reset[0x12], "Force_reset12")
                        : FacilitySummaryNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(id)?.Flags12, "Force_flags12");
                    color = (flags & 2) != 0 ? FacilitySummaryPalettes[8]
                        : Solid(MapSpritePalettes.FromAuthored(uint.MaxValue));
                }
                nameColors[4] = color; nameColors[5] = color;
            });
            var name = new MapSummaryPage(4, receiver.ReceiverId, "00_82_07_sum_buil_offset_00", true, nameText)
            {
                TextPresets = namePresets, Visibility = nameVisible, Colors = nameColors,
                Images = nameImages.AsReadOnly(), UnresolvedFields = nameMissing
            };

            var durabilityText = new Dictionary<int, string>();
            var durabilityVisible = new Dictionary<int, bool> { [3] = false };
            var durabilityMissing = new Dictionary<int, string>();
            var bars = new List<MapSummaryBar>();
            Read(durabilityMissing, durabilityVisible, [5], () => durabilityText[5] = ReadHistoryDataText(1, 0x20B));
            Read(durabilityMissing, durabilityVisible, [2, 6], () =>
            {
                int value = FacilitySummaryNeed(current.ReadObjectRawField14(obj), "Object_durability14");
                _baseCapacityInputs.BindCurrent(state, current);
                int maximum = FacilitySummaryNeed(FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs),
                    "current_maximum_durability");
                durabilityText[6] = FormatFacilitySummaryDurability(ReadHistoryDataText(1, 0x325), value, maximum);
                bars.Add(new(2, value, maximum) { InitialCurrent = 70, InitialMaximum = 100 });
            });
            var durability = new MapSummaryPage(4, receiver.ReceiverId, "00_82_05_sum_city_offset_09", true, durabilityText)
            {
                TextPresets = new Dictionary<int, int> { [5] = 13, [6] = 0 },
                Visibility = durabilityVisible,
                Images = [new(4, "000_cmn_icon_taikyu_fuchi")],
                Colors = new Dictionary<int, MapCornerColors> { [4] = FacilitySummaryPalettes[21] },
                Bars = bars.AsReadOnly(), UnresolvedFields = durabilityMissing
            };
            return root with { Children = [new("facility", 7, name), new("durability", 14, durability)] };
        }
        catch (InvalidDataException error) { return root with { UnresolvedOwner = error.Message }; }

        static MapCornerColors Solid(AreaMarkerColor color) => new(color, color, color, color);
        static void Read(Dictionary<int, string> missing, Dictionary<int, bool> visible, int[] nodes, Action read)
        {
            try { read(); }
            catch (InvalidDataException error)
            { foreach (int node in nodes) { missing[node] = error.Message; visible[node] = false; } }
        }
    }

    private static string FormatFacilitySummaryDurability(string format, int value, int maximum)
    {
        var result = new StringBuilder();
        int argument = 0;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { result.Append(format[index]); continue; }
            if (++index < format.Length && format[index] == '%') { result.Append('%'); continue; }
            int width = 0;
            while (index < format.Length && format[index] is >= '0' and <= '9')
                width = checked(width * 10 + format[index++] - '0');
            if (index >= format.Length || format[index] != 'd' || argument >= 2)
                throw FacilitySummaryMissing("durability_format");
            result.Append((argument++ == 0 ? value : maximum).ToString(CultureInfo.InvariantCulture).PadLeft(width));
        }
        if (argument != 2) throw FacilitySummaryMissing("durability_format_arguments");
        return result.ToString();
    }

    private static T FacilitySummaryNeed<T>(T? value, string field) where T : struct => value
        ?? throw FacilitySummaryMissing(field);
    private static InvalidDataException FacilitySummaryMissing(string field) => new("map.summary.facility." + field);
}
