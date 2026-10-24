using San14.Core;

namespace San14.Data;

internal sealed record NativeArmyFoodTurnResult(GameState State, ArmyMapGroupingOwner Groups, bool GroupsChanged);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual Turn food consumer, after Progress. Quotes are made
    /// at each reached bill, using its current receiver, quantities and stock;
    /// the provisional whole-period quantity ledger does not run alongside it.</summary>
    internal NativeArmyFoodTurnResult CompleteNativeArmyFoodTurn(GameState input,
        IPlayableNativeArmyProgressStageRules progressStages)
    {
        var working = input with { Cities = new(input.Cities), Gates = new(input.Gates), Units = new(input.Units) };
        var current = CaptureOwnedArmyReturnWorld(working).Context;
        working = ResetCityMerchantPeopleForTurn(working, current);
        var pool = current.ReadOwnedCurrentListPool() ?? throw FoodTurnMissing("current_pool");
        var groups = progressStages is OrdinaryArmyCombatStages progress
            ? progress.CopyMapGroupsForTurn() : throw FoodTurnMissing("owned_Progress_groups");
        bool groupsChanged = false;
        byte[]? terrain = null;
        TacticRemakeCurrentAbilities? abilities = null;
        // manager80 order is semantically significant, including reset and
        // shared mapped stocks. Do not replace it with all-garrisons/all-Armies.
        foreach (var entry in current.ReadRegisteredBases() ?? throw FoodTurnMissing("manager80"))
        {
            var city = entry ?? throw FoodTurnMissing("Base_pointer");
            int location = FoodTurnKnown(current.ReadBaseObjectLink(city), "Base_location");
            working = ResetCityMerchantCityForTurn(working, current, location);
            ResetExpenditure(city, location);
            if (location >= 52) continue;
            int rawForce = FoodTurnKnown(current.ReadBaseForceSlot(city), "City_Force");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw FoodTurnMissing("City_Force_pointer");
            if (!FoodTurnKnown(current.ReadForceNativeValidity(force), "City_Force_validity"))
            {
                if ((working.NativeMapSeason?.ThirdDate ?? throw FoodTurnMissing("Turn_date")).Day == 1)
                    working = RefreshCityMerchantMonth(working, current, city);
                continue;
            }
            if (location == 0 || !working.Cities.ContainsKey(location)) throw FoodTurnMissing("City_domain_receiver");
            if ((working.NativeMapSeason?.ThirdDate ?? throw FoodTurnMissing("Turn_date")).Day == 1)
                working = CompleteNativeMonthlySalary(working, current, city);

            BillGarrison(city, location);
            var garrisonGates = current.CreateNearbyTemporary();
            try
            {
                FillGates(city, garrisonGates);
                foreach (var gate in Bases(garrisonGates))
                    BillGarrison(gate, FoodTurnKnown(current.ReadBaseObjectLink(gate), "Gate_location"));
            }
            finally { current.ClearNearbyTemporary(garrisonGates, true); }

            // 2DCDB0 owns a fresh Gate list and Army list even when no Army
            // qualifies. Its allocator effects belong to this same Turn view.
            var gates = current.CreateNearbyTemporary();
            try
            {
                FillGates(city, gates);
                var armies = current.CreateNearbyTemporary();
                try
                {
                    foreach (var army in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
                    {
                        int physical = FoodTurnKnown(army.PhysicalSlot, "Army_pointer");
                        if (FoodTurnKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, physical), "Army_exclusion")) continue;
                        var image = ArmyImage(physical);
                        byte flags = image.NativeTacticRuntime?.Flags26 ?? throw FoodTurnMissing("Army26");
                        if ((flags & 2) == 0) continue;
                        var receiver = new ArmyUnitReference(physical);
                        var home = current.FindArmyUnitCurrentBase(receiver);
                        int homeLocation = FoodTurnKnown(current.ReadBaseObjectLink(home), "Home_location");
                        if (homeLocation != location && !(homeLocation is >= 52 and <= 61 && Bases(gates).Contains(home))) continue;
                        current.AppendNearbyArmyTemporary(armies, receiver);
                    }
                    SortArmies(armies);
                    bool any = false;
                    foreach (var army in Armies(armies))
                    {
                        any = true;
                        int physical = FoodTurnKnown(army.PhysicalSlot, "bill_Army_pointer");
                        var image = ArmyImage(physical);
                        terrain ??= ArmyFoodTerrainModifiers.Capture(working, current);
                        int due = ArmyFoodExpenseSource.Read(working, current, image, terrain, out _);
                        var payer = CurrentGateMapQueries.FindCity(city, current);
                        if (!FoodTurnKnown(current.ReadBaseNativeValidity(payer), "Army_payer_validity")) continue;
                        int payerId = FoodTurnKnown(payer.PhysicalSlot, "Army_payer_identity");
                        // Source shortage compares the collector City's stock,
                        // then debits its mapped payer. These can differ.
                        int stock = FoodTurnKnown(current.ReadBaseResource(city, BaseResourceKind.Supplies), "collector_food");
                        bool supplied = stock >= due;
                        int paid = due;
                        if (!supplied)
                        {
                            working = ApplyArmyFoodShortagesCurrent(working, current,
                                [new(image.Id, image.ForceId, image.OfficerId, payerId, due - stock)]);
                            paid = FoodTurnKnown(current.ReadBaseResource(city, BaseResourceKind.Supplies), "collector_food_after_shortage");
                            Report("food_shortage", image.ForceId, payerId, image.OfficerId, due - paid);
                        }
                        // The shortage callback can retire the active Army;
                        // update its actual remaining receiver, never recreate it.
                        WriteSupplyFlag(physical, supplied);
                        Debit(payer, paid);
                        if (paid != 0) Report("food_upkeep", image.ForceId, payerId, image.OfficerId, paid);
                    }
                    if (any)
                    {
                        var refresh = TryRefreshBaseNearbyArmiesCurrent(working, current);
                        working = refresh.State ?? throw FoodTurnMissing(refresh.UnresolvedOwner ?? "Base_nearby_tail");
                        var grouped = RecomputeMapArmyGroups(working, groups);
                        if (!grouped.Applied) throw FoodTurnMissing(grouped.Missing ?? "map_group_tail");
                        groupsChanged = true;
                    }
                }
                finally { current.ClearNearbyTemporary(armies, true); }
            }
            finally { current.ClearNearbyTemporary(gates, true); }
            if ((working.NativeMapSeason?.ThirdDate ?? throw FoodTurnMissing("Turn_date")).Day == 1)
                working = RefreshCityMerchantMonth(working, current, city);
        }
        return new(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() }), groups, groupsChanged);

        UnitState ArmyImage(int physical)
        {
            var slots = working.ReadArmyLifecycle()?.ReceiverSlots() ?? throw FoodTurnMissing("Army_arena");
            if ((uint)physical >= slots.Length) throw FoodTurnMissing("Army_receiver");
            var receiver = slots[physical];
            return receiver.ActiveUnitId is int id && working.Units.TryGetValue(id, out var image) ? image :
                receiver.PendingImage ?? throw FoodTurnMissing("Army_image");
        }
        void WriteSupplyFlag(int physical, bool supplied)
        {
            var image = ArmyImage(physical);
            var runtime = image.NativeTacticRuntime ?? throw FoodTurnMissing("supply_Army26");
            var updated = image with { NativeTacticRuntime = runtime with
                { Flags26 = supplied ? (byte)(runtime.Flags26 | 0x40) : (byte)(runtime.Flags26 & ~0x40) } };
            if (working.Units.ContainsKey(image.Id)) working.Units[image.Id] = updated;
            else
            {
                var arena = working.ReadArmyLifecycle() ?? throw FoodTurnMissing("supply_Army_arena");
                var receiver = arena.ReceiverSlots()[physical].Receiver;
                if (receiver.PendingImage?.Id != image.Id) throw FoodTurnMissing("supply_pending_receiver");
                working = working.WithArmyLifecycle(arena with
                    { Receivers = arena.Receivers.With(receiver with { PendingImage = updated }) });
            }
        }
        void ResetExpenditure(BaseReference receiver, int location)
        {
            if (receiver.Kind == BaseKind.City && working.Cities.TryGetValue(location, out var city))
                working.Cities[location] = city with { PeriodGoldExpenditure = 0, PeriodFoodExpenditure = 0 };
            else if (receiver.Kind == BaseKind.Gate && working.Gates.TryGetValue(location, out var gate))
                working.Gates[location] = gate with { PeriodGoldExpenditure = 0, PeriodFoodExpenditure = 0 };
            else if (receiver.PhysicalSlot != 0) throw FoodTurnMissing("expense_reset_receiver");
        }
        void BillGarrison(BaseReference target, int location)
        {
            int due = CityCaptureFoodWarningSource.ReadGarrisonExpense(working, current, target).Expense;
            if (due == 0) return;
            if (due < 0) throw FoodTurnMissing("signed_garrison_credit");
            var payer = CurrentGateMapQueries.FindCity(target, current);
            if (!FoodTurnKnown(current.ReadBaseNativeValidity(payer), "garrison_payer_validity")) return;
            int payerId = FoodTurnKnown(payer.PhysicalSlot, "garrison_payer_identity");
            int stock = FoodTurnKnown(current.ReadBaseResource(payer, BaseResourceKind.Supplies), "garrison_stock");
            int force = working.Cities.GetValueOrDefault(payerId)?.ForceId ?? throw FoodTurnMissing("garrison_report_Force");
            int paid = due;
            if (stock < due)
            {
                int percent = Math.Max(1, unchecked((due - stock) * 100) / due);
                working = ApplyGarrisonFoodShortageCurrent(working, current, new(location, payerId, force, due, stock, percent));
                paid = FoodTurnKnown(current.ReadBaseResource(payer, BaseResourceKind.Supplies), "garrison_stock_after_shortage");
                Report("food_shortage", force, payerId, null, due - paid, location);
            }
            Debit(payer, paid);
            if (paid != 0) Report("food_upkeep", force, payerId, null, paid, location);
        }
        void Debit(BaseReference payer, int amount)
        {
            int id = FoodTurnKnown(payer.PhysicalSlot, "debit_City");
            if (payer.Kind != BaseKind.City || !working.Cities.TryGetValue(id, out var city)) throw FoodTurnMissing("debit_domain_City");
            int before = FoodTurnKnown(current.ReadBaseResource(payer, BaseResourceKind.Supplies), "debit_stock");
            int cap = BasePostLoad.ReadResourceCapacities(payer, current).Supplies;
            int after = Math.Clamp(unchecked(before - amount), 0, cap);
            current.SetBaseResource(payer, BaseResourceKind.Supplies, after);
            int expense = city.PeriodFoodExpenditure ?? throw FoodTurnMissing("debit_expense_reset_order");
            working.Cities[id] = city with { Supplies = after, PeriodFoodExpenditure = unchecked(expense + amount) };
        }
        void Report(string kind, int force, int city, int? person, int amount, int? location = null)
        {
            working = working with { PeriodReports = [.. working.PeriodReports,
                new(kind, force, city, person, amount) { LocationId = location, RuleId = "CE-Turn-current-food" }] };
        }
        void FillGates(BaseReference city, HexListHandle list)
        {
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw FoodTurnMissing("managerA0"))
            {
                var gate = entry ?? throw FoodTurnMissing("Gate_pointer");
                if (CurrentGateMapQueries.FindCity(gate, current) != city) continue;
                var node = current.AllocateBaseRegistryNode(new(list.Identity)) ?? throw FoodTurnMissing("Gate_node");
                if (node.Identity.HasValue) current.WriteBaseRegistryNodePayload(node, gate);
            }
        }
        IEnumerable<BaseReference> Bases(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw FoodTurnMissing("Gate_list_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadBaseRegistryNodePayload(node) ?? throw FoodTurnMissing("Gate_list_payload");
                node = current.ReadRegistryNodeNext(node) ?? throw FoodTurnMissing("Gate_list_next");
            }
        }
        IEnumerable<ArmyUnitReference> Armies(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw FoodTurnMissing("Army_list_head");
            while (node.Identity.HasValue)
            {
                var pointer = current.ReadRegistryNodePayload(node) ?? throw FoodTurnMissing("Army_list_payload");
                if (pointer.Kind != RegistryKind.Army) throw FoodTurnMissing("Army_list_kind");
                yield return new(FoodTurnKnown(pointer.PhysicalSlot, "Army_list_receiver"));
                node = current.ReadRegistryNodeNext(node) ?? throw FoodTurnMissing("Army_list_next");
            }
        }
        void SortArmies(HexListHandle list)
        {
            var nodes = new List<HexListNodeReference>();
            var node = current.ReadHexListHead(list) ?? throw FoodTurnMissing("Army_sort_head");
            while (node.Identity.HasValue)
            {
                nodes.Add(node);
                node = current.ReadHexListNodeNext(node) ?? throw FoodTurnMissing("Army_sort_next");
            }
            // 24BAA0 moves actual nodes only on strict2538C0 less-than.
            // Equal commanders retain their manager order and node identity.
            for (int index = 1; index < nodes.Count; index++)
            {
                var moving = nodes[index];
                int at = index;
                while (at > 0 && Before(moving, nodes[at - 1])) at--;
                if (at == index) continue;
                nodes.RemoveAt(index);
                nodes.Insert(at, moving);
                current.ReorderCurrentList(list, nodes);
            }
        }
        bool Before(HexListNodeReference leftNode, HexListNodeReference rightNode)
        {
            int left = Commander(leftNode), right = Commander(rightNode);
            byte a = FoodTurnKnown(current.ReadPersonStatus(left), "sort_left_status");
            byte b = FoodTurnKnown(current.ReadPersonStatus(right), "sort_right_status");
            if (a != b) return a < b;
            // The source rereads right then left if the first comparison did
            // not decide. These are current numerical queries, not snapshots.
            int rightLeadership = Leadership(right), leftLeadership = Leadership(left);
            if (rightLeadership < leftLeadership) return true;
            rightLeadership = Leadership(right); leftLeadership = Leadership(left);
            if (leftLeadership < rightLeadership) return false;
            return FoodTurnKnown(current.ReadPersonIdentity(left), "sort_left_identity") <
                FoodTurnKnown(current.ReadPersonIdentity(right), "sort_right_identity");
        }
        int Commander(HexListNodeReference node)
        {
            var pointer = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw FoodTurnMissing("sort_Army_payload");
            if (pointer.Kind != RegistryKind.Army) throw FoodTurnMissing("sort_Army_kind");
            ushort id = FoodTurnKnown(current.ReadArmyUnitCommanderId(new(FoodTurnKnown(pointer.PhysicalSlot, "sort_Army_receiver"))), "sort_Commander12");
            return FoodTurnKnown((current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                ?? throw FoodTurnMissing("sort_Person_pointer")).PhysicalSlot, "sort_Person_receiver");
        }
        int Leadership(int person)
        {
            int id = FoodTurnKnown(current.ReadPersonIdentity(person), "sort_Person_identity");
            abilities ??= new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            return FoodTurnKnown(abilities.ResolveDimension(working, id, 0,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(working, force, effect)), "sort_leadership");
        }
    }

    internal void PublishNativeFoodTurnGroups(NativeArmyFoodTurnResult completed)
    {
        if (completed.GroupsChanged) PublishMapArmyGroups(completed.Groups);
    }
    private static T FoodTurnKnown<T>(T? value, string field) where T : struct => value ?? throw FoodTurnMissing(field);
    private static InvalidDataException FoodTurnMissing(string field) => new("army.Turn_food." + field);
}
