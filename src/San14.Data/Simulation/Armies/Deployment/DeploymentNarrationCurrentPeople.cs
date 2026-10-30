using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current actors and address relationships used by the finite
/// ordinary narration. Missing old-save fields are never read from scenario.</summary>
internal sealed class DeploymentNarrationCurrentPeople(GameState state, SourceWorldContext current,
    TacticRemakeCurrentAbilities abilities, int? speaker, int? ruler, int? area = null, Func<int,int,int?>? panelReader = null,
    ushort? cityArgument = null, Func<string>? readZeroStrengthText = null,
    GameResourceRepository? appearanceRepository = null, LoadedScenario? appearanceScenario = null,
    string? appearanceLanguage = null, int? forceArgument = null,
    int? personArgument2 = null, int? personArgument3 = null,
    NarrationVoiceRequests? voiceRequests = null, int? forceArgument1 = null, int? personArgument4 = null,
    MessageDocument? appearanceDataSource = null, LoadedForceNameDefinitions? appearanceNamesSource = null) : INarrationCurrentContext
{
    private readonly int?[] _arguments = [speaker, ruler, personArgument2, personArgument3, personArgument4];
    private int Argument(int index) => (uint)index < (uint)_arguments.Length && _arguments[index] is int id
        ? id : throw Missing("person_argument");
    private static T Need<T>(T? value, string input) where T:struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("deployment.narration." + input);
    private int Physical(int id) => current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0)?.PhysicalSlot ?? throw Missing("person_pointer");
    private bool Valid(int id) => Need(current.ReadPersonNativeValidity(Physical(id)), "person_validity");
    private OfficerState Person(int id)
    {
        int p = Physical(id);
        if (current.ReadPersonIdentity(p) != id || current.ResolveOfficerSource(id) != p || !state.Officers.TryGetValue(id, out var person))
            throw Missing("current_person_owner");
        return person;
    }
    private int Status(int id) => Need(current.ReadPersonStatus(Physical(id)), "person_status");
    private int Force(int id) => Need(current.ReadPersonForce(Physical(id))?.PhysicalSlot, "person_force_pointer");
    private bool ForceValid(int force) => Need(current.ReadForceNativeValidity(new(force)), "force_validity");
    private int Rank(int id) => Need(current.ReadMoraleWaveForceRank47(Force(id)), "force_rank");
    private int Field(int id, int field)
    {
        if (state.NativeArmySimulation?.Parameters.Int32Values is not {} values ||
            values.GetValueOrDefault(OwnedPersonRelationInputs.VersionKey) != 1 ||
            !values.TryGetValue(OwnedPersonRelationInputs.PersonFieldKey(Physical(id), field), out int value) || value is < 0 or > 65535)
            throw Missing("person_relation_" + field.ToString("X"));
        return value;
    }
    //310EC0 cases5D/5E test the current related Person pointer's native
    //validity. The canonical relation owner supplies the saved identity;
    //out-of-range identities use the separate physical placeholder table.
    private bool RelatedPersonValid(int id, int field)
    {
        int raw = Field(id, field);
        var related = (raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0)) ?? throw Missing("related_person_pointer");
        return related.PhysicalSlot is int physical &&
            Need(current.ReadPersonNativeValidity(physical), "related_person_validity");
    }
    private int Style(int id) => Need(Person(id).DialogueStyle, "current_dialogue_style");
    private int Birth(int id) => Need(Person(id).BirthYear, "current_birth_year");
    private int Generation(int id) => Need(Person(id).FamilyGeneration, "current_family_generation");
    private int Age(int id) => PersonCurrentAgeSource.Read(state, current, Physical(id));
    public int PersonValue(int argument, int field)
    {
        int id = Argument(argument);
        if (field == 0) return id;
        if (id is < 1 or > 6000 || !Valid(id) && id is not (>= 2001 and <= 2088)) return 0;
        return field switch
        {
            '2' => id, 'J' => Style(id), 'Y' => Status(id) == 1 ? 1 : 0,
            //Person message case60: actual Force getter, then native validity.
            //The domain Force dictionary and Person status are not substitutes.
            0x60 => Need(current.ReadForceNativeValidity(current.ReadPersonForce(Physical(id))
                ?? throw Missing("person60_force_pointer")), "person60_force_validity") ? 1 : 0,
            'Z' => Status(id) == 2 ? 1 : 0, '[' => Status(id) == 3 ? 1 : 0,
            'V' => Person(id).Sex is OfficerSex sex ? sex == OfficerSex.Male ? 1 : 0 : throw Missing("person_sex"),
            'W' => Person(id).Sex is OfficerSex sex ? sex == OfficerSex.Female ? 1 : 0 : throw Missing("person_sex"),
            't' => (Need(current.ReadArmyReturnPersonFlags196(Physical(id)), "current_person_flags") & 0x400) != 0 ? 1 : 0,
            '_' => (uint)(id - 5012) < 22 ? 1 : 0,
            '\\' => id == Field(id, 0xBC) ? 1 : 0,
            ']' => RelatedPersonValid(id, 0xBC) ? 1 : 0,
            '^' => RelatedPersonValid(id, 0xBA) ? 1 : 0,
            'G' => Status(id) == 1 ? Rank(id) : 0,
            'H' => StatusField(id).Value,
            //310EC0 case4C reads the current signed Person16C word. This
            //is the saved allegiance disposition, not an opening template.
            'L' => Need(current.ReadAllegianceDisposition(Physical(id)), "current_allegiance_disposition16C"),
            'U' => HealthIdentity(id),
            'c' => CurrentPlayerField(id),
            'o' => Need(current.ReadMoraleWavePersonPostE7(Physical(id)), "current_office"),
            '=' => Age(id),
            'A' => EffectiveOperand(id, 0), 'B' => EffectiveOperand(id, 1),
            'C' => abilities.ResolveDimension(state, id, 2, panelReader) ?? throw Missing("current_effective_intelligence"),
            'D' => abilities.ResolveDimension(state, id, 3, panelReader) ?? throw Missing("current_effective_politics"),
            'b' => Need(current.ReadPersonSecondLocation(Physical(id)), "current_second_location") is >= 62 and <= 561 ? 1 : 0,
            'X' => IsAdvisor(id) ? 1 : 0,
            'Q' => PersonForceDisplayName(id).Value,
            'R' => PersonForceName(id).Value,
            >= 0x8A and <= 0x93 => CommanderTerrain(id, field),
            _ => throw Missing("person_value_" + field.ToString("X"))
        };
    }
    private int CommanderTerrain(int id, int field)
    {
        //3108E0's finite8A..93 cases call22A200(Person,1), then232A90.
        //The latter compares22B8C0's actual terrain receiver ordinal. It
        //does not inspect formation, trait, or the Person's Home terrain.
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, Physical(id))
            ?? throw Missing("commander_terrain_Army_pointer");
        if (!Need(current.ReadArmyUnitNativeValidity(army), "commander_terrain_Army_validity")) return 0;
        ushort rawHex = Need(current.ReadArmyUnitRawHex2A(army), "commander_terrain_Army2A");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw Missing("commander_terrain_Hex_pointer");
        int surface = 0;
        if (hex.PhysicalSlot.HasValue)
        {
            byte rawSurface = Need(current.ReadHexSurfaceCode(hex), "commander_terrain_Hex10");
            if (rawSurface < 31) surface = rawSurface;
        }
        var receiver = current.ReadSurfaceTypeTableReference(surface)
            ?? throw Missing("commander_terrain_Surface_pointer");
        int physical = Need(receiver.PhysicalSlot, "commander_terrain_Surface_ordinal");
        int wanted = field switch
        { 0x8A => 13, 0x8B => 14, 0x8C => 12, 0x8D => 5, 0x8E => 8,
          0x8F => 9, 0x90 => 10, 0x91 => 6, 0x92 => 7, 0x93 => 4,
          _ => throw Missing("commander_terrain_field") };
        return physical == wanted ? 1 : 0;
    }
    private int EffectiveOperand(int id, int dimension)
    {
        _ = Person(id);
        int first = abilities.ResolveDimension(state, id, dimension, panelReader)
            ?? throw Missing("current_effective_ability." + dimension);
        string display = dimension == 1 && first == 0
            ? readZeroStrengthText?.Invoke() ?? throw Missing("zero_strength_resource")
            : first.ToString(System.Globalization.CultureInfo.InvariantCulture);
        //The helper's display append precedes the independent numeric query,
        //even when this field is a condition and the display buffer is discarded.
        _ = CityDomesticMessageFormatting.FormatAbilityField(display);
        return abilities.ResolveDimension(state, id, dimension, panelReader)
            ?? throw Missing("current_effective_ability." + dimension);
    }
    public string CityText(int argument, int field)
    {
        if (argument != 0 || field != '2') throw Missing("city_text_field");
        ushort raw = cityArgument ?? throw Missing("city_argument");
        if (raw is 0 or > 61) return string.Empty;
        var selected = BaseLookup.Find(raw, current);
        int physical = Need(selected.PhysicalSlot, "city_pointer");
        string? name = selected.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name,
            BaseKind.Gate => state.Gates.GetValueOrDefault(physical + 51)?.Name,
            _ => null
        };
        return CityDomesticMessageFormatting.FormatCityNameField(name ?? throw Missing("city_name"));
    }
    public string PersonText(int argument, int field)
    {
        int id = Argument(argument);
        if (id is < 1 or > 6000) return "";
        int physical = Physical(id);
        if (!Need(current.ReadPersonNativeValidity(physical), "person_validity") && id is not (>= 2001 and <= 2088)) return "";
        if (field == 'H') return StatusField(id).Text;
        if (field == 'U') return HealthText(id);
        if (field == 'T') return FirstLocationText(physical);
        if (field == 'o') return OfficeText(id);
        if (field == 'Q') return PersonForceDisplayName(id).Text;
        if (field == 'R') return PersonForceName(id).Text;
        var person = Person(id);
        string value = field switch
        {
            '3' => person.Name,
            '4' => SpecialName(id) ? person.Name : Surname(person),
            '6' => SpecialName(id) ? person.Name : Surname(person) + (Courtesy(person) is { Length: > 0 } courtesy
                ? courtesy : person.CurrentGivenName ?? throw Missing("current_given_name")),
            '7' => Courtesy(person) is { Length: > 0 } name ? name : person.Name,
            '8' => Courtesy(person) is { Length: > 0 } name ? name : id is >= 5101 and <= 5200 or >= 2062 and <= 2066
                ? person.Name : person.CurrentGivenName ?? throw Missing("current_given_name"),
            _ => throw Missing("person_text_" + field.ToString("X"))
        };
        return appearanceLanguage is null ? "^02" + value + "^00" :
            CityDomesticMessageFormatting.FormatPersonNameField(AppearanceName(value));
        static string Courtesy(OfficerState person) => person.CurrentCourtesyName ?? throw Missing("current_courtesy_name");
    }
    private string FirstLocationText(int personPhysical)
    {
        ushort raw = Need(current.ReadPersonFirstLocation(personPhysical), "current_first_location");
        var selected = BaseLookup.Find(raw, current);
        if (!Need(current.ReadBaseNativeValidity(selected), "T_base_validity")) return string.Empty;
        int physical = Need(selected.PhysicalSlot, "T_base_pointer");
        string? name = selected.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name,
            BaseKind.Gate => state.Gates.GetValueOrDefault(physical + 51)?.Name,
            _ => null
        };
        return CityDomesticMessageFormatting.FormatCityNameField(name ?? throw Missing("T_base_name"));
    }
    private static bool SpecialName(int id) => id is >= 5101 and <= 5200 or >= 2062 and <= 2066;
    private static string Surname(OfficerState person) => person.CurrentSurname ?? throw Missing("current_surname");

    private LoadedForceNameDefinitions? appearanceNames = appearanceNamesSource;
    private MessageDocument? appearanceData = appearanceDataSource;
    private (int Value, string Text) PersonForceDisplayName(int id)
    {
        // Q keeps the raw District Force value even when its Force is invalid.
        // Its display goes directly through22EE20; R independently prefers
        // the current custom kingdom name before this same fallback.
        int person = Physical(id);
        var force = current.ReadPersonForce(person) ?? throw Missing("Q_force_pointer");
        byte raw = Need(current.ReadPersonOldForceSlot(person), "Q_raw_district_Force");
        if (!Need(current.ReadForceNativeValidity(force), "Q_force_validity")) return (raw, string.Empty);
        return (raw, CityDomesticMessageFormatting.FormatCityNameField(ForceFallback(force)));
    }

    private (int Value, string Text) PersonForceName(int id)
    {
        //R validates Person in its dispatcher, then its actual Force before
        //reading byte48. P3/P4's custom-first capture cannot implement this prefix.
        var force = current.ReadPersonForce(Physical(id)) ?? throw Missing("R_force_pointer");
        if (!Need(current.ReadForceNativeValidity(force), "R_force_validity")) return (0, string.Empty);
        int at = Need(force.PhysicalSlot, "R_force_physical");
        byte raw = Need(current.ReadForceKingdomNameIndex(at), "R_force48");
        var catalog = AppearanceCatalog();
        int index = raw <= 100 ? raw : 0;
        if ((uint)index >= (uint)catalog.Catalog.Definitions.Count) throw Missing("R_catalog_row");
        var selected = catalog.Catalog.Definitions[index];
        if (selected.NativeValidity)
            return (raw, CityDomesticMessageFormatting.FormatCityNameField(Head(selected.ShortName)));
        return (0, CityDomesticMessageFormatting.FormatCityNameField(ForceFallback(force)));
    }

    public string ForceText(int argument, int field)
    {
        if (argument is not (0 or 1) || field != '4') throw Missing("appearance_force_field");
        int raw = (argument == 0 ? forceArgument : forceArgument1) ?? throw Missing("appearance_force_argument");
        if (raw is < 1 or > 51) return string.Empty;
        //P4 independently relooks up its registered virtual60 argument and
        //retains its custom-first order, unlike Person R above.
        var operand = TrapCounterReportForceNameSource.Capture(state, current, (byte)raw);
        string text = operand.Form switch
        {
            TrapCounterForceNameForm.Empty => string.Empty,
            TrapCounterForceNameForm.CustomKingdom => Custom(operand.CustomKingdomIndex!.Value),
            TrapCounterForceNameForm.PersonWithArmySuffix => AppearanceName(operand.PersonNameOperandAtSource) + AppearanceSuffix(),
            TrapCounterForceNameForm.PersonWithoutArmySuffix => AppearanceName(operand.PersonNameOperandAtSource),
            _ => throw Missing("appearance_force_form")
        };
        return CityDomesticMessageFormatting.FormatCityNameField(text);
    }

    private string ForceFallback(ForceReference force)
    {
        if (!Need(current.ReadForceNativeValidity(force), "R_fallback_force_validity")) return string.Empty;
        int at = Need(current.ReadForcePhysicalSlot(force), "R_fallback_force_identity");
        if (at == 0) throw Missing("R_fallback_force0_profile");
        ushort raw = Need(current.ReadForceRecord(at)?.RawRuler10, "R_fallback_ruler10");
        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("R_fallback_person_pointer");
        int physical = Need(person.PhysicalSlot, "R_fallback_person_physical");
        if (!Need(current.ReadPersonNativeValidity(physical), "R_fallback_person_validity")) return string.Empty;
        int identity = Need(current.ReadPersonIdentity(physical), "R_fallback_person_identity");
        if (current.ResolveOfficerSource(identity) != physical || !state.Officers.TryGetValue(identity, out var owner))
            throw Missing("R_fallback_person_owner");
        if (at is < 46 or > 50) return AppearanceName(owner.Name) + AppearanceSuffix();
        return AppearanceName(SpecialName(identity) ? owner.Name : owner.CurrentSurname);
    }

    private LoadedForceNameDefinitions AppearanceCatalog() => appearanceNames ??=
        (appearanceRepository ?? throw Missing("appearance_repository"))
        .LoadForceNameDefinitions((appearanceScenario ?? throw Missing("appearance_scenario")).FixedDataSource.SourcePath);
    private string Custom(byte index)
    {
        var definitions = AppearanceCatalog().Catalog.Definitions;
        if ((uint)index >= (uint)definitions.Count || !definitions[index].NativeValidity)
            throw Missing("appearance_custom_name");
        return Head(definitions[index].ShortName);
    }
    private string AppearanceSuffix()
    {
        appearanceData ??= (appearanceRepository ?? throw Missing("appearance_repository"))
            .LoadMessages(appearanceLanguage ?? throw Missing("appearance_language"), MessageKind.Data).Messages;
        if (appearanceData.Sections.Count <= 1 || appearanceData.Sections[1].Records.Count <= 0x34F)
            throw Missing("appearance_suffix_resource");
        string suffix = Head(appearanceData.Sections[1].Records[0x34F].Text ?? throw Missing("appearance_suffix"));
        if (suffix.Length > 1) throw Missing("appearance_suffix_profile");
        return suffix;
    }
    private string AppearanceName(string? value)
    {
        if (value is null) throw Missing("appearance_current_name");
        if (appearanceLanguage is not ("TC" or "SC")) throw Missing("appearance_language");
        string text = Head(value);
        if (text.Length > (appearanceLanguage == "SC" ? 18 : 14) || text.Any(char.IsSurrogate))
            throw Missing("appearance_name_profile");
        return text;
    }
    private static string Head(string value)
    { int nul = value.IndexOf('\0'); return nul < 0 ? value : value[..nul]; }
    private string OfficeText(int id)
    {
        int post = Need(current.ReadMoraleWavePersonPostE7(Physical(id)), "current_office");
        if (!Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "office_validity")) return string.Empty;
        return state.OfficerOffices.TryGetValue(post, out var office) ? "^05" + office.Name + "^00" : throw Missing("current_office_name");
    }

    private int HealthIdentity(int id) => Need(OfficerHealthSourceAdapter.ReadByte(current, Physical(id)), "U_current_health");
    //Person case55 ->35C7E0 ->30BE70. Its label belongs to Data table0,
    //and the source wraps even an out-of-range empty label in color03.
    private string HealthText(int id)
    {
        int health = HealthIdentity(id);
        return "^03" + (health <= 3 ? StatusLabel(0, 0x123 + health) : string.Empty) + "^00";
    }

    private (int Value, string Text) StatusField(int id)
    {
        int physical = Physical(id);
        string label;
        if ((Need(current.ReadArmyReturnPersonFlags196(physical), "H_flags15E") & 0x4000) != 0)
            label = StatusLabel(1, 0x859);
        else
        {
            byte selector = Need(current.ReadPersonStatus(physical), "H_label_status");
            label = selector <= 9 ? StatusLabel(0, 0xF9 + selector) : string.Empty;
        }
        string text = CityDomesticMessageFormatting.FormatAbilityField(label);
        return (Need(current.ReadPersonStatus(physical), "H_fresh_numeric_status"), text);
    }
    private string StatusLabel(int section, int index)
    {
        //The reached repository is the existing resource-service profile. A
        //missing owner/load failure is Unknown, never a fabricated not-ready result.
        if (appearanceLanguage is not ("TC" or "SC")) throw Missing("H_resource_language");
        appearanceData ??= (appearanceRepository ?? throw Missing("H_resource_owner"))
            .LoadMessages(appearanceLanguage, MessageKind.Data).Messages;
        if ((uint)section >= (uint)appearanceData.Sections.Count ||
            (uint)index >= (uint)appearanceData.Sections[section].Records.Count) throw Missing("H_label_resource");
        return Head(appearanceData.Sections[section].Records[index].Text ?? throw Missing("H_label_text"));
    }
    private int CurrentPlayerField(int id)
    {
        var first = current.ReadPersonForce(Physical(id)) ?? throw Missing("c_first_Force_pointer");
        int firstOrdinal = Need(current.ReadForcePhysicalSlot(first), "c_first_Force_ordinal");
        if (firstOrdinal is < 1 or > 51) return 0;
        var fresh = current.ReadPersonForce(Physical(id)) ?? throw Missing("c_fresh_Force_pointer");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("c_player_Force_pointer");
        int playerOrdinal = Need(current.ReadForcePhysicalSlot(player), "c_player_Force_ordinal");
        return playerOrdinal != 0 && playerOrdinal == Need(current.ReadForcePhysicalSlot(fresh), "c_fresh_Force_ordinal") ? 1 : 0;
    }
    public string AreaText(int argument, int field)
    {
        if (argument != 0 || field != '3') throw Missing("area_text_field");
        int physical = area ?? throw Missing("area_argument");
        if (physical is < 1 or > 500 || !Need(current.ReadAreaNativeValidity(new(physical)), "area_validity")) return string.Empty;
        return state.Areas.TryGetValue(physical, out var owner) ? "^05" + owner.Name + "^00" : throw Missing("current_area_name");
    }
    private bool IsAdvisor(int id)
    {
        int force = Force(id);
        if (!ForceValid(force)) return false;
        int advisor = Need(current.ReadAssignmentForceAdvisor52(force), "current_force_advisor");
        return Valid(advisor) && Need(current.ReadPersonIdentity(Physical(advisor)), "advisor_identity") == id;
    }
    public int Personality(int first, Func<int> readTraitKey)
    {
        //Source order is first expression, pointer lookup, second expression,
        //then native validity/range and the ordered raw selector.
        int physical = Physical(first);
        int second = readTraitKey();
        if (!Need(current.ReadPersonNativeValidity(physical), "person_validity") || second is < 1 or > 355) return 0;
        for (int slot = 0; slot < 9; slot++)
        {
            int raw = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, slot)
                ?? throw Missing("current_trait_lookup");
            if (raw == second) return 1;
        }
        return 0;
    }
    public int Relationship(int field, int first, int second)
    {
        // Function4's second operand is a voice ID, not another Person. Its
        // current actor was admitted by the owning merchant preparation.
        if (field == '4') return (voiceRequests ?? throw Missing("voice_request_owner"))
            .Request(Person(first), second) ? 1 : 0;
        if (field == '5')
        {
            if (!Need(current.ReadPersonNativeValidity(Physical(first)), "person_validity")) return 0;
            return (voiceRequests ?? throw Missing("spatial_voice_request_owner"))
                .RequestSpatial(Person(first), second) ? 1 : 0;
        }
        if (field == 'L') return Personality(first, () => second);
        if (field == '<') return Valid(first) ? 1 : 0;
        //The installed dispatcher reserves K but only consumes its operands.
        if (field == 'K') return 0;
        if (!Valid(first) || !Valid(second)) return 0;
        return (field switch
        {
            '=' => Senior(first, second), '>' => Junior(first, second),
            '?' => Precedes(first, second), '@' => SameForce(first, second),
            'A' => Sworn(first, second), 'B' => Spouse(first, second),
            'C' => Parent(first, second) || Parent(second, first),
            'D' => Siblings(first, second), 'E' => Grandparent(first, second),
            'F' => ParentSibling(first, second, true), 'G' => ParentSibling(first, second, false),
            'H' => Field(first, 0xB2) == Field(second, 0xB2),
            'I' => first != second && Birth(first) < Birth(second),
            'J' => first != second && Birth(second) < Birth(first),
            'M' => RawRelationship(first, second, true),
            'N' => RawRelationship(first, second, false),
            _ => throw Missing("relationship_" + field.ToString("X"))
        }) ? 1 : 0;
    }
    //30F0E0 cases1B/1C call233010/232B30 after both actual validity gates.
    private bool RawRelationship(int first, int second, bool like)
    {
        int person = Physical(first);
        ushort target = Need(current.ReadPersonIdentity(Physical(second)), "raw_relation_target");
        for (int index = 0; index < 8; index++)
            if (Need(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, person, index)
                : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, person, index), "raw_relation_slot") == target) return true;
        return false;
    }
    private bool Father(int a, int b) => a != b && Field(a, 0xB4) is int value && value != 0 && value == b;
    private bool Mother(int a, int b) => a != b && Field(a, 0xB6) is int value && value != 0 && value == b;
    private bool Parent(int a, int b) => Father(a, b) || Mother(a, b);
    private bool Spouse(int a, int b) => a != b && Field(a, 0xBA) is int value && value != 0 && value == b;
    private bool Sworn(int a, int b) => a != b && Field(a, 0xBC) is int value && value != 0 && value == Field(b, 0xBC);
    private bool Siblings(int a, int b) => a != b &&
        (Field(a, 0xB4) is int father && father != 0 && father == Field(b, 0xB4) ||
         Field(a, 0xB6) is int mother && mother != 0 && mother == Field(b, 0xB6));
    private bool SameForce(int a, int b)
    {
        int first = Force(a), second = Force(b);
        return ForceValid(first) && ForceValid(second) && first == second;
    }
    private bool Grandparent(int a, int b)
    {
        if (a == b) return false;
        int father = Field(a, 0xB4);
        if (Valid(father) && Parent(father, b)) return true;
        int mother = Field(a, 0xB6);
        return Valid(mother) && Parent(mother, b);
    }
    private bool ParentSibling(int a, int b, bool older)
    {
        if (a == b) return false;
        int relative = Field(a, 0xB4);
        if (!Valid(relative) || !Siblings(relative, b))
        {
            relative = Field(a, 0xB6);
            if (!Valid(relative) || !Siblings(relative, b)) return false;
        }
        return older ? Birth(b) < Birth(relative) : Birth(relative) < Birth(b);
    }
    private bool Precedes(int a, int b)
    {
        if (!SameForce(a, b)) return true;
        if (Status(a) == 1 && Status(b) != 1) return true;
        if (Status(a) != 1 && Status(b) == 1) return false;
        int first = Need(current.ReadMoraleWavePersonPostE7(Physical(a)), "current_office");
        int second = Need(current.ReadMoraleWavePersonPostE7(Physical(b)), "current_office");
        bool validFirst = Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, first), "office_validity");
        if (validFirst && Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, second), "office_validity") && first != second)
            return first < second;
        return validFirst || !Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, second), "office_validity");
    }
    private int Emperor => Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "current_emperor");
    private bool Senior(int a, int b)
        => new PersonCurrentSeniority(state, current).IsSenior(Physical(a), Physical(b));
    private bool Junior(int a, int b)
    {
        if (Status(b) == 1 && Rank(b) == 1 || b == Emperor)
        {
            if (Status(a) != 1) { if (a != Emperor) return true; }
            else if (Rank(a) != 1) return true;
        }
        if (Father(a, b) || Mother(a, b)) return true;
        if (Field(a, 0xB2) == Field(b, 0xB2))
        {
            if (Generation(b) < Generation(a)) return true;
            if (Generation(a) == Generation(b) && Age(a) < Age(b)) return true;
        }
        if (Field(a, 0xBC) == b) return true;
        if (Sworn(a, b) && Field(a, 0xBC) != a && Age(a) < Age(b)) return true;
        if (Spouse(a, b) && Need(Person(b).Sex, "person_sex") == OfficerSex.Male) return true;
        int force = Force(a);
        if (force is >= 1 and <= 51 && force == Force(b) && Status(b) == 1) return true;
        int mother = Field(a, 0xB6);
        return Valid(mother) && (Father(mother, b) || Mother(mother, b) || Siblings(mother, b));
    }
}
