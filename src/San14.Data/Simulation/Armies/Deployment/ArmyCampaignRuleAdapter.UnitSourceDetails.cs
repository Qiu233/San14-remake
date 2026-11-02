using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read one existing army on opening or changing the formation
    /// preview. No command, random draw, preview Army or world write occurs.</summary>
    public UnitSourceDetails QueryUnitSourceDetails(GameState state, int unitId, bool? landSelected = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var captured = state.Units.ContainsKey(unitId) ? CaptureUnitDetailsCurrent(state) : default;
        return ReadUnitSourceDetails(state, unitId, landSelected, captured.Current, captured.Rules, captured.Missing);
    }

    public UnitSourceDetailsList QueryUnitSourceDetailsList(GameState state, IReadOnlyList<int> unitIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unitIds);
        if (unitIds.Count < 1 || unitIds.Distinct().Count() != unitIds.Count)
            return new(Array.Empty<UnitSourceDetails>(), DetailMissing("list_selection").Message);
        var captured = CaptureUnitDetailsCurrent(state);
        var rows = unitIds.Select(id => ReadUnitSourceDetails(state, id, null,
            captured.Current, captured.Rules, captured.Missing)).ToArray();
        return new(Array.AsReadOnly(rows), captured.Missing);
    }

    private (SourceWorldContext? Current, TacticRemakeCurrentContext? Rules, string? Missing)
        CaptureUnitDetailsCurrent(GameState state)
    {
        try
        {
            var current = CaptureOfficerCommandCapacityCurrent(state);
            //The page reads actual Army receivers, including target Army names.
            //Restore the existing saved receiver inputs once for the publication.
            if (state.ReadArmyLifecycle() is { } lifecycle)
            {
                //The definitions capture owns a fresh local index pool. Bind
                //the saved receiver/logical identities before restoring the
                //trait descriptors, which are indexed by logical UnitId.
                current.OwnedIndexes?.BindWorld(state);
                current.RestoreOwnedArmyReturnSlots(state, lifecycle);
                //The saved Person registry is independent of receiver identity
                //binding. Supply its actual ordered physical view for230C70;
                //an unknown saved registry remains unknown rather than empty.
                if (current.OwnedIndexes is { } indexes)
                    current.SetRegisteredPersonPhysicalSlots(lifecycle.Indexes.Registries
                        .GetValueOrDefault(WorldEntityKind.Officer)?
                        .Select(person => indexes.ToSource(person).PhysicalSlot).ToArray());
            }
            return (current, new(current, _catalog, _map, _terrain, _usage, Revision), null);
        }
        catch (InvalidDataException error) { return (null, null, error.Message); }
    }

    private UnitSourceDetails ReadUnitSourceDetails(GameState state, int unitId, bool? landSelected,
        SourceWorldContext? current, TacticRemakeCurrentContext? rules, string? captureMissing)
    {
        var missing = new Dictionary<string, string>();
        IReadOnlyDictionary<string, string> Missing() => new ReadOnlyDictionary<string, string>(missing);
        if (!state.Units.TryGetValue(unitId, out var unit))
            return new(unitId, false, null, null, new(null, null, null, null), null,
                new(null, null, null), null, null, null, Missing());

        SourceWorldContext Current() => current ?? throw new InvalidDataException(captureMissing!);
        TacticRemakeCurrentContext Rules() => rules ?? throw new InvalidDataException(captureMissing!);
        T? Value<T>(string group, Func<T?> read) where T : struct
        {
            try { return read() ?? throw DetailMissing(group); }
            catch (InvalidDataException error) { missing[group] = error.Message; return null; }
        }
        T? Reference<T>(string group, Func<T?> read) where T : class
        {
            try { return read() ?? throw DetailMissing(group); }
            catch (InvalidDataException error) { missing[group] = error.Message; return null; }
        }
        int? PersonPhysical()
        {
            var context = Current();
            var reference = unit.OfficerId <= 6000 && unit.OfficerId >= 0
                ? context.ReadPersonLookupReference(unit.OfficerId) : context.ReadTransferPersonTableReference(0);
            if (reference is null) throw DetailMissing("commander_pointer");
            return reference.PhysicalSlot;
        }
        bool? nameplateVisible = Value<bool>("identity.nameplate", () => PersonPhysical() is int physical
            ? Current().ReadPersonNativeValidity(physical) : false);
        int Person()
        {
            int physical = PersonPhysical() ?? throw DetailMissing("commander_pointer");
            if (nameplateVisible != true || Current().ReadPersonIdentity(physical) != unit.OfficerId)
                throw DetailMissing("commander_identity");
            return physical;
        }

        // Army detail validity only consumes its own receiver header. Command
        // eligibility additionally requires a valid/deployed commander, which
        // would incorrectly suppress this page's known-invalid placeholder.
        bool? exists = Value<bool>("exists", () =>
        {
            if (unit.NativeArmyOrigin is not { } origin || unit.NativeTacticRuntime is not { } header) return null;
            if (origin.NativeSlotIndex == 0) return false;
            if (origin.NativeSlotIndex is < 0 or > 500) return null;
            return unit.OfficerId != 0 && header.Status10 != 0;
        });
        string? commanderName = nameplateVisible == false ? "" : Reference("identity.commander", () =>
        {
            _ = Person(); return state.Officers.GetValueOrDefault(unit.OfficerId)?.Name;
        });
        int? hex = Value<int>("identity.current_hex", () => ArmyMarchSourceAdapter.ReadCurrentHex(state, unit));
        int? district = Value<int>("identity.district", () =>
        {
            // An actually invalid Person selects the default District receiver.
            // Unknown validity cannot take that known-invalid branch.
            int raw = nameplateVisible == false ? 0 : Current().ReadPersonDistrictSlot(Person())
                ?? throw DetailMissing("commander_district");
            return Current().ReadDistrictTableReference(raw <= 51 ? raw : 0)?.PhysicalSlot;
        });
        int? districtNumber = Value<int>("identity.district_number", () => district is int physical
            ? Current().ReadDistrictNumber(physical) : null);
        int? forceLookup = Value<int>("identity.force_lookup", () =>
        {
            var context = Current();
            int physical = district ?? throw DetailMissing("commander_district");
            bool valid = context.ReadDistrictNativeValidity(physical) ?? throw DetailMissing("district_validity");
            int raw = valid ? context.ReadDistrictForceId(physical) ?? throw DetailMissing("district_force") : 0;
            return raw <= 51 ? raw : 0;
        });
        int? forcePhysical = Value<int>("identity.force_physical", () => forceLookup is int lookup
            ? Current().ReadForceTableReference(lookup)?.PhysicalSlot : null);
        // Domain Forces and their policy owners are keyed by canonical physical
        // identity. Only an external resource lookup consumes ForceLookupId.
        string? forceName = Reference("identity.force_name", () => forcePhysical is int id
            ? state.Forces.GetValueOrDefault(id)?.Name : null);
        var identity = new UnitSourceIdentity(unit.OfficerId, commanderName, hex, forceLookup,
            forcePhysical, forceName, district, districtNumber, nameplateVisible);

        int? land = Value<int>("formation.land", () => ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit));
        int? naval = Value<int>("formation.naval", () => ArmyConfigurationSourceAdapter.ReadNaval(state, unit));
        int? effective = Value<int>("formation.current", () => Rules().EffectiveFormation(state, unit));
        bool? selectedLand = landSelected ?? Value<bool>("formation.selection", () => effective is int id
            ? TacticRemakeCurrentFormations.ReadRawByte(state, id <= 20 ? id : 0, 0x7E) is byte kind ? kind != 2 : null : null);
        var selected = Reference("formation.selected", () =>
        {
            int requested = (selectedLand == true ? land : selectedLand == false ? naval : null)
                ?? throw DetailMissing("selected_formation");
            int id = requested <= 20 ? requested : 0;
            if (id == 0) throw DetailMissing("selected_formation_invalid");
            byte kind = TacticRemakeCurrentFormations.ReadRawByte(state, id, 0x7E)
                ?? throw DetailMissing("selected_formation_kind");
            var definition = state.Formations.GetValueOrDefault(id) ?? throw DetailMissing("selected_formation_name");
            return new DeploymentFormationPresentation(id, definition.Name, kind, ArmyMapUiSources.FormationIcon((byte)id, kind));
        });
        var formation = new UnitSourceFormation(effective, land, naval, selectedLand, selected);

        int? ready = Value<int>("resources.ready", () => unit.NativeArmyOrigin is not null && unit.Troops is >= 0 and <= ushort.MaxValue
            ? unit.Troops : null);
        int? wounded = Value<int>("resources.wounded", () => unit.NativeArmyOrigin is not null && unit.WoundedTroops is >= 0 and <= ushort.MaxValue
            ? unit.WoundedTroops : null);
        int? maximum = Value<int>("resources.morale_maximum", () => forcePhysical is int id ? Rules().ForceMoraleMaximum(state, id) : null);
        int? morale = Value<int>("resources.morale", () => ArmyMoraleSourceAdapter.Read(unit) is byte raw && maximum is int cap
            ? Math.Min((int)raw, cap) : null);
        var resources = new UnitSourceResources(ready, wounded, morale, maximum);
        var abnormal = Reference("status.counts", () => unit.NativeTacticRuntime is { } runtime
            ? new UnitSourceAbnormalCounts(runtime.Abnormal23, runtime.Abnormal24, runtime.Abnormal25) : null);

        bool? cargoApplies = Value<bool>("cargo.applicability", () => unit.NativeTacticRuntime is { } runtime
            ? runtime.Status10 == 2 : null);
        int? gold = null, food = null;
        if (cargoApplies == true)
        {
            // Receiver stores are the live cargo owner; Mission and source city
            // balances are different consumers and cannot replace these values.
            var stores = state.ArmyLifecycle?.Receivers.FindUnit(unit.Id)?.StoredResources;
            gold = Value<int>("cargo.gold", () => stores?.Gold);
            food = Value<int>("cargo.food", () => stores?.Food);
        }
        var cargo = new UnitSourceCargo(cargoApplies, gold, food);
        var portrait = Reference("portrait", () =>
        {
            // The original known-invalid Person branch selects bank9/2198
            // before any live Person appearance or eligibility query. This is
            // a stock placeholder route, never a fabricated valid Person.
            if (nameplateVisible == false)
                return new DeploymentNarrationPortraitRoute(2198, 2198,
                    "texture_chara_anime_2198", ["face_2198_b", "face_2198_a", "face_2198_c"])
                    { Kind = StandingPortraitKind.ThreePart };
            int physical = Person();
            ushort appearance = Current().ReadPersonPortraitAppearance(physical) ?? throw DetailMissing("portrait_appearance");
            return ReadNarrationPortrait(state, Current(), _definitions, unit.OfficerId, appearance);
        });
        missing["axes.without_modifiers"] = "unit.details.existing_army_axes_without_modifiers";
        missing["axes.with_modifiers"] = "unit.details.existing_army_axes_with_modifiers";
        var formatted = new Dictionary<int, string>();
        var palettes = new Dictionary<int, int>();
        void Text(int node, string group, Func<string> read)
        {
            if (Reference(group, read) is string value) formatted[node] = value;
        }
        Text(17, "format.home", () => ReadUnitDetailHome(state, Current(), unit));
        Text(116, "format.command", () => ReadUnitDetailAction(state, Current(), unit));
        Text(121, "format.target", () => ReadUnitDetailTarget(state, Current(), unit, false));
        Text(126, "format.temporary_target", () => ReadUnitDetailTarget(state, Current(), unit, true));
        Text(131, "format.construction", () => ReadUnitDetailConstruction(state, Current(), unit));
        Text(136, "format.captives", () => ReadUnitDetailCaptiveCount(state, Current(), unit));
        foreach (int node in new[] { 142, 147, 152, 157 })
            Text(node, "format.preference." + node, () => ReadUnitDetailPreference(state, unit, node));
        if (unit.NativeTacticRuntime is { } status)
        {
            foreach (var row in new[] { (Text: 73, Art: 72, Unit: 74, Count: status.Abnormal25),
                (Text: 78, Art: 77, Unit: 79, Count: status.Abnormal23),
                (Text: 83, Art: 82, Unit: 84, Count: status.Abnormal24) })
            {
                Text(row.Text, "format.status." + row.Text,
                    () => ReadUnitDetailAbnormal(state, Current(), unit, row.Count));
                Text(row.Unit, "format.status_unit." + row.Unit, () => UnitDetailData(1, 0x469));
                palettes[row.Art] = row.Count != 0 ? 12 : 45;
            }
        }
        else missing["format.status"] = "unit.details.current_abnormal_counters";
        if (Value<int>("supply.icon", () => ReadUnitDetailSupplyPalette(state, Current(), unit)) is int supplyPalette)
            palettes[57] = supplyPalette;
        return new(unitId, exists, identity, formation, resources, abnormal, cargo, portrait, null, null, Missing())
        {
            FormattedValues = new ReadOnlyDictionary<int, string>(formatted),
            NodePalettes = new ReadOnlyDictionary<int, int>(palettes)
        };
    }
    private static InvalidDataException DetailMissing(string field) => new("unit.details." + field);
}
