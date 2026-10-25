using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnDisasterConsequencesSource
    {
        private void ApplyArmies(int area, BaseReference groupCity, int percentage, int cap)
        {
            foreach (int physical in Registry(RegistryKind.Army))
            {
                var army = new ArmyUnitReference(physical);
                var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("Army_resources");
                int ready = N(fields.Ready16, "Army16");
                if (ready <= 100) continue;
                int rawHex = N(current.ReadArmyUnitRawHex2A(army), "Army2A");
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("Army_Hex_pointer");
                int rawArea = N(current.ReadHexAreaSlot(hex), "Army_Hex12");
                var location = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw Missing("Army_Area_pointer");
                if (N(location.PhysicalSlot, "Army_Area_ordinal") != area) continue;
                int loss = Clamp(unchecked(ready * percentage) / 100, 1, cap);
                int wounded = N(fields.Wounded18, "Army18");
                int woundsLost = Clamp(unchecked(wounded * percentage * 2) / 100, 0, cap);
                var force = current.ReadArmyUnitForceReference(army) ?? throw Missing("Army_Force_pointer");
                if (Player(force))
                {
                    if (rawHex != AreaHex(area)) Intent(MonthlyDisasterConsequenceOperation.AreaOrBaseEffect, 0,
                        Physical(groupCity), rawHex, army: physical);
                    Intent(MonthlyDisasterConsequenceOperation.ArmyLossNumber, 0, Physical(groupCity), rawHex, amount: loss, army: physical);
                }
                ushort finalReady = (ushort)Clamp(unchecked(ready - loss), 0, 65535);
                ushort finalWounded = (ushort)Clamp(unchecked(wounded - woundsLost), 0, 20000);
                current.BindArmyReturnResourceFields(physical, fields with { Ready16 = finalReady, Wounded18 = finalWounded });
                WriteArmy(physical, Image(physical) with { Troops = finalReady, WoundedTroops = finalWounded });
                int commander = Person(N(current.ReadArmyUnitCommanderId(army), "Army12"));
                ApplyIllness(commander);
            }
        }

        //2E0A30 separately visits unassociated people in the Area after
        //the Army commander loop. No deduplication or fabricated status gate.
        private void ApplyUnlinkedPeople(int area)
        {
            foreach (int person in Registry(RegistryKind.Person))
            {
                int status = N(current.ReadPersonStatus(person), "PersonE6");
                bool serving = status is >= 1 and <= 4;
                if (serving)
                {
                    var force = PersonForce(person);
                    int first = N(current.ReadForcePhysicalSlot(force), "Person_Force60_first");
                    if (first == 51 || N(current.ReadForcePhysicalSlot(force), "Person_Force60_second") is >= 46 and <= 50) continue;
                }
                ushort flags = N(current.ReadOwnedReturnPersonFlags(person), "Person15E");
                if ((flags & 0x2000) != 0 || (flags & 0x4000) != 0 && !serving) continue;
                var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("Person_associated");
                if (N(current.ReadLinkedEntityNativeValidity(linked), "Person_associated_validity")) continue;
                var hex = PersonReturnCommands.ResolvePersonPosition(state, current, person, () => Date);
                if (!hex.PhysicalSlot.HasValue) continue; //All actual CHex receivers, including0, are valid.
                if (N(current.ReadHexAreaSlot(hex), "Person_Hex12") == area) ApplyIllness(person);
            }
        }

        //2E0B70: a separate deterministic trial. Native16 suppresses only
        //this reached trial; no normal health-stage algorithm is substituted.
        private void ApplyIllness(int person)
        {
            if (!N(current.ReadPersonNativeValidity(person), "illness_Person_validity")) return;
            var date = Date;
            int health = N(OfficerHealthSourceAdapter.ReadByte(current, person), "illness_Person161");
            if (health >= 2) return;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("illness_associated");
            if (N(current.ReadLinkedEntityNativeValidity(linked), "illness_associated_validity"))
            {
                var owner = current.ReadOwnedPersonCommandManager() ?? throw Missing("illness_command_owner");
                if (linked.Identity is not int identity) throw Missing("illness_linked_receiver");
                if (owner.Disappearances is null) throw Missing("illness_native16_owner");
                if (owner.Disappearances.ContainsKey(identity)) return;
                if (!owner.Returns.ContainsKey(identity) && owner.Talents?.ContainsKey(identity) != true &&
                    owner.Diplomacies?.ContainsKey(identity) != true && owner.Strategies?.ContainsKey(identity) != true &&
                    owner.Independences?.ContainsKey(identity) != true) throw Missing("illness_associated_virtual98");
            }
            int id = N(current.ReadPersonIdentity(person), "illness_Person10");
            var sampler = package.ReadArmyDestructionSampler(state, current) ?? throw Missing("illness_World450_45C");
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1,
                unchecked((short)date.Year + unchecked((sbyte)date.Month) + unchecked((sbyte)date.Day)), id, 0, 0, sampler);
            if (sample >= P("plague-illness-rate")) return;
            var officer = state.Officers.GetValueOrDefault(id) ?? throw Missing("illness_Person_owner");
            if (current.ResolveOfficerSource(id) != person) throw Missing("illness_Person_binding");
            var after = new OfficerHealth(Math.Min(unchecked(health + 1), 3));
            current.SetOfficerHealth(id, after);
            state = state with { Officers = new(state.Officers) { [id] = officer with { Health = after } } };
            if (!Player(PersonForce(person))) return;
            var home = BaseLookup.Find(N(current.ReadPersonFirstLocation(person), "illness_PersonE2"), current);
            var text = package.FormatNormalTurnReport(state, current, 0x5E38,
                new(Base0: N(current.ReadBaseObjectLink(home), "illness_report_Base4E"), Person0: id));
            state = text.State;
            int district = N(current.ReadPersonDistrictSlot(person), "illness_report_PersonE0");
            int hex = current.ReadBaseCurrentHexIndex(home);
            int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("illness_report_day450")).Day450;
            var effects = new List<CampaignEffectRequest>();
            state = package.WriteHistory(state, current, new(0, district, hex, day, text.Text,
                CampaignHistoryWriter.Supplemental(state) ?? throw Missing("current_empty_text"), id, MarkBit4: 1), effects);
            Append(effects);
        }

        private bool IsCityCenter(int area)
        {
            var city = City(area);
            if (!Valid(city)) return false;
            int raw = N(current.ReadBaseObjectLink(city), "center_City4E");
            var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw Missing("center_Object_pointer");
            int hex = N(current.ReadObjectNativeValidity(obj), "center_Object_validity")
                ? N(current.ReadObjectHexIndex(obj), "center_Object10") : 48400;
            return hex == AreaHex(area);
        }
        private int AreaHex(int area) => N(current.ReadAreaHexIndex(new(area)), "Area3A");
        private AreaState Area(int physical) => state.Areas.GetValueOrDefault(physical) ?? throw Missing("Area_owner");
        private BaseReference City(int area)
        {
            int raw = N(current.ReadAreaCitySlot(new(area)), "Area35");
            return current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw Missing("Area_City_pointer");
        }
        private bool Valid(BaseReference target) => N(current.ReadBaseNativeValidity(target), "Base_validity");
        private ForceReference Force(BaseReference target)
        {
            int raw = N(current.ReadBaseForceSlot(target), "Base_Force");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("Base_Force_pointer");
        }
        private ForceReference AreaForce(int area)
        {
            //22C950 reads Area3A directly, without an Area-validity gate.
            int rawHex = AreaHex(area);
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("Area_Force_Hex");
            int raw = N(current.ReadHexForceSlot(hex), "Area_Force_Hex14");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("Area_Force_pointer");
        }
        private ForceReference PersonForce(int person)
        {
            int raw = N(current.ReadPersonOldForceSlot(person), "Person_Force");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("Person_Force_pointer");
        }
        private bool Player(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force_pointer");
            return N(current.ReadForcePhysicalSlot(player), "player_Force60") != 0 && player == force;
        }
        private bool BasePlayer(BaseReference target)
        {
            //233A00 differs from2338E0: it first requires the actual
            //current player Force's native validity, then calls Base.vB0.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("Base_player_Force_pointer");
            return N(current.ReadForceNativeValidity(player), "Base_player_validity") && player == Force(target);
        }
        private static int Physical(BaseReference value) => N(value.PhysicalSlot, "Base_receiver");
        private int Person(int raw) => N((raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private UnitState Image(int physical)
        {
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("Army_lifecycle");
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) ?? throw Missing("Army_receiver");
            return receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw Missing("Army_active_image")
                : receiver.PendingImage ?? throw Missing("Army_pending_image");
        }
        private void WriteArmy(int physical, UnitState image)
        {
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("Army_write_lifecycle");
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) ?? throw Missing("Army_write_receiver");
            state = receiver.ActiveUnitId is int id ? state with { Units = new(state.Units) { [id] = image } }
                : state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with { PendingImage = image }) });
        }
        private bool Advanced() => N(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "advanced_settings");
        private int Draw(int bound)
        {
            if (bound < 2) return 0;
            uint next = SourceRandom32Mix.Advance((state.CampaignAppRandom32 ?? throw Missing("App_random")).State);
            state = state with { CampaignAppRandom32 = new(next) };
            return (int)((next & 0x7FFF_FFFFu) % (uint)bound);
        }
        private IEnumerable<int> Registry(RegistryKind kind)
        {
            foreach (var entry in current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind))
            {
                if (entry is null || entry.Kind != kind) throw Missing("registry_payload_" + kind);
                yield return N(entry.PhysicalSlot, "registry_pointer_" + kind);
            }
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary");
        private void Add(HexListHandle list, int physical)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Area, physical));
        }
        private IEnumerable<int> Values(HexListHandle list)
        {
            for (var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next"))
            {
                var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload");
                if (value.Kind != RegistryKind.Area) throw Missing("temporary_Area");
                yield return N(value.PhysicalSlot, "temporary_Area_pointer");
            }
        }
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Intent(MonthlyDisasterConsequenceOperation operation, int kind, int city, int hex,
            string? text = null, int? amount = null, int? army = null, int? location = null)
            => Append([new(null, null) { MonthlyDisasterConsequence = new(operation, kind, city, hex, text, amount, army) { BaseLocationId = location } }]);
        private void Append(IReadOnlyList<CampaignEffectRequest> requests)
        {
            if (requests.Count != 0) state = state with { CampaignEffectRequests =
                (state.CampaignEffectRequests ?? throw Missing("pending_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                    state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), requests) };
        }
        private int P(string name)
        {
            string key = "simulation.normal-turn.disaster-consequence." + name;
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("parameters");
            if (bank.Float32Values.ContainsKey(key)) throw Missing("integer_parameter_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            if (package.Seed.Language != "TC") throw Missing("locale_parameter_" + name);
            //File-backed TC data1A4A8D0..954. Saved current values win.
            return name switch { "loss-random-bound" => 6, "plague-base-cap" => 3000, "advanced-plague-base-cap" => 10000,
                "plague-unit-cap" => 500, "advanced-plague-unit-cap" => 2500, "advanced-plague-population-cap" => 5000,
                "plague-security-random-bound" => 4, "locust-security-random-bound" => 4, "plague-illness-rate" => 6,
                "locust-base-food-cap" => 10000, "advanced-locust-base-food-cap" => 40000,
                _ => throw Missing("parameter_" + name) };
        }
        private static int Clamp(int value, int minimum, int maximum) => value < minimum ? minimum : value > maximum ? maximum : value;
        private static T N<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => new("normal-turn.disaster-consequence." + name);
    }
}
