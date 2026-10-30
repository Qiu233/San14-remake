using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary Story recall and associated personnel-command completion.
    /// The caller keeps its current world, registry pool and invocation selectors.</summary>
    internal GameState ApplyStoryUnitLifecycleCurrent(GameState state, SourceWorldContext current,
        int code, IReadOnlyList<int> arguments, Func<int, int> resolvePerson,
        ForceReference? targetForce = null, BaseReference? targetBase = null)
        => new EventPersonMovementCurrent(this, state, current,
            new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)))
            .ApplyUnitLifecycle(code, arguments, resolvePerson, targetForce, targetBase);

    private sealed partial class EventPersonMovementCurrent
    {
        private bool unitLifecycleInputsBound;

        private void BindUnitLifecycleInputs()
        {
            if (unitLifecycleInputsBound) return;
            BindInputs();
            unitLifecycleInputsBound = true;
        }

        internal GameState ApplyUnitLifecycle(int code, IReadOnlyList<int> arguments,
            Func<int, int> resolvePerson, ForceReference? targetForce, BaseReference? targetBase)
        {
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            storyImmediateRetirement = true;
            try
            {
                switch (code)
                {
                    case 245:
                        //Each of the first five operands is resolved at its own
                        //source point; repeated Persons repeat the operation.
                        for (int index = 0; index < 5; index++)
                            EndAbsent(resolvePerson(arguments[index]));
                        break;
                    case 252:
                        EndAbsentByForce(targetForce ?? throw Missing("story_units_target_Force"));
                        break;
                    case 285:
                        EndAbsentByHome(targetBase ?? throw Missing("story_units_target_Base"));
                        break;
                    case 294:
                        RecallByForce(targetForce ?? throw Missing("story_units_target_Force"));
                        FinishRecall();
                        break;
                    case 295:
                        RecallByHome(targetBase ?? throw Missing("story_units_target_Base"));
                        FinishRecall();
                        break;
                    default: throw Missing("story_units_opcode");
                }
                if (unitLifecycleInputsBound) Publish();
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("effect_calendar"), effects.ToArray()) };
                return latest;
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldAreaRemoval;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private IEnumerable<int> UnitLifecycleRegistry(RegistryKind kind)
        {
            var sequence = current.ReadRegistryReferences(kind) ?? throw Missing("story_units_registered_" + kind);
            foreach (var entry in sequence)
                yield return Need(entry?.PhysicalSlot, "story_units_registered_pointer");
        }

        private void EndAbsent(int person)
        {
            if (Need(current.ReadPersonNativeValidity(person), "story_units_Person_validity"))
            {
                BindUnitLifecycleInputs();
                ResetAssociated(person);
            } //3D8050: cancel, retire, re-query, arrive, retire.
        }

        private void EndAbsentByHome(BaseReference target)
        {
            if (!Need(current.ReadBaseNativeValidity(target), "story_units_Base_validity")) return;
            ushort home = Need(current.ReadBaseObjectLink(target), "story_units_Base4E");
            if (home is < 1 or > 61) return;
            foreach (int person in UnitLifecycleRegistry(RegistryKind.Person))
                if (Need(current.ReadPersonFirstLocation(person), "story_units_PersonE2") == home &&
                    Need(current.ReadPersonStatus(person), "story_units_PersonE6") is >= 1 and <= 4)
                    EndAbsent(person);
        }

        private void EndAbsentByForce(ForceReference target)
        {
            if (!Need(current.ReadForceNativeValidity(target), "story_units_Force_validity")) return;
            int force = Need(current.ReadForcePhysicalSlot(target), "story_units_Force_identity");
            if (force is < 1 or > 51) return;
            foreach (int person in UnitLifecycleRegistry(RegistryKind.Person))
                if (Need(current.ReadPersonOldForceSlot(person), "story_units_Person_Force22CF60") == force &&
                    Need(current.ReadPersonStatus(person), "story_units_PersonE6") is >= 1 and <= 4)
                    EndAbsent(person);
        }

        private void RecallByHome(BaseReference target)
        {
            if (!Need(current.ReadBaseNativeValidity(target), "story_units_Base_validity")) return;
            ushort home = Need(current.ReadBaseObjectLink(target), "story_units_Base4E");
            if (home is < 1 or > 61) return;
            foreach (int physical in UnitLifecycleRegistry(RegistryKind.Army))
            {
                if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(latest, current, physical), "story_units_Army233560")) continue;
                var army = new ArmyUnitReference(physical);
                int commander = Person(Need(current.ReadArmyUnitCommanderId(army), "story_units_Army12"));
                ushort armyHome = Need(current.ReadPersonNativeValidity(commander), "story_units_Commander_validity")
                    ? Need(current.ReadPersonFirstLocation(commander), "story_units_CommanderE2") : (ushort)0;
                if (armyHome == home) RecallArmy(army, target);
            }
        }

        private void RecallByForce(ForceReference target)
        {
            if (!Need(current.ReadForceNativeValidity(target), "story_units_Force_validity")) return;
            int force = Need(current.ReadForcePhysicalSlot(target), "story_units_Force_identity");
            if (force is < 1 or > 51) return;
            foreach (int physical in UnitLifecycleRegistry(RegistryKind.Army))
            {
                var army = new ArmyUnitReference(physical);
                int commander = Person(Need(current.ReadArmyUnitCommanderId(army), "story_units_Army12"));
                int armyForce = Need(current.ReadPersonNativeValidity(commander), "story_units_Commander_validity")
                    ? Need(current.ReadPersonOldForceSlot(commander), "story_units_Army_Force22CD80") : 0;
                if (armyForce != force || Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(latest, current, physical), "story_units_Army233560")) continue;
                //22A730 resolves the commander's current Home again after
                //the iterator predicate, rather than using the Force capital.
                commander = Person(Need(current.ReadArmyUnitCommanderId(army), "story_units_return_Army12"));
                ushort home = Need(current.ReadPersonNativeValidity(commander), "story_units_return_Commander_validity")
                    ? Need(current.ReadPersonFirstLocation(commander), "story_units_return_CommanderE2") : (ushort)0;
                RecallArmy(army, BaseLookup.Find(home, current));
            }
        }

        private void RecallArmy(ArmyUnitReference army, BaseReference target)
        {
            if (!Need(current.ReadArmyUnitNativeValidity(army), "story_units_return_Army_validity")) return;
            int commander = Person(Need(current.ReadArmyUnitCommanderId(army), "story_units_return_Army12"));
            if (!Need(current.ReadPersonNativeValidity(commander), "story_units_return_Commander_validity")) return;
            //29F220 marks an injured commander before its affiliation gate.
            //The mature same-Force return kernel owns every subsequent write.
            BindUnitLifecycleInputs();
            byte health = Need(OfficerHealthSourceAdapter.ReadByte(current, commander), "story_units_return_health161");
            if (health != 0)
            {
                ushort flags = Need(current.ReadArmyReturnPersonFlags196(commander), "story_units_return_Person15E");
                current.WriteArmyReturnPersonFlags196(commander, OfficerBehaviorSourceAdapter.MarkReturn(flags));
            }
            ReturnArmy(army, target, 1);
        }

        private void FinishRecall()
        {
            //Both tails run even when the requested Force/Base was invalid.
            BindUnitLifecycleInputs();
            ConsumePending();
            latest = rules.MaintainEditedDeathArmyCommandsCurrent(latest, current); //274D70.
        }
    }
}
