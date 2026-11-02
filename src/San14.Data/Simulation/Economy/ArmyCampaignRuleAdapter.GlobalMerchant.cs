using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The top-bar merchant begins with the current user's admitted
    /// cities, in City-table order. It does not preselect a city or quote a trade.</summary>
    public AreaAdministrationQuery<CityInformationPageSet> PrepareGlobalMerchantCitySelection(
        GameState state, int playerForceId)
    {
        try
        {
            CityDomesticParameterCatalog.RequireScenario(_scenario, Seed.Language);
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            PersonReturnCommands.BindCurrent(state, current);
            if (!_baseCapacityInputs.BindCurrent(state, current)) throw CityMerchantSource.Missing("capacity_owner");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw CityMerchantSource.Missing("selector_player");
            if (CityMerchantSource.K(player.PhysicalSlot, "selector_player_identity") != playerForceId)
                throw CityMerchantSource.Missing("selector_player_owner");
            var cities = new List<int>();
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw CityMerchantSource.Missing("selector_City_pointer");
                if (!CityMerchantSource.K(current.ReadBaseNativeValidity(city), "selector_City_validity")) continue;
                int id = CityMerchantSource.K(city.PhysicalSlot, "selector_City_identity");
                // The original admission first requires the user's force and a
                // ruler-led district. The ordinary command boundary also excludes
                // delegated districts when no delegation context was supplied.
                if (CityMerchantSource.K(current.ReadBaseForceSlot(city), "selector_City_force") != playerForceId) continue;
                var district = current.ReadCityDistrictReference(city) ?? throw CityMerchantSource.Missing("selector_District_pointer");
                if (!CityMerchantSource.K(current.ReadDistrictNativeValidity(district), "selector_District_validity")) continue;
                int districtId = CityMerchantSource.K(district.PhysicalSlot, "selector_District_identity");
                var owner = state.Districts.GetValueOrDefault(districtId) ?? throw CityMerchantSource.Missing("selector_District_owner");
                if (owner.Number != 1) continue;
                int leaderId = CityMerchantSource.K(current.ReadDistrictLeader(districtId), "selector_District_leader");
                int leader = CityMerchantSource.K((leaderId <= 6000 ? current.ReadPersonLookupReference(leaderId)
                    : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "selector_leader_pointer");
                if (!CityMerchantSource.K(current.ReadPersonNativeValidity(leader), "selector_leader_validity") ||
                    CityMerchantSource.K(current.ReadPersonStatus(leader), "selector_leader_status") != 1) continue;
                byte flags = state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(id)?.C9
                    ?? throw CityMerchantSource.Missing("selector_C9");
                if ((flags & 1) == 0 || (flags & 4) != 0 || owner.OrdersRemaining is not > 0) continue;
                // All remaining prices, capacities and executor checks are the
                // same readers used by city entry and formal trade execution.
                if (CreateCityMerchantSource(state, id, sharedCurrent: current).CanOpen()) cities.Add(id);
            }
            return AreaAdministrationQuery<CityInformationPageSet>.Known(
                new CityInformationCurrent(this, state, playerForceId, current).MerchantPages(cities));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CityInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class CityInformationCurrent
    {
        internal CityInformationPageSet MerchantPages(IReadOnlyList<int> ids)
        {
            // The merchant's one basic group uses the ordinary City descriptor
            // bank. Its authored sequence differs from both Hire and Information.
            int[] sequence = [0, 5, 24, 25, 6, 26, 27, 20, 28, 4];
            var group = new CityInformationGroup(0, 0x3CD, Data(1, 0x3CD), sequence.Select(id =>
            {
                var row = CityInformationColumnsSource.Rows[id];
                int format = (row.Flags & 2) != 0 ? 5 : (row.Flags & 8) != 0 ? 4 : 3;
                return new CityInformationColumn(id, row.CaptionId, Data(1, row.CaptionId), row.Width,
                    format, (row.Flags & 0x20000000) == 0, true, row.Visible);
            }).ToImmutableArray());
            return new(Data(1, 0x2B0), [group], ids,
                (id, column) => Cell(id, column).Display with { SortUnresolvedOwner = Cell(id, column).SortUnresolvedOwner },
                (id, column) => Cell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Cell(left, column).Key ?? throw Missing("merchant.sort_key"))
                    .Before(Cell(right, column).Key ?? throw Missing("merchant.sort_key"), ascending));
        }
    }
}
