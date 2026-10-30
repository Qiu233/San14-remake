using System.Globalization;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static readonly MapSpritePalettes SummaryChildPalettes = MapSpritePalettes.Create();
    private static readonly ArmyMapUiSources SummaryChildIcons = ArmyMapUiSources.Create();

    // Shared original child presentations. Callers retain ownership of the
    // actual scalar rules and pass their result, not another captured world.
    internal MapSummaryPage SummaryCityIdentity(SourceWorldContext current,
        MapSummaryReceiver receiver, ForceReference? force, string? title,
        int? districtNumber, bool? showArmyMarker = false, bool? emperorVisible = false)
    {
        var text = new Dictionary<int, string>();
        var colors = new Dictionary<int, MapCornerColors>();
        var visible = new Dictionary<int, bool>();
        var images = new List<MapSummaryImage>();
        var missing = new Dictionary<int, string>();
        SummaryChildText(text, missing, 6, title);
        SummaryChildVisibility(visible, missing, 5, emperorVisible);
        SummaryChildVisibility(visible, missing, 8, showArmyMarker);
        if (districtNumber is int number)
        {
            visible[7] = number is >= 1 and <= 8;
            if (visible[7]) SummaryChildImage(images, missing, 7, 0x230 + number);
        }
        else missing[7] = "map-summary.identity.District_number";

        try
        {
            if (force is null) throw new InvalidDataException("map-summary.identity.Force_receiver");
            bool valid = current.ReadForceNativeValidity(force)
                ?? throw new InvalidDataException("map-summary.identity.Force_validity");
            int physical = force.PhysicalSlot
                ?? throw new InvalidDataException("map-summary.identity.Force_identity");
            int color = current.ReadForceReportColor(force)
                ?? throw new InvalidDataException("map-summary.identity.Force_color49");
            var fill = valid || physical is >= 46 and <= 51
                ? MapSpritePalettes.ForceFill(color)
                : MapSpritePalettes.FromAuthored(0xFFFFFFFF);
            colors[3] = new(fill, fill, fill, fill);
            var letter = SummaryChildPalettes.ForceLetter(color);
            colors[4] = new(letter, letter, letter, letter);
            _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
            text[4] = _mapInformationNames.Resolve(current, force).NativeShortName;
        }
        catch (InvalidDataException error)
        {
            missing[3] = error.Message;
            missing[4] = error.Message;
        }
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_00", true, text)
        {
            Colors = colors, Visibility = visible, Images = images, UnresolvedFields = missing
        };
    }

    internal MapSummaryPage SummaryCityPair(MapSummaryReceiver receiver, int captionId,
        string? value, int? valuePreset = null)
    {
        var text = new Dictionary<int, string> { [2] = ReadHistoryDataText(1, captionId) };
        var missing = new Dictionary<int, string>();
        SummaryChildText(text, missing, 3, value);
        var presets = new Dictionary<int, int> { [2] = 13, [3] = 0 };
        if (valuePreset is int preset) presets[3] = preset;
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_02", true, text)
        { TextPresets = presets, UnresolvedFields = missing };
    }

    internal MapSummaryPage SummaryCityNumber(MapSummaryReceiver receiver, int iconId,
        int captionId, string? formattedValue)
    {
        var text = new Dictionary<int, string> { [3] = ReadHistoryDataText(1, captionId) };
        var missing = new Dictionary<int, string>();
        var images = new List<MapSummaryImage>();
        SummaryChildImage(images, missing, 2, iconId);
        SummaryChildText(text, missing, 4, formattedValue);
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_03", true, text)
        {
            Images = images,
            Colors = new Dictionary<int, MapCornerColors> { [2] = SummaryChildPalettes[21] },
            TextPresets = new Dictionary<int, int> { [3] = 13, [4] = 0 },
            UnresolvedFields = missing
        };
    }

    internal MapSummaryPage SummaryCityTotal(MapSummaryReceiver receiver,
        string? current, string? maximum, int? currentPreset = null)
    {
        var text = new Dictionary<int, string>
        { [3] = ReadHistoryDataText(1, 0x20E), [5] = "/" };
        var missing = new Dictionary<int, string>();
        var images = new List<MapSummaryImage>();
        SummaryChildImage(images, missing, 2, 0x19C);
        SummaryChildText(text, missing, 4, current);
        SummaryChildText(text, missing, 6, maximum);
        var presets = new Dictionary<int, int> { [3] = 13, [4] = 0 };
        if (currentPreset is int preset) presets[4] = preset;
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_03_pk", true, text)
        {
            TextPresets = presets,
            Images = images,
            Colors = new Dictionary<int, MapCornerColors> { [2] = SummaryChildPalettes[21] },
            UnresolvedFields = missing
        };
    }

    internal MapSummaryPage SummaryCityGauge(MapSummaryReceiver receiver, int iconId,
        int captionId, string? formattedValue, int? current, int? maximum)
    {
        var text = new Dictionary<int, string> { [5] = ReadHistoryDataText(1, captionId) };
        var missing = new Dictionary<int, string>();
        var images = new List<MapSummaryImage>();
        SummaryChildImage(images, missing, 4, iconId);
        SummaryChildText(text, missing, 6, formattedValue);
        IReadOnlyList<MapSummaryBar> bars = Array.Empty<MapSummaryBar>();
        if (current is int actual && maximum is int capacity)
            bars = new[] { new MapSummaryBar(2, actual, capacity, 1)
                { InitialCurrent = 70, InitialMaximum = 100 } };
        else missing[2] = "map-summary.gauge.current_or_capacity";
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_09", true, text)
        {
            Images = images,
            Visibility = new Dictionary<int, bool> { [3] = false },
            Colors = new Dictionary<int, MapCornerColors> { [4] = SummaryChildPalettes[21] },
            TextPresets = new Dictionary<int, int> { [5] = 13, [6] = 0 },
            Bars = bars, UnresolvedFields = missing
        };
    }

    internal MapSummaryPage SummaryCityStatus(MapSummaryReceiver receiver,
        IReadOnlyDictionary<int, int?> statePresets)
    {
        var text = new Dictionary<int, string> { [2] = ReadHistoryDataText(1, 0x44F) };
        var colors = new Dictionary<int, MapCornerColors>();
        var missing = new Dictionary<int, string>();
        var visible = new Dictionary<int, bool>();
        for (int node = 3; node <= 7; node++)
        {
            visible[node] = statePresets.TryGetValue(node, out int? preset);
            if (!visible[node]) continue;
            if (preset is int known) colors[node] = SummaryChildPalettes[known];
            else missing[node] = "map-summary.status.node_" + node.ToString(CultureInfo.InvariantCulture);
        }
        return new(receiver.Slot, receiver.ReceiverId, "00_82_05_sum_city_offset_06_pk", true, text)
        { TextPresets = new Dictionary<int, int> { [2] = 13 }, Colors = colors,
            Visibility = visible, UnresolvedFields = missing };
    }

    internal MapSummaryPage SummaryShuOverflow(MapSummaryReceiver receiver, int extraCount)
        => new(receiver.Slot, receiver.ReceiverId, "00_82_10_sum_shu_offset_03", true,
            new Dictionary<int, string>
            { [2] = FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x3C3), extraCount) });

    private static void SummaryChildImage(ICollection<MapSummaryImage> images,
        IDictionary<int, string> missing, int node, int spriteId)
    {
        string name;
        // These normal Common entries are recorded in the finite141
        // resource table. Other shared entries use the mature Army UI table.
        if (spriteId == 0x199) name = "000_cmn_icon_genek_fuchi";
        else if (spriteId == 0x1E2) name = "000_cmn_icon_siki_fuchi";
        else if (spriteId == 0x1F2) name = "000_cmn_icon_taikyu_fuchi";
        else
        {
            try { name = SummaryChildIcons.IconName(new(spriteId, 0)); }
            catch (KeyNotFoundException)
            {
                missing[node] = "map-summary.child.Common_sprite_" + spriteId.ToString("X", CultureInfo.InvariantCulture);
                return;
            }
        }
        images.Add(new(node, name));
    }

    private static void SummaryChildText(IDictionary<int, string> text,
        IDictionary<int, string> missing, int node, string? value)
    {
        if (value is not null) text[node] = value;
        else missing[node] = "map-summary.child.text_" + node.ToString(CultureInfo.InvariantCulture);
    }

    private static void SummaryChildVisibility(IDictionary<int, bool> visible,
        IDictionary<int, string> missing, int node, bool? value)
    {
        if (value is bool known) visible[node] = known;
        else missing[node] = "map-summary.child.visibility_" + node.ToString(CultureInfo.InvariantCulture);
    }
}
