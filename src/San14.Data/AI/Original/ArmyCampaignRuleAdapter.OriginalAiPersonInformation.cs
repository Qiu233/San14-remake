using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadOriginalAiPersonInformationReceiver(SourceWorldContext current, int officerId) =>
        AiPersonKnown((officerId is >= 0 and <= 6000 ? current.ReadPersonLookupReference(officerId)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");

    internal OriginalAiEntity? ReadOriginalAiPersonInformationEntity(SourceWorldContext current, int person, int mode)
    {
        if (mode == 1)
        {
            var force = current.ReadPersonForce(person) ?? throw AiPersonMissing("Force_pointer");
            return force.PhysicalSlot is int physical ? new(WorldEntityKind.Force, physical) : null;
        }
        if (mode == 2)
        {
            var home = BaseLookup.Find(AiPersonKnown(current.ReadPersonFirstLocation(person), "Home_E2"), current);
            return home.PhysicalSlot.HasValue ? AiForceInformationBaseEntity(current, home) : null;
        }
        throw AiPersonMissing("entity_mode_" + mode);
    }

    internal int ReadOriginalAiPersonInformationScalar(GameState state, SourceWorldContext current, int person, int mode)
    {
        if (mode is >= 7 and <= 11 || mode == 26)
        {
            int identity = AiPersonKnown(current.ReadPersonIdentity(person), "ability_Person10");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            int Read(int dimension) => AiPersonKnown(abilities.ResolveDimension(state, identity, dimension,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "effective_ability_" + dimension);
            //08E3A0's selector10 reads dimension3, then falls through selector11
            //which reads dimension3 again. It does not return dimension4.
            if (mode == 10) { _ = Read(3); return Read(3); }
            if (mode <= 11) return Read(mode == 11 ? 3 : mode - 7);
            int maximum = Read(0);
            for (int dimension = 1; dimension < 5; dimension++) maximum = Math.Max(maximum, Read(dimension));
            return maximum;
        }
        if (mode is >= 13 and <= 20 || mode is 31 or 32 or 34 or 35)
        {
            int offset = mode <= 20 ? 0x16A + (mode - 13) * 2
                : mode switch { 31 => 0x17A, 32 => 0x17C, 34 => 0x17E, _ => 0x180 };
            return offset == 0x16C ? AiPersonKnown(current.ReadAllegianceDisposition(person), "disposition16C")
                : AiPersonKnown(RulerCaptureReleaseInputs.ReadCurrentWord(state, person, offset), "preference_" + offset.ToString("X"));
        }
        if (mode == 21)
            return AiPersonKnown(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, Post()), "office_command_capacity");
        if (mode == 24)
        {
            //Only the raw office salary basis of08E3A0: no monthly policy or
            //personality discount. Invalid office uses actual table entry0.
            //TC and SC independently use the same multiplier in both office branches.
            int post = Post();
            if (!AiPersonKnown(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "salary_office_validity")) post = 0;
            var values = state.NativeArmySimulation?.Parameters.Int32Values ?? throw AiPersonMissing("salary_owner");
            if (values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1 ||
                !values.TryGetValue(OwnedArmyGovernorComparatorInputs.OfficeGuard36Key(post), out int pay) || (uint)pay > byte.MaxValue)
                throw AiPersonMissing("office_salary36");
            return unchecked(pay * 10);
        }
        if (mode == 30) return AiPersonAway(state, current, person) ? 1 : 0;
        if (mode == 33) return AiPersonKnown(OwnedArmyMoraleWaveInputs.ReadPost37(state, Post()), "office_grade37");
        if (mode is 36 or 37 or 38)
        {
            if (mode == 36) return unchecked(PersonAreaAppointmentSource.AdditionalCount(state, current, person) + 1);
            int count = AiPersonKnown(current.ReadAssignmentPersonAreaVector(person)?.ActiveCount1E8, "assigned_area_count");
            return mode == 37 ? count : unchecked(PersonAreaAppointmentSource.AdditionalCount(state, current, person) + 1) <= count ? 1 : 0;
        }
        return mode switch
        {
            0 => AiPersonKnown(current.ReadPersonIdentity(person), "Person10"),
            3 => AiPersonKnown(current.ReadPersonSecondLocation(person), "position_E4"),
            4 => AiPersonKnown(OwnedPersonRelationInputs.ReadCurrentAffinity(state, person), "affinity_BE"),
            5 => AiPersonKnown(current.ReadPersonStatus(person), "status_E6"),
            6 => AiPersonKnown(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "loyalty_E8"),
            12 => AiPersonKnown(current.ReadArmyReturnPersonFlags196(person), "behavior_flags") & 1,
            _ => throw AiPersonMissing("scalar_mode_" + mode)
        };
        int Post() => AiPersonKnown(current.ReadMoraleWavePersonPostE7(person), "office_E7");
    }

    internal IReadOnlyList<OriginalAiPersonSkillReference> ReadOriginalAiPersonInformationSkills(GameState state,
        SourceWorldContext current, int person, OriginalAiPersonSkillKind kind)
    {
        var result = new List<OriginalAiPersonSkillReference>();
        if (kind == OriginalAiPersonSkillKind.Formation)
        {
            uint mask = AiPersonKnown(current.ReadPersonFormationMask164(person), "formation_mask");
            int id = AiPersonKnown(current.ReadPersonIdentity(person), "formation_Person10");
            //08E3A0 visits definition1..20; repeated capabilities never create
            //duplicate definition receivers and definition0 is not appended.
            for (int formation = 1; formation <= 20; formation++)
                if (DeploymentFormationEligibility.Allows(mask, formation, id))
                {
                    var definition = current.ReadStaticTableReference(0x77428, formation) ?? throw AiPersonMissing("formation_lookup");
                    result.Add(new(kind, AiPersonKnown(definition.PhysicalSlot, "formation_pointer")));
                }
        }
        else if (kind == OriginalAiPersonSkillKind.Tactic)
        {
            int id = AiPersonKnown(current.ReadPersonIdentity(person), "tactic_Person10");
            var available = new HashSet<int>();
            for (int slot = 0; slot < 10; slot++)
            {
                //The actual0922D0 membership getter is22A4B0, including its
                //current gem qualification gate. Reading raw tactic bytes is insufficient.
                var tactic = DeploymentTacticSelectionSource.ReadCurrentSlot(current, id, person, slot);
                available.Add(AiPersonKnown(current.ReadSkillTacticPhysicalSlot(tactic), "tactic_pointer"));
            }
            for (int tactic = 1; tactic <= 200; tactic++)
                if (available.Contains(tactic)) result.Add(new(kind, tactic));
        }
        else throw AiPersonMissing("skill_kind");
        return result;
    }

    internal IReadOnlyList<OriginalAiEntity> ReadOriginalAiPersonInformationAffections(GameState state,
        SourceWorldContext current, int person, bool dislike)
    {
        var result = new List<OriginalAiEntity>(8);
        for (int slot = 0; slot < 8; slot++)
        {
            int raw = AiPersonKnown(dislike ? OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, person, slot)
                : OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, person, slot), "affection_slot_" + slot);
            int physical = ReadOriginalAiPersonInformationReceiver(current, raw);
            if (AiPersonKnown(current.ReadPersonNativeValidity(physical), "affection_validity"))
                result.Add(new(WorldEntityKind.Officer, AiPersonKnown(current.ReadPersonIdentity(physical), "affection_identity")));
        }
        //22E460/22B9D0 mode0 retain source slot order and duplicate pointers.
        //The detail-page's special-pair compaction/sort is a different caller.
        return result;
    }

    private static bool AiPersonAway(GameState state, SourceWorldContext current, int person)
    {
        if (AiPersonKnown(current.ReadPersonStatus(person), "away_status") is >= 1 and <= 4 &&
            PersonPositionRepair.DecodeFirstLocation(AiPersonKnown(current.ReadPersonFirstLocation(person), "away_home")) !=
            PersonPositionRepair.DecodeSecondLocation(AiPersonKnown(current.ReadPersonSecondLocation(person), "away_position"))) return true;
        var linked = current.ReadPersonLinkedEntity(person) ?? throw AiPersonMissing("away_linked_pointer");
        if (!AiPersonKnown(current.ReadLinkedEntityNativeValidity(linked), "away_linked_validity")) return false;
        int identity = AiPersonKnown(linked.Identity, "away_linked_identity");
        var manager = current.ReadOwnedPersonCommandManager() ?? throw AiPersonMissing("away_task_owner");
        if (manager.Disappearances is null) throw AiPersonMissing("away_disappearance_owner");
        if (manager.Disappearances.ContainsKey(identity)) return true;
        if (manager.Returns.ContainsKey(identity) || manager.Talents?.ContainsKey(identity) == true ||
            manager.Diplomacies?.ContainsKey(identity) == true || manager.Strategies?.ContainsKey(identity) == true ||
            manager.Independences?.ContainsKey(identity) == true || manager.Caravans?.ContainsKey(identity) == true ||
            manager.Intelligences?.ContainsKey(identity) == true) return false;
        throw AiPersonMissing("away_linked_kind");
    }
    private static T AiPersonKnown<T>(T? value, string field) where T : struct => value ?? throw AiPersonMissing(field);
    private static InvalidDataException AiPersonMissing(string field) => new("original_ai.person_information." + field);
}
