using San14.Core;

namespace San14.Data;

public sealed record ArmyProgressNotificationResult(GameState? State, int RequestCount, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    // Offline paired CE SC/TC fixed rule data (not application admission rates).
    private const int ProgressLowTroops = 1000, ProgressLowMorale = 40;

    /// <summary>The complete ordinary manager order for stage10. Speech is an
    /// ordered application request, not a played report or a simulation wait.</summary>
    public ArmyProgressNotificationResult TryAdvanceArmyProgressNotifications(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.ArmyNotifications ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Army notifications require progress stage10 entry.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            _baseCapacityInputs.BindCurrent(state, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            if (current.ReadRegistryDescriptor(RegistryKind.District) is null) throw NotificationMissing("district_manager");
            int playerDistrict = NotificationKnown(OwnedArmyLifecycleAuxiliaries.CurrentPlayerPrimaryDistrict(state, current), "player_primary_district");
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw NotificationMissing("manager60");
            int half = moment.PreWriterThirdDate.Hour / 2;
            bool warningDue = half >= 2 && (half - 2) % 12 == 0;
            var requests = new List<CampaignEffectRequest>();
            bool poolChanged = false, personnelBound = false;
            foreach (var entry in registry)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int slot } ? slot : throw NotificationMissing("manager_receiver");
                var army = new ArmyUnitReference(physical);
                var resources = current.ReadArmyReturnResourceFields(army) ?? throw NotificationMissing("army_resources");
                var image = NotificationArmy(state, physical);
                byte? land = null;
                bool IsTransport() => NotificationKnown(land ??= ArmyConfigurationSourceAdapter.ReadCurrentLand(state, image), "land_formation") is 19 or 20;
                bool handled = false;
                if (NotificationKnown(resources.Ready16, "ready_troops") < ProgressLowTroops)
                {
                    int force = NotificationForce(army);
                    if (force != 51 && unchecked((uint)(force - 46)) > 4 && !IsTransport())
                    {
                        if (warningDue) Speech(army, image, CombatSpeechEvent.ArmyLowTroops);
                        handled = true;
                    }
                }
                int upper = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                int morale = Math.Min(upper, NotificationKnown(resources.Morale1A, "morale_byte"));
                if (morale < ProgressLowMorale && !IsTransport())
                {
                    if (warningDue) Speech(army, image, CombatSpeechEvent.ArmyLowMorale);
                    handled = true;
                }
                if ((NotificationKnown(current.ReadArmyUnitFlags150(army), "flags150") & 1) == 0) continue;
                byte action = NotificationKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "action");
                bool transport = IsTransport();
                if (transport)
                {
                    if (action is 11 or 12) { BaseSpeech(CombatSpeechEvent.ArmyRetreating); continue; }
                    var target = Target();
                    if (target.Kind38 is 2 or 5 or 6) { BaseSpeech(CombatSpeechEvent.ArmyTransporting); continue; }
                }
                else
                {
                    switch (action)
                    {
                        case 2: case 3: BaseSpeech(CombatSpeechEvent.ArmyMarchingToBase); handled = true; break;
                        case 4: case 5: BaseSpeech(CombatSpeechEvent.ArmyAttackingBase); handled = true; break;
                        case 7:
                            int targetSlot = NotificationKnown(Target().Identity3A, "target_army");
                            targetSlot = targetSlot <= 500 ? targetSlot : 0;
                            // Keep the stable campaign identity. Do not invent
                            // an entity for an unresolved/placeholder receiver.
                            var targetImage = NotificationArmy(state, targetSlot);
                            Speech(army, image, CombatSpeechEvent.ArmyAttackingArmy, CombatParticipantKind.Army, targetImage.Id);
                            handled = true; break;
                        case 8:
                            var selectedConstruction = FacilityConstructionDefinitions.Selected(state, current, image);
                            if (NotificationKnown(current.ReadObjectDefinitionRaw2C(selectedConstruction), "construction_definition_category") != 4)
                            { Speech(army, image, CombatSpeechEvent.ArmyBuildingFacility); handled = true; }
                            break;
                        case 10:
                            int objectSlot = NotificationKnown(Target().Identity3A, "target_object");
                            var obj = current.ReadObjectTableReference(objectSlot <= 3000 ? objectSlot : 0) ?? throw NotificationMissing("object_pointer");
                            var fields = current.ReadPhysicalObject(NotificationKnown(obj.PhysicalSlot, "object_receiver")) ?? throw NotificationMissing("object_fields");
                            var definition = current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0)
                                ?? throw NotificationMissing("object_definition");
                            if (NotificationKnown(current.ReadObjectDefinitionRaw2C(definition), "object_definition_category") != 4)
                            { Speech(army, image, CombatSpeechEvent.ArmyRemovingFacility, CombatParticipantKind.FieldObject, objectSlot); handled = true; }
                            break;
                        case 11: case 12: BaseSpeech(CombatSpeechEvent.ArmyRetreating); handled = true; break;
                        case 14:
                            int rawHex = NotificationKnown(Target().Identity3A, "target_hex");
                            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw NotificationMissing("target_hex_pointer");
                            int area = NotificationKnown(current.ReadHexAreaSlot(hex), "target_area");
                            Speech(army, image, CombatSpeechEvent.ArmyOccupyingArea, CombatParticipantKind.Area, area); handled = true; break;
                        case 16: Speech(army, image, CombatSpeechEvent.ArmyPreparingFire); handled = true; break;
                    }
                    int commander = NotificationKnown(current.ReadArmyUnitCommanderId(army), "district_commander");
                    int person = NotificationPerson(commander);
                    int district = NotificationKnown(current.ReadPersonNativeValidity(person), "district_person_validity")
                        ? NotificationKnown(current.ReadPersonDistrictSlot(person), "district") : 0;
                    if (district != playerDistrict)
                    {
                        var lifecycle = state.ReadArmyLifecycle()!;
                        var owner = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId)!;
                        var intent = owner.SupplyInterdiction ?? throw NotificationMissing("nonprimary_supply_cut_order");
                        if (intent.IsActive)
                        {
                            var targetId = intent.TargetReceiverId ?? throw NotificationMissing("supply_cut_target_receiver");
                            int targetPhysical = lifecycle.Slots.FirstOrDefault(slot => slot.ReceiverId == targetId)?.PhysicalSlot
                                ?? throw NotificationMissing("supply_cut_target_identity");
                            var targetArmy = current.ReadArmyUnitTableReference(targetPhysical) ?? throw NotificationMissing("supply_cut_target_pointer");
                            int targetForce = NotificationKnown(current.ReadArmyUnitNativeValidity(targetArmy), "supply_cut_target_validity")
                                ? current.ReadArmyUnitRawForceSlot(targetArmy) : 0;
                            Speech(army, image, CombatSpeechEvent.ArmyCuttingSupplyLines, CombatParticipantKind.Force, targetForce);
                            continue;
                        }
                    }
                }
                if (handled) continue;
                var nearby = current.CreateNearbyTemporary();
                poolChanged = true;
                ArmyUnitReference? nearest = null;
                try
                {
                    _ = NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, army, 3, nearby);
                    var node = current.ReadRegistryHead(new(nearby.Identity)) ?? throw NotificationMissing("nearby_head");
                    while (node.Identity.HasValue)
                    {
                        var candidate = current.ReadRegistryNodePayload(node) ?? throw NotificationMissing("nearby_payload");
                        if (candidate is not { Kind: RegistryKind.Army, PhysicalSlot: int candidateSlot }) throw NotificationMissing("nearby_receiver");
                        var other = new ArmyUnitReference(candidateSlot);
                        if (current.ReadArmyUnitRawForceSlot(other) == current.ReadArmyUnitRawForceSlot(army))
                        {
                            if (nearest is null || !NotificationKnown(current.ReadArmyUnitNativeValidity(nearest), "nearest_validity")) nearest = other;
                            else
                            {
                                int from = NotificationKnown(current.ReadArmyUnitRawHex2A(army), "from_hex");
                                int oldDistance = FireCommandDistance(from, NotificationKnown(current.ReadArmyUnitRawHex2A(nearest), "nearest_hex"));
                                int distance = FireCommandDistance(from, NotificationKnown(current.ReadArmyUnitRawHex2A(other), "candidate_hex"));
                                if (distance < oldDistance || distance == oldDistance &&
                                    NotificationKnown(current.ReadArmyUnitCommanderId(other), "candidate_commander") <
                                    NotificationKnown(current.ReadArmyUnitCommanderId(nearest), "nearest_commander")) nearest = other;
                            }
                        }
                        node = current.ReadRegistryNodeNext(node) ?? throw NotificationMissing("nearby_next");
                    }
                    if (nearest is not null && NotificationKnown(current.ReadArmyUnitNativeValidity(nearest), "nearest_final_validity"))
                    {
                        int related = NotificationKnown(current.ReadArmyUnitCommanderId(nearest), "nearby_commander");
                        Speech(army, image, CombatSpeechEvent.ArmyNearbyAlly, related: related);
                        handled = true;
                    }
                }
                finally { current.ClearNearbyTemporary(nearby, true); }
                if (!handled) Speech(army, image, CombatSpeechEvent.ArmyMarching);

                ArmyTargetSourceFields Target() => OwnedArmyLifecycleTargets.Read(state,
                    state.ReadArmyLifecycle()!.Slots[physical].ReceiverId) ?? throw NotificationMissing("target");
                void BaseSpeech(CombatSpeechEvent actionEvent)
                {
                    var target = Target(); int kind = NotificationKnown(target.Kind38, "target_kind");
                    int id = NotificationKnown(target.Identity3A, "target_identity");
                    int baseId = kind == 6 ? id is >= 1 and <= 10 ? id + 51 : 0 : id;
                    Speech(army, image, actionEvent, baseId >= 52 ? CombatParticipantKind.Gate : CombatParticipantKind.City, baseId);
                }
            }
            var result = personnelBound ? state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ??
                throw NotificationMissing("speech_personnel_publication") } : state;
            if (poolChanged) result = result.WithArmyLifecycle(result.ReadArmyLifecycle()! with
                { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
            if (requests.Count != 0) result = result with { CampaignEffectRequests =
                (state.CampaignEffectRequests ?? throw NotificationMissing("effect_requests"))
                .Append(CampaignEffectSource.ArmyProgressNotifications, moment.CalendarCycleOrdinal, requests) };
            return new(result, requests.Count, null);

            int NotificationForce(ArmyUnitReference army) => NotificationKnown(
                current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "current_force");
            int NotificationPerson(int id) => NotificationKnown(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot, "person_pointer");
            void Speech(ArmyUnitReference army, UnitState image, CombatSpeechEvent actionEvent,
                CombatParticipantKind? kind = null, int? id = null, int? related = null)
            {
                //1B4AB0 is void: rejected native receivers do not undo earlier
                //work, and App message admission is never a busy result here.
                if (!NotificationKnown(current.ReadArmyUnitNativeValidity(army), "speech_army_validity")) return;
                if (!personnelBound)
                {
                    if (state.PersonnelCommands is null) throw NotificationMissing("speech_personnel_owner");
                    PersonReturnCommands.BindCurrent(state, current);
                    personnelBound = true;
                    poolChanged = true;
                }
                int person = NotificationPerson(NotificationKnown(current.ReadArmyUnitCommanderId(army), "speech_commander"));
                int at = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
                int officer = NotificationKnown(current.ReadPersonIdentity(person), "speech_person_identity");
                requests.Add(new(new(CombatParticipantKind.Army, image.Id, officer, actionEvent, at, kind, id, related, null, null), null));
            }
        }
        catch (InvalidDataException error) { return new(null, 0, error.Message); }
    }
    private static UnitState NotificationArmy(GameState state, int physical)
    {
        var receiver = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical) ?? throw NotificationMissing("army_receiver");
        return receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw NotificationMissing("active_army")
            : receiver.PendingImage ?? throw NotificationMissing("retained_army_image");
    }
    private static T NotificationKnown<T>(T? value, string field) where T : struct => value ?? throw NotificationMissing(field);
    private static InvalidDataException NotificationMissing(string field) => new("army.progress.notifications." + field);
}
