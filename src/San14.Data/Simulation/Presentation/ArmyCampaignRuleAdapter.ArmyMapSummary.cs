using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryArmyMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "", null, new Dictionary<int, string>());
        try
        {
            if (!SummaryArmyNeed(current.ReadArmyUnitNativeValidity(new(receiver.ReceiverId)), "validity"))
                return result with { ReceiverValid = false };
            var unit = SummaryArmyUnit(state, receiver.ReceiverId);
            byte status = unit.NativeTacticRuntime?.Status10 ?? throw SummaryArmyMissing("status10");
            if (status is not (1 or 2))
                return result with { ReceiverValid = true, UnresolvedOwner = "map-summary.army.ordinary_status10" };
            var child = SummaryArmyBasic(state, current, receiver, unit, status == 2);
            //917610 explicitly initializes Damage0180 to null. The ordinary
            //manager only supplies the first Army receiver; no comparison
            //caller installs the second Army. Its9283A0 branch is hidden.
            return result with { ReceiverValid = true, Children = [new("basic", null, child)] };
        }
        catch (InvalidDataException error) { return result with { UnresolvedOwner = error.Message }; }
    }

    private MapSummaryPage SummaryArmyBasic(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, UnitState unit, bool transport)
    {
        var text = new Dictionary<int, string>(); var presets = new Dictionary<int, int>();
        var visible = new Dictionary<int, bool>(); var issues = new Dictionary<int, string>();
        var colors = new Dictionary<int, MapCornerColors>(); var children = new List<MapSummaryChild>();
        var bars = new List<MapSummaryBar>(); var radars = new List<MapSummaryRadar>();
        var layout = transport ? "00_82_03_00_sum_butai_trs" : "00_82_03_01_sum_butai_btl";
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, layout, true, text)
        { TextPresets = presets, Visibility = visible, Colors = colors, Children = children,
            Bars = bars, Radars = radars, UnresolvedFields = issues };
        var rules = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain, _usage, Revision);
        var details = SummaryArmyScalars(state, current, unit, rules);
        void Read(int node, Action write)
        { try { write(); } catch (InvalidDataException error) { issues[node] = error.Message; } }
        void Child(string key, int mount, Func<MapSummaryPage> read)
            => Read(mount, () => children.Add(new(key, mount, read())));
        int nIdentity = transport ? 8 : 10, nIcon = transport ? 17 : 19;
        Child("identity", nIdentity, () =>
        {
            var force = current.ReadArmyUnitForceReference(new(receiver.ReceiverId)) ?? throw SummaryArmyMissing("identity.force");
            int person = SummaryArmyCommander(current, receiver.ReceiverId);
            int district = SummaryArmyNeed(current.ReadPersonNativeValidity(person), "identity.commander_validity")
                ? SummaryArmyNeed(current.ReadPersonDistrictSlot(person), "identity.districtE0") : 0;
            var districtPointer = current.ReadDistrictTableReference(district <= 51 ? district : 0)
                ?? throw SummaryArmyMissing("identity.district_pointer");
            int? number = SummaryArmyNeed(current.ReadDistrictNativeValidity(districtPointer), "identity.district_validity")
                ? SummaryArmyNeed(current.ReadDistrictNumber(SummaryArmyNeed(districtPointer.PhysicalSlot, "district_receiver")), "identity.district_number") : 0;
            byte mark = unit.NativeMapUiByte13C ?? state.ReadArmyLifecycle()?.ReceiverSlots()[receiver.ReceiverId].MapUiByte13C
                ?? throw SummaryArmyMissing("identity.Army13C");
            return SummaryCityIdentity(current, receiver, force,
                ReadHistoryArmyName(state, current, new(receiver.ReceiverId)), number, mark is >= 1 and <= 50);
        });
        Child("formation", nIcon, () => SummaryArmyFormation(state, current, receiver, unit, details));
        Child("troops", transport ? 29 : 31, () => SummaryCityNumber(receiver, 0x19C, 0x20E,
            details.Resources.ReadyTroops?.ToString(CultureInfo.InvariantCulture)));
        Child("morale", transport ? 34 : 36, () => SummaryCityGauge(receiver, 0x1E2, 0x225,
            details.Resources.CurrentMorale?.ToString(CultureInfo.InvariantCulture), details.Resources.CurrentMorale,
            details.Resources.MaximumMorale));
        if (transport)
        {
            Child("gold", 35, () => SummaryCityNumber(receiver, 0x1C1, 0x20C,
                details.Cargo.Gold?.ToString(CultureInfo.InvariantCulture)));
            Child("food", 40, () => SummaryCityNumber(receiver, 0x1A0, 0x20D,
                details.Cargo.Food?.ToString(CultureInfo.InvariantCulture)));
        }
        Child("status", 45, () =>
        {
            var runtime = unit.NativeTacticRuntime ?? throw SummaryArmyMissing("status.runtime");
            var values = new Dictionary<int, int?> { [3] = runtime.Abnormal25 != 0 ? 12 : 45,
                [4] = runtime.Abnormal23 != 0 ? 12 : 45, [5] = runtime.Abnormal24 != 0 ? 12 : 45 };
            try { values[6] = ReadUnitDetailSupplyPalette(state, current, unit); }
            catch (InvalidDataException error) { issues[45] = error.Message; values[6] = null; }
            values[7] = SummaryArmyFire(state, unit);
            return SummaryCityStatus(receiver, values);
        });
        if (transport) return result;
        //The battle summary does not update an excluded Army's statistics.
        Read(53, () =>
        {
            if (SummaryArmyNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, receiver.ReceiverId), "excluded233560"))
                throw SummaryArmyMissing("excluded_statistics_not_updated");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            var axes = ReadUnitSourceAxesCurrent(state, unit.Id, null, current, rules, abilities, null,
                abilityPanelReader: (force, effect) => current.BaseForceModifierReader?.Invoke(new(force), effect));
            int[] labels = [56, 57, 58, 60, 59], nodes = [61, 62, 63, 65, 64];
            int[] captions = [0x22F, 0x22C, 0x22D, 0x230, 0x22E];
            int?[] before = [axes.WithoutConditionalModifiers.Mobility, axes.WithoutConditionalModifiers.FieldAttack,
                axes.WithoutConditionalModifiers.SiegeTroopAttack, axes.WithoutConditionalModifiers.WallAttack,
                axes.WithoutConditionalModifiers.Defense];
            int?[] after = [axes.WithConditionalModifiers.Mobility, axes.WithConditionalModifiers.FieldAttack,
                axes.WithConditionalModifiers.SiegeTroopAttack, axes.WithConditionalModifiers.WallAttack,
                axes.WithConditionalModifiers.Defense];
            int?[] signed = axes.SignedModifiers is { } m
                ? [m.Mobility, m.FieldAttack, m.SiegeTroopAttack, m.WallAttack, m.Defense] : [null, null, null, null, null];
            for (int index = 0; index < 5; index++)
            {
                text[labels[index]] = ReadHistoryDataText(1, captions[index]);
                if (after[index] is int value) text[nodes[index]] = value.ToString(CultureInfo.InvariantCulture);
                else issues[nodes[index]] = axes.Missing.Values.FirstOrDefault() ?? "map-summary.army.axis";
                if (before[index] is int a && after[index] is int b) presets[nodes[index]] = b > a ? 15 : b < a ? 16 : 0;
                if (signed[index] is int modifier) presets[labels[index]] = modifier > 0 ? 73 : modifier < 0 ? 74 : 13;
            }
            text[41] = ReadHistoryDataText(1, 0x234);
            if (after.All(value => value.HasValue)) radars.Add(new(53, after.Select(value => value!.Value).ToArray()));
            foreach (var pair in axes.Missing) issues.TryAdd(53, pair.Value);
        });
        Read(39, () =>
        {
            //2314A0(selector0)'s display denominator, independently of the
            //business activation's InitialWaiting40. Offline CE TC data100.
            const int maximum = 100;
            bars.Add(new MapSummaryBar(39, maximum - (unit.NativeTacticRuntime ?? throw SummaryArmyMissing("waiting.runtime")).Waiting20, maximum)
                { InitialCurrent = 70, InitialMaximum = 100 });
            visible[40] = false;
        });
        Read(42, () =>
        {
            var tactics = SummaryArmyTactics(state, current, unit, details.Formation?.LandSelected);
            byte[] waiting = unit.NativeTacticRuntime is { } r ? [r.Waiting20, r.Waiting21, r.Waiting22]
                : throw SummaryArmyMissing("tactic.waiting");
            for (int index = 0; index < 3; index++)
            {
                int node = 42 + index; visible[node] = index < tactics.Count;
                if (index >= tactics.Count) continue;
                text[node] = tactics[index];
                var color = waiting[index] == 0 ? new AreaMarkerColor(0xFFFFFFFF, 0xFFFFFFFF) : new AreaMarkerColor(0xFF808080, 0xFF808080);
                colors[node] = new(color, color, color, color);
            }
        });
        return result;
    }

    private MapSummaryPage SummaryArmyFormation(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, UnitState unit, UnitSourceDetails details)
    {
        var text = new Dictionary<int, string>(); var visible = new Dictionary<int, bool>();
        var issues = new Dictionary<int, string>(); var portraits = new List<MapSummaryPortrait>();
        var images = new List<MapSummaryImage>(); var colors = new Dictionary<int, MapCornerColors>();
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "00_82_03_sum_butai_offset_01", true, text)
            { Visibility = visible, UnresolvedFields = issues, Portraits = portraits, Images = images, Colors = colors };
        try
        {
            int person = SummaryArmyCommander(current, receiver.ReceiverId);
            if (SummaryArmyNeed(current.ReadPersonNativeValidity(person), "formation.commander_validity"))
            {
                ushort appearance = SummaryArmyNeed(current.ReadPersonPortraitAppearance(person), "formation.portrait");
                portraits.Add(new(1, ReadOfficerPortraitAppearance(state, current, _definitions, unit.OfficerId,
                    appearance, ArmyMapUiSources.Create())));
            }
            else visible[1] = false;
            var formation = details.Formation?.Selected ?? throw SummaryArmyMissing("formation.current");
            images.Add(new(4, ArmyMapUiSources.Create().IconName(formation.Icon),
                formation.Icon.SpriteId == 0x3F0 ? "texture_pk_common" : null));
            colors[4] = MapSpritePalettes.Create()[21];
            var classification = DeploymentTroopClassificationSource.Read(state, current, person, formation.FormationId, unit.Troops);
            bool[] mask = classification.Marker switch
            {
                DeploymentTroopMarker.Default => [true, true, true, false, false],
                DeploymentTroopMarker.AtSecondaryThreshold => [true, true, true, true, true],
                _ => [true, false, false, false, false]
            };
            for (int index = 0; index < 5; index++) visible[7 + index] = mask[index];
            int hex = SummaryArmyNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, unit), "formation.Hex2A");
            var pointer = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw SummaryArmyMissing("formation.hex_pointer");
            int areaId = SummaryArmyNeed(current.ReadHexAreaSlot(pointer), "formation.area34");
            var area = current.ReadAreaTableReference(areaId < 501 ? areaId : 0) ?? throw SummaryArmyMissing("formation.area_pointer");
            text[12] = state.Areas.GetValueOrDefault(SummaryArmyNeed(area.PhysicalSlot, "formation.area_receiver"))?.Name
                ?? throw SummaryArmyMissing("formation.area_name");
        }
        catch (InvalidDataException error)
        {
            issues[4] = error.Message;
            if (portraits.Count == 0 && !visible.ContainsKey(1))
            { visible[1] = false; issues[1] = error.Message; }
        }
        return result;
    }

    private IReadOnlyList<string> SummaryArmyTactics(GameState state, SourceWorldContext current,
        UnitState unit, bool? landSelected)
    {
        bool land = landSelected ?? throw SummaryArmyMissing("tactic.formation_selection");
        var result = new List<string>(3);
        void Add(TacticReference tactic)
        {
            if (!SummaryArmyNeed(current.ReadSkillTacticValidity(tactic), "tactic.validity")) return;
            int id = SummaryArmyNeed(current.ReadSkillTacticPhysicalSlot(tactic), "tactic.receiver");
            result.Add(state.Tactics.GetValueOrDefault(id)?.Name ?? throw SummaryArmyMissing("tactic.name"));
        }
        if (land)
        {
            if (unit.TacticIds is not { Length: 3 } slots) throw SummaryArmyMissing("tactic.slots");
            foreach (int raw in slots) Add(current.ReadSkillTacticReference(raw is >= 0 and <= 200 ? raw : 0)
                ?? throw SummaryArmyMissing("tactic.pointer"));
        }
        else
        {
            int person = SummaryArmyCommander(current, SummaryArmyNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "tactic.army_receiver"));
            int identity = SummaryArmyNeed(current.ReadPersonIdentity(person), "tactic.commander_identity");
            int naval = SummaryArmyNeed(ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "tactic.naval_formation");
            for (int index = 0; index < 10 && result.Count < 3; index++)
            {
                var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current, identity, person, index);
                if (!SummaryArmyNeed(current.ReadSkillTacticValidity(tactic), "tactic.validity")) continue;
                uint mask = 0;
                for (int part = 0; part < 4; part++) mask |= (uint)SummaryArmyNeed(current.ReadSkillTacticRawByte(tactic, 0x6C + part), "tactic.naval_mask") << (part * 8);
                if (naval < 21 && (mask & (1u << naval)) != 0) Add(tactic);
            }
        }
        return result;
    }

    private static UnitState SummaryArmyUnit(GameState state, int physical)
    {
        var slots = state.ReadArmyLifecycle()?.ReceiverSlots() ?? throw SummaryArmyMissing("lifecycle");
        if ((uint)physical >= (uint)slots.Length) throw SummaryArmyMissing("receiver_range");
        var slot = slots[physical];
        return slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw SummaryArmyMissing("unit_owner")
            : slot.PendingImage ?? throw SummaryArmyMissing("pending_image");
    }

    private UnitSourceDetails SummaryArmyScalars(GameState state, SourceWorldContext current, UnitState unit,
        TacticRemakeCurrentContext rules)
    {
        var missing = new Dictionary<string, string>();
        T? Read<T>(string key, Func<T> read) where T : struct
        { try { return read(); } catch (InvalidDataException error) { missing[key] = error.Message; return null; } }
        int? effective = Read("formation", () => SummaryArmyNeed(rules.EffectiveFormation(state, unit), "formation.effective"));
        byte? kind = Read("formation.kind", () => SummaryArmyNeed(TacticRemakeCurrentFormations.ReadRawByte(state,
            SummaryArmyNeed(effective, "formation.effective"), 0x7E), "formation.kind"));
        DeploymentFormationPresentation? selected = null;
        if (effective is int id && kind is byte raw && state.Formations.GetValueOrDefault(id) is { } definition)
            selected = new(id, definition.Name, raw, ArmyMapUiSources.FormationIcon(checked((byte)id), raw));
        int? maximum = Read("morale.maximum", () => rules.ForceMoraleMaximum(state,
            SummaryArmyNeed(current.ReadArmyUnitForceReference(new(SummaryArmyNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "receiver")))?.PhysicalSlot, "force"))
            ?? throw SummaryArmyMissing("morale.maximum"));
        int? morale = Read("morale", () => Math.Min(SummaryArmyNeed(ArmyMoraleSourceAdapter.Read(unit), "morale"),
            SummaryArmyNeed(maximum, "morale.maximum")));
        bool cargo = unit.NativeTacticRuntime?.Status10 == 2;
        var stores = cargo ? state.ArmyLifecycle?.Receivers.FindUnit(unit.Id)?.StoredResources : null;
        return new(unit.Id, true, null, new(effective, ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit),
            ArmyConfigurationSourceAdapter.ReadNaval(state, unit), kind is byte k ? k != 2 : null, selected),
            new(unit.Troops, unit.WoundedTroops, morale, maximum), null,
            new(cargo, stores?.Gold, stores?.Food), null, null, null, missing);
    }
    private static int SummaryArmyCommander(SourceWorldContext current, int physical)
    {
        int id = SummaryArmyNeed(current.ReadArmyUnitCommanderId(new(physical)), "commander12");
        return SummaryArmyNeed((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "commander_pointer");
    }
    private static int? SummaryArmyFire(GameState state, UnitState unit)
    {
        int? fire = ArmySavedCommandKnowledge.ReadControl(state, unit);
        return fire is int value ? value != 0 ? 12 : 45 : null;
    }
    private static T SummaryArmyNeed<T>(T? value, string field) where T : struct => value ?? throw SummaryArmyMissing(field);
    private static InvalidDataException SummaryArmyMissing(string field) => new("map-summary.army." + field);
}
