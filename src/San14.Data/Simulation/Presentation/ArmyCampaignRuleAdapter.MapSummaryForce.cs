using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryForceMapSummaryCurrent(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
    {
        int id = receiver.ReceiverId, mode = receiver.Slot == 5 ? 0 : 1;
        var data = OtherSummaryText();
        var page = new OtherSummaryBuilder(receiver.Slot, id, "00_82_08_sum_force_offset_00_cn", data);
        bool? valid = null;
        page.Read(-1, () => valid = OtherSummaryNeed(current.ReadForceNativeValidity(
            current.ReadForceTableReference(id) ?? throw OtherSummaryMissing("force.pointer")), "force.valid"));
        if (valid != true) return WrapForce(page.Finish(valid));
        var values = new ForceInformationCurrent(this, state, current);
        foreach (var caption in new (int Node, int Message)[]
        { (0x24,0x21B),(0xD,0x20C),(0x29,0x207),(0x11,0x20D),(0x2C,0x218),(0x14,0x20E),
          (0x30,0x1FE),(0x1D,0x293),(0x21,0x419),(9,0x413),(0x38,0x414),(0x60,0x386),
          (0x65,0x54D),(0x18,0x835),(0x34,0x829),(0x73,0x83E),(0x81,0x83E) })
            page.Caption(caption.Node, caption.Message);
        page.Text[0x62] = "/";
        page.Presets[0x3F] = 41;
        foreach (int node in new[] { 0x41, 0x49, 0x54 }) page.Visible[node] = false;
        if (mode == 0) foreach (int node in new[] { 9, 10, 0x38, 0x39 }) page.Visible[node] = false;
        else foreach (int node in new[] { 0x60, 99, 0x62, 0x61, 100, 0x65, 0x66 }) page.Visible[node] = false;
        page.Visible[0x72] = mode == 0; page.Visible[0x80] = mode == 1;
        page.Value(8, () => OtherSummaryPersonName(state, current, values.SummaryRuler(id)));
        page.Value(0x25, () => values.SummaryRankName(id, data));
        page.Value(0x28, () => OtherSummaryNumber(values.SummaryCount(id, 6)));
        page.Value(0x2D, () => OtherSummaryNumber(values.SummaryCount(id, 7)));
        page.Value(0x31, () => OtherSummaryNumber(values.SummaryCount(id, 9)));
        page.Value(0x35, () => values.SummaryRestricted() || values.SummaryRank(id) == 0 ? data(1, 0x353)
            : OtherSummaryNumber(values.SummaryLearnedPanels(id)));
        page.Value(0x74, () => values.SummaryHeaven(id, data));
        page.Value(0x82, () => values.SummaryHeaven(id, data));
        page.Value(10, () => values.SummaryRestricted() ? data(1, 0x353) : values.SummaryRelationship(id, data));
        if (mode == 1) page.Value(0x39, () => values.SummaryAllianceMonths(id, data));
        //Only the exact stock getters reached by this compact page are evaluated.
        page.Read(-2, () =>
        {
            var economy = ReadForceDetailEconomyCurrent(state, current, id, new HashSet<int> { 69, 90, 111 });
            foreach (var binding in new[] { (Target: 0xC, Source: 69), (Target: 0x10, Source: 90), (Target: 0x15, Source: 111) })
            {
                if (economy.UnresolvedFields.TryGetValue(binding.Source, out var missing))
                { page.Missing[binding.Target] = missing; page.Visible[binding.Target] = false; }
                else if (economy.Text.TryGetValue(binding.Source, out var text)) page.Text[binding.Target] = text;
            }
        });
        page.Value(0x19, () => values.SummaryIntelligence(id, data));
        page.Value(0x1C, () => values.SummaryCoalitionRole(id, data));
        page.Value(0x20, () => values.SummaryCoalitionMonths(id, data));
        page.Visible[0x3B] = false;
        bool doctrineComplete = false;
        page.Read(0x3F, () =>
        {
            if (id is >= 46 and <= 50)
            { page.Visible[0x3B] = false; page.Visible[0x75] = false; page.Visible[0x80] = false; return; }
            page.Text[0x3F] = values.SummaryDoctrine(id);
            page.Visible[0x3B] = true;
            int level = values.SummaryDoctrineLevel(id);
            page.Text[0x40] = FormatSceneryPanelNumber(data(1, 0x384), level);
            var progress = OwnedForce164Inputs.ReadProgress(state, id);
            int within = OtherSummaryNeed(progress.WithinLevel, "force.doctrine_within");
            int span = OtherSummaryNeed(progress.LevelSpan, "force.doctrine_span");
            //Original named gauge uses100% when its native denominator is below1.
            page.Gauges.Add(new(61, 62, span < 1 ? 100 : within, span < 1 ? 100 : span) { AnimationName = "gauge" });
            if (mode == 0)
            {
                int capacity = OtherSummaryNeed(ReadGovernanceCostLimit(state, id), "force.policy_capacity");
                int used = OtherSummaryNeed(GovernanceRosterSource.Read(state, id, _scenario.Snapshot.DoctrineParameters, []).PolicyCost,
                    "force.policy_used");
                page.Text[99] = OtherSummaryNumber(used); page.Text[0x61] = OtherSummaryNumber(capacity);
                page.Text[0x66] = values.SummaryPolicyExpansion(id, data);
            }
            int first = mode == 0 ? 0x69 : 0x77;
            for (int group = 0; group < 5; group++) page.Text[first + group * 2] = OtherSummaryNumber(values.SummaryDepartment(id, group));
            page.Visible[0x67] = mode == 0; page.Visible[0x75] = mode == 1;
            doctrineComplete = true;
        });
        if (!doctrineComplete) page.Visible[0x3B] = false;
        page.Read(7, () => OtherSummaryFlag(state, current, page, 7, id));
        return WrapForce(page.Finish());

        MapSummaryPage WrapForce(MapSummaryPage child)
        {
            var parent = new OtherSummaryBuilder(receiver.Slot, id, "00_82_08_sum_force", data);
            parent.Visible[1] = parent.Visible[2] = parent.Visible[3] = false;
            parent.Visible[4] = mode == 0; parent.Visible[8] = mode == 1;
            int mount = mode == 0 ? 7 : 11, portrait = mode == 0 ? 6 : 10;
            parent.Children.Add(new("force-information", mount, child));
            if (valid == true)
                parent.Read(portrait, () => OtherSummaryPortrait(state, current, parent, portrait,
                    OtherSummaryPerson(current, OtherSummaryNeed(current.ReadForceRecord(id)?.RawRuler10, "force.ruler"))));
            else parent.Visible[portrait] = false;
            return parent.Finish(valid);
        }
    }

    private sealed partial class ForceInformationCurrent
    {
        internal ForceInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, SourceWorldContext current)
        { this.rules = rules; this.state = state; playerForceId = -1; captured = new(() => current); }
        internal int SummaryRuler(int id) => Ruler(id);
        internal int SummaryCount(int id, int column) => Count(id, column, false);
        internal int SummaryDepartment(int id, int group) => DepartmentCount(id, group);
        internal string SummaryDoctrine(int id) => Doctrine(id).Name;
        internal int SummaryDoctrineLevel(int id) => DoctrineLevel(id);
        internal bool SummaryRestricted()
        {
            if (Need(rules.ReadApplicationInformationRestriction(state), "summary.application_selection")) return true;
            if (Need(Current.EventConditions.ScenarioMode, "summary.scenario_mode") == 3) throw Missing("summary.CCCPersonSelectState");
            return false;
        }
        internal string SummaryRankName(int id, Func<int,int,string> data) => Rank(id) is int rank && rank != 0
            ? rank == 9 ? data(1, 0x53F) : (rules._scenario.Snapshot.RankParameters ?? throw Missing("summary.rank_catalog")).Ranks[rank].Name : "";
        internal int SummaryRank(int id) => Rank(id);
        internal int SummaryLearnedPanels(int id)
        {
            var effects = state.Forces.GetValueOrDefault(id)?.PolicyEffects ?? throw Missing("summary.research_owner");
            var activeEffects = effects.ActiveEffectIds ?? throw Missing("summary.research_active_effects");
            researchPanels ??= rules.ReadDoctrineResearchDefinitions(Current);
            int count = 0;
            for (int raw = 0; raw < 80; raw++)
            {
                int physical = Need(Current.ReadStaticTableReference(0x85F20, raw)?.PhysicalSlot, "summary.research_pointer");
                if (physical is 0 or 1 or >= 74 and <= 79) continue;
                if (!researchPanels.ContainsKey(physical)) throw Missing("summary.research_definition");
                if (activeEffects.Contains(physical)) count++;
            }
            return count;
        }
        internal string SummaryIntelligence(int id, Func<int,int,string> data)
        {
            if (SummaryRestricted() || Special(id)) return data(1, 0x353);
            int sum = 0;
            foreach (var entry in Current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("summary.district_registry"))
            {
                int physical = Need(entry?.PhysicalSlot, "summary.district_pointer");
                if (Need(Current.ReadDistrictForceId(physical), "summary.district_force") != id ||
                    !Need(Current.ReadDistrictNativeValidity(entry!), "summary.district_valid")) continue;
                sum = unchecked(sum + ReadDistrictAdministrationIntelligenceCurrent(Current, physical));
            }
            return OtherSummaryNumber(sum);
        }
        internal string SummaryHeaven(int id, Func<int,int,string> data)
        {
            if (SummaryRestricted()) return data(1, 0x353);
            if (Special(id)) return "";
            byte flags = DiplomacyFlags(id);
            return (flags & 8) != 0 ? data(1, 0x338) : (flags & 16) != 0 ? data(1, 0x339) : "";
        }
        internal string SummaryRelationship(int id, Func<int,int,string> data)
        {
            int player = DiplomacyPlayer();
            return player == 0 || player == id ? data(1, 0x353) : data(0, 0x141 + DLevel(state, DiplomacyFriendship(id, player)));
        }
        internal string SummaryAllianceMonths(int id, Func<int,int,string> data)
        {
            if (SummaryRestricted()) return data(1, 0x353);
            int months = DiplomacyAllianceMonths(id);
            return months == 0 ? data(1, 0x353) : FormatSceneryPanelNumber(data(1, 0x45F), months);
        }
        internal string SummaryCoalitionRole(int id, Func<int,int,string> data)
        {
            int months = Need(state.NativeArmySimulation?.World.Flags165C, "summary.coalition_months");
            int role = DiplomacyCoalitionRole(id);
            return months == 0 || role > 3 ? data(1, 0x353) : data(0, 0x801 + role);
        }
        internal string SummaryCoalitionMonths(int id, Func<int,int,string> data)
        {
            int months = Need(state.NativeArmySimulation?.World.Flags165C, "summary.coalition_months");
            int role = DiplomacyCoalitionRole(id);
            return months == 0 || role > 3 || role == 0 ? data(1, 0x353) : FormatSceneryPanelNumber(data(1, 0x45F), months);
        }
        internal string SummaryPolicyExpansion(int id, Func<int,int,string> data)
        {
            int remaining = Need(rules._force164Inputs.ReadAdditionalDepartmentSlots(state, id), "summary.additional_department_slots");
            var values = state.NativeArmySimulation?.Parameters ?? throw Missing("summary.department_owner");
            for (int group = 0; group < 5; group++)
            {
                string key = BattleRemakeCurrentForcePolicies.DepartmentTrailerKey(id, group);
                if (values.Float32Values.ContainsKey(key) || !values.Int32Values.TryGetValue(key, out int used) || used is < 0 or > 255)
                    throw Missing("summary.department_expansion");
                remaining = unchecked(remaining - used);
            }
            return remaining > 0 ? FormatSceneryPanelNumber(data(1, 0x38C), remaining) : OtherSummaryNumber(remaining);
        }
    }
}
