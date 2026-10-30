using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryStateMapSummaryCurrent(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
    {
        int id = receiver.ReceiverId;
        var data = OtherSummaryText();
        var page = new OtherSummaryBuilder(receiver.Slot, id, "00_82_10_sum_shu", data);
        bool? valid = null;
        page.Read(-1, () => valid = OtherSummaryNeed(current.ReadConditionStateValidity(new(id)), "state.valid"));
        if (valid != true) return page.Finish(valid);
        foreach (int node in new[] { 9, 10, 19, 28, 37, 46 }) page.Visible[node] = false;
        var header = new OtherSummaryBuilder(receiver.Slot, id, "00_82_10_sum_shu_offset_00", data);
        header.Value(2, () => S14ForceNameDefinitionReader.ReadLiteralUnits(_growthQueries.Definitions.Catalog.States[id].RawBytes.AsSpan(24, 6)));
        page.Children.Add(new("state-name", 0x3A, header.Finish()));
        page.Read(-2, () =>
        {
            //91D410 and915260 share the real Base-manager order. Gate/Base
            //receivers resolve to their parent City before comparing StateCE.
            var forces = new List<int>();
            int troops = 0;
            foreach (var entry in current.ReadRegisteredBases() ?? throw OtherSummaryMissing("state.base_registry"))
            {
                var location = entry ?? throw OtherSummaryMissing("state.base_pointer");
                var city = BaseCurrencyAdditionSource.CurrencyCity(location, current);
                if (!OtherSummaryNeed(current.ReadBaseNativeValidity(city), "state.city_valid")) continue;
                int cityId = OtherSummaryNeed(city.PhysicalSlot, "state.city_physical");
                int province = OtherSummaryNeed(_growthQueries.ReadCityState(state, cityId), "state.city_province");
                int actual = province < 23 ? province : 0;
                if (actual != id) continue;
                troops = unchecked(troops + OtherSummaryNeed(current.ReadBaseResource(location, BaseResourceKind.Troops), "state.base_troops"));
                var force = current.ReadForceTableReference(OtherSummaryNeed(current.ReadBaseForceSlot(city), "state.city_force"))
                    ?? throw OtherSummaryMissing("state.force_pointer");
                if (!OtherSummaryNeed(current.ReadForceNativeValidity(force), "state.force_valid")) continue;
                int physical = OtherSummaryNeed(force.PhysicalSlot, "state.force_physical");
                if (!forces.Contains(physical)) forces.Add(physical);
            }
            var values = new ForceInformationCurrent(this, state, current);
            var keys = new Dictionary<int, ulong>();
            foreach (int force in forces)
            {
                var economy = ReadForceDetailEconomyCurrent(state, current, force, new HashSet<int> { 111 });
                if (economy.UnresolvedFields.TryGetValue(111, out var error)) throw new InvalidDataException(error);
                int count = economy.NumericValues.TryGetValue(111, out int scalar) ? scalar : throw OtherSummaryMissing("state.force_total_troops");
                keys[force] = values.SummaryStateSortKey(force, count);
            }
            forces.Sort((a, b) => keys[b].CompareTo(keys[a]));
            int main = forces.Count > 0 ? forces[0] : 0;
            var owner = new OtherSummaryBuilder(receiver.Slot, id, "00_82_05_sum_city_offset_02", data);
            owner.Value(5, () => OtherSummaryForceName(state, current, main));
            owner.Read(3, () => OtherSummaryFlag(state, current, owner, 3, main, 4));
            page.Children.Add(new("main-force", 0x41, owner.Finish()));
            var relationship = new OtherSummaryBuilder(receiver.Slot, id, "00_82_10_sum_shu_offset_01", data);
            relationship.Caption(2, 0x413);
            if (forces.Count > 0)
            {
                OtherSummaryRelation(state, current, relationship, 4, 5, main, data);
                page.Read(9, () => OtherSummaryPortrait(state, current, page, 9, values.SummaryRuler(main)));
            }
            else { relationship.Visible[0] = false; page.Visible[9] = false; }
            page.Children.Add(new("main-relation", 0x47, relationship.Finish()));
            int[] mounts = [0xA, 0x13, 0x1C, 0x25, 0x2E];
            int others = Math.Max(0, forces.Count - 1);
            for (int index = 0; index < mounts.Length; index++)
            {
                bool shown = index < others;
                page.Visible[mounts[index]] = shown;
                var child = new OtherSummaryBuilder(receiver.Slot, id, "00_82_10_sum_shu_offset_02", data);
                if (shown)
                {
                    int force = forces[index + 1];
                    OtherSummaryPortrait(state, current, child, 4, values.SummaryRuler(force));
                    child.Value(5, () => OtherSummaryPersonName(state, current, values.SummaryRuler(force)));
                    OtherSummaryRelation(state, current, child, 7, 8, force, data);
                }
                page.Children.Add(new("other-force-" + index, mounts[index], child.Finish()));
            }
            var overflow = new OtherSummaryBuilder(receiver.Slot, id, "00_82_10_sum_shu_offset_03", data);
            page.Visible[0x37] = others >= 6;
            if (others >= 6) overflow.Value(2, () => FormatSceneryPanelNumber(data(1, 0x3C4), others - 5));
            page.Children.Add(new("more-forces", 0x37, overflow.Finish()));
            var total = new OtherSummaryBuilder(receiver.Slot, id, "00_82_05_sum_city_offset_02", data);
            total.Caption(2, 0x3C2); total.Text[3] = OtherSummaryNumber(troops);
            page.Children.Add(new("troops", 0x3D, total.Finish()));
        });
        return page.Finish();
    }

    private string OtherSummaryForceName(GameState state, SourceWorldContext current, int id)
    {
        var force = current.ReadForceTableReference(id) ?? throw OtherSummaryMissing("force_name.pointer");
        if (!OtherSummaryNeed(current.ReadForceNativeValidity(force), "force_name.valid")) return "";
        return new CampaignHistoryWriter(_repository, _scenario, Seed.Language).FallbackName(state, current, force,
            (person, full) => full ? person.Name : person.CurrentSurname ?? throw OtherSummaryMissing("force_name.surname"));
    }

    //9227D0: own/self, alliance, and target-to-player friendship are separate
    //branches. Metadata sprites come from the same original UI category1.
    private static void OtherSummaryRelation(GameState state, SourceWorldContext current, OtherSummaryBuilder page,
        int imageNode, int textNode, int other, Func<int,int,string> data)
    {
        page.Read(textNode, () =>
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OtherSummaryMissing("relation.player");
            if (!OtherSummaryNeed(current.ReadForceNativeValidity(player), "relation.player_valid"))
            { page.Visible[imageNode] = page.Visible[textNode] = false; return; }
            int own = OtherSummaryNeed(player.PhysicalSlot, "relation.player_physical");
            int icon;
            if (own == other) { page.Text[textNode] = data(1, 0x353); icon = 0x21F; }
            else if ((ReadReportForceRelationByte(state, new(other), own, OtherSummaryMissing) & 0x7F) != 0)
            { page.Text[textNode] = data(1, 0x289); icon = 0x21C; }
            else
            {
                int level = DLevel(state, DRelations(state, other)[own]);
                int[] icons = [0x221, 0x221, 0x222, 0x21F, 0x21C, 0x21C];
                page.Text[textNode] = data(0, 0x141 + level);
                if ((uint)level >= icons.Length) throw OtherSummaryMissing("relation.level");
                icon = icons[level];
            }
            var naming = ArmyMapUiSources.Create();
            page.Images.Add(new(imageNode, naming.IconName(new(icon, 1))));
            page.Visible[imageNode] = page.Visible[textNode] = true;
        });
    }

    private sealed partial class ForceInformationCurrent
    {
        internal ulong SummaryStateSortKey(int id, int troops)
        {
            int player = DiplomacyPlayer();
            uint rank = (uint)Need(Current.ReadMoraleWaveForceRank47(id), "summary.sort_rank");
            ulong friendship = id < 52 ? DiplomacyFriendship(player, id) : 0u;
            return (unchecked((uint)(-id - 13)) & 0x3Fu) | friendship << 6 |
                (((ulong)(unchecked(0u - rank - 4u) & 0xFu) << 24) | ((uint)troops & 0xFFFFFFu)) << 14;
        }
    }
}
