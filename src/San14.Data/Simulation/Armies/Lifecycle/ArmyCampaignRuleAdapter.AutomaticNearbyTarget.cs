using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record NearbyTargetNode(HexListNodeReference Node, ArmyUnitReference Army);

    //2B5700, including the distinct2A82F0 collector. The caller owns output.
    private void FillAutomaticNearbyTargets(GameState state, SourceWorldContext current,
        UnitState image, HexListHandle output)
    {
        current.ClearNearbyTemporary(output, false);
        //2A82F0 constructs/fills the Hex descriptor before constructing its
        //returned Army descriptor; the Hex list is destroyed before filtering.
        var hexes = current.CreateNearbyTemporary();
        HexListHandle? candidates = null;
        try
        {
            try
            {
                ushort at = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, image), "nearby_center2A");
                var center = current.ReadHexTableReference(at < 48400 ? at : 0) ?? throw MaintenanceMissing("nearby_center_pointer");
                _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, 3, hexes);
                candidates = current.CreateNearbyTemporary();
                var node = current.ReadSkillTemporaryHead(hexes) ?? throw MaintenanceMissing("nearby_Hex_head");
                while (node.Identity.HasValue)
                {
                    var hex = current.ReadHexListNodePayload(node) ?? throw MaintenanceMissing("nearby_Hex_payload");
                    int raw = unchecked((short)MaintenanceKnown(current.ReadHexRaw1C(hex), "nearby_signed1C"));
                    if (raw != 0)
                    {
                        //2A82F0 uses signed<=500, not2A78D0's1..500. A negative
                        //table pointer has no domain receiver; never treat it as empty.
                        if (raw < 0) throw MaintenanceMissing("nearby_negative_Army_pointer");
                        var army = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw MaintenanceMissing("nearby_Army_pointer");
                        if (MaintenanceKnown(current.ReadArmyUnitNativeValidity(army), "nearby_Army_validity") &&
                            !MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, ArmyPhysical(army)), "nearby_233560"))
                            current.AppendNearbyArmyTemporary(candidates, army);
                    }
                    node = current.ReadHexListNodeNext(node) ?? throw MaintenanceMissing("nearby_Hex_next");
                }
            }
            finally { current.ClearNearbyTemporary(hexes, true); }
            foreach (var row in ReadNearbyTargetNodes(current, candidates ?? throw MaintenanceMissing("nearby_Army_descriptor")))
            {
                int physical = ArmyPhysical(row.Army);
                if (AutomaticTargetAtHome(state, current, row.Army) ||
                    MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "candidate_233560")) continue;
                if ((image.NativeTacticRuntime!.Flags26 & 8) == 0 &&
                    MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, FArmy(state, physical)), "candidate_action37") is 11 or 12) continue;
                int selfForce = current.ReadArmyUnitRawForceSlot(new(image.NativeArmyOrigin!.NativeSlotIndex));
                int otherForce = current.ReadArmyUnitRawForceSlot(row.Army);
                if (MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, otherForce, selfForce), "candidate_relation2CA650"))
                    current.AppendNearbyArmyTemporary(output, row.Army);
            }
        }
        finally { if (candidates is not null) current.ClearNearbyTemporary(candidates, true); }
    }

    private bool AutomaticTargetAtHome(GameState state, SourceWorldContext current, ArmyUnitReference army)
    {
        int physical = ArmyPhysical(army);
        if (MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "candidate_home233560")) return false;
        int force = current.ReadArmyUnitRawForceSlot(army);
        if (force > 51) force = 0;
        if (force == 51) return false;
        if (force is >= 46 and <= 50) throw MaintenanceMissing("candidate_special_force_home232860");
        var unit = FArmy(state, physical);
        int home = MaintenanceKnown(new ArmyLogisticsPreparation(Context(current), current, _map, _terrain)
            .ReadCommanderBaseHex(state, unit), "candidate_current_home");
        return home == MaintenanceKnown(current.ReadArmyUnitRawHex2A(army), "candidate_current2A");
    }

    //009410 copy passed by value to28E910. Sorting reorders real allocated
    //nodes; it neither allocates new payloads nor assumes a transitive comparer.
    private GameState ApplyAutomaticNearbyTarget(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment moment, HexListHandle input)
    {
        var copy = current.CreateNearbyTemporary();
        try
        {
            foreach (var row in ReadNearbyTargetNodes(current, input)) current.AppendNearbyArmyTemporary(copy, row.Army);
            var nodes = ReadNearbyTargetNodes(current, copy);
            if (nodes.Count == 0) return state;
            int commander = MaintenanceKnown(current.ReadArmyUnitCommanderId(new(image.NativeArmyOrigin!.NativeSlotIndex)), "sort_actor_commander");
            var actorPerson = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0) ?? throw MaintenanceMissing("sort_actor_Person_pointer");
            for (int i = 1; i < nodes.Count; i++)
            {
                var item = nodes[i]; int at = i;
                while (at > 0 && AutomaticTargetBefore(state, current, image, actorPerson, item.Army, nodes[at - 1].Army, moment))
                { nodes[at] = nodes[at - 1]; at--; }
                nodes[at] = item;
            }
            current.ReorderCurrentList(copy, nodes.Select(row => row.Node).ToArray());
            int selected = ArmyPhysical(nodes[0].Army);
            ushort oldIdentity = MaintenanceKnown(ArmyOverrideTargetSourceAdapter.Read(state, image).Identity3E, "selected_old3E");
            if (oldIdentity == selected) return state;
            var arena = state.ReadArmyLifecycle()!;
            var selectedReceiver = arena.Slots[selected].ReceiverId;
            var changed = ArmyOverrideTargetWrites.Apply(image, new(ArmyOverrideTargetKind.ArmyReceiver,
                new(ArmyOverrideDestinationKind.ArmyReceiver, ReceiverId: selectedReceiver)));
            var working = (state with { Units = new(state.Units) { [image.Id] = changed } }).WithArmyLifecycle(arena with
            { Receivers = arena.Receivers.With(arena.Receivers.FindUnit(image.Id)! with { OverrideTarget = changed.OverrideTarget }) });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
            if (IsPlayerRulerLedDistrict(working, current, changed))
            {
                var actor = new ArmyUnitReference(FPhysical(changed));
                var formatted = FormatHistoryReport(working, current, 0x5F6D,
                    new HistoryMessageOperands(Army0: actor, Army1: nodes[0].Army));
                working = formatted.State;
                //28E910 retains fresh metadata in this order after formatting,
                //before the original Next48 reset and route continuation.
                int payload = MaintenanceKnown(current.ReadArmyUnitCommanderId(actor), "nearby_report_Commander12");
                int day = StatusCampaignDay(working, moment);
                int hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(actor), "nearby_report_Army2A");
                int district = ReadFacilityHistoryDistrict(current, actor);
                var requests = new List<CampaignEffectRequest>();
                working = WriteHistory(working, current, new ReportCommand(5, district, hex, day, formatted.Text,
                    CampaignHistoryWriter.Supplemental(working), payload, MarkBit4: 1), requests);
                if (requests.Count != 0)
                    working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw MaintenanceMissing("nearby_effect_owner"))
                        .Append(CampaignEffectSource.ArmyCommandMaintenance, moment.CalendarCycleOrdinal, requests) };
            }
            var routed = working.Units[image.Id] with { March = working.Units[image.Id].March! with { NextStep = ArmyMarchDestination.None } };
            working = working with { Units = new(working.Units) { [image.Id] = routed } };
            return FRoute(working, image.Id, true, current, false);
        }
        finally { current.ClearNearbyTemporary(copy, true); }
    }

    private bool AutomaticTargetBefore(GameState state, SourceWorldContext current, UnitState actor,
        TransferPersonReference actorPerson, ArmyUnitReference left, ArmyUnitReference right, NativeArmyProgressStageMoment moment)
    {
        int actorAt = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, actor), "sort_actor2A");
        var main = OwnedArmyLifecycleTargets.Read(state, state.ReadArmyLifecycle()!.Slots[actor.NativeArmyOrigin!.NativeSlotIndex!.Value].ReceiverId)
            ?? throw MaintenanceMissing("sort_actor38_3A");
        byte mainKind = MaintenanceKnown(main.Kind38, "sort_actor38");
        if (mainKind == 27)
        {
            ushort target = MaintenanceKnown(main.Identity3A, "sort_actor3A");
            bool Preferred(ArmyUnitReference candidate)
            {
                if (ArmyPhysical(candidate) != target) return false;
                int goal = AutomaticCandidateMainHex(state, current, candidate);
                return goal < 48400 && FireCommandDistance(actorAt, goal) < 4;
            }
            bool a = Preferred(left), b = Preferred(right);
            if (a != b) return a;
        }
        int leftDistance = FireCommandDistance(actorAt, MaintenanceKnown(current.ReadArmyUnitRawHex2A(left), "sort_left2A"));
        int rightDistance = FireCommandDistance(actorAt, MaintenanceKnown(current.ReadArmyUnitRawHex2A(right), "sort_right2A"));
        if (leftDistance != rightDistance) return leftDistance < rightDistance;
        TransferPersonReference Person(ArmyUnitReference army)
        {
            ushort id = MaintenanceKnown(current.ReadArmyUnitCommanderId(army), "sort_candidate12");
            return current.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw MaintenanceMissing("sort_candidate_Person_pointer");
        }
        var leftPerson = Person(left); var rightPerson = Person(right);
        bool Valid(TransferPersonReference person) => person.PhysicalSlot is int p && MaintenanceKnown(current.ReadPersonNativeValidity(p), "sort_Person_validity");
        if (!Valid(leftPerson) || !Valid(rightPerson)) return true;
        bool Disliked(TransferPersonReference person)
        {
            ushort id = MaintenanceKnown(current.ReadPersonIdentity(person.PhysicalSlot!.Value), "sort_Person10");
            for (int i = 0; i < 8; i++)
                if (MaintenanceKnown(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state,
                    actorPerson.PhysicalSlot ?? throw MaintenanceMissing("sort_actor_D0_pointer"), i), "sort_current_D0") == id) return true;
            return false;
        }
        bool leftDisliked = Disliked(leftPerson), rightDisliked = Disliked(rightPerson);
        if (leftDisliked != rightDisliked) return leftDisliked;
        bool leftRuler = MaintenanceKnown(current.ReadPersonStatus(leftPerson.PhysicalSlot!.Value), "sort_leftE6") == 1;
        bool rightRuler = MaintenanceKnown(current.ReadPersonStatus(rightPerson.PhysicalSlot!.Value), "sort_rightE6") == 1;
        if (leftRuler != rightRuler) return leftRuler;
        ushort Troops(ArmyUnitReference candidate)
        {
            int value = FArmy(state, ArmyPhysical(candidate)).Troops;
            return value is >= 0 and <= ushort.MaxValue ? (ushort)value : throw MaintenanceMissing("sort_current16");
        }
        ushort leftTroops = Troops(left), rightTroops = Troops(right);
        if (leftTroops != rightTroops) return leftTroops < rightTroops;
        int selfForce = current.ReadArmyUnitRawForceSlot(new(actor.NativeArmyOrigin!.NativeSlotIndex));
        var force = current.ReadForceTableReference(selfForce <= 51 ? selfForce : 0) ?? throw MaintenanceMissing("sort_Force_pointer");
        if (!MaintenanceKnown(current.ReadForceNativeValidity(force), "sort_Force_validity")) return false;
        int leftForce = current.ReadArmyUnitRawForceSlot(left), rightForce = current.ReadArmyUnitRawForceSlot(right);
        byte Relation(int other) => other >= 52 ? (byte)0 :
            state.NativeArmySimulation?.Forces.GetValueOrDefault(force.PhysicalSlot!.Value)?.NormalAftermath?.Relations13 is { Length: 52 } values
                ? values[other] : throw MaintenanceMissing("sort_Force_relations13");
        byte leftRelation = Relation(leftForce), rightRelation = Relation(rightForce);
        if (leftRelation != rightRelation) return leftRelation < rightRelation;
        return TacticEffectSourceSamplerMath.Sample(2, 1, ArmyPhysical(left), 0, 0, 0, FSampler(state, moment)) == 0;
    }

    private static int AutomaticCandidateMainHex(GameState state, SourceWorldContext current, ArmyUnitReference army)
    {
        var arena = state.ReadArmyLifecycle() ?? throw MaintenanceMissing("sort_main_lifecycle");
        var main = OwnedArmyLifecycleTargets.Read(state, arena.Slots[ArmyPhysical(army)].ReceiverId) ?? throw MaintenanceMissing("sort_candidate38_3A");
        byte kind = MaintenanceKnown(main.Kind38, "sort_candidate38"); ushort raw = MaintenanceKnown(main.Identity3A, "sort_candidate3A");
        if (kind == 9) return raw;
        if (kind == 27) return MaintenanceKnown(current.ReadArmyUnitRawHex2A(new(raw <= 500 ? raw : 0)), "sort_main_Army2A");
        if (kind is not (5 or 6 or 11)) return 48400;
        //Generic Object placement/retirement is owned by the pending stage12
        //work. Its scenario prefix is not a saved current position provider.
        if (kind == 11) throw MaintenanceMissing("sort_main_current_Object_owner");
        ushort link = raw;
        if (kind != 11)
        {
            var selected = kind == 5 ? current.ReadCityTableReference(raw <= 51 ? raw : 0) : current.ReadGateTableReference(raw <= 10 ? raw : 0);
            link = MaintenanceKnown(current.ReadBaseObjectLink(selected ?? throw MaintenanceMissing("sort_main_Base_pointer")), "sort_main_Base4E");
        }
        if (link is < 1 or > 61) throw MaintenanceMissing("sort_main_current_Object_owner");
        var obj = current.ReadObjectTableReference(link) ?? throw MaintenanceMissing("sort_main_Object_pointer");
        return MaintenanceKnown(current.ReadObjectHexIndex(obj), "sort_main_Object10");
    }

    private static List<NearbyTargetNode> ReadNearbyTargetNodes(SourceWorldContext current, HexListHandle list)
    {
        var result = new List<NearbyTargetNode>();
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw MaintenanceMissing("nearby_head");
        while (node.Identity.HasValue)
        {
            var payload = current.ReadRegistryNodePayload(node) ?? throw MaintenanceMissing("nearby_payload");
            if (payload.Kind != RegistryKind.Army) throw MaintenanceMissing("nearby_payload_kind");
            result.Add(new(new(node.Identity), new(payload.PhysicalSlot)));
            node = current.ReadRegistryNodeNext(node) ?? throw MaintenanceMissing("nearby_next");
        }
        return result;
    }
    private static int ArmyPhysical(ArmyUnitReference army) => army.PhysicalSlot ?? throw MaintenanceMissing("nearby_Army_physical");
}
