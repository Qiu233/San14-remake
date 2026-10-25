using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //42BCD0 -> 2BDD20. Each qualifying person counts once when any of the
    //first four current abilities reaches90; Charm is not part of this count.
    internal GameState ApplyNormalTurnForceServiceRewardsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        var effects = new List<CampaignEffectRequest>();
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("ForceC0"))
        {
            if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical }) throw Missing("Force_payload");
            var force = new ForceReference(physical);
            if (!K(current.ReadForceNativeValidity(force), "Force_validity")) continue;
            int exceptional = 0, serviceExperience = 0;
            int key = K(current.ReadForcePhysicalSlot(force), "Force60");
            //2349D0(mode0) traverses the live Person100 chain and includes
            //statuses1..4, retaining the source Force key throughout the pass.
            foreach (int? candidate in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
            {
                int person = K(candidate, "Person_payload");
                if (key is < 1 or > 51 || K(current.ReadPersonOldForceSlot(person), "Person_Force") != key ||
                    K(current.ReadPersonStatus(person), "PersonE6") is < 1 or > 4) continue;
                if (!K(current.ReadPersonNativeValidity(person), "Person_validity")) continue;
                int personId = K(current.ReadPersonIdentity(person), "Person10");
                for (int dimension = 0; dimension < 4; dimension++)
                    if (K(abilities.ResolveDimension(state, personId, dimension,
                            (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)), "current_ability") >= 90)
                    { exceptional = unchecked(exceptional + 1); break; }

                int ruler = Ruler(force);
                if (Doctrine(ruler) != Doctrine(person) ||
                    !K(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 0x30), "Person_effect48_presence")) continue;
                int basis = unchecked(K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x30), "Person_effect48") *
                    Rule("doctrine-effect48-scale", 1));
                int trait = unchecked(K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x4C), "Person_effect76") *
                    ArmyParameter(SimulationParameterKeys.ArmyDestruction.ForceExperienceTraitMultiplier));
                int advantage = Advantage(force);
                int amount = unchecked(unchecked(trait * basis) / 100 +
                    unchecked(ArmyParameter(SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier) * advantage * basis) / 100 + basis);
                if ((Flags(force) & 8) != 0)
                    amount = unchecked(amount + unchecked(ArmyParameter(SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent) * basis) / 100);
                serviceExperience = unchecked(serviceExperience + amount);
            }
            //The ordinary service award precedes the new-record writer and
            //its separate award, including a real zero-delta258A60 call.
            state = ApplyAdministrationForceExperienceCurrent(state, current, force, serviceExperience, effects);
            if (exceptional == 0) continue;
            var native = state.NativeArmySimulation ?? throw Missing("record_native_owner");
            var forceOwner = native.Forces.GetValueOrDefault(physical) ?? throw Missing("record_Force_owner");
            if (K(forceOwner.BestExceptionalOfficerCount, "Force174_best_exceptional_count") >= exceptional) continue;
            state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                { [physical] = forceOwner with { BestExceptionalOfficerCount = (ushort)Math.Clamp(exceptional, 0, ushort.MaxValue) } } } };

            int recordBasis = Rule("exceptional-record-experience", 300);
            int leader = Ruler(force);
            int rulerTrait = K(current.ReadPersonNativeValidity(leader), "record_ruler_validity")
                ? unchecked(K(TacticRemakeCurrentTraits.PersonContribution(current, leader, 0x4C), "record_ruler_effect76") *
                    ArmyParameter(SimulationParameterKeys.ArmyDestruction.ForceExperienceTraitMultiplier)) : 0;
            int recordAmount = unchecked(recordBasis +
                unchecked(recordBasis * ArmyParameter(SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier) * Advantage(force)) / 100 +
                unchecked(recordBasis * rulerTrait) / 100);
            if ((Flags(force) & 8) != 0)
                recordAmount = unchecked(recordAmount +
                    unchecked(recordBasis * ArmyParameter(SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent)) / 100);
            state = ApplyAdministrationForceExperienceCurrent(state, current, force, recordAmount, effects);
        }
        if (effects.Count != 0)
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("effect_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision"), effects) };
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw Missing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });

        int Ruler(ForceReference force)
        {
            int id = K(current.ReadForceRecord(K(force.PhysicalSlot, "ruler_Force_pointer"))?.RawRuler10, "Force10");
            return K((id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ruler_Person_pointer");
        }
        int Doctrine(int person)
        {
            int id = K(current.ReadPersonIdentity(person), "doctrine_Person10");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw Missing("doctrine_Person_owner");
            return K(OfficerDoctrineRules.ReadDoctrineId(owner.DoctrineId, owner.AbilityState), "Person14C");
        }
        int Advantage(ForceReference force)
        {
            var value = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
            return value.Value ?? throw Missing(value.UnresolvedOwner ?? "Force_land_advantage6");
        }
        byte Flags(ForceReference force) => K(state.NativeArmySimulation?.Forces.GetValueOrDefault(K(force.PhysicalSlot, "flags_Force_pointer"))?.Flags12, "Force12");
        //TC file-backed1A4909C=300 and1A490A8=1 are independent of the
        //shared saved1A490B4=1/1A49BB8=1/1A49794=10 experience modifiers.
        int Rule(string name, int value) => StrategicParameter(state, "normal-force-service-rewards." + name, value);
        int ArmyParameter(string key) => K(ArmyDestructionParameterSeed.ReadCurrent(state, key), key);
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.force-service-rewards." + field);
    }
}
