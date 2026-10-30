using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class CityInformationCurrent
    {
        private IReadOnlyList<string>? regionSortText;
        private Dictionary<int,int>? areaCounts;
        private PreparedCell GeographyCell(City city,int column) => column switch
        {
            30 => MakeCell(() => Region(city) is int region ? rules._growthQueries.Definitions.Catalog.RegionNames[region] : "",
                () => Region(city) is int region ? new NameKey(RegionSortText()[region]) : new NameKey("",true)),
            31 => MakeCell(() => Province(city) is int province ? StateText(province,24,6) : "",
                () => Province(city) is int province ? new NameKey(StateText(province,10,14)) : new NameKey("",true)),
            32 => MakeCell(() => Number(AreaCount(city)), () => new NumberKey(AreaCount(city))),
            _ => throw Missing("geography_column."+column)
        };
        private int? Province(City city)
        {
            int raw = Need(rules._growthQueries.ReadCityState(state,city.PhysicalSlot),"CityCE_province");
            var selected = Current.ReadStaticTableReference(0xDC80,raw < 23 ? raw : 0) ?? throw Missing("State_lookup");
            if (!Need(Current.ReadConditionStateValidity(new(selected.PhysicalSlot)),"State_validity")) return null;
            return Need(selected.PhysicalSlot,"State_receiver");
        }
        private int? Region(City city)
        {
            int? province = Province(city);
            if (!province.HasValue) return null;
            int raw = Need(BaseCaptureRewardInputs.ReadStateRegion(state,province.Value),"State2E_region");
            var selected = Current.ReadStaticTableReference(0xDC20,raw < 12 ? raw : 0) ?? throw Missing("Region_lookup");
            if (!selected.PhysicalSlot.HasValue) return null;
            int zero = Need(Current.ReadStaticTableReference(0xDC20,0)?.PhysicalSlot,"Region0_receiver");
            //234510's actual vslot3 tests the receiver against table0.
            return selected.PhysicalSlot == zero ? null : selected.PhysicalSlot.Value;
        }
        private string StateText(int physical,int offset,int count)
        {
            var row = rules._growthQueries.Definitions.Catalog.States[physical];
            if (row.Slot != physical || row.RawBytes.Length != 32) throw Missing("State_definition");
            return S14ForceNameDefinitionReader.ReadLiteralUnits(row.RawBytes.AsSpan(offset,count));
        }
        private IReadOnlyList<string> RegionSortText() => regionSortText ??=
            rules._repository.LoadCityInformationRegionSortText(rules._scenario.FixedDataSource.SourcePath);
        private int AreaCount(City city)
        {
            if (areaCounts is null)
            {
                var counts = new Dictionary<int,int>();
                foreach (var item in Current.ReadRegisteredAreaReferences() ?? throw Missing("AreaB0_registry"))
                {
                    var area = item ?? throw Missing("Area_pointer");
                    int owner = Need(Current.ReadAreaCitySlot(area),"Area35_City");
                    if (owner is < 1 or > 51) continue;
                    counts[owner] = counts.GetValueOrDefault(owner)+1;
                }
                areaCounts = counts;
            }
            return areaCounts.GetValueOrDefault(city.PhysicalSlot);
        }
    }
}
