using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //890BF0 consumes the ordered allowed/help pairs, not the later page's
    //editable offer, executor sorting, reserved books or target-name rows.
    private MapMainCommandEntryPresentation ReadDiplomacyCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, bool? restricted, Action bindCurrent,
        Action bindIndexes, Action bindItems, Action bindAreas)
    {
        try
        {
            var kind = command switch
            {
                MapMenuCommand.Goodwill => DiplomaticMissionKind.Goodwill,
                MapMenuCommand.Alliance => DiplomaticMissionKind.Alliance,
                MapMenuCommand.CancelAlliance => DiplomaticMissionKind.CancelAlliance,
                MapMenuCommand.ReturnCaptives => DiplomaticMissionKind.ReturnCaptives,
                MapMenuCommand.RequestAttack => DiplomaticMissionKind.RequestAttack,
                MapMenuCommand.DemandSurrender => DiplomaticMissionKind.DemandSurrender,
                MapMenuCommand.Exchange => DiplomaticMissionKind.Exchange,
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            MapMainCommandEntryPresentation No(int help) => new(command, false, help);
            if (kind is not (DiplomaticMissionKind.CancelAlliance or DiplomaticMissionKind.ReturnCaptives))
            {
                if (restricted is null) throw MainCommandMissing("diplomacy_application_selection");
                if (restricted.Value) return No(0x11171);
            }
            var player = current.CurrentPlayerForce ?? throw MainCommandMissing("diplomacy_player");
            var district = ForcePrimaryDistrictSource.Resolve(current, player);
            int districtId = MainNeed(district.PhysicalSlot, "diplomacy_primary_district");
            int leader = DiplomacyMenuPerson(current,
                MainNeed(current.ReadDistrictLeader(districtId), "diplomacy_District12"));
            //233080 does not test District validity or write the old help.
            if (!MainNeed(current.ReadPersonNativeValidity(leader), "diplomacy_leader_validity") ||
                MainNeed(current.ReadPersonStatus(leader), "diplomacy_leader_status") != 1)
                return new(command, false, null) { RetainGameMessage = true };
            int rawForce = MainNeed(current.ReadDistrictForceId(districtId), "diplomacy_District10");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                ?? throw MainCommandMissing("diplomacy_district_Force");
            int own = MainNeed(force.PhysicalSlot, "diplomacy_Force_identity");
            if (kind is DiplomaticMissionKind.RequestAttack or DiplomaticMissionKind.DemandSurrender)
            {
                int rawRank = MainNeed(current.ReadMoraleWaveForceRank47(own), "diplomacy_rank47");
                int rank = MainNeed(current.ReadStaticTableReference(0x78490, rawRank < 13 ? rawRank : 0)?.PhysicalSlot,
                    "diplomacy_rank_pointer");
                int mask = kind == DiplomaticMissionKind.RequestAttack ? 4 : 2;
                if (rank == 0 || (MainNeed(_scenario.Snapshot.RankParameters?.Ranks.ElementAtOrDefault(rank)?.MainCommandFlags,
                    "diplomacy_rank_B2") & mask) == 0) return No(0x5991);
            }
            if (MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "diplomacy_books") <
                DiplomacyCommandSource.ReadBooks(state, kind)) return No(0x57C7);
            if (kind is DiplomaticMissionKind.Goodwill or DiplomaticMissionKind.Alliance or
                DiplomaticMissionKind.RequestAttack or DiplomaticMissionKind.Exchange)
            {
                if (!DiplomacyMenuHasOffer(state, current, force, kind, bindIndexes, bindItems, bindAreas)) return No(0x5992);
            }
            else if (kind == DiplomaticMissionKind.ReturnCaptives)
                _ = DiplomacyMenuCapital(current, own); //70E050 performs this call before7109B0.
            bindCurrent();
            if (!DiplomacyMenuHasExecutor(current, own, districtId)) return No(0x598E);
            if (kind is DiplomaticMissionKind.CancelAlliance or DiplomaticMissionKind.RequestAttack)
            {
                bool reverseTreaty = false;
                foreach (var entry in DiplomacyMenuRegistry(current, RegistryKind.Force))
                    if (DiplomacyMenuMonths(state, MainNeed(entry.PhysicalSlot, "diplomacy_reverse_Force"), own) != 0)
                    { reverseTreaty = true; break; }
                if (!reverseTreaty) return No(0x5990);
            }
            if (kind == DiplomaticMissionKind.ReturnCaptives && !DiplomacyMenuHasFormerCaptive(current, rawForce))
                return No(0x5999);
            bool any = DiplomacyMenuTargets(state, current, kind, force);
            int help = kind switch
            {
                DiplomaticMissionKind.Goodwill => any ? 0x5994 : 0x5993,
                DiplomaticMissionKind.Alliance => any ? 0x5996 : 0x5995,
                DiplomaticMissionKind.CancelAlliance => any ? 0x5998 : 0x5997,
                DiplomaticMissionKind.ReturnCaptives => any ? 0x599B : 0x599A,
                DiplomaticMissionKind.RequestAttack => any ? 0x599D : 0x599C,
                DiplomaticMissionKind.DemandSurrender => any ? 0x599F : 0x599E,
                _ => any ? 0x59A1 : 0x59A0
            };
            return new(command, any, help);
        }
        catch (InvalidDataException error) { return new(command, null, null, error.Message); }
    }

    private static int DiplomacyMenuPerson(SourceWorldContext current, ushort raw) => MainNeed(
        (raw < 6001 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
        "diplomacy_Person_pointer");

    private static BaseReference DiplomacyMenuCapital(SourceWorldContext current, int own)
    {
        int ruler = DiplomacyMenuPerson(current, current.ReadForceRecord(own)?.RawRuler10
            ?? throw MainCommandMissing("diplomacy_ruler10"));
        return BaseLookup.Find(MainNeed(current.ReadPersonFirstLocation(ruler), "diplomacy_ruler_E2"), current);
    }

    private bool DiplomacyMenuHasExecutor(SourceWorldContext current, int own, int district)
    {
        if (district is < 1 or > 51) return false;
        foreach (int person in NormalTurnServingPeople(current, own, district, false))
            if (MainNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "diplomacy_health161") < 2 &&
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "diplomacy_flags15E") & 1) == 0 &&
                !PersonCurrentBusySource.Read(current, person, MainCommandMissing) &&
                !DuelAftermathSpecialPerson(current, person)) return true;
        return false;
    }

    private static IEnumerable<RegistryObjectReference> DiplomacyMenuRegistry(SourceWorldContext current, RegistryKind kind)
    {
        foreach (var entry in current.ReadRegistryReferences(kind) ?? throw MainCommandMissing("diplomacy_registry_" + kind))
        {
            if (entry is null || entry.Kind != kind || !entry.PhysicalSlot.HasValue)
                throw MainCommandMissing("diplomacy_registry_payload_" + kind);
            yield return entry;
        }
    }

    private bool DiplomacyMenuHasOffer(GameState state, SourceWorldContext current, ForceReference force,
        DiplomaticMissionKind kind, Action bindIndexes, Action bindItems, Action bindAreas)
    {
        int own = MainNeed(force.PhysicalSlot, "diplomacy_offer_Force");
        var capital = DiplomacyMenuCapital(current, own);
        int ruler = DiplomacyMenuPerson(current, current.ReadForceRecord(own)?.RawRuler10
            ?? throw MainCommandMissing("diplomacy_offer_ruler10"));
        int gold = MainNeed(current.ReadBaseResource(capital, BaseResourceKind.Gold), "diplomacy_offer_gold");
        int food = MainNeed(current.ReadBaseResource(capital, BaseResourceKind.Supplies), "diplomacy_offer_food");
        //Existing installed command profile supplies these independently named
        //operands. Older profiles retain the already-used1000/10000 minima;
        //this finite TC source does not claim a separate SC numeric audit.
        string key = "simulation.diplomacy." + (kind switch
        {
            DiplomaticMissionKind.Goodwill => "goodwill", DiplomaticMissionKind.Alliance => "alliance",
            DiplomaticMissionKind.RequestAttack => "request-attack", _ => "exchange"
        }) + "-entry-";
        int minGold = DiplomacyMenuParameter(state, key + "minimum-gold", 1000);
        int minFood = DiplomacyMenuParameter(state, key + "minimum-food", 10000);
        if (gold >= minGold || food >= minFood) return true;
        //QueryMapHeader captures current Person/Base fields but deliberately
        //does not open the command manager. The reached Item/Area iterators
        //still require their actual saved registration graph, not opening lists.
        bindIndexes();
        int rulerId = MainNeed(current.ReadPersonIdentity(ruler), "diplomacy_offer_ruler_identity");
        bool itemsBound = false;
        foreach (var item in DiplomacyMenuRegistry(current, RegistryKind.Item))
        {
            if (!itemsBound)
            {
                if (state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(PersonArmyCaptureInputs.VersionKey) != 1)
                    throw MainCommandMissing("diplomacy_offer_Item_current_profile");
                bindItems();
                itemsBound = true;
            }
            int id = MainNeed(item.PhysicalSlot, "diplomacy_offer_Item");
            if (MainNeed(current.ReadCaptureItemOwner4A(id), "diplomacy_Item_owner") != rulerId) continue;
            if (!MainNeed(current.ReadRegistryObjectNativeValidity(item), "diplomacy_Item_validity") ||
                !MainNeed(current.ReadForceNativeValidity(force), "diplomacy_offer_Force_validity")) continue;
            if (MainNeed(current.ReadCaptureItemOwner4A(id), "diplomacy_Item_owner_predicate") !=
                MainNeed(current.ReadMoraleWaveRuler22EAC0(force), "diplomacy_Item_Force_ruler")) continue;
            if ((MainNeed(state.Items.GetValueOrDefault(id)?.Lifecycle?.RetainedFlags, "diplomacy_Item4F") & 1) != 0 &&
                MainNeed(current.ReadRegistryItemRawField50(item), "diplomacy_Item50") == 1) return true;
        }
        if (own is < 1 or > 51) return false;
        bool statisticsBound = false;
        void BindStatistics(AreaReference area)
        {
            var statistics = state.RegionStatistics ?? throw MainCommandMissing("diplomacy_offer_Area_statistics");
            if (!statistics.Entries.ContainsKey(MainNeed(area.PhysicalSlot, "diplomacy_offer_Area_identity")))
                throw MainCommandMissing("diplomacy_offer_Area_statistics_row");
            if (!statisticsBound) { bindAreas(); statisticsBound = true; }
        }
        foreach (var area in current.ReadRegisteredAreaReferences() ?? throw MainCommandMissing("diplomacy_Area_registry"))
        {
            if (area is null) throw MainCommandMissing("diplomacy_Area_receiver");
            if (MainNeed(current.ReadAreaCurrentForceSlot(area), "diplomacy_Area_iterator_Force") != own ||
                !MainNeed(current.ReadAreaNativeValidity(area), "diplomacy_Area_iterator_validity")) continue;
            if (DiplomacyMenuAreaOffer(current, area, own, () => BindStatistics(area))) return true;
        }
        return false;
    }

    //3340F0 is the first, recipient-independent half of333F20. It has no
    //recipient-neighbor test and does not open the gift selector.
    private static bool DiplomacyMenuAreaOffer(SourceWorldContext current, AreaReference area, int own, Action bindStatistics)
    {
        int rawCity = MainNeed(current.ReadAreaCitySlot(area), "diplomacy_offer_Area35");
        var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
            ?? throw MainCommandMissing("diplomacy_offer_assigned_City");
        if (MainNeed(current.ReadBaseNativeValidity(city), "diplomacy_offer_City_validity") &&
            current.ReadBaseCurrentHexIndex(city) == MainNeed(current.ReadAreaHexIndex(area), "diplomacy_offer_Area3A")) return false;
        var center = DiplomacyAreaCenter(current, area);
        if (MainNeed(current.ReadHexSurfaceCode(center), "diplomacy_offer_surface") == 17 ||
            MainNeed(current.ReadAreaCurrentForceSlot(area), "diplomacy_offer_owner") != own) return false;
        bindStatistics();
        return MainNeed(current.ReadAreaRawField68(area), "diplomacy_offer_Area68") ==
            MainNeed(current.ReadAreaRawField76(area), "diplomacy_offer_Area76");
    }

    private static int DiplomacyMenuParameter(GameState state, string key, int retainedValue)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw MainCommandMissing("diplomacy_parameters");
        if (parameters.Float32Values.ContainsKey(key)) throw MainCommandMissing(key);
        return parameters.Int32Values.TryGetValue(key, out int value) ? value : retainedValue;
    }

    private static bool DiplomacyMenuHasFormerCaptive(SourceWorldContext current, int rawForce)
    {
        foreach (var entry in DiplomacyMenuRegistry(current, RegistryKind.Person))
        {
            int p = MainNeed(entry.PhysicalSlot, "diplomacy_captive_Person");
            if (!MainNeed(current.ReadPersonNativeValidity(p), "diplomacy_captive_validity") ||
                MainNeed(current.ReadPersonStatus(p), "diplomacy_captive_status") != 6) continue;
            if (MainNeed(current.ReadOfficerAftermath(p)?.SourceKnowledge?.PreviousForceCode, "diplomacy_captive165") == rawForce ||
                MainNeed(current.ReadPersonOldForceSlot(p), "diplomacy_captive_old_Force") == rawForce) return true;
        }
        return false;
    }

    private bool DiplomacyMenuTargets(GameState state, SourceWorldContext current, DiplomaticMissionKind kind,
        ForceReference force)
    {
        int own = MainNeed(force.PhysicalSlot, "diplomacy_targets_own");
        var list = FinalizationTemporary(current);
        try
        {
            current.ClearTemporaryHexList(list);
            foreach (var entry in DiplomacyMenuRegistry(current, RegistryKind.Force))
            {
                int other = MainNeed(entry.PhysicalSlot, "diplomacy_targets_other");
                if (other == own) continue;
                bool accept;
                switch (kind)
                {
                    case DiplomaticMissionKind.Goodwill:
                        accept = DRelations(state, own)[other] < 200 && !DiplomacyMenuCoalition(state, own, other) &&
                            (other is < 46 or > 50 || DiplomacyMenuSpecialGoodwill(state, current, force, other) &&
                                DiplomacyMenuForceByte(state, other, 0x194) == 0) &&
                            DiplomacyMenuInteraction(state, current, own, other) && !DiplomacyMenuPending(state, current, own, other);
                        break;
                    case DiplomaticMissionKind.Alliance:
                        accept = other is < 46 or > 50 && DiplomacyMenuMonths(state, own, other) <=
                            48 - DiplomacyMenuParameter(state, "simulation.diplomacy.alliance-entry-minimum-months", 6) &&
                            DLevel(state, DRelations(state, own)[other]) > 2 && DiplomacyMenuInteraction(state, current, own, other) &&
                            !DiplomacyMenuCoalition(state, own, other) && !DiplomacyMenuPending(state, current, own, other);
                        break;
                    case DiplomaticMissionKind.CancelAlliance:
                        accept = DiplomacyMenuMonths(state, other, own) != 0 && !DiplomacyMenuPending(state, current, own, other);
                        break;
                    case DiplomaticMissionKind.ReturnCaptives:
                        if (!DiplomacyMenuCoalition(state, own, other))
                            DiplomacyMenuWithCaptiveTargets(current, own, other, () =>
                            {
                                if (DiplomacyMenuInteraction(state, current, own, other) &&
                                    !DiplomacyMenuPending(state, current, own, other)) FinalizationAppend(current, list, entry);
                            });
                        continue;
                    case DiplomaticMissionKind.RequestAttack:
                        if (DiplomacyMenuMonths(state, own, other) != 0 &&
                            MainNeed(state.Forces.GetValueOrDefault(other)?.CombatObjective?.RemainingMonths, "diplomacy_target161") == 0 &&
                            DLevel(state, DRelations(state, own)[other]) > 3 && !DiplomacyMenuCoalition(state, own, other))
                            DiplomacyMenuWithAttackTargets(state, current, own, other, () =>
                            {
                                if (!DiplomacyMenuPending(state, current, own, other)) FinalizationAppend(current, list, entry);
                            });
                        continue;
                    case DiplomaticMissionKind.DemandSurrender:
                        accept = other is < 46 or > 50 && DiplomacyMenuMonths(state, own, other) == 0 &&
                            DLevel(state, DRelations(state, own)[other]) > 0 && DiplomacyMenuInteraction(state, current, own, other) &&
                            !DiplomacyMenuCoalition(state, own, other) && !DiplomacyMenuPending(state, current, own, other);
                        break;
                    default:
                        accept = other is < 46 or > 50 && DLevel(state, DRelations(state, own)[other]) > 3 &&
                            (DiplomacyMenuMonths(state, own, other) != 0 ||
                                (DiplomacyMenuForceByte(state, own, 0x12) & 2) != 0 && (DiplomacyMenuForceByte(state, other, 0x12) & 2) != 0) &&
                            !DiplomacyMenuPending(state, current, own, other);
                        break;
                }
                if (accept) FinalizationAppend(current, list, entry);
            }
            return MainNeed(current.ReadTemporaryHexListCount(list), "diplomacy_targets_count") != 0;
        }
        finally { FinalizationRelease(current, list); }
    }

    private static void DiplomacyMenuWithCaptiveTargets(SourceWorldContext current, int own, int other, Action nonempty)
    {
        var list = FinalizationTemporary(current);
        try
        {
            current.ClearTemporaryHexList(list);
            foreach (var entry in DiplomacyMenuRegistry(current, RegistryKind.Person))
            {
                int p = MainNeed(entry.PhysicalSlot, "diplomacy_target_captive_Person");
                if (!MainNeed(current.ReadPersonNativeValidity(p), "diplomacy_target_captive_validity") ||
                    MainNeed(current.ReadPersonStatus(p), "diplomacy_target_captive_status") != 6) continue;
                var at = BaseLookup.Find(MainNeed(current.ReadPersonFirstLocation(p), "diplomacy_target_captive_E2"), current);
                if (MainNeed(current.ReadBaseForceSlot(at), "diplomacy_target_captive_holding_Force") != other) continue;
                if (MainNeed(current.ReadPersonOldForceSlot(p), "diplomacy_target_captive_old_Force") != own &&
                    MainNeed(current.ReadOfficerAftermath(p)?.SourceKnowledge?.PreviousForceCode, "diplomacy_target_captive165") != own) continue;
                int position = PersonPositionRepair.DecodeSecondLocation(
                    MainNeed(current.ReadPersonSecondLocation(p), "diplomacy_target_captive_E4")).Kind;
                if (position is 5 or 6 or 27) FinalizationAppend(current, list, entry);
            }
            if (MainNeed(current.ReadTemporaryHexListCount(list), "diplomacy_captive_count") != 0) nonempty();
        }
        finally { FinalizationRelease(current, list); }
    }

    private void DiplomacyMenuWithAttackTargets(GameState state, SourceWorldContext current, int own, int other, Action nonempty)
    {
        var list = FinalizationTemporary(current);
        try
        {
            current.ClearTemporaryHexList(list);
            var neighbors = _eventCityNeighborhoods ??= new(_scenario);
            if (other is >= 1 and <= 51)
                foreach (var origin in current.ReadRegisteredBases() ?? throw MainCommandMissing("diplomacy_target_Base_registry"))
                {
                    if (origin is null) throw MainCommandMissing("diplomacy_target_Base_receiver");
                    if (MainNeed(current.ReadBaseForceSlot(origin), "diplomacy_origin_Force") != other ||
                        !MainNeed(current.ReadBaseNativeValidity(origin), "diplomacy_origin_validity")) continue;
                    var adjacent = FinalizationTemporary(current);
                    try
                    {
                        neighbors.FillCities22F430(current, origin, adjacent);
                        foreach (var candidate in DiplomacyMenuList(current, adjacent))
                        {
                            var city = candidate.Kind switch
                            {
                                RegistryKind.City => new BaseReference(BaseKind.City, candidate.PhysicalSlot),
                                RegistryKind.Gate => new BaseReference(BaseKind.Gate, candidate.PhysicalSlot),
                                _ => throw MainCommandMissing("diplomacy_neighbor_kind")
                            };
                            int victim = MainNeed(current.ReadBaseForceSlot(city), "diplomacy_victim_Force");
                            if (victim is < 1 or > 51) continue;
                            int link = MainNeed(current.ReadBaseObjectLink(city), "diplomacy_victim4E");
                            if (link is >= 52 and <= 61 || victim == other || victim == own ||
                                DiplomacyMenuMonths(state, other, victim) != 0 || DiplomacyMenuMonths(state, own, victim) != 0 ||
                                victim is >= 46 and <= 50) continue;
                            if (!DiplomacyMenuList(current, list).Contains(candidate)) FinalizationAppend(current, list, candidate);
                        }
                    }
                    finally { FinalizationRelease(current, adjacent); }
                }
            if (MainNeed(current.ReadTemporaryHexListCount(list), "diplomacy_attack_count") != 0) nonempty();
        }
        finally { FinalizationRelease(current, list); }
    }

    private static IEnumerable<RegistryObjectReference> DiplomacyMenuList(SourceWorldContext current, HexListHandle list)
    {
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw MainCommandMissing("diplomacy_list_head");
        while (node.Identity.HasValue)
        {
            yield return current.ReadRegistryNodePayload(node) ?? throw MainCommandMissing("diplomacy_list_payload");
            node = current.ReadRegistryNodeNext(node) ?? throw MainCommandMissing("diplomacy_list_next");
        }
    }

    private bool DiplomacyMenuPending(GameState state, SourceWorldContext current, int own, int other)
    {
        var manager = current.ReadOwnedPersonCommandManager() ?? throw MainCommandMissing("diplomacy_command_manager");
        foreach (var linked in current.ReadCurrentPersonLinkedEntities() ?? throw MainCommandMissing("diplomacy_command_chain"))
        {
            if (linked?.Identity is not int id) throw MainCommandMissing("diplomacy_command_receiver");
            if (manager.Diplomacies is not { } diplomacies || !diplomacies.TryGetValue(id, out var task)) continue;
            //22BB90 returns the actual District at Cmd28, not an executor.
            var district = current.ReadDistrictTableReference(task.SourceDistrictId is >= 0 and < 52 ? task.SourceDistrictId : 0)
                ?? throw MainCommandMissing("diplomacy_pending_District");
            if (!MainNeed(current.ReadDistrictNativeValidity(district), "diplomacy_pending_District_validity")) continue;
            int rawForce = MainNeed(current.ReadDistrictForceId(MainNeed(district.PhysicalSlot, "diplomacy_pending_District_identity")), "diplomacy_pending_Force10");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw MainCommandMissing("diplomacy_pending_Force");
            if (MainNeed(force.PhysicalSlot, "diplomacy_pending_Force_identity") != own) continue;
            if (task.NativeKind is not (>= 4 and <= 9 or 19)) continue;
            var target = current.ReadForceTableReference(task.TargetForceId is >= 0 and < 52 ? task.TargetForceId : 0)
                ?? throw MainCommandMissing("diplomacy_pending_target");
            if (MainNeed(target.PhysicalSlot, "diplomacy_pending_target_identity") == other &&
                DiplomacyContinuationAllowedCurrent(state with { PersonnelCommands = manager }, current, task).Allowed) return true;
        }
        return false;
    }

    private static int DiplomacyMenuMonths(GameState state, int own, int other) =>
        ReadReportForceRelationByte(state, new ForceReference(own), other, MainCommandMissing) & 127;

    private static byte DiplomacyMenuForceByte(GameState state, int force, int field)
    {
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
            return MainNeed(reset[field], "diplomacy_Force_reset" + field.ToString("X"));
        var owner = state.NativeArmySimulation?.Forces.GetValueOrDefault(force)
            ?? throw MainCommandMissing("diplomacy_Force_owner");
        return MainNeed(field switch { 0x12 => owner.Flags12,
            0x178 => owner.DiplomacyFlags178 is ushort flags ? unchecked((byte)flags) : (byte?)null,
            0x194 => owner.Flags194, _ => null }, "diplomacy_Force_field" + field.ToString("X"));
    }

    private static bool DiplomacyMenuCoalition(GameState state, int own, int other)
    {
        int selected = MainNeed(state.NativeArmySimulation?.World.SelectedForce1656, "diplomacy_selected_Force");
        return selected == own && (DiplomacyMenuForceByte(state, other, 0x12) & 2) != 0 ||
            (DiplomacyMenuForceByte(state, own, 0x12) & 2) != 0 && selected == other;
    }

    private static bool DiplomacyMenuInteraction(GameState state, SourceWorldContext current, int own, int other)
    {
        int a = DiplomacyMenuForceByte(state, own, 0x178) & 1;
        if (a == 0 && (DiplomacyMenuForceByte(state, other, 0x178) & 1) == 0) return true;
        bool IsPlayer(int id)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw MainCommandMissing("diplomacy_interaction_player");
            int physical = MainNeed(player.PhysicalSlot, "diplomacy_interaction_player_identity");
            return physical != 0 && physical == id;
        }
        if (a != 0 && !IsPlayer(other)) return true;
        return !IsPlayer(own) && (DiplomacyMenuForceByte(state, other, 0x178) & 1) != 0;
    }

    private bool DiplomacyMenuSpecialGoodwill(GameState state, SourceWorldContext current,
        ForceReference force, int recipient)
    {
        //The Force was already resolved from District10. Re-looking up its
        //physical identity as a table slot would apply an alias a second time.
        var app = ReadApplicationGameplay();
        if (app is not null) app = app with { TutorialAllowsSpecialGoodwill =
            TacticRemakeCurrentBattleControl.Read(ReadTutorialControlState(state), 54) };
        var result = PersonReturnPathSource.ReadSpecialPersonnelEligibility(state, current, force, recipient, app);
        return result.Value ?? throw MainCommandMissing(result.UnresolvedOwner ?? "diplomacy_special_Advantage");
    }
}
