using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class CityInformationCurrent
    {
        internal CityInformationPageSet DistrictCityPages(IReadOnlyList<int> ids)
        {
            //72D280 actually passes141A34CB0, the same descriptor bank as
            //ordinary City information. Hire uses a different bank: its
            //selector-id mapping must not be applied to this caller.
            int[][] sequences = [[0,33,2,3,4,5,6,7,8,11], [0,7,12,13,14,8,15,16,17,18], [0,19,21,13]];
            int[] captions = [0x3CD,0x3FB,0x2A3];
            var groups = sequences.Select((sequence, index) => new CityInformationGroup(index,
                captions[index], Data(1,captions[index]), sequence.Select(id =>
                {
                    var row = id == 33 ? new CityInformationColumnsSource.Definition(33,76,0x20000008,0x214,0,true)
                        : CityInformationColumnsSource.Rows[id];
                    int format = (row.Flags & 1) != 0 ? 3 : 4;
                    if ((row.Flags & 2) != 0) format = 5;
                    return new CityInformationColumn(id,row.CaptionId,Data(1,row.CaptionId),row.Width,
                        format,(row.Flags & 0x20000000) == 0,true,row.Visible);
                }).ToImmutableArray())).ToImmutableArray();
            var governors = new Dictionary<int,PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (column != 33) return Cell(id,column);
                if (governors.TryGetValue(id,out var saved)) return saved;
                bool IsLeader()
                {
                    var city = ReadCity(id);
                    if (!Need(Current.ReadBaseNativeValidity(city.Receiver),"district.City_validity")) return false;
                    int governor = Governor(city);
                    if (!Need(Current.ReadPersonNativeValidity(governor),"district.City_governor_validity")) return false;
                    var district = District(city);
                    if (!Need(Current.ReadDistrictNativeValidity(district),"district.City_District_validity")) return false;
                    int physical = Need(district.PhysicalSlot,"district.City_District_receiver");
                    int leader = Person(Need(Current.ReadDistrictLeader(physical),"district.City_District_leader12"));
                    return governor == leader;
                }
                return governors[id] = MakeCell(() => IsLeader() ? Data(1,0x338) : "", () => new NumberKey(IsLeader() ? 1 : 0));
            }
            return new(Data(1,0x5B3),groups,ids,
                (id,column) => Read(id,column).Display with { SortUnresolvedOwner = Read(id,column).SortUnresolvedOwner },
                (id,column) => Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending) => (Read(left,column).Key ?? throw Missing("district.sort_key"))
                    .Before(Read(right,column).Key ?? throw Missing("district.sort_key"),ascending));
        }
    }
}
