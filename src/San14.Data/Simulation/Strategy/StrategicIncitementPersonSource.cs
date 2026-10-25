using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2D3FB0's special Person receiver. The argument is the actual
    /// District table slot passed through 2D3410, not a Force identity. The
    /// caller owns the current world, its list pool, and eventual publication.</summary>
    internal (GameState State, int? PersonPhysical) CreateStrategicIncitementPersonCurrent(
        GameState state, SourceWorldContext current, int specialDistrict)
    {
        if (specialDistrict < 0) throw new ArgumentOutOfRangeException(nameof(specialDistrict));
        int district = specialDistrict < 52 ? specialDistrict : 0;
        int rawForce = StrategyNeed(current.ReadDistrictForceId(district), "incitement_person_District10");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
            ?? throw StrategyMissing("incitement_person_Force_pointer");
        int forcePhysical = StrategyNeed(current.ReadForcePhysicalSlot(force), "incitement_person_Force_identity");
        var working = state;

        // The template draw precedes the attempt to find a vacant receiver.
        // An exhausted dynamic range therefore still advances this App stream.
        int templateId;
        if (forcePhysical == 51) templateId = 4031 + Draw(2);
        else
        {
            int offset = rawForce switch { 46 => 5, 47 => 10, 48 => 15, 49 => 20, 50 => 25, _ => 0 };
            int[] templates = [0, 0, 0, 0, 0, 4002, 4003, 4004, 4005, 4006,
                4008, 4009, 4010, 4011, 4012, 4014, 4015, 4016, 4017, 4018,
                4020, 4021, 4022, 4023, 4024, 4026, 4027, 4028, 4029, 4030];
            templateId = templates[offset + Draw(5)];
        }
        var templatePointer = current.ReadPersonLookupReference(templateId)
            ?? throw StrategyMissing("incitement_person_template_lookup");
        int templatePhysical = StrategyNeed(templatePointer.PhysicalSlot, "incitement_person_template_pointer");

        //32BEF0 only finds the first invalid nonzero-identity receiver. It does
        //not reset it or copy the whole template over it. Untouched fields,
        //including Home/position, trait slots, sort keys and caps, must survive.
        int? selected = null;
        for (int identity = 4033; identity <= 4100; identity++)
        {
            var pointer = current.ReadPersonLookupReference(identity)
                ?? throw StrategyMissing("incitement_person_dynamic_lookup");
            int physical = StrategyNeed(pointer.PhysicalSlot, "incitement_person_dynamic_pointer");
            if (StrategyNeed(current.ReadPersonNativeValidity(physical), "incitement_person_dynamic_validity")) continue;
            if (StrategyNeed(current.ReadPersonIdentity(physical), "incitement_person_dynamic_identity") == 0) continue;
            selected = physical;
            break;
        }
        if (selected is not int receiver) return (working, null);
        var template = Person(templatePhysical);
        var previous = Person(receiver);
        string first, second;
        //The special-name accessor is relevant if a template lookup was
        //legitimately rebound to one of the source's special name identities.
        if (template.Id is >= 5101 and <= 5200 or >= 2062 and <= 2066)
            first = second = CopyName(template.Name);
        else
        { first = Name(template.CurrentSurname, "surname"); second = Name(template.CurrentGivenName, "given_name"); }
        var lifespan = previous.Lifespan ?? throw StrategyMissing("incitement_person_receiver_lifespan_kind");
        var templateLifespan = template.Lifespan ?? throw StrategyMissing("incitement_person_template_terminal_year");
        ushort portrait = StrategyNeed(current.ReadPersonPortraitAppearance(templatePhysical), "incitement_person_template_portraitA6");
        byte dialogue = StrategyNeed(template.DialogueStyle, "incitement_person_template_dialogueA8");
        var sex = template.Sex ?? throw StrategyMissing("incitement_person_template_sexA9");
        var status = OfficerServiceStatusSourceAdapter.Decode(4);
        var generated = previous with
        {
            Name = first + second, CurrentSurname = first, CurrentGivenName = second,
            PortraitAppearanceIndex = portrait, DialogueStyle = dialogue,
            Sex = (int)sex > 1 ? OfficerSex.Female : sex,
            AppearanceYear = (ushort)Math.Min(350, (int)StrategyNeed(template.AppearanceYear, "incitement_person_template_appearanceAA")),
            BirthYear = (ushort)Math.Min(350, (int)StrategyNeed(template.BirthYear, "incitement_person_template_birthAC")),
            Lifespan = lifespan with { TerminalYear = (ushort)Math.Min(350, (int)templateLifespan.TerminalYear) },
            ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, previous.Role)
        };
        Set(generated);
        current.SetPersonPortraitAppearance(receiver, portrait);
        ushort bloodline = StrategyNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(working, templatePhysical, 0xB2),
            "incitement_person_template_bloodlineB2");
        var native = working.NativeArmySimulation ?? throw StrategyMissing("incitement_person_relation_owner");
        working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with
        {
            Int32Values = new(native.Parameters.Int32Values)
                { [OwnedPersonRelationInputs.PersonFieldKey(receiver, 0xB2)] = bloodline }
        } } };
        _personRelationInputs.BindCurrent(() => working, current);
        current.WritePersonStatus(receiver, 4);

        //240DB0 removes old Force associations, clears the two assignment
        //counters only across Forces, then writes the new District membership.
        var associations = new List<PersonAssignmentPrecallResult>();
        var effects = new List<CampaignEffectRequest>();
        PersonDistrictTransfer.Apply(receiver, specialDistrict,
            ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, Receive));
        working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, receiver, associations);
        int actualDistrict = StrategyNeed(current.ReadPersonDistrictSlot(receiver), "incitement_person_written_District");
        var actualForce = current.ReadPersonForce(receiver) ?? throw StrategyMissing("incitement_person_written_Force_pointer");
        int affiliation = StrategyNeed(current.ReadForcePhysicalSlot(actualForce), "incitement_person_written_Force_identity");
        Set(working.Officers[previous.Id] with
        {
            DistrictId = actualDistrict == 0 ? null : actualDistrict,
            ForceId = affiliation == 0 ? null : affiliation,
            LocationState = current.ReadOfficerLocation(previous.Id) ?? throw StrategyMissing("incitement_person_written_membership")
        });

        var abilitySource = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        int[] dimensions = new int[5];
        for (int dimension = 0; dimension < 5; dimension++)
        {
            int ability = StrategyNeed(abilitySource.ResolveDimension(working, template.Id, dimension),
                "incitement_person_template_effective_ability_" + dimension);
            int addition = Draw(10);
            int cap = forcePhysical is >= 46 and <= 50 && dimension < 2 ? 70 : 60;
            dimensions[dimension] = Math.Clamp(Math.Min(unchecked(ability + addition), cap), 0, 100);
            current.SetPersonPostLoadByte(receiver, 0x124 + dimension, (byte)dimensions[dimension]);
        }
        Set(working.Officers[previous.Id] with { BaseAbilities = new(dimensions[0], dimensions[1],
            dimensions[2], dimensions[3], dimensions[4]) });

        //The TC runtime byte150 is the voice channel, not the project's
        //canonical trait150 window (which corresponds to TC runtime118).
        //2391C0 serializes it at row209; A8 is dialogue at row211.
        Set(working.Officers[previous.Id] with { VoiceType = 1 });
        _ = ObjectRegistration.Register(new(RegistryKind.Person, receiver), current);
        if (forcePhysical is >= 46 and <= 50)
        {
            ushort flags = StrategyNeed(current.ReadArmyReturnPersonFlags196(receiver), "incitement_person_flags15E");
            current.WriteArmyReturnPersonFlags196(receiver, (ushort)(flags | 0x2000));
            Set(working.Officers[previous.Id] with { Behavior = current.ReadPersonBehavior(receiver) });
        }
        if (effects.Count != 0)
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                ?? throw StrategyMissing("incitement_person_pending_effects")).Append(CampaignEffectSource.StrategicCommands,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw StrategyMissing("incitement_person_calendar"), effects) };
        return (working, receiver);

        int Draw(int bound)
        {
            var owner = working.CampaignAppRandom32 ?? throw StrategyMissing("incitement_person_App_random");
            var random = new SharedSourceRandom32(owner.State);
            int result = random.Next(bound);
            working = working with { CampaignAppRandom32 = new(random.State) };
            return result;
        }
        OfficerState Person(int physical)
        {
            int identity = StrategyNeed(current.ReadPersonIdentity(physical), "incitement_person_ID10");
            return current.ResolveOfficerSource(identity) == physical && working.Officers.TryGetValue(identity, out var person)
                ? person : throw StrategyMissing("incitement_person_owned_receiver");
        }
        void Set(OfficerState person) => working = working with { Officers = new(working.Officers) { [person.Id] = person } };
        static string CopyName(string value) => value.Length > 7 ? value[..7] : value;
        static string Name(string? value, string field) => CopyName(value ?? throw StrategyMissing("incitement_person_template_" + field));
        void Receive(PersonAssignmentPrecallResult result)
        {
            //The actual old-Force cleanup's report path is shared with other
            //current personnel operations when that branch is reached.
            if (result.Reports.Count == 0) return;
            working = ApplyPersonAssociationRemovalReports(working, current, receiver, result,
                () => (ReadArmyDestructionSampler(working, current) ?? throw StrategyMissing("incitement_person_report_World450")).Day450,
                effects);
        }
    }

    /// <summary>The failed 2D3410 branches call precisely 226480 and 32B890.
    /// This shared reset preserves appearance/base abilities and does not run
    /// death, injury, recruitment, loyalty calculation or any random draw.</summary>
    internal GameState ResetStrategicIncitementPersonCurrent(GameState state, SourceWorldContext current, int personPhysical)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw StrategyMissing("incitement_person_reset_list_pool");
        return new PersonDeathSource(this, state, current, pool).ReclaimHirePersonCurrent(personPhysical);
    }
}
