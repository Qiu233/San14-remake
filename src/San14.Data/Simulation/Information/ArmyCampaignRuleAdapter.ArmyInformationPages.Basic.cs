using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ArmyInformationCurrent
    {
        private CityDomesticPersonCellSource? basicArmyPersonCells;
        private readonly Dictionary<int, CityDomesticPersonForceName?> basicArmyForceNames = [];
        private Dictionary<int, int>? basicArmyCaptiveLocations;

        private CityDomesticPersonCellSource BasicArmyPersonCells => basicArmyPersonCells ??= new(state, Current,
            rules._definitions, rules._governorInputs, rules._scenario, rules._repository, rules.Seed.Language);

        private PreparedCell BasicCell(Unit unit, int column) => column switch
        {
            0 => MakeCell(() => BasicArmyValid(unit) ? rules.ReadHistoryArmyName(state, Current, new(unit.PhysicalSlot)) : "",
                () => new NameKey(BasicPersonNameKey(BasicCommander(unit)))),
            1 => BasicArmyForceCell(unit),
            2 => MakeCell(() => BasicDistrictText(unit), () => new NumberKey(BasicDistrictNumber(unit) * 100000)),
            3 => MakeCell(() => rules.ReadUnitDetailHome(state, Current, unit.Owner), () => BasicHomeKey(unit)),
            4 => MakeCell(() => Number(BasicReady(unit)), () => new NumberKey(BasicReady(unit))),
            5 => MakeCell(() => Number(BasicWounded(unit)), () => new NumberKey(BasicWounded(unit))),
            6 => MakeCell(() => Number(BasicCaptives(unit)), () => new NumberKey(BasicCaptives(unit))),
            7 or 8 => MakeCell(() => BasicCargo(unit, column == 7) is int value ? Number(value) : "",
                () => new NumberKey(BasicCargo(unit, column == 7) ?? -1)),
            9 => MakeCell(() => Number(BasicMorale(unit)), () => new NumberKey(BasicMorale(unit))),
            10 => MakeCell(() => Data(1, BasicSupplied(unit) ? 0x338 : 0x339),
                () => new NumberKey(BasicSupplied(unit) ? 1 : 0)),
            >= 11 and <= 13 => MakeCell(() => rules.ReadUnitDetailAbnormal(state, Current, unit.Owner,
                    BasicAbnormal(unit, column)), () => new NumberKey(BasicAbnormalKey(unit, column))),
            14 => MakeCell(() => Data(1, (BasicRuntime(unit).Flags26 & 1) != 0 ? 0x338 : 0x339),
                () => new NumberKey(BasicRuntime(unit).Flags26 & 1)),
            42 => MakeCell(() => Data(1, BasicMapUiActive(unit) ? 0x338 : 0x339),
                () => new NumberKey(BasicMapUiActive(unit) ? 1 : 0)),
            _ => throw Missing("basic.column." + column)
        };

        private bool BasicArmyValid(Unit unit) => Need(Current.ReadArmyUnitNativeValidity(new(unit.PhysicalSlot)),
            "basic.Army_validity");

        private int BasicCommander(Unit unit)
        {
            int raw = Need(Current.ReadArmyUnitCommanderId(new(unit.PhysicalSlot)), "basic.Army12_commander");
            return Need((raw <= 6000 ? Current.ReadPersonLookupReference(raw)
                : Current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "basic.commander_lookup");
        }

        //231080 has no native-validity gate: read the current two name-key
        //parts even for the real placeholder Person, never the display name.
        private string BasicPersonNameKey(int physical)
        {
            int identity = Need(Current.ReadPersonIdentity(physical), "basic.name_key_Person10");
            if (state.Officers.TryGetValue(identity, out var owner))
                return owner.CurrentNameSortKey ?? throw Missing("basic.current_name_key34_5A");
            if (physical != 0) throw Missing("basic.name_key_current_Person_owner");
            var rows = rules._scenario.Snapshot.Characters;
            if (rows.Count == 0 || rows[0].Slot != 0) throw Missing("basic.Person0_definition");
            return rows[0].NameSortKey ?? throw Missing("basic.Person0_name_key34_5A");
        }

        private ForceReference BasicForce(Unit unit) => Current.ReadArmyUnitForceReference(new(unit.PhysicalSlot))
            ?? throw Missing("basic.Army_Force");

        private PreparedCell BasicArmyForceCell(Unit unit)
        {
            CityDomesticPersonForceName? Name()
            {
                if (!basicArmyForceNames.TryGetValue(unit.Id, out var value))
                {
                    var force = BasicForce(unit);
                    if (!Need(Current.ReadForceNativeValidity(force), "basic.force_name_validity"))
                    { basicArmyForceNames.Add(unit.Id, null); return null; }
                    int physical = BasicCommander(unit);
                    var personForce = Current.ReadPersonForce(physical)
                        ?? throw Missing("basic.force_name_Person_Force");
                    //22C990 validates the commander before resolving District;
                    //the reusable Person formatter does not. Reuse only the
                    //same actual Force receiver, including that invalid branch.
                    if (Need(Current.ReadForcePhysicalSlot(force), "basic.force_name_Army_Force_receiver") !=
                        Need(Current.ReadForcePhysicalSlot(personForce), "basic.force_name_Person_Force_receiver"))
                        throw Missing("basic.force_name_Army_Person_Force_receiver_mismatch");
                    int identity = Need(Current.ReadPersonIdentity(physical), "basic.force_name_Person10");
                    basicArmyForceNames.Add(unit.Id, value = BasicArmyPersonCells.ForceName(new(checked((ushort)identity), physical)));
                }
                return value;
            }
            var cell = MakeCell(() => Name()?.Text ?? "", () =>
            {
                var force = BasicForce(unit);
                if (!Need(Current.ReadForceNativeValidity(force), "basic.force_key_validity")) return new NameKey("", true);
                int physical = Need(Current.ReadForcePhysicalSlot(force), "basic.force_key_receiver");
                int raw = Need(Current.ReadForceRecord(physical)?.RawRuler10, "basic.force_key_ruler10");
                int person = Need((raw <= 6000 ? Current.ReadPersonLookupReference(raw)
                    : Current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "basic.force_key_ruler_lookup");
                return new NameKey(BasicPersonNameKey(person));
            });
            //22E7A0's inline marker has a real Force49 fill. This is separate
            //from the descriptor's unused dynamic text-palette callbacks.
            try { return cell with { Display = cell.Display with { MarkerColour = Name()?.MarkerColour } }; }
            catch (InvalidDataException error)
            {
                return cell with { Display = cell.Display with
                { UnresolvedOwner = cell.Display.UnresolvedOwner is null ? error.Message : cell.Display.UnresolvedOwner + ";" + error.Message } };
            }
        }

        private DistrictReference BasicDistrict(Unit unit)
        {
            int person = BasicCommander(unit);
            int raw = Need(Current.ReadPersonNativeValidity(person), "basic.district_commander_validity")
                ? Need(Current.ReadPersonDistrictSlot(person), "basic.commander_DistrictE0") : 0;
            return Current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw Missing("basic.District_lookup");
        }

        private int BasicDistrictNumber(Unit unit) => Need(Current.ReadDistrictNumber(
            Need(BasicDistrict(unit).PhysicalSlot, "basic.District_receiver")), "basic.District_number11");

        private string BasicDistrictText(Unit unit) => Need(Current.ReadDistrictNativeValidity(BasicDistrict(unit)),
            "basic.District_validity") ? Data(1, 0x494).Replace("%d", Number(BasicDistrictNumber(unit)), StringComparison.Ordinal) : "";

        private int BasicHomeDescriptor(Unit unit)
        {
            int person = BasicCommander(unit);
            return Need(Current.ReadPersonNativeValidity(person), "basic.Home_commander_validity")
                ? Need(Current.ReadPersonFirstLocation(person), "basic.Home_E2") : 0;
        }

        private SortKey BasicHomeKey(Unit unit)
        {
            int raw = BasicHomeDescriptor(unit);
            int kind = raw is >= 52 and <= 61 ? 6 : 5; //22A6C0.
            //353890 compares22A730's registered Base secondary text, not the
            //numeric descriptor key345430 or visible name355F80.
            var home = BaseLookup.Find(raw, Current);
            int physical = Need(home.PhysicalSlot, "basic.Home_sort_Base_receiver");
            string key;
            if (home.Kind == BaseKind.City)
                key = rules.ReadCityDetailNameSortKeyCurrent(Current, physical);
            else
            {
                var rows = rules._scenario.Snapshot.Gates;
                if ((uint)physical >= (uint)rows.Count || rows[physical].Slot != physical)
                    throw Missing("basic.Home_sort_Gate_definition");
                key = rows[physical].SecondaryText ?? throw Missing("basic.Home_sort_Gate_secondary_text");
            }
            return new HomeKey(kind, key);
        }

        private sealed record HomeKey(int Kind, string SecondaryText) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (HomeKey)other;
                if (!ascending) return right.Before(this, true);
                return Kind != right.Kind ? Kind < right.Kind
                    : PersonNameOrder.Compare(SecondaryText, right.SecondaryText) < 0;
            }
        }

        private static NativeArmyTacticRuntime BasicRuntime(Unit unit) => unit.Owner.NativeTacticRuntime
            ?? throw Missing("basic.Army_runtime");

        private static int BasicReady(Unit unit) => unit.Owner.NativeArmyOrigin is not null && unit.Owner.Troops is >= 0 and <= ushort.MaxValue
            ? unit.Owner.Troops : throw Missing("basic.Army16_ready");
        private static int BasicWounded(Unit unit) => unit.Owner.NativeArmyOrigin is not null && unit.Owner.WoundedTroops is >= 0 and <= ushort.MaxValue
            ? unit.Owner.WoundedTroops.Value : throw Missing("basic.Army18_wounded");

        private int BasicCaptives(Unit unit)
        {
            if (basicArmyCaptiveLocations is null)
            {
                var locations = new Dictionary<int, int>();
                //230C70's actual Person100 chain and status6/E4 predicate,
                //aggregated once for this immutable page-set capture.
                foreach (int? entry in Current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw Missing("basic.captives_Person_registry100"))
                {
                    int person = Need(entry, "basic.captives_Person_pointer");
                    if (Need(Current.ReadPersonStatus(person), "basic.captives_PersonE6") != 6) continue;
                    int at = Need(Current.ReadPersonSecondLocation(person), "basic.captives_PersonE4");
                    locations[at] = locations.GetValueOrDefault(at) + 1;
                }
                basicArmyCaptiveLocations = locations;
            }
            int location = unit.PhysicalSlot is >= 1 and <= 500 ? unit.PhysicalSlot + 61 : 0;
            return basicArmyCaptiveLocations.GetValueOrDefault(location);
        }

        private int? BasicCargo(Unit unit, bool gold)
        {
            if (!BasicArmyValid(unit) || BasicRuntime(unit).Status10 != 2) return null;
            var slots = (state.ReadArmyLifecycle() ?? throw Missing("basic.cargo_lifecycle")).ReceiverSlots();
            if ((uint)unit.PhysicalSlot >= (uint)slots.Count) throw Missing("basic.cargo_receiver");
            var stores = slots[unit.PhysicalSlot].StoredResources;
            return Need(gold ? stores.Gold : stores.Food, gold ? "basic.Army5C_cargo_gold" : "basic.Army60_cargo_food");
        }

        private int BasicMorale(Unit unit)
        {
            int force = Need(Current.ReadForcePhysicalSlot(BasicForce(unit)), "basic.morale_Force_receiver");
            int maximum = Need(Rules.ForceMoraleMaximum(state, force), "basic.morale_maximum");
            return Math.Min(Need(ArmyMoraleSourceAdapter.Read(unit.Owner), "basic.Army1A_morale"), maximum);
        }

        private bool BasicSupplied(Unit unit)
        {
            int raw = BasicHomeDescriptor(unit);
            var basis = (raw is >= 1 and <= 51 ? Current.ReadCityTableReference(raw)
                : raw is >= 52 and <= 61 ? Current.ReadGateTableReference(raw - 51)
                : Current.ReadCityTableReference(0)) ?? throw Missing("basic.supply_Base_lookup");
            if (Need(Current.ReadBaseNativeValidity(basis), "basic.supply_Base_validity") &&
                Current.ReadBaseCurrentHexIndex(basis) == Need(Current.ReadArmyUnitRawHex2A(new(unit.PhysicalSlot)), "basic.supply_Army2A"))
                return true;
            return (BasicRuntime(unit).Flags26 & 0x42) == 0x42;
        }

        private static byte BasicAbnormal(Unit unit, int column)
        {
            var value = BasicRuntime(unit);
            return column switch { 11 => value.Abnormal25, 12 => value.Abnormal23, 13 => value.Abnormal24,
                _ => throw Missing("basic.abnormal_column") };
        }

        private int BasicAbnormalKey(Unit unit, int column)
        {
            byte value = BasicAbnormal(unit, column);
            if (value == 0) return -1; //Numeric tag0/-1, not an empty tag2.
            return rules.UnitDetailCommandVisible(state, Current, unit.Owner) ? (value + 3) / 4 : 0;
        }

        private bool BasicMapUiActive(Unit unit)
        {
            if (unit.Owner.NativeMapUiByte13C is byte known) return known != 0;
            var slots = (state.ReadArmyLifecycle() ?? throw Missing("basic.Army13C_lifecycle")).ReceiverSlots();
            if ((uint)unit.PhysicalSlot >= (uint)slots.Count) throw Missing("basic.Army13C_receiver");
            return Need(slots[unit.PhysicalSlot].MapUiByte13C, "basic.Army13C") != 0;
        }
    }
}
