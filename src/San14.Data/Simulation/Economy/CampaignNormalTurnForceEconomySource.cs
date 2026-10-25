using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //303650/303830: the three intelligence assignments use current gold,
    //the retained primary District and actual gained intelligence, in order.
    internal GameState ApplyNormalTurnForceEconomyCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        var effects = new List<CampaignEffectRequest>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("ForceC0"))
        {
            if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical }) throw Missing("Force_payload");
            var force = new ForceReference(physical);
            if (!ValidForce(force)) continue;
            var native = state.NativeArmySimulation ?? throw Missing("native_owner");
            var owner = native.Forces.GetValueOrDefault(physical) ?? throw Missing("native_Force");
            state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                { [physical] = owner with { AssociationIntelligenceGain = Math.Min(ForceCapacity(force), 0), AssociationGoldExpense = 0 } } } };
            Settle(force);
        }
        //303650's second pass only invokes pure32FB90 getters and discards
        //every result. It has no reward, report or additional numeric writer.
        if (effects.Count != 0)
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("effect_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision"), effects) };
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw Missing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });

        void Settle(ForceReference force)
        {
            var primary = ForcePrimaryDistrictSource.Resolve(current, force);
            //Cancellation re-reads the first matching target slot inside
            //278090. Duplicate target rows must not select by this loop index.
            for (int slot = 0; slot < 3; slot++)
            {
                var other = Target(force, slot);
                int person = Actor(force, slot);
                if (!ValidForce(other) || !ValidPerson(person)) continue;
                if (!Adjacent(force, other) ||
                    (ReadReportForceRelationByte(state, force, Ordinal(other), Missing) & 127) != 0 ||
                    (Flags(force) & 2) != 0 && (Flags(other) & 2) != 0) Cancel(force, other);
            }
            int district = K(primary.PhysicalSlot, "primary_District_pointer");
            for (int slot = 0; slot < 3; slot++)
            {
                if (ReadHireAssociationBudgetCapacity(state, current, primary) <= ReadDiplomacyDistrictBudget(current, district)) return;
                var other = Target(force, slot);
                int person = Actor(force, slot);
                if (!ValidForce(other) || !ValidPerson(person)) continue;
                int fee = ReadHireAssociationFee(state, current, force, person);
                var home = Home(force);
                if (BaseCurrencyAdditionSource.ReadCurrentGold(home, current) < fee) return;
                int gain = ReadHireAssociationGain(state, current, force, other, person);
                if (gain <= 0) continue;

                int before = ReadDiplomacyDistrictBudget(current, district);
                int after = Math.Min(unchecked(before + gain), ReadHireAssociationBudgetCapacity(state, current, primary));
                after = Bound(after, ReadHireAssociationBudgetCapacity(state, current, primary));
                WriteBudget(district, after);
                int physical = K(force.PhysicalSlot, "gain_Force_pointer");
                var native = state.NativeArmySimulation ?? throw Missing("gain_native_owner");
                var owner = native.Forces.GetValueOrDefault(physical) ?? throw Missing("gain_native_Force");
                int accumulated = Bound(unchecked(K(owner.AssociationIntelligenceGain, "Force1A8") + after - before), ForceCapacity(force));
                state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                    { [physical] = owner with { AssociationIntelligenceGain = accumulated } } } };

                //22EA10 is repeated after the gain writer, as in the native
                //gold capacity/read/set virtual chain. Gate money belongs to City.
                state = AddNormalTurnBaseResourceCurrent(state, current, Home(force), BaseResourceKind.Gold, unchecked(-fee), recordExpense: true);
                native = state.NativeArmySimulation ?? throw Missing("expense_native_owner");
                owner = native.Forces.GetValueOrDefault(physical) ?? throw Missing("expense_native_Force");
                state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                    { [physical] = owner with { AssociationGoldExpense = Math.Max(0, unchecked(K(owner.AssociationGoldExpense, "Force1AC") + fee)) } } } };
                Reward(person);
                //The remaining World17C4/3E9750 tail serves achievements.
                //It is outside the product scope and adds no game reward here.
            }
        }
        void Reward(int person)
        {
            if (!ValidPerson(person)) return;
            ushort location = K(current.ReadPersonFirstLocation(person), "reward_PersonE2");
            if (location is >= 1 and <= 61)
            {
                int force = K(current.ReadForcePhysicalSlot(current.ReadPersonForce(person) ?? throw Missing("reward_Person_Force")), "reward_Force_ordinal");
                if (force is >= 46 and <= 50 || BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Id(person))) return;
            }
            bool busy = (K(current.ReadArmyReturnPersonFlags196(person), "reward_Person15E") & 1) != 0 ||
                ReadHireAssociationActorBusy(current, person);
            int xp = Rule(busy ? "busy-intelligence-experience" : "idle-intelligence-experience", busy ? 15 : 50);
            int merit = unchecked(Rule(busy ? "busy-merit" : "idle-merit", busy ? 25 : 50) * 10);
            //This source calls2BA120 once. The command helper's five-slot
            //growth loop would award unrelated trait75 minimum experience.
            var growth = _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries, Id(person), 2, xp, 0);
            if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) throw Missing("reward_2BA120");
            state = ApplyTalentPendingMeritCurrent(growth.State, current, person, merit);
            //Cached1695C0..169889 never reads incomingR8: caller37/38
            //have no numeric consumer, and are not invented report contexts.
        }
        void Cancel(ForceReference own, ForceReference other)
        {
            if (!ValidForce(own) || !ValidForce(other) || own == other) return;
            int selected = -1;
            int target = Ordinal(other);
            for (int index = 0; index < 3; index++)
                if (K(current.ReadForceAssociationTargetSlot(own, index), "cancel_target19C") == target) { selected = index; break; }
            if (selected < 0) return;
            int person = Actor(own, selected);
            if (!ValidPerson(person)) return;
            current.WriteForceAssociationTargetSlot(own, selected, 0);
            current.WriteForceAssociationPersonId(own, selected, 0);
            state = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, [], [],
                [(K(own.PhysicalSlot, "cancel_Force_pointer"), selected)]);
            if (!AbsorptionPlayer(current, state, own)) return;

            var main = FormatNormalTurnReport(state, current, 0x3452, new(Person0: Id(person), Force0: target)); state = main.State;
            ushort location = K(current.ReadPersonFirstLocation(person), "cancel_report_PersonE2");
            int day = (ReadArmyDestructionSampler(state, current) ?? throw Missing("cancel_World450")).Day450;
            var home = BaseLookup.Find(location, current);
            int rawDistrict = K(current.ReadPersonDistrictSlot(person), "cancel_PersonE0");
            var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0) ?? throw Missing("cancel_District_pointer");
            var hex = current.ReadReportBaseHex(home) ?? throw Missing("cancel_Base_Hex");
            state = WriteHistory(state, current, new(0, K(current.ReadDistrictPhysicalSlot(district), "cancel_District_ordinal"),
                K(current.ReadReportMapCellPhysicalSlot(hex), "cancel_Hex_ordinal"), day, main.Text,
                CampaignHistoryWriter.Supplemental(state), 5012), effects);

            //This entry is exclusively NormalTurn40, outside the original
            //CEventPlaybackState script controller. That future script caller
            //must supply its own actual presentation-suppression lifecycle.
            var source = state;
            var line = FormatNormalTurnReport(state, current, 0x344D,
                new(Person0: Id(person), Person1: K(current.ReadForceRecord(K(own.PhysicalSlot, "cancel_ruler_Force"))?.RawRuler10, "cancel_ruler10"), Force0: target));
            state = line.State;
            effects.Add(CreateStrategicCommandPresentation(current, source, Id(person), line));
        }
        bool Adjacent(ForceReference own, ForceReference other)
        {
            int first = Ordinal(own), second = Ordinal(other);
            if (first == 0 || second == 0) return false;
            //2243A0 walks the fixed52 City pointers; it does not consume
            //the City registration chain or add a City native-validity guard.
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw Missing("neighbor_City_pointer");
                if (K(current.ReadBaseForceSlot(city), "neighbor_City_Force") != first) continue;
                var temporary = Temporary();
                try
                {
                    (_eventCityNeighborhoods ??= new(_scenario)).FillCities22F430(current, city, temporary);
                    var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("neighbor_head");
                    while (node.Identity.HasValue)
                    {
                        var entry = current.ReadRegistryNodePayload(node) ?? throw Missing("neighbor_payload");
                        if (entry.Kind != RegistryKind.City) throw Missing("neighbor_kind");
                        var adjacent = new BaseReference(BaseKind.City, entry.PhysicalSlot);
                        if (K(current.ReadBaseForceSlot(adjacent), "neighbor_other_Force") == Ordinal(other)) return true;
                        node = current.ReadRegistryNodeNext(node) ?? throw Missing("neighbor_next");
                    }
                }
                finally { Lock(true); try { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); } finally { Lock(false); } }
            }
            return false;
        }
        void WriteBudget(int district, int value)
        {
            for (int index = 0; index < 4; index++) current.SetDistrictRawByte(district, 0x24 + index, unchecked((byte)(value >> (8 * index))));
            var saved = state.NativeForceUnregisterCurrent ?? throw Missing("District24_owner");
            state = state with { NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10)
                { [district] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(district, at)).ToArray() } } };
        }
        int ForceCapacity(ForceReference force)
        {
            int rank = K(current.ReadMoraleWaveForceRank47(K(force.PhysicalSlot, "capacity_Force_pointer")), "capacity_Force47");
            return HireAssociationParameter(state, rank switch
            {
                1 or 2 or 3 or 10 or 11 or 12 => "highest-rank-budget-capacity", 4 or 5 => "high-rank-budget-capacity",
                6 or 7 => "middle-rank-budget-capacity", _ => "other-rank-budget-capacity"
            });
        }
        BaseReference Home(ForceReference force)
        {
            int ruler = HireAssociationPerson(current, K(current.ReadForceRecord(K(force.PhysicalSlot, "home_Force_pointer"))?.RawRuler10, "home_Force10"));
            return BaseLookup.Find(K(current.ReadPersonFirstLocation(ruler), "home_rulerE2"), current);
        }
        ForceReference Target(ForceReference force, int slot)
        {
            int raw = K(current.ReadForceAssociationTargetSlot(force, slot), "Force19C");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw Missing("target_Force_pointer");
        }
        int Actor(ForceReference force, int slot) => HireAssociationPerson(current, K(current.ReadForceAssociationPersonId(force, slot), "Force196"));
        ushort Id(int person) => K(current.ReadPersonIdentity(person), "Person10");
        int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force60");
        bool ValidForce(ForceReference force) => K(current.ReadForceNativeValidity(force), "Force_validity");
        bool ValidPerson(int person) => K(current.ReadPersonNativeValidity(person), "Person_validity");
        byte Flags(ForceReference force) => K(state.NativeArmySimulation?.Forces.GetValueOrDefault(K(force.PhysicalSlot, "flags_Force_pointer"))?.Flags12, "Force12");
        //File-backed TC values from the existing typed data table:
        //1A4B094=50,1A4B088=15,1A4AE18=50,1A4AE0C=25.
        int Rule(string name, int value) => StrategicParameter(state, "normal-force-economy." + name, value);
        int Bound(int value, int cap) => value < 0 ? 0 : value > cap ? cap : value;
        HexListHandle Temporary()
        {
            Lock(true); try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_owner"); } finally { Lock(false); }
        }
        void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.force-economy." + field);
    }
}
