using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        private byte Relation(ForceReference force, int other, bool alliance)
        {
            if (other >= 52) return 0;
            if (other < 0) throw Missing("relation_negative_identity");
            int physical = Need(force.PhysicalSlot, "relation_force_pointer");
            if (alliance) return ReadReportForceRelationByte(state, force, other, Missing);
            if (physical == 0)
            {
                var placeholder = state.NativeForceUnregisterCurrent?.PlaceholderRelations13;
                return placeholder is { Length: 52 } ? placeholder[other] : throw Missing("placeholder_relation_owner");
            }
            var owner = state.NativeArmySimulation?.Forces.GetValueOrDefault(
                physical);
            byte[] values = (alliance ? owner?.RelationsEA : owner?.NormalAftermath?.Relations13)
                ?? throw Missing(alliance ? "alliance_owner" : "relation_owner");
            return values.Length == 52 ? values[other] : throw Missing("relation_width");
        }
        private void WriteAlliance(ForceReference force, int other, byte value)
        {
            if (other >= 52) return;
            var simulation = state.NativeArmySimulation ?? throw Missing("alliance_state");
            int own = Need(force.PhysicalSlot, "alliance_force_pointer");
            if (own == 0)
            {
                var placeholder = state.NativeForceUnregisterCurrent ?? throw Missing("placeholder_alliance_owner");
                var relations = placeholder.PlaceholderRelationsEA.ToArray();
                relations[other] = value;
                current.SetForceRelationByteEA(own, other, value);
                state = state with { NativeForceUnregisterCurrent = placeholder with { PlaceholderRelationsEA = relations } };
                Observe($"monthly.alliance.written.{own}.{other}");
                return;
            }
            var owner = simulation.Forces.GetValueOrDefault(own) ?? throw Missing("alliance_force_owner");
            var values = owner.RelationsEA is { Length: 52 } array ? array.ToArray() : throw Missing("alliance_values");
            values[other] = value;
            current.SetForceRelationByteEA(own, other, value);
            state = state with { NativeArmySimulation = simulation with
                { Forces = new(simulation.Forces) { [own] = owner with { RelationsEA = values } } } };
            Observe($"monthly.alliance.written.{own}.{other}");
        }
        private IEnumerable<int> ForceArmies(ForceReference force)
        {
            int key = Physical(force); //222FD0 captures the key.
            _ = Physical(force); //223250's fresh end receiver.
            foreach (int physical in Registry(RegistryKind.Army))
            {
                if (key is < 1 or > 51) continue;
                if (current.ReadArmyUnitRawForceSlot(new(physical)) != key) continue;
                // CForceArmyUnitIterator also excludes233560 receivers before
                // any caller can mutate their morale, territory or logistics.
                if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "force_iterator_Army233560")) continue;
                yield return physical;
                _ = Physical(force);
            }
        }
        private HexReference ArmyHex(int physical)
        {
            int raw = Need(current.ReadArmyUnitRawHex2A(new(physical)), "army_hex");
            return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("army_hex_lookup");
        }
        private bool Crossed(ForceReference own, ForceReference opposite, bool rejectExcluded)
        {
            foreach (int army in ForceArmies(own))
            {
                if (rejectExcluded && Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, army), "army_excluded")) continue;
                var hex = ArmyHex(army);
                int areaSlot = Need(current.ReadHexAreaSlot(hex), "army_hex_area");
                var area = current.ReadAreaTableReference(areaSlot <= 500 ? areaSlot : 0) ?? throw Missing("army_area_lookup");
                int areaForce = Need(current.ReadAreaCurrentForceSlot(area), "army_area_force");
                if (areaForce == Physical(opposite) && Need(current.ReadHexForceSlot(hex), "army_hex_force") == Physical(opposite)) return true;
            }
            return false;
        }
        private PersonDeathHistorySelection Selection() => new(state, current, pool,
            new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
            (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect));
        private void ProcessAlliances()
        {
            foreach (int outer in Registry(RegistryKind.Force))
            {
                var own = Force(outer);
                foreach (int inner in Registry(RegistryKind.Force))
                {
                    var opposite = Force(inner);
                    int selected = Physical(opposite);
                    int old = Relation(own, selected, true) & 0x7F;
                    if (old == 0) continue;
                    selected = Physical(opposite);
                    if (selected < 52)
                    {
                        int before = Relation(own, selected, true);
                        WriteAlliance(own, selected, (byte)((before & 0x80) | Math.Clamp(old - 1, 0, 48)));
                    }
                    if (Player(own) && old is >= 2 and <= 4 && Crossed(own, opposite, true))
                    {
                        selected = Physical(opposite);
                        if ((Relation(own, selected, true) & 0x80) == 0)
                        {
                            WriteAlliance(own, selected, (byte)(Relation(own, selected, true) | 0x80));
                            var selection = Selection();
                            var speaker = selection.Select(selection.PrimaryDistrict(own));
                            int speakerId = Need(current.ReadPersonIdentity(Need(speaker.PhysicalSlot, "warning_speaker_pointer")), "warning_speaker_identity");
                            string text = Format(0x5EF8, new(Person0: speakerId, Person1: Ruler(own), Force0: Physical(opposite), Force1: 0));
                            Speaker(speakerId, text);
                        }
                    }
                    if (old == 1) ExpireAlliance(own, opposite);
                }
            }
        }
        private void ExpireAlliance(ForceReference first, ForceReference second)
        {
            Queue(8);
            Narration(Format(0x324A, new(Person0: Ruler(first), Person1: Ruler(second), Force0: 0, Force1: 0)));
            bool crossedFirst = Crossed(first, second, true);
            bool crossedSecond = Crossed(second, first, true);
            EndAlliance(first, second);
            if (crossedFirst) ApplyCrossingConsequences(first, second);
            if (crossedSecond) ApplyCrossingConsequences(second, first);
            Observe("monthly.alliance.expired");
        }
        private void EndAlliance(ForceReference first, ForceReference second)
        {
            SetAllianceLow(first, second, 0);
            SetAllianceLow(second, first, 0);
            if (Player(first) || Player(second))
            {
                //The original refresh only clears the current player's one
                //direction warning bit. View caches requery published state.
                if (Player(first)) ClearWarning(first, second); else ClearWarning(second, first);
            }
            Cancel(first, second); Cancel(second, first);
            byte firstFlags = ForceFlags(first), secondFlags;
            if ((firstFlags & 2) != 0 && ((secondFlags = ForceFlags(second)) & 2) != 0) return;
            RemoveCrossedTerritory(first, second);
            RemoveCrossedTerritory(second, first);

            void Cancel(ForceReference owner, ForceReference target)
            {
                if (!RequesterEquals(owner, Physical(target))) return;
                if (Need(Objective(owner).RemainingMonths, "cancel_request_term") == 0) return;
                WriteObjective(owner, Objective(owner) with { RemainingMonths = 0 });
                Observe("monthly.request.cancelled-term");
                ResolveRequest(owner, true);
            }
            void SetAllianceLow(ForceReference owner, ForceReference target, int remaining)
            {
                int index = Physical(target);
                if (index < 52) WriteAlliance(owner, index, (byte)((Relation(owner, index, true) & 0x80) | Math.Clamp(remaining, 0, 48)));
            }
            void ClearWarning(ForceReference owner, ForceReference target)
            {
                int index = Physical(target);
                if (index < 52) WriteAlliance(owner, index, (byte)(Relation(owner, index, true) & 0x7F));
            }
        }
        private byte ForceFlags(ForceReference force)
        {
            int physical = Need(force.PhysicalSlot, "flags_force_pointer");
            if (physical == 0) return state.NativeForceUnregisterCurrent?.PlaceholderFlags12 ?? throw Missing("placeholder_flags12");
            return state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true
                ? Need(reset[0x12], "reset_force_flags12")
                : state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12 ?? throw Missing("force_flags12");
        }
        private void RemoveCrossedTerritory(ForceReference own, ForceReference opposite)
        {
            foreach (int army in ForceArmies(own))
            {
                var hex = ArmyHex(army);
                if (Need(current.ReadHexForceSlot(hex), "crossed_land_owner") != Physical(opposite)) continue;
                hex = ArmyHex(army);
                //2413D0(Hex,0) stores BEFORE reading any surface. Its0
                //argument proves the numeric player/Area refresh impossible.
                current.WriteHexForceSlot(hex, 0);
                int hexId = Need(hex.PhysicalSlot, "crossed_hex_pointer");
                var cell = state.Territory.GetValueOrDefault(hexId) ?? throw Missing("crossed_territory_owner");
                var areas = new Dictionary<int, AreaState>(state.Areas);
                foreach (var representedArea in areas.Values.ToArray())
                    if (representedArea.HexId == hexId) areas[representedArea.Id] = representedArea with { ForceId = null };
                state = state with { Territory = new(state.Territory) { [hexId] = cell with { ForceId = null } }, Areas = areas };
                Observe($"monthly.territory.cleared.{hexId}");
                //The original optional scenery dirty flag is view cache work;
                //successful world publication drives actual view requery.
                hex = ArmyHex(army);
                int commanderId = Need(current.ReadArmyUnitCommanderId(new(army)), "crossed_commander");
                int commander = PersonLookup(commanderId);
                int home = Need(current.ReadPersonNativeValidity(commander), "crossed_commander_validity")
                    ? Need(current.ReadPersonFirstLocation(commander), "crossed_commander_first_location") : 0;
                var target = BaseLookup.Find(home, current);
                var homeHex = CurrentGateMapQueries.FindHex(target, current);
                int areaId = Need(current.ReadHexAreaSlot(homeHex), "crossed_home_area");
                var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0) ?? throw Missing("crossed_home_area_pointer");
                int citySlot = Need(current.ReadAreaNativeValidity(area), "crossed_home_area_validity")
                    ? Need(current.ReadAreaCitySlot(area), "crossed_home_area_city") : 0;
                var city = current.ReadCityTableReference(citySlot <= 51 ? citySlot : 0) ?? throw Missing("crossed_city_lookup");
                int cityId = Need(city.PhysicalSlot, "crossed_city_physical");
                RestoreMonthlyOccupation(hex, Physical(own), cityId);
            }
        }
        //292BA0's185D00 flags1,1,0, on the same current and pool. Presentation
        //requests describe source work; they never claim animation or sound ACK.
        private bool RestoreMonthlyOccupation(HexReference hex, int force, int cityId, bool enqueueEncirclement = true,
            bool allowType3Override = false, bool showAppearance = true)
        {
            if (!hex.PhysicalSlot.HasValue) return false;
            if (!GateCaptureTerritorySource.Eligible(state, current, hex, force, false) &&
                (!allowType3Override || !TerritoryExpansionType3Override(hex))) return false;
            if (Need(current.ReadHexForceSlot(hex), "restore_Hex14") == force) return false;
            int physical = Need(hex.PhysicalSlot, "restore_Hex_identity");
            int surface = Need(current.ReadHexSurfaceCode(hex), "restore_Hex10");
            bool ordinary = (force is >= 1 and <= 45 or 51) && cityId is >= 1 and <= 51;
            bool center = surface is >= 15 and <= 18;
            bool appearanceCenter = center && ordinary;
            int? changedArea = null;
            if (center)
            {
                int rawArea = Need(current.ReadHexAreaSlot(hex), "restore_Area12");
                var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0) ?? throw Missing("restore_Area_pointer");
                if (ordinary)
                {
                    //This existing leaf retains240AA0's Person home writes,
                    //native HomeArmy key30 notices and the encirclement seed.
                    state = package.PrepareRegionCenterTransfer(state, current, hex, force, cityId, enqueueEncirclement: enqueueEncirclement, sourceDate: borrowedTerritoryDate);
                    changedArea = rawArea;
                }
                else if (force is >= 46 and <= 51)
                {
                    var assigned = current.AssignAreaCity(area, Need(current.ReadAreaValidityCode(area), "restore_original_City34"));
                    var officers = new Dictionary<int, OfficerState>(state.Officers);
                    foreach (int person in assigned.PersonPhysicalSlotsWritten)
                    {
                        int id = Identity(person);
                        var owner = officers.GetValueOrDefault(id) ?? throw Missing("restore_Person_owner");
                        int home = Need(current.ReadPersonFirstLocation(person), "restore_PersonE2");
                        officers[id] = owner with { LocationId = home == 0 ? null : home };
                    }
                    if (assigned.PersonPhysicalSlotsWritten.Count != 0) state = state with { Officers = officers };
                    changedArea = rawArea;
                }
            }
            int sceneryRaw = Need(current.ReadHexScenerySlot(hex), "restore_Hex1E");
            var scenery = current.ReadSceneryTableReference(sceneryRaw <= 100 ? sceneryRaw : 0)
                ?? throw Missing("restore_Scenery_pointer");
            if (Need(current.ReadSceneryNativeValidity(scenery), "restore_Scenery_validity") &&
                (Need(current.ReadSceneryFlags47(scenery), "restore_Scenery47") & 1) != 0 && ordinary)
            {
                var city = current.ReadCityTableReference(cityId) ?? throw Missing("restore_notify_City_pointer");
                if (Need(current.ReadBaseNativeValidity(city), "restore_notify_City_validity"))
                    QueueSceneryControl(city, hex, Need(scenery.PhysicalSlot, "restore_Scenery_identity"));
            }
            int requested = force == 51 ? 0 : force;
            bool write = true;
            if (force is >= 46 and <= 50)
            {
                int areaRaw = Need(current.ReadHexAreaSlot(hex), "restore_special_Area12");
                var area = current.ReadAreaTableReference(areaRaw <= 500 ? areaRaw : 0) ?? throw Missing("restore_special_Area_pointer");
                if (!area.PhysicalSlot.HasValue) requested = 0;
                else
                {
                    int cityRaw = Need(current.ReadAreaCitySlot(area), "restore_special_Area35");
                    var city = current.ReadCityTableReference(cityRaw <= 51 ? cityRaw : 0) ?? throw Missing("restore_special_City_pointer");
                    if (Need(current.ReadBaseForceSlot(city), "restore_special_City_Force") != force)
                    {
                        //185D00's parameter is a raw Force table operand.
                        //The validity and194 reads belong to its lookup result.
                        var owner = current.ReadForceTableReference(force) ?? throw Missing("restore_special_Force_pointer");
                        if (Valid(owner)) write = Need(current.ReadConditionForceDependence(new(Physical(owner))), "restore_special_Force194")
                            != Need(current.ReadHexForceSlot(hex), "restore_special_Hex14");
                        requested = 0;
                    }
                }
            }
            if (write) current.AssignHexForce(hex, requested);
            PublishRestoredHex(hex, changedArea);
            int currentForce = Need(current.ReadHexForceSlot(hex), "restore_final_Hex14");
            var appearanceForce = current.ReadForceTableReference(currentForce <= 51 ? currentForce : 0)
                ?? throw Missing("restore_appearance_Force_pointer");
            if (showAppearance && Valid(appearanceForce))
                Append(new(MonthlyMaintenanceOperation.TerritoryOccupation, appearanceCenter ? 4 : 5, null, null)
                    { ForceId = force, HexId = physical });
            var mode = current.ArmyReturnTransferMode ?? throw Missing("restore_transfer_mode");
            if (mode.Present && Need(mode.Value, "restore_transfer_mode_value") != 0)
                state = IncrementTutorialTerritoryTransfer(state);
            return true;
        }
        private void PublishRestoredHex(HexReference hex, int? changedArea)
        {
            int physical = Need(hex.PhysicalSlot, "restore_published_Hex_identity");
            int force = Need(current.ReadHexForceSlot(hex), "restore_published_Hex14");
            var tile = state.Territory.GetValueOrDefault(physical) ?? throw Missing("restore_Territory_owner");
            var areas = new Dictionary<int, AreaState>(state.Areas);
            var cities = new Dictionary<int, CityState>(state.Cities);
            foreach (var owner in areas.Values.ToArray())
            {
                bool moved = owner.Id == changedArea;
                if (!moved && owner.HexId != physical) continue;
                int? city = moved ? AreaCityProjection.FromNative(Need(current.ReadAreaCitySlot(new(owner.Id)), "restore_published_Area35")) : owner.CityId;
                areas[owner.Id] = owner with { CityId = city, ForceId = force == 0 ? null : force };
                if (owner.CityId == city) continue;
                if (owner.CityId is int previousId)
                {
                    var previous = cities.GetValueOrDefault(previousId) ?? throw Missing("restore_previous_City_owner");
                    cities[previousId] = previous with { AreaIds = previous.AreaIds.Where(id => id != owner.Id).ToArray() };
                }
                if (city is int receivingId)
                {
                    var next = cities.GetValueOrDefault(receivingId) ?? throw Missing("restore_new_City_owner");
                    cities[receivingId] = next with { AreaIds = next.AreaIds.Where(id => id != owner.Id).Append(owner.Id).Order().ToArray() };
                }
            }
            state = state with { Territory = new(state.Territory) { [physical] = tile with { ForceId = force == 0 ? null : force } },
                Areas = areas, Cities = cities };
        }
        private void QueueSceneryControl(BaseReference city, HexReference hex, int scenery)
        {
            int home = Need(current.ReadBaseObjectLink(city), "scenery_notify_City4E");
            foreach (int physical in Registry(RegistryKind.Army))
            {
                var army = new ArmyUnitReference(physical);
                int rawCommander = Need(current.ReadArmyUnitCommanderId(army), "scenery_notify_Army12");
                int person = PersonLookup(rawCommander);
                int actualHome = ValidPerson(person) ? Need(current.ReadPersonFirstLocation(person), "scenery_notify_PersonE2") : 0;
                if (actualHome != home || !Need(current.ReadArmyUnitNativeValidity(army), "scenery_notify_Army_validity") ||
                    Need(current.ReadArmyUnitRawHex2A(army), "scenery_notify_Army2A") != hex.PhysicalSlot) continue;
                //1B4AB0 captures the commander and its actual position inside
                //this source call; later playback does not reselect a speaker.
                if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(state, current);
                var date = borrowedTerritoryDate ?? state.NativeMapSeason?.ThirdDate ?? throw Missing("scenery_notify_source_date");
                int position = PersonReturnCommands.ResolvePersonPosition(state, current, person, date).PhysicalSlot ?? 48400;
                var arena = state.ReadArmyLifecycle() ?? throw Missing("scenery_notify_lifecycle");
                var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw Missing("scenery_notify_Army_owner");
                int id = receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw Missing("scenery_notify_Army_identity");
                state = package.QueueSceneryControlArmySpeechCurrent(state, current, id, Identity(person), position, scenery);
                state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw Missing("scenery_notify_Personnel_owner") };
            }
        }
        internal GameState ApplyRelation(ForceReference first, ForceReference second, int amount, int notify)
        {
            ChangeRelation(first, second, amount, notify);
            return state;
        }
        private void ChangeRelation(ForceReference first, ForceReference second, int amount, int notify)
        {
            ForceRelationChangeCurrent.ApplyMonthly(() => state, value => state = value, current, first, second,
                amount, notify, P, Queue,
                (root, a, b, queueAfterFormat) =>
                {
                    string text = Format(root, new(Force0: Physical(a), Force1: Physical(b)));
                    if (queueAfterFormat) Queue(8);
                    Narration(text);
                }, Policy, ConsumeSpecialPersonnel, package.ReadApplicationGameplay(), removedPeople: RecallSpecialPersonnel);
        }
        private void ApplyCrossingConsequences(ForceReference offender, ForceReference opposite)
        {
            int low = P(SimulationParameterKeys.MonthlyMaintenance.CrossingRelationBase);
            int threshold = P(SimulationParameterKeys.MonthlyMaintenance.CrossingRelationThreshold);
            int high = P(SimulationParameterKeys.MonthlyMaintenance.CrossingRelationHighBase);
            int old = Relation(offender, Physical(opposite), false);
            int delta = old < threshold
                ? unchecked(P(SimulationParameterKeys.MonthlyMaintenance.CrossingRelationAddend) + low)
                : unchecked(high + P(SimulationParameterKeys.MonthlyMaintenance.CrossingRelationAddend) - old);
            Queue(Player(offender) ? 6 : 8);
            Narration(Format(0x324B, new(Person0: Ruler(offender), Person1: Ruler(opposite), Force0: 0, Force1: 0)));
            ChangeRelation(offender, opposite, delta, 1);
            int otherDelta = unchecked(P(SimulationParameterKeys.MonthlyMaintenance.ThirdForceRelationAddend) +
                P(SimulationParameterKeys.MonthlyMaintenance.ThirdForceRelationBase));
            foreach (int physical in Registry(RegistryKind.Force))
            {
                var third = Force(physical);
                if (!Valid(third) || Physical(third) == Physical(offender) || Physical(third) == Physical(opposite)) continue;
                ChangeRelation(offender, third, otherDelta, 0);
            }
            ApplyCrossingLoyalty(offender);
        }
        private int Family(int person, int field) => Need(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "person_family_" + field);
        private bool ValidPerson(int person) => Need(current.ReadPersonNativeValidity(person), "person_validity");
        private int Identity(int person) => Need(current.ReadPersonIdentity(person), "person_identity");
        private bool Spouse(int first, int second) => ValidPerson(first) && ValidPerson(second) &&
            !(ValidPerson(first) && ValidPerson(second) && Identity(first) == Identity(second)) &&
            Family(first, 0xBA) != 0 && Family(first, 0xBA) == Identity(second);
        private bool Sworn(int first, int second) => ValidPerson(first) && ValidPerson(second) &&
            !(ValidPerson(first) && ValidPerson(second) && Identity(first) == Identity(second)) &&
            Family(first, 0xBC) != 0 && Family(first, 0xBC) == Family(second, 0xBC);
        private bool Likes(int first, int second)
        {
            //233010 checks only the second person's validity, then the
            //eight raw C0 identities. No first validity or self shortcut.
            if (!ValidPerson(second)) return false;
            for (int i = 0; i < 8; i++)
                if (Need(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i), "person_like_slot") == Identity(second)) return true;
            return false;
        }
        private bool Dislikes(int first, int second)
        {
            //232B30 is the analogous D0 test, not the C0/like predicate.
            if (!ValidPerson(second)) return false;
            for (int i = 0; i < 8; i++)
                if (Need(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, first, i), "person_dislike_slot") == Identity(second)) return true;
            return false;
        }
        private int Doctrine(int person)
        {
            var owner = PersonOwner(person);
            return OfficerDoctrineRules.ReadDoctrineId(owner.DoctrineId, owner.AbilityState) ?? throw Missing("person_doctrine");
        }
        private void ApplyCrossingLoyalty(ForceReference offender)
        {
            int ruler = PersonLookup(Need(current.ReadForceRecord(Need(offender.PhysicalSlot, "loyalty_force_pointer"))?.RawRuler10, "loyalty_ruler"));
            bool notice = false;
            int forceKey = Physical(offender);
            foreach (int person in Registry(RegistryKind.Person))
            {
                if (forceKey is < 1 or > 51 || Need(current.ReadPersonOldForceSlot(person), "service_person_force") != forceKey) continue;
                byte status = Need(current.ReadPersonStatus(person), "service_person_status");
                if (status is < 1 or > 4) continue;
                if (Identity(person) == Identity(ruler) || Spouse(ruler, person) || Sworn(ruler, person) ||
                    Family(ruler, 0xB2) == Family(person, 0xB2) || Likes(person, ruler)) continue;
                byte before = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "person_loyalty");
                if (before > 100) continue;
                int doctrineLoss = P(SimulationParameterKeys.MonthlyMaintenance.DifferentDoctrineLoyaltyLoss);
                if (Doctrine(ruler) == Doctrine(person)) doctrineLoss = P(SimulationParameterKeys.MonthlyMaintenance.SameDoctrineLoyaltyLoss);
                int bound = P(SimulationParameterKeys.MonthlyMaintenance.LoyaltyRandomBound);
                int otherLoss = P(SimulationParameterKeys.MonthlyMaintenance.LoyaltyOtherAffinityLoss);
                int affinityLoss = otherLoss;
                if (!Dislikes(person, ruler) && ValidPerson(ruler) && ValidPerson(person))
                {
                    affinityLoss = P(SimulationParameterKeys.MonthlyMaintenance.LoyaltyCloseAffinityLoss);
                    int distance = Math.Abs(Need(OwnedPersonRelationInputs.ReadCurrentAffinity(state, ruler), "ruler_affinity") -
                        Need(OwnedPersonRelationInputs.ReadCurrentAffinity(state, person), "person_affinity"));
                    distance = Math.Min(Math.Min(distance, 150 - distance), 75);
                    if (distance is > 15 and < 31) affinityLoss = P(SimulationParameterKeys.MonthlyMaintenance.LoyaltyMiddleAffinityLoss);
                    else if (distance >= 31) affinityLoss = otherLoss;
                }
                int sum = unchecked(Draw(bound) + affinityLoss + doctrineLoss);
                if (sum > 0) notice = true;
                byte freshLoyalty = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "person_fresh_loyalty");
                byte after = (byte)Math.Clamp(freshLoyalty - (sum & 0xFF), 0, 255);
                //The named policy bit is owned separately from residual flags.
                bool maximum = Need(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "person_loyalty_policy");
                if (maximum) after = 255;
                current.SetPersonDestructionLoyaltyE8(person, after);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
                Observe($"monthly.loyalty.written.{person}");
                _ = Physical(offender); //fresh iterator end key.
            }
            if (notice && Player(offender)) Narration(Format(0x3174));
        }
    }
}
