using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal string ReadStoryMonthMessageNameCurrent(int month)
    {
        int id = unchecked(0x69D + month);
        if (id is < 0 or >= 3000) return string.Empty;
        string value = ReadHistoryDataText(1, id);
        int end = value.IndexOf('\0');
        if (end >= 0) value = value[..end];
        return value.Length <= 62 ? value : string.Empty;
    }

    private sealed class StoryProvinceMessageCurrent(ArmyCampaignRuleAdapter package,
        Func<GameState> latest, SourceWorldContext current, IReadOnlyList<int?>? arguments)
    {
        internal int ProvinceValue(int argument, int field)
        {
            int raw = Argument(argument);
            if (field == 0) return raw;
            if (!Valid(raw)) return 0;
            if (field == '2') return ProvinceText(argument, field).Length;
            if (field != '3') return 0;
            return Region(raw) ?? 0;
        }

        internal string ProvinceText(int argument, int field)
        {
            int raw = Argument(argument);
            if (field == 0 || !Valid(raw)) return string.Empty;
            if (field == '2')
            {
                var row = package._growthQueries.Definitions.Catalog.States[raw];
                if (row.Slot != raw || row.RawBytes.Length != 32) throw StoryTextMissing("Province_definition");
                return CityDomesticMessageFormatting.FormatCityNameField(
                    S14ForceNameDefinitionReader.ReadLiteralUnits(row.RawBytes.AsSpan(24, 6)));
            }
            return field == '3' && Region(raw) is int region
                ? package._growthQueries.Definitions.Catalog.RegionNames[region] : string.Empty;
        }

        private int Argument(int argument) => arguments is not null && (uint)argument < (uint)arguments.Count &&
            arguments[argument] is int value ? value : throw StoryTextMissing("Province" + argument);
        private bool Valid(int raw)
        {
            if (raw is < 1 or > 22) return false;
            var selected = current.ReadConditionStateTableSlot(raw)
                ?? throw StoryTextMissing("Province_lookup");
            return current.ReadConditionStateValidity(selected) ?? throw StoryTextMissing("Province_validity");
        }
        private int? Region(int province)
        {
            int raw = BaseCaptureRewardInputs.ReadStateRegion(latest(), province)
                ?? throw StoryTextMissing("Province2E_region");
            var selected = current.ReadStaticTableReference(0xDC20, raw < 12 ? raw : 0)
                ?? throw StoryTextMissing("Region_lookup");
            int physical = selected.PhysicalSlot ?? throw StoryTextMissing("Region_receiver");
            int zero = current.ReadStaticTableReference(0xDC20, 0)?.PhysicalSlot
                ?? throw StoryTextMissing("Region0_receiver");
            return physical == zero ? null : physical;
        }
    }
}
