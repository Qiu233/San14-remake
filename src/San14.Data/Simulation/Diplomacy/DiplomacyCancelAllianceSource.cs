using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //821BE0. One borrowed current and primary pool span the detection list,
    //territory writers,292BA0, relations and per-person App32 draws.
    private GameState ApplyDiplomacyCancelAllianceCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, List<CampaignEffectRequest> effects)
    {
        var q = new DiplomacyQuery(this, state, current, task);
        int own = q.OwnOrdinal, other = q.OtherOrdinal, ruler = q.Ruler(q.Own);
        _ = DRulerHome(q, q.Own); _ = DRulerHome(q, q.Other);
        var armies = current.CreateNearbyTemporary();
        var working = state;
        Dictionary<int, TerritoryState>? ownedTerritory = null;
        try
        {
            bool trespass = false;
            foreach (int army in CancelArmies(own))
            {
                if (DNeed(current.ReadHexForceSlot(ArmyHex(army)), "cancel_Army_Hex14") == other)
                { trespass = true; break; }
                current.AppendNearbyArmyTemporary(armies, new(army));
            }
            //Only armies preceding a direct hit are appended. With no direct
            //hit the list contains the actual complete accepted iterator.
            if (!trespass && (current.ReadRegistryHead(new(armies.Identity)) ?? throw DiplomacyNumericMissing("cancel_Army_head")).Identity.HasValue)
            {
                var nearby = current.CreateNearbyTemporary();
                try
                {
                    foreach (var location in q.ForceBases(q.Other))
                    {
                        var center = CurrentGateMapQueries.FindHex(location, current);
                        for (int radius = 1; radius < 4; radius++)
                        {
                            var ring = current.CreateNearbyTemporary();
                            try
                            {
                                NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, radius, ring);
                                foreach (var hex in Hexes(ring)) current.AppendNearbyHexTemporary(nearby, hex);
                            }
                            finally { current.ClearNearbyTemporary(ring, true); }
                        }
                    }
                    foreach (int army in ArmyPayloads())
                    {
                        var position = ArmyHex(army);
                        if (!Hexes(nearby).Any(hex => hex == position)) continue;
                        trespass = true; break;
                    }
                }
                finally { Release(nearby); }
            }

            if (!BothCoalitionMembers())
            { RestoreOccupiedHexes(own, other, false); RestoreOccupiedHexes(other, own, false); }

            //292BA0 first revokes the bilateral low-seven-bit counters,
            //then settles each matching still-live attack request.
            working = DSetAllianceMonths(working, own, other, 0);
            CancelRequest(own, other); CancelRequest(other, own);
            if (!BothCoalitionMembers())
            { RestoreOccupiedHexes(own, other, true); RestoreOccupiedHexes(other, own, true); }
            //292BA0(param3=0) does not run276DB0's extra Army refresh loop.

            working = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(working, current,
                own, unchecked(-P("cancel-popularity-loss")));
            int relation = DRelations(working, own)[other];
            int adjustment = trespass ? P("cancel-trespass-adjustment") : 0;
            adjustment = unchecked(adjustment + (relation < P("cancel-low-relation-threshold")
                ? P("cancel-low-relation-adjustment") : unchecked(P("cancel-high-relation-target") - relation)));
            working = ApplyDiplomacyRelationCurrent(working, current, own, other, adjustment, effects);
            int thirdAdjustment = unchecked(P("cancel-third-force-adjustment") +
                (trespass ? P("cancel-third-force-trespass-adjustment") : 0));
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw DiplomacyNumericMissing("cancel_Force_registryC0"))
            {
                int third = DNeed(entry?.PhysicalSlot, "cancel_Force_payload");
                if (third == own || third == other || third == 51 || third is >= 46 and <= 50) continue;
                working = ForceRelationChangeCurrent.ApplyWithoutPresentation(working, current, own, third, thirdAdjustment,
                    key => ArmyDestructionParameterSeed.ReadCurrent(working, key) ?? throw DiplomacyNumericMissing("cancel_relation_parameter_" + key));
            }

            bool loyaltyReduced = false;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Person) ?? throw DiplomacyNumericMissing("cancel_Person_registry100"))
            {
                int person = DNeed(entry?.PhysicalSlot, "cancel_Person_payload");
                //334810:22CF60, then232F10(mode0); no additional Person
                //native-validity predicate precedes the raw relationship fields.
                if (own is < 1 or > 51 || DNeed(current.ReadPersonOldForceSlot(person), "cancel_Person_Force22CF60") != own) continue;
                if (DNeed(current.ReadPersonStatus(person), "cancel_Person_E6") is < 1 or > 4) continue;
                if (q.Spouse(person, ruler) || q.Sworn(person, ruler) || q.Word(person, 0xB2) == q.Word(ruler, 0xB2) ||
                    q.RawHit(person, ruler, true)) continue;
                byte before = DNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, person, _governorInputs), "cancel_Person_E8");
                if (before >= 101) continue;
                int loss = P("cancel-loyalty-fixed-loss");
                if (Doctrine(person) == Doctrine(ruler)) loss = P("cancel-loyalty-same-doctrine-loss");
                int additional = 0;
                if (trespass)
                {
                    additional = P("cancel-loyalty-disliked-penalty");
                    if (!q.RawHit(person, ruler, false))
                    {
                        int distance = Affinity(ruler, person);
                        //Keep the existing scalar keys: E0 is the <=15 band,
                        //E8 is16..30, despite their historical key labels.
                        if (distance <= 15) additional = P("cancel-loyalty-affinity-middle-penalty");
                        else if (distance < 31) additional = P("cancel-loyalty-affinity-near-penalty");
                    }
                }
                int draw = Draw(P("cancel-loyalty-random-bound"));
                int after = Math.Max(0, unchecked(unchecked(before - additional) - loss - draw));
                if (after < before) loyaltyReduced = true;
                byte raw = unchecked((byte)after);
                if (DNeed(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "cancel_Person_bit13")) raw = 255;
                current.SetPersonDestructionLoyaltyE8(person, raw);
                working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, person, raw);
            }
            if (loyaltyReduced)
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw DiplomacyNumericMissing("cancel_player_Force");
                if (DNeed(current.ReadForcePhysicalSlot(player), "cancel_player60") != 0 &&
                    DNeed(current.ReadForcePhysicalSlot(player), "cancel_player60") == own)
                    working = WriteDiplomacyCancelLoyaltyNoticeCurrent(working, current, effects);
            }
            return working;
        }
        finally { Release(armies); }

        int P(string key) => DParameter(working, key);
        IEnumerable<int> CancelArmies(int force)
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw DiplomacyNumericMissing("cancel_Army_registry60"))
            {
                if (force is < 1 or > 51) continue;
                int physical = DNeed(entry?.PhysicalSlot, "cancel_Army_payload");
                if (current.ReadArmyUnitRawForceSlot(new(physical)) != force) continue;
                if (DNeed(current.ReadOwnedArmyExclusion233560(entry!), "cancel_Army233560")) continue;
                yield return physical;
            }
        }
        HexReference ArmyHex(int army)
        {
            int raw = DNeed(current.ReadArmyUnitRawHex2A(new(army)), "cancel_Army2A");
            return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw DiplomacyNumericMissing("cancel_Army_Hex_pointer");
        }
        IEnumerable<int> ArmyPayloads()
        {
            var node = current.ReadRegistryHead(new(armies.Identity)) ?? throw DiplomacyNumericMissing("cancel_Army_temporary_head");
            while (node.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(node) ?? throw DiplomacyNumericMissing("cancel_Army_temporary_payload");
                yield return DNeed(entry.PhysicalSlot, "cancel_Army_temporary_pointer");
                node = current.ReadRegistryNodeNext(node) ?? throw DiplomacyNumericMissing("cancel_Army_temporary_next");
            }
        }
        IEnumerable<HexReference> Hexes(HexListHandle list)
        {
            var node = current.ReadHexListHead(list) ?? throw DiplomacyNumericMissing("cancel_Hex_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadHexListNodePayload(node) ?? throw DiplomacyNumericMissing("cancel_Hex_payload");
                node = current.ReadHexListNodeNext(node) ?? throw DiplomacyNumericMissing("cancel_Hex_next");
            }
        }
        void Release(HexListHandle list) => current.ClearNearbyTemporary(list, true);
        bool BothCoalitionMembers() => (Flags(own) & 2) != 0 && (Flags(other) & 2) != 0;
        byte Flags(int force) => working.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true
            ? DNeed(reset[0x12], "cancel_reset_Force12") : DNeed(working.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags12, "cancel_Force12");
        void CancelRequest(int force, int partner)
        {
            var objective = working.Forces.GetValueOrDefault(force)?.CombatObjective ?? throw DiplomacyNumericMissing("cancel_attack_request_owner");
            int requester = objective.RequestingForceId ?? throw DiplomacyNumericMissing("cancel_attack_request15F");
            if (requester != partner) return;
            int months = objective.RemainingMonths ?? throw DiplomacyNumericMissing("cancel_attack_request161");
            if (months == 0) return;
            var owner = working.Forces[force];
            working = working with { Forces = new(working.Forces)
            { [force] = owner with { CombatObjective = objective with { RemainingMonths = 0 } } } };
            working = CancelDiplomacyAttackRequestCurrent(working, current, force, effects);
        }
        void RestoreOccupiedHexes(int force, int enemy, bool enqueueEncirclement)
        {
            foreach (int army in CancelArmies(force))
            {
                var hex = ArmyHex(army);
                if (DNeed(current.ReadHexForceSlot(hex), "cancel_restore_Hex14") != enemy) continue;
                current.AssignHexForce(hex, 0); //2413D0 precedes185D00, even if its admission fails.
                PublishHex(hex);
                var home = current.FindArmyUnitCurrentBase(new(army));
                var city = CurrentGateMapQueries.FindCity(home, current);
                int cityId = DNeed(city.PhysicalSlot, "cancel_restore_Home_City");
                ApplyHexOccupation(hex, force, cityId, enqueueEncirclement);
            }
        }
        void ApplyHexOccupation(HexReference hex, int force, int city, bool encirclement)
        {
            if (!hex.PhysicalSlot.HasValue ||
                !GateCaptureTerritorySource.Eligible(working, current, hex, force, false)) return;
            if (DNeed(current.ReadHexForceSlot(hex), "cancel_restore_fresh_Hex14") == force) return;
            if (force is >=46 and <=51) throw DiplomacyNumericMissing("cancel_restore_special_Force185D00");
            int surface = DNeed(current.ReadHexSurfaceCode(hex), "cancel_restore_Hex10");
            AreaState? centerBefore = null;
            int areaId = 0;
            if (surface is >=15 and <=18 && force is >=1 and <=45 && city is >=1 and <=51)
            {
                areaId = DNeed(current.ReadHexAreaSlot(hex), "cancel_center_Area12");
                centerBefore = working.Areas.GetValueOrDefault(areaId) ?? throw DiplomacyNumericMissing("cancel_center_Area_owner");
                working = PrepareRegionCenterTransfer(working, current, hex, force, city, encirclement);
            }
            byte rawScenery = DNeed(current.ReadHexScenerySlot(hex), "cancel_restore_Hex1E");
            var scenery = current.ReadSceneryTableReference(rawScenery <=100 ? rawScenery : 0) ?? throw DiplomacyNumericMissing("cancel_restore_Scenery_pointer");
            if (DNeed(current.ReadSceneryNativeValidity(scenery), "cancel_restore_Scenery_validity") &&
                (DNeed(current.ReadSceneryFlags47(scenery), "cancel_restore_Scenery47") & 1) != 0 && force is >=1 and <=45 && city is >=1 and <=51)
                throw DiplomacyNumericMissing("cancel_restore_Scenery31_feedback_owner");
            current.AssignHexForce(hex, force); PublishHex(hex);
            if (centerBefore is not null)
            {
                int? assigned = AreaCityProjection.FromNative(DNeed(current.ReadAreaCitySlot(new(areaId)), "cancel_center_Area35"));
                var cities = new Dictionary<int, CityState>(working.Cities);
                if (centerBefore.CityId != assigned)
                {
                    if (centerBefore.CityId is int previousId)
                    {
                        var oldCity = cities.GetValueOrDefault(previousId) ?? throw DiplomacyNumericMissing("cancel_center_old_City");
                        cities[previousId] = oldCity with { AreaIds = oldCity.AreaIds.Where(value => value != areaId).ToArray() };
                    }
                    if (assigned is int receivingId)
                    {
                        var newCity = cities.GetValueOrDefault(receivingId) ?? throw DiplomacyNumericMissing("cancel_center_new_City");
                        cities[receivingId] = newCity with { AreaIds = newCity.AreaIds.Where(value => value != areaId).Append(areaId).Order().ToArray() };
                    }
                }
                working = working with { Cities = cities, Areas = new(working.Areas)
                { [areaId] = centerBefore with { CityId = assigned, ForceId = force == 0 ? null : force } } };
            }
            var mode = current.ArmyReturnTransferMode ?? throw DiplomacyNumericMissing("cancel_restore_transfer_mode");
            if (mode.Present && DNeed(mode.Value, "cancel_restore_transfer_mode_value") !=0)
                throw DiplomacyNumericMissing("cancel_restore_tutorial_counter");
        }
        void PublishHex(HexReference hex)
        {
            int id = DNeed(hex.PhysicalSlot, "cancel_restore_Hex_identity");
            var tile = working.Territory.GetValueOrDefault(id) ?? throw DiplomacyNumericMissing("cancel_restore_Territory_owner");
            int force = DNeed(current.ReadHexForceSlot(hex), "cancel_restore_written_Hex14");
            ownedTerritory ??= new(working.Territory);
            ownedTerritory[id] = tile with { ForceId = force };
            working = working with { Territory = ownedTerritory };
        }
        int Doctrine(int person)
        {
            var officer = working.Officers.GetValueOrDefault(q.Id(person)) ?? throw DiplomacyNumericMissing("cancel_Doctrine_Person_owner");
            return officer.DoctrineId ?? officer.AbilityState?.ObservedDoctrineId ?? throw DiplomacyNumericMissing("cancel_Person14C");
        }
        int Affinity(int first, int second)
        {
            if (!q.Valid(first) || !q.Valid(second)) return 0;
            int a = DNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(working, first), "cancel_rulerBE");
            int b = DNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(working, second), "cancel_PersonBE");
            int distance = Math.Abs(a-b); return Math.Min(Math.Min(distance,150-distance),75);
        }
        int Draw(int bound)
        {
            if (bound<2) return 0;
            var saved = working.CampaignAppRandom32 ?? throw DiplomacyNumericMissing("cancel_App32");
            var random = new SharedSourceRandom32(saved.State); int value=random.Next(bound);
            working = working with { CampaignAppRandom32 = new(random.State) }; return value;
        }
    }
}
