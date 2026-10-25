using San14.Core;

namespace San14.Data;

public sealed record ArmyPersonalityMaintenanceResult(GameState? State, int ArmyCount, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The complete reached stage17 manager, without an hourly gate.
    /// All actors, history chains, timers and application intentions share one
    /// operation current and publish together after the last reached input.</summary>
    public ArmyPersonalityMaintenanceResult TryAdvanceArmyPersonalityMaintenance(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.PersonalityMaintenance ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Personality maintenance requires reached stage17 entry.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var pool = current.ReadOwnedCurrentListPool() ?? throw TraitMissing("owned_pool");
            var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw TraitMissing("manager60");
            var changedPeople = new HashSet<int>();
            var feedback = new ArmyTraitFeedback(state, current, moment, currentState: () => state,
                speechConsumer: CreateArmyTraitSpeechConsumer(current, () => state, next => state = next));
            var fields = new ArmyReturnTraitCleanup.Current(current, pool, feedback.Refresh,
                physical => changedPeople.Add(physical));
            int count = 0;
            foreach (var entry in order)
            {
                var army = entry is { Kind: RegistryKind.Army, PhysicalSlot: int physical }
                    ? new ArmyUnitReference(physical) : throw TraitMissing("manager_pointer");
                if (!TraitNeed(current.ReadArmyUnitNativeValidity(army), "manager_Army_validity") ||
                    TraitNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current,
                        army.PhysicalSlot!.Value), "manager_excluded")) continue;
                count++;
                ushort commanderId = TraitNeed(current.ReadArmyUnitCommanderId(army), "commander");
                int commander = fields.Person(commanderId);
                var targets = current.CreateNearbyTemporary();
                try
                {
                    for (int index = 0; index < 9; index++)
                    {
                        //The source reads each timer before resolving the key,
                        //even for an invalid or inactive personality.
                        ushort timer = TraitNeed(current.ReadPersonTraitExtra1BE(commander, index), "own_timer");
                        ushort key = fields.TraitKey(commander, index);
                        var trait = current.ReadPersonalityTableReference(key <= 355 ? key : 0) ?? throw TraitMissing("own_trait_pointer");
                        bool valid = TraitNeed(current.ReadSkillPersonalityValidity(trait), "own_trait_validity");
                        bool unconditional = !valid || TraitNeed(current.ReadPersonalityWord(trait, 0xB4), "own_definition_timer") == ushort.MaxValue;
                        ushort mask = TraitNeed(current.ReadPersonTraitMask1BC(commander), "own_active_mask");
                        if ((mask & (1 << index)) != 0)
                        {
                            byte flags = TraitNeed(current.ReadSkillPersonalityFlags(trait), "own_dynamic_flags");
                            if ((flags & 4) == 0 || unconditional)
                            {
                                if (!ArmyTraitConditions.Collect(state, current, army, trait, 1, targets))
                                    fields.ClearCommanderSlot(army, index);
                            }
                            else if (timer == 0) fields.ClearCommanderSlot(army, index);
                            else
                            {
                                current.WritePersonTraitExtra1BE(commander, index, (ushort)Math.Clamp(timer - 1, 0, 720));
                                changedPeople.Add(commander);
                            }
                        }
                        else if (valid)
                        {
                            //Active branches intentionally retain the previous
                            //slot's scratch chain. Only this branch clears it.
                            current.ClearNearbyTemporary(targets, false);
                            if (ArmyTraitConditions.Collect(state, current, army, trait, 1, targets))
                                ArmyTraitApplication.Apply(state, current, fields, army, trait, targets, feedback,
                                    person => changedPeople.Add(person));
                        }

                        ushort actualKey = checked((ushort)(trait.PhysicalSlot ?? throw TraitMissing("history_trait_identity")));
                        int slot = fields.MatchingTrait(army, actualKey);
                        var descriptor = fields.Fields(army).Descriptors160[slot < 0 ? 0 : slot];
                        bool removed = false;
                        foreach (var related in fields.Receivers(descriptor))
                        {
                            if (!TraitNeed(current.ReadArmyUnitNativeValidity(related), "history_Army_validity")) continue;
                            if (fields.Receivers(new(targets.Identity)).Any(target => target.PhysicalSlot == related.PhysicalSlot)) continue;
                            for (int i = 0; i < 36; i++)
                            {
                                var prior = fields.Fields(related);
                                if (prior.Traits64[i] != actualKey || prior.PersonsAC[i] !=
                                    TraitNeed(current.ReadArmyUnitCommanderId(army), "history_source_Commander")) continue;
                                fields.ClearReceivedSlot(related, i);
                                removed = true; break;
                            }
                            //The native goto exits the entire related walk
                            //after its first actual slot removal.
                            if (removed) break;
                        }
                    }
                    for (int index = 0; index < 36; index++)
                    {
                        var prior = fields.Fields(army); ushort timer = prior.AuxiliaryF4[index];
                        ushort key = prior.Traits64[index];
                        var trait = current.ReadPersonalityTableReference(key <= 355 ? key : 0) ?? throw TraitMissing("external_trait_pointer");
                        if (!TraitNeed(current.ReadSkillPersonalityValidity(trait), "external_trait_validity")) continue;
                        ushort definitionTimer = TraitNeed(current.ReadPersonalityWord(trait, 0xB4), "external_definition_timer");
                        byte flags = TraitNeed(current.ReadSkillPersonalityFlags(trait), "external_dynamic_flags");
                        if ((flags & 4) == 0 || definitionTimer == ushort.MaxValue) continue;
                        if (timer != 0)
                        {
                            var timers = prior.AuxiliaryF4.ToArray(); timers[index] = (ushort)Math.Clamp(timer - 1, 0, 720);
                            current.WriteArmyReturnTraitFields(army.PhysicalSlot!.Value,
                                new(prior.Traits64, prior.PersonsAC, timers, prior.Descriptors160));
                            if (ArmyTraitConditions.Collect(state, current, army, trait, 2, targets)) continue;
                        }
                        fields.ClearReceivedSlot(army, index);
                    }
                }
                finally { current.ClearNearbyTemporary(targets, true); }
            }
            var working = ArmyReturnImagePublication.Publish(state, current, []);
            if (changedPeople.Count != 0)
            {
                var people = new Dictionary<int, OfficerState>(working.Officers);
                foreach (int physical in changedPeople)
                {
                    int id = TraitNeed(current.ReadPersonIdentity(physical), "publication_Person_identity");
                    if (!people.TryGetValue(id, out var person) ||
                        person.TraitActivation is not { DurationSlotCount: 9, ActiveSlots: not null })
                        throw TraitMissing("publication_full_current_activation_owner");
                    var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                    if (activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                        throw TraitMissing("publication_lossless_activation");
                    people[id] = person with { TraitActivation = activation };
                }
                working = working with { Officers = people };
            }
            working = feedback.Publish(working);
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
            return new(working, count, null);
        }
        catch (InvalidDataException error) { return new(null, 0, error.Message); }
    }
    private static T TraitNeed<T>(T? value, string field) where T : struct => value ?? throw TraitMissing(field);
    private static InvalidDataException TraitMissing(string field) => new("army.trait-maintenance." + field);
}
