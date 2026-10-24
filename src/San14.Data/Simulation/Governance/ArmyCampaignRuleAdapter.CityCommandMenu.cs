using San14.Core;
using Command = San14.Data.CityCommandMenuCommand;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Finite source admission slices for the actual City tree, without opening an editor.</summary>
    public AreaAdministrationQuery<CityCommandMenuProjection> QueryCityCommandMenu(
        GameState state, int cityId, int? settledCameraLevel)
    {
        try { return AreaAdministrationQuery<CityCommandMenuProjection>.Known(new CityMenuRead(this, state, cityId, settledCameraLevel).Read()); }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<CityCommandMenuProjection>.Unknown(missing.Message); }
    }

    private sealed class CityMenuRead
    {
        private readonly ArmyCampaignRuleAdapter _adapter;
        private readonly GameState _state;
        private readonly int _cityId;
        private readonly int? _camera;
        private readonly SourceWorldContext _current;
        private readonly BaseReference _city;
        private bool _indexesReady, _locationsReady, _associationReady;
        private AreaAdministrationQuery<(int Count, int Books)>? _areaInputs;

        internal CityMenuRead(ArmyCampaignRuleAdapter adapter, GameState state, int cityId, int? camera)
        {
            _adapter = adapter; _state = state; _cityId = cityId; _camera = camera;
            if (cityId is < 1 or > 51 || !state.Cities.TryGetValue(cityId, out var city) || city.Id != cityId)
                throw Missing("city_domain_identity");
            _current = OwnedArmyReturnCurrentFields.CaptureCityMenuCurrent(state, adapter._definitions);
            _city = _current.ReadCityTableReference(cityId) ?? throw Missing("city_pointer");
            if (_city.PhysicalSlot != cityId) throw Missing("city_physical_identity");
        }

        internal CityCommandMenuProjection Read()
        {
            // Entry performs this discarded virtual before its City-valid gate.
            _ = District("entry_discarded_district_pointer");
            if (!K(_current.ReadBaseNativeValidity(_city), "entry_city_validity")) return Absent();
            var district = District("presence_district_pointer");
            bool primary = false;
            if (K(_current.ReadDistrictNativeValidity(district), "presence_district_validity"))
            {
                _current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(_state, _current);
                primary = BaseCaptureRewardSource.ReadPlayerRulerDistrict(_current, district, Missing);
            }
            if (!primary && !SecondaryPresent()) return Absent();
            var common = Evaluate(() =>
            {
                if (!K(_current.ReadBaseNativeValidity(_city), "updater_city_validity"))
                    throw Missing("updater_not_applied");
                var at = District("updater_district_pointer");
                if (!K(_current.ReadDistrictNativeValidity(at), "updater_district_validity"))
                    throw Missing("updater_not_applied");
                _ = District("retained_district_pointer");
                int level = K(_camera, "settled_camera_level");
                if (level is < 0 or > 6) throw Missing("settled_camera_level_range");
                return level <= 5;
            });
            AreaAdministrationQuery<bool> ReadEnabled(Func<bool> read) => common.Available ? Evaluate(read) : common;
            bool Zoom() => common.Value;
            if (!primary)
                return new(CityCommandMenuBranch.Secondary, Array.AsReadOnly(new[]
                {
                    Entry(Command.Transport, null, 0x25, ReadEnabled(() => Transport() && Zoom())),
                    Entry(Command.CityInformation, null, 0x27, common.Available ? Known(true) : common),
                    Entry(Command.AreaInformation, null, 0x5E, common.Available ? Known(true) : common)
                }));

            var deploy = ReadEnabled(() => Deploy() && Zoom());
            var cancel = ReadEnabled(() => Cancel() && Zoom());
            var transfer = ReadEnabled(() => Movement(OfficerMoveMode.Dispatch));
            var summon = ReadEnabled(() => Movement(OfficerMoveMode.Summon));
            var transport = ReadEnabled(() => Transport() && Zoom());
            var merchant = ReadEnabled(Merchant);
            var investment = common.Available ? Unknown("investment_external_mode") : common;
            // These are separate source slices. An unavailable command does
            // not turn the whole structural projection into known absence.
            var area = ReadEnabled(() =>
            {
                var inputs = AreaInputs();
                if (inputs.Count == 0) return false;
                int cost = K(PersonAssignmentPrecallInputs.ReadAreaAppointmentCost(_state), "area_command_cost");
                return inputs.Books >= cost && Zoom();
            });
            var domestic = ReadEnabled(() =>
            {
                var inputs = AreaInputs();
                return inputs.Count != 0 && inputs.Books != 0 && Zoom();
            });
            var information = common.Available ? Known(true) : common;
            return new(CityCommandMenuBranch.Primary, Array.AsReadOnly(new[]
            {
                Entry(Command.Deploy, null, 0x223, deploy),
                Entry(Command.CancelDeployment, null, 0x1D, cancel),
                Entry(Command.Administration, null, 0x1E, Or(area, domestic)),
                Entry(Command.AreaAdministration, Command.Administration, 0x4B7, area),
                Entry(Command.CityDomestic, Command.Administration, 0x476, domestic),
                Entry(Command.Merchant, null, 0x22, merchant),
                Entry(Command.Move, null, 0x24, Or(transfer, summon, transport)),
                Entry(Command.Transfer, Command.Move, 0x270, transfer),
                Entry(Command.Summon, Command.Move, 0x271, summon),
                Entry(Command.Transport, Command.Move, 0x25, transport),
                Entry(Command.Investment, null, 0xB1, investment),
                Entry(Command.CityInformation, null, 0x27, information),
                Entry(Command.AreaInformation, null, 0x5E, information)
            }));
        }

        private bool SecondaryPresent()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(_state, _current)
                ?? throw Missing("secondary_player_force_pointer");
            if (!K(_current.ReadForceNativeValidity(player), "secondary_player_force_validity")) return false;
            int raw = K(_current.ReadBaseForceSlot(_city), "secondary_city_force");
            if (raw < 0) throw Missing("secondary_force_selector_domain");
            var force = _current.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw Missing("secondary_city_force_pointer");
            return force.PhysicalSlot == player.PhysicalSlot;
        }

        private bool Deploy()
        {
            if (!K(_current.ReadBaseNativeValidity(_city), "deploy_city_validity")) return false;
            var district = District("deploy_district_pointer");
            if (!K(_current.ReadDistrictNativeValidity(district), "deploy_district_validity")) return false;
            if (Books(district, "deploy") == 0) return false;
            int troops = Troops();
            if (troops < MinimumTroops()) return false;
            if (DeploymentCandidates().Count == 0) return false;
            return RegisteredArmies() <= 499;
        }

        private bool Transport()
        {
            if (!K(_current.ReadBaseNativeValidity(_city), "transport_city_validity")) return false;
            var district = PrimaryDistrict();
            if (!K(_current.ReadDistrictNativeValidity(district), "transport_district_validity")) return false;
            if (Books(district, "transport") == 0) return false;
            if (DeploymentCandidates().Count == 0) return false;
            int troops = Troops();
            if (troops < MinimumTroops()) return false;
            return RegisteredArmies() <= 499;
        }

        private bool Movement(OfficerMoveMode mode)
        {
            if (!K(_current.ReadBaseNativeValidity(_city), "movement_city_validity")) return false;
            var district = District("movement_district_pointer");
            if (!K(_current.ReadDistrictNativeValidity(district), "movement_district_validity")) return false;
            if (Books(district, "movement") < CityOfficerMovementSource.ReadCost(_state)) return false;
            return _adapter.CreateCityOfficerMovementSource(_state, _cityId, mode).CanOpen();
        }

        private bool Merchant()
        {
            var district = District("merchant_district_pointer");
            if (K(_current.ReadBaseObjectLink(_city), "merchant_base_link") > 51) return false;
            if (Books(district, "merchant") < CityMerchantSource.CommandCost) return false;
            return _adapter.CreateCityMerchantSource(_state, _cityId).CanOpen();
        }

        private List<int> DeploymentCandidates()
        {
            EnsureIndexes();
            ushort link = K(_current.ReadBaseObjectLink(_city), "candidate_base_link");
            var result = new List<int>();
            foreach (int physical in EnumerateBaseResidents(_current, link, Missing))
            {
                if ((K(PersonAssignmentPrecallInputs.ReadCurrentResetWord1FF(_state, _current, physical), "candidate_reset") & 255) != 0) continue;
                ushort currentLink = K(_current.ReadBaseObjectLink(_city), "candidate_current_base_link");
                if (!_locationsReady)
                {
                    _current.BindOfficerLocations(_state, OfficerLocationSourceAdapter.Receivers(_state));
                    _locationsReady = true;
                }
                if (currentLink != K(_current.ReadPersonSecondLocation(physical), "candidate_second_location")) continue;
                if ((K(_current.ReadArmyReturnPersonFlags196(physical), "candidate_flags") & 1) != 0) continue;
                if (!_associationReady)
                {
                    if (_state.PersonnelCommands is null) throw Missing("candidate_association_owner");
                    PersonReturnCommands.BindCurrent(_state, _current); _associationReady = true;
                }
                var linked = _current.ReadPersonLinkedEntity(physical) ?? throw Missing("candidate_association");
                if (!linked.Identity.HasValue) result.Add(physical);
            }
            return result;
        }

        private int RegisteredArmies()
        {
            EnsureIndexes();
            var descriptor = _current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("army_registry");
            return K(_current.ReadTemporaryHexListCount(new(descriptor.Identity)), "army_registry_count");
        }

        private bool Cancel()
        {
            var arena = _state.ReadArmyLifecycle() ?? throw Missing("cancel_receiver_owner");
            var slots = arena.ReceiverSlots();
            if (slots.Count != 501) throw Missing("cancel_complete_receiver_table");
            int count = 0;
            for (int physical = 0; physical < 501; physical++)
            {
                var slot = slots[physical];
                if (slot.PhysicalSlot != physical) throw Missing("cancel_receiver_table_identity");
                // v18 short circuit: neither missing header nor unit data is
                // needed for placeholder0 or a known zero commander.
                if (physical == 0) continue;
                ushort commander = K(slot.Commander12, "cancel_commander");
                if (commander == 0) continue;
                if (K(slot.Status10, "cancel_status") == 0) continue;
                var image = slot.ActiveUnitId is int id
                    ? _state.Units.GetValueOrDefault(id) ?? throw Missing("cancel_active_image")
                    : slot.PendingImage ?? throw Missing("cancel_current_hex");
                ushort position = K(ArmyMarchSourceAdapter.ReadCurrentHex(_state, image), "cancel_current_hex");
                int cityHex = _current.ReadBaseCurrentHexIndex(_city);
                if (position != cityHex) continue;
                if (K(image.NativeMapUiByte13C, "cancel_map_ui") == 0) count++;
            }
            return count > 0;
        }

        // The exact current player-primary pointer leaf. Existing lifecycle
        // readers retain their APIs and their own read order unchanged.
        private DistrictReference PrimaryDistrict()
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(_state, _current)
                ?? throw Missing("primary_player_pointer");
            int force = K(player.PhysicalSlot, "primary_player_identity");
            if (force is >= 1 and <= 51)
            {
                EnsureIndexes();
                var districts = _current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("primary_district_registry");
                foreach (var entry in districts)
                {
                    var at = entry ?? throw Missing("primary_district_pointer");
                    int physical = K(at.PhysicalSlot, "primary_district_identity");
                    if (K(_current.ReadDistrictForceId(physical), "primary_district_force") != force) continue;
                    ushort leader = K(_current.ReadDistrictLeader(physical), "primary_district_leader");
                    int currentForce = K(player.PhysicalSlot, "primary_current_player_identity");
                    ushort ruler = currentForce == 0 ? (ushort)5014 :
                        _current.ReadForceRecord(currentForce)?.RawRuler10 ?? throw Missing("primary_current_ruler");
                    if (leader == ruler) return at;
                    if (K(_current.ReadDistrictNumber(physical), "primary_district_number") == 1) return at;
                }
            }
            return _current.ReadDistrictTableReference(0) ?? throw Missing("primary_district_zero_pointer");
        }

        private (int Count, int Books) AreaInputs()
        {
            if (_areaInputs is null)
            {
                try
                {
                    _areaInputs = AreaAdministrationQuery<(int Count, int Books)>.Known(ReadCityMenuCandidatesAndBooks(
                        _state, _current, _cityId, District("retained_district_pointer"), EnsureIndexes, Missing));
                }
                catch (InvalidDataException missing)
                { _areaInputs = AreaAdministrationQuery<(int Count, int Books)>.Unknown(missing.Message); }
            }
            return _areaInputs.Available ? _areaInputs.Value : throw new InvalidDataException(_areaInputs.UnresolvedInput);
        }

        private void EnsureIndexes()
        {
            if (_indexesReady) return;
            _ = WorldIndexContext.ImportOwnedState(_current,
                _state.ReadArmyLifecycle()?.Indexes ?? throw Missing("saved_indexes"), _state);
            _indexesReady = true;
        }

        private DistrictReference District(string field) => _current.ReadCityDistrictReference(_city) ?? throw Missing(field);
        private int Troops() => _state.Cities[_cityId].Troops;
        private int Books(DistrictReference district, string field)
        {
            int physical = K(district.PhysicalSlot, field + "_district_identity");
            var owner = _state.Districts.GetValueOrDefault(physical) ?? throw Missing(field + "_district_owner");
            if (owner.Id != physical) throw Missing(field + "_district_domain_identity");
            int books = K(owner.OrdersRemaining, field + "_books");
            if (books is < 0 or > 255) throw Missing(field + "_books_range");
            return books;
        }
        private int MinimumTroops()
        {
            var rule = _state.NativeArmySimulation?.Parameters ?? throw Missing("minimum_rule_profile");
            if (rule.Language != _adapter.Seed.Language || rule.RuleVersion != SimulationRuleIdentity.ForLocale(_adapter.Seed.Language))
                throw Missing("minimum_rule_profile");
            return DeploymentDraftDefaults.CeMinimumNewUnitTroops;
        }
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("city-command-menu." + field);
        private static AreaAdministrationQuery<bool> Evaluate(Func<bool> read)
        {
            try { return Known(read()); }
            catch (InvalidDataException missing) { return AreaAdministrationQuery<bool>.Unknown(missing.Message); }
        }
        private static AreaAdministrationQuery<bool> Known(bool value) => AreaAdministrationQuery<bool>.Known(value);
        private static AreaAdministrationQuery<bool> Unknown(string field) => AreaAdministrationQuery<bool>.Unknown(Missing(field).Message);
        private static AreaAdministrationQuery<bool> Or(params AreaAdministrationQuery<bool>[] children)
        {
            if (children.Any(child => child.Available && child.Value)) return Known(true);
            return children.FirstOrDefault(child => !child.Available) ?? Known(false);
        }
        private static CityCommandMenuEntry Entry(Command command, Command? parent, int caption, AreaAdministrationQuery<bool> enabled)
            => new(command, parent, caption, enabled);
        private static CityCommandMenuProjection Absent() => new(CityCommandMenuBranch.Absent, Array.Empty<CityCommandMenuEntry>());
    }
}
