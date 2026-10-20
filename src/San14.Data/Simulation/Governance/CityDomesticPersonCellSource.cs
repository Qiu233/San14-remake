using San14.Core;

namespace San14.Data;

/// <summary>Only the current City Basic consumer's finite leaves. No candidate
/// eligibility, whole-card resolve, saved-vector publication or state writer.</summary>
internal sealed class CityDomesticPersonCellSource(GameState state, SourceWorldContext current,
    ArmyOperationContext definitions, OwnedArmyGovernorComparatorInputs? governor = null, LoadedScenario? scenario = null, GameResourceRepository? repository = null, string? language = null)
{
    private San14.Data.Formats.MessageDocument? _forceMessages;

    internal string Name(CityDomesticCandidate candidate, bool sortKey = false)
    {
        int p = candidate.PhysicalSlot;
        if (!K(current.ReadPersonNativeValidity(p), "name_validity")) return "";
        int id = Identity(candidate);
        if (definitions.ResolveOfficerSource(id) != p) throw Missing("name_owner_binding");
        var owner = state.Officers.GetValueOrDefault(id) ?? throw Missing("name_owner");
        return (sortKey ? owner.CurrentNameSortKey : owner.Name) ?? throw Missing(sortKey ? "name_key" : "name");
    }

    internal int Identity(CityDomesticCandidate candidate)
    {
        int id = K(current.ReadPersonIdentity(candidate.PhysicalSlot), "candidate_identity");
        if (id != candidate.RawIdentity) throw Missing("candidate_identity_binding");
        return id;
    }

    internal int RowState(CityDomesticCandidate candidate)
    {
        Identity(candidate);
        byte health = K(OfficerHealthSourceAdapter.ReadByte(current, candidate.PhysicalSlot), "health_paint");
        return health is >= 1 and <= 3 ? 2 : 0;
    }

    internal bool Personality(CityDomesticCandidate candidate, CityDomesticDuty duty)
    {
        Identity(candidate);
        int wanted = duty == CityDomesticDuty.Recruiter ? 117 : 118;
        for (int index = 0; index < 9; index++)
            if (K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,
                    candidate.PhysicalSlot, index), "role_personality." + index) == wanted) return true;
        return false;
    }

    internal bool? Returning(CityDomesticCandidate candidate)
    {
        if (!K(current.ReadPersonNativeValidity(candidate.PhysicalSlot), "returning_validity")) return null;
        return ReturningKey(candidate) != 0;
    }
    internal int ReturningKey(CityDomesticCandidate candidate)
    {
        Identity(candidate);
        return -(K(current.ReadArmyReturnPersonFlags196(candidate.PhysicalSlot), "returning_flags") & 1);
    }

    internal bool AreaLocation(CityDomesticCandidate candidate, bool display)
    {
        if (display && !K(current.ReadPersonNativeValidity(candidate.PhysicalSlot), "area_location_validity")) return false;
        Identity(candidate);
        return K(current.ReadPersonSecondLocation(candidate.PhysicalSlot), "second_location") is >= 62 and <= 561;
    }

    internal int? Task(CityDomesticCandidate candidate)
    {
        var linked = TaskLink(candidate);
        if (linked.Identity is null) return null;
        throw Missing("task_associated_display_consumer");
    }

    internal int? TaskKey(CityDomesticCandidate candidate)
    {
        var linked = TaskLink(candidate);
        if (linked.Identity is not int identity) return null;
        // These two actual virtual98 values are already consumed by the
        // shared current Busy leaf; they do not establish display formatting.
        var commands = current.ReadOwnedPersonCommandManager();
        if (commands?.Returns.ContainsKey(identity) == true) return 15;
        if (commands?.Disappearances?.ContainsKey(identity) == true) return 16;
        throw Missing("task_associated_virtual98");
    }

    private PersonLinkedEntityReference TaskLink(CityDomesticCandidate candidate)
    {
        Identity(candidate);
        // The actual commander-army leaf is shared with existing consumers;
        // an active Army's display/visibility and virtual task remain unclosed.
        if (K(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, candidate.PhysicalSlot), "task_commander_army"))
            throw Missing("task_army_display_consumer");
        var linked = current.ReadPersonLinkedEntity(candidate.PhysicalSlot) ?? throw Missing("task_associated22AC00");
        return linked;
    }

    internal bool Special(CityDomesticCandidate candidate)
    {
        int physical = candidate.PhysicalSlot;
        int id = Identity(candidate);
        if (!(unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
              unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 ||
              unchecked((uint)(id - 4191)) <= 9)) return false;
        if (K(current.ReadPersonFirstLocation(physical), "special_first_location") is not (>= 1 and <= 61)) return false;
        var force = current.ReadPersonForce(physical) ?? throw Missing("special_force");
        return K(force.PhysicalSlot, "special_force_pointer") is not (>= 46 and <= 50);
    }

    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    internal static InvalidDataException Missing(string name) => new("city-domestic.person." + name);

    internal readonly record struct SortValue(int Number, string? Text, bool NotApplicable)
    {
        internal static SortValue Numeric(int value) => new(value, null, false);
        internal static SortValue String(string value) => new(0, value, false);
        internal static SortValue Empty => new(0, null, true);
    }
    private bool NativeValid(CityDomesticCandidate c) => K(current.ReadPersonNativeValidity(c.PhysicalSlot), "person_validity");
    private byte Status(CityDomesticCandidate c) => K(current.ReadPersonStatus(c.PhysicalSlot), "service_status");
    private ushort Flags(CityDomesticCandidate c) => K(current.ReadArmyReturnPersonFlags196(c.PhysicalSlot), "person_flags");
    private ForceReference Force(CityDomesticCandidate c) => current.ReadPersonForce(c.PhysicalSlot) ?? throw Missing("person_force");
    private int ForcePhysical(ForceReference f) => K(current.ReadForcePhysicalSlot(f), "force_pointer");
    private bool ForceValid(ForceReference f) => K(current.ReadForceNativeValidity(f), "force_validity");
    private bool SpecialForce(CityDomesticCandidate c) => ForcePhysical(Force(c)) is >= 46 and <= 50;
    private bool Force51(CityDomesticCandidate c) => ForcePhysical(Force(c)) == 51;
    private OfficerState Owner(CityDomesticCandidate c)
    {
        int id = Identity(c);
        if (current.ResolveOfficerSource(id) != c.PhysicalSlot || definitions.ResolveOfficerSource(id) != c.PhysicalSlot ||
            !state.Officers.TryGetValue(id, out var person)) throw Missing("person_owner_binding");
        return person;
    }
    private DistrictReference District(CityDomesticCandidate c)
    {
        int raw = K(current.ReadPersonDistrictSlot(c.PhysicalSlot), "person_district");
        return current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw Missing("district_pointer");
    }
    private bool DistrictValid(DistrictReference d) => K(current.ReadDistrictNativeValidity(d), "district_validity");

    internal DeploymentPersonServiceStatus? ServiceStatus(CityDomesticCandidate c)
    {
        if ((Flags(c) & 0x4000) != 0) return new(true, null);
        byte status = Status(c); return status < 10 ? new(false, status) : null;
    }
    internal int ServiceStatusKey(CityDomesticCandidate c)
    { int status = Status(c); return status * 10 + ((Flags(c) >> 14) & 1); }
    internal int? Health(CityDomesticCandidate c)
    { byte value = K(OfficerHealthSourceAdapter.ReadByte(current, c.PhysicalSlot), "health"); return value <= 3 ? value : null; }
    internal int HealthKey(CityDomesticCandidate c) => K(OfficerHealthSourceAdapter.ReadByte(current, c.PhysicalSlot), "health");
    internal int? Sex(CityDomesticCandidate c) => SexKey(c);
    internal int SexKey(CityDomesticCandidate c) => Owner(c).Sex switch
    { OfficerSex.Male => 0, OfficerSex.Female => 1, _ => throw Missing("sex") };
    internal int? DistrictNumber(CityDomesticCandidate c)
    {
        var d = District(c); if (!DistrictValid(d)) return null;
        return K(current.ReadDistrictNumber(K(d.PhysicalSlot, "district_identity")), "district_number");
    }
    internal SortValue DistrictKey(CityDomesticCandidate c)
    {
        var district = District(c);
        int physical = K(district.PhysicalSlot, "district_identity");
        byte number = K(current.ReadDistrictNumber(physical), "district_number");
        return number == 0 ? SortValue.Empty : SortValue.Numeric(number);
    }
    internal int? Loyalty(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c) || (Flags(c) & 0x4000) != 0) return null;
        byte status = Status(c); var force = Force(c);
        bool forceValid = ForceValid(force), personValid = NativeValid(c);
        if (!personValid || status is 1 or 5 || !forceValid) return null;
        return Math.Min(RawLoyalty(c), (byte)100);
    }
    internal SortValue LoyaltyKey(CityDomesticCandidate c)
    { int? value = Loyalty(c); return value is int n ? SortValue.Numeric(n) : SortValue.Empty; }
    private byte RawLoyalty(CityDomesticCandidate c) => K(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current,
        c.PhysicalSlot, governor ?? throw Missing("governor_owner")), "loyalty_raw");
    internal int? ServiceYears(CityDomesticCandidate c, bool display)
    {
        if (SpecialForce(c) || Special(c)) return null;
        ushort firstYear = K((governor ?? throw Missing("governor_owner")).ReadCurrentWord166(state, c.PhysicalSlot), "service_year_word");
        short year = K(state.NativeMapSeason?.ThirdDate.Year, "current_year");
        byte status = Status(c); var force = Force(c);
        bool forceValid = ForceValid(force), personValid = NativeValid(c);
        if (!personValid || status is 1 or 5 || !forceValid) return null;
        if (display && SpecialForce(c)) return null;
        return unchecked(year - firstYear + 1);
    }
    internal SortValue ServiceYearsKey(CityDomesticCandidate c)
    { int? value = ServiceYears(c, false); return value is int n ? SortValue.Numeric(n) : SortValue.Empty; }
    internal string? Office(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c)) return null;
        if (!NativeValid(c) || Status(c) == 1) return null;
        byte raw = K(current.ReadMoraleWavePersonPostE7(c.PhysicalSlot), "office");
        //The supported canonical 256-row directory resolves byteN to ordinalN.
        int ordinal = raw;
        if (!K(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, ordinal), "office_post_validity")) return "";
        if (!state.OfficerOffices.TryGetValue(ordinal, out var office) || office.Id != ordinal)
            throw Missing("office_definition");
        return office.Name ?? throw Missing("office_name");
    }
    internal SortValue OfficeKey(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c)) return SortValue.Empty;
        byte office = K(OfficerAppointmentSourceAdapter.ReadOfficeId(Owner(c).Appointment), "office");
        bool valid = NativeValid(c);
        if (!valid || Status(c) == 1 || office == 0) return SortValue.Empty;
        return SortValue.Numeric(office);
    }
    internal int? OtherMetric(CityDomesticCandidate c)
    {
        int value = 0;
        if (DistrictValid(District(c)) && Status(c) != 1)
            if (!Force51(c) && !SpecialForce(c)) throw Missing("other_metric_value");
        if (DistrictValid(District(c)) && Status(c) != 1) return value;
        return null;
    }
    internal SortValue OtherMetricKey(CityDomesticCandidate c)
    {
        if (NativeValid(c) && Status(c) != 1 && !Force51(c) && !SpecialForce(c))
            throw Missing("other_metric_value");
        return SortValue.Numeric(0);
    }
    internal int? ItemCount(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c)) return null;
        var result = ItemKey(c); return result.NotApplicable ? int.MaxValue : result.Number;
    }
    internal SortValue ItemKey(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c)) return SortValue.Empty;
        throw Missing("item_filtered_iterator");
    }
    internal string? Doctrine(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c) || (Flags(c) & 0x4000) != 0) return null;
        int raw = DoctrineByte(c);
        //22DD50 is the canonical record ordinal, not name/native validity.
        if (raw == 0 || raw >= 7) return "";
        if (!state.Doctrines.TryGetValue(raw, out var definition) || definition.Id != raw || definition.Name is null)
            throw Missing("doctrine_current_definition");
        return definition.Name;
    }
    private int DoctrineByte(CityDomesticCandidate c)
    {
        var person = Owner(c);
        int raw = person.DoctrineId ?? person.AbilityState?.ObservedDoctrineId ?? throw Missing("doctrine_raw");
        if (raw is < 0 or > 255) throw Missing("doctrine_byte");
        return raw;
    }
    internal SortValue DoctrineKey(CityDomesticCandidate c)
    {
        if (SpecialForce(c) || Special(c) || (Flags(c) & 0x4000) != 0) return SortValue.Empty;
        _ = DoctrineByte(c);
        throw Missing("doctrine_sort_table_binding");
    }
    private int Mode() => state.CampaignEventWorld?.ScenarioMode ?? throw Missing("scenario_mode");
    internal int? Age(CityDomesticCandidate c, bool display)
    {
        if (Mode() == 2 || Force51(c) || SpecialForce(c) || (Flags(c) & 0x400) != 0 || Special(c)) return null;
        if (display && Mode() == 3) throw Missing("age_mode3_display_control");
        //2A62E0 rereads mode before the ordinary birth/year arithmetic.
        if (Mode() == 3) throw Missing("age_mode3_helper_control");
        if (Mode() == 2) throw Missing("age_special_mode");
        int birth = Owner(c).BirthYear ?? throw Missing("age_current_birth_year");
        int year = state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("age_current_year");
        int value = Math.Max(0, year - birth + 1);
        return value == 0 ? null : value;
    }
    internal SortValue AgeKey(CityDomesticCandidate c)
    {
        int? value = Age(c, false); return value is int n ? SortValue.Numeric(n) : SortValue.Empty;
    }

    internal string? Relation(CityDomesticCandidate c, int field, bool key)
    {
        //The display does an independent scalar read after its discarded numeric format.
        if (!key) _ = RelationWord(c, field).ToString(System.Globalization.CultureInfo.InvariantCulture);
        ushort raw = RelationWord(c, field);
        if (raw == 0 || raw > (key ? 10000 : 9999)) return key ? null : "";
        int physical = K((raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "relation_person_pointer");
        return PersonName(physical, key);
    }
    private ushort RelationWord(CityDomesticCandidate c, int field)
    {
        Identity(c);
        return K(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, c.PhysicalSlot, field), "relation_word");
    }
    private string PersonName(int physical, bool key)
    {
        if (!key && !K(current.ReadPersonNativeValidity(physical), "related_person_validity")) return "";
        var person = PersonNameOwner(physical);
        return (key ? person.CurrentNameSortKey : person.Name) ?? throw Missing(key ? "related_name_key" : "related_name");
    }
    private OfficerState PersonNameOwner(int physical)
    {
        int id = K(current.ReadPersonIdentity(physical), "related_person_identity");
        if (current.ResolveOfficerSource(id) != physical || definitions.ResolveOfficerSource(id) != physical ||
            !state.Officers.TryGetValue(id, out var person)) throw Missing("related_person_owner");
        return person;
    }
    internal SortValue RelationKey(CityDomesticCandidate c, int field)
    { string? value = Relation(c, field, true); return value is null ? SortValue.Empty : SortValue.String(value); }

    private void CreditDisplayContext()
    {
        if (state.CampaignEventWorld?.CurrentUserIndex != -1) throw Missing("credit_current_user_gate");
        if (Mode() == 3) throw Missing("credit_person_selection_state");
    }
    private byte Stage(CityDomesticCandidate c) => K(OfficerMeritSourceAdapter.EncodeStage(Owner(c).MeritState), "credit_stage");
    internal string? RankName(CityDomesticCandidate c)
    {
        CreditDisplayContext();
        if (Status(c) == 1 || Special(c) || (Flags(c) & 0x4000) != 0) return null;
        byte stage = Stage(c);
        if (stage is < 1 or > 6) return null;
        if (scenario is null || state.ContentHashes.GetValueOrDefault(scenario.FixedDataSource.ResourceUri) != scenario.FixedDataSource.Sha256 ||
            scenario.Snapshot.CreditRankParameters is not { FormatMarker: 0x66 } catalog || catalog.Ranks.Count != 7 ||
            catalog.Ranks[stage].Slot != stage) throw Missing("credit_rank_catalog");
        return catalog.Ranks[stage].Name ?? throw Missing("credit_rank_name");
    }
    internal SortValue RankStageKey(CityDomesticCandidate c)
    {
        if (Status(c) == 1 || Special(c) || (Flags(c) & 0x4000) != 0 || Stage(c) == 0) return SortValue.Empty;
        if (Status(c) == 1 || Special(c) || (Flags(c) & 0x4000) != 0) return SortValue.Numeric(0);
        return SortValue.Numeric(Stage(c));
    }
    internal int? Merit(CityDomesticCandidate c, bool display)
    {
        if (display) CreditDisplayContext();
        if (Status(c) == 1 || Special(c) || (Flags(c) & 0x4000) != 0 || Stage(c) == 0) return null;
        return K(OfficerMeritSourceAdapter.EncodeMerit(Owner(c).MeritState), "merit");
    }
    internal SortValue MeritKey(CityDomesticCandidate c)
    { int? value = Merit(c, false); return value is int n ? SortValue.Numeric(n) : SortValue.Empty; }
    internal int CreditBonus(CityDomesticCandidate c, int kind)
    {
        if (!NativeValid(c) || Status(c) == 1) return 0;
        Identity(c);
        var result = kind == 1 ? OfficerCommandCapacity.ResolveCreditBonus(state, current, c.PhysicalSlot)
            : OfficerCommandCapacity.ResolveCreditKindBonus(state, current, c.PhysicalSlot, 3);
        return result.Bonus ?? throw new InvalidDataException(result.UnresolvedInput);
    }
    internal int AdditionalBonus(CityDomesticCandidate c, Func<int> read)
    {
        if (!NativeValid(c) || SpecialForce(c)) return 0;
        Identity(c); return read();
    }
    internal int? BonusDisplay(CityDomesticCandidate c, Func<int> read)
    { int first = read(); return first > 0 ? read() : null; }
    internal int? Reset(CityDomesticCandidate c)
    {
        if (!NativeValid(c)) return null;
        int value = ResetByte(c); return value == 0 ? null : value;
    }
    private int ResetByte(CityDomesticCandidate c) => K(PersonAssignmentPrecallInputs.ReadCurrentResetWord1FF(state, current, c.PhysicalSlot), "reset_word") & 255;
    internal SortValue ResetKey(CityDomesticCandidate c) => NativeValid(c) ? SortValue.Numeric(ResetByte(c)) : SortValue.Empty;

    internal CityDomesticPersonForceName? ForceName(CityDomesticCandidate c)
    {
        var f = Force(c); if (!ForceValid(f)) return null;
        int physical = ForcePhysical(f); string body;
        if (physical is >= 46 and <= 50)
        {
            int repeated = ForcePhysical(f);
            int city = repeated switch { 46 => 47, 47 => 48, 48 => 49, 49 => 50, 50 => 51, _ => 0 };
            if (city == 0) throw Missing("force_special_repeated_pointer");
            var selected = current.ReadCityTableReference(city) ?? throw Missing("force_special_city_pointer");
            body = LocationName(selected);
        }
        else
        {
            ushort raw = current.ReadForceRecord(physical)?.RawRuler10 ?? throw Missing("force_ruler");
            int person = K((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "force_ruler_pointer");
            if (!K(current.ReadPersonNativeValidity(person), "force_ruler_validity")) return null;
            body = PersonName(person, false);
        }
        _ = "^00~0~"; //The fixed reset formatting precedes the marker resource.
        if (repository is null || language is not ("TC" or "SC")) throw Missing("force_display_marker_resource_owner");
        var messages = _forceMessages;
        if (messages is null)
        {
            try { messages = repository.LoadMessages(language, San14.Data.Formats.MessageKind.Data).Messages; }
            catch (IOException error) { throw new InvalidDataException("city-domestic.person.force_display_marker_resource", error); }
        }
        if (messages.Sections.Count <= 1 || messages.Sections[1].Records.Count <= 0x571 ||
            messages.Sections[1].Records[0x571].Text is not string marker) throw Missing("force_display_marker_resource");
        _forceMessages = messages;
        byte rawColour = K(current.ReadForceReportColor(f), "force_colour");
        return new(marker + body, MapSpritePalettes.ForceFill(rawColour));
    }
    private string ForceKey(ForceReference force)
    {
        if (!ForceValid(force)) return "";
        int p = ForcePhysical(force);
        ushort raw = p == 0 ? (ushort)5014 : current.ReadForceRecord(p)?.RawRuler10 ?? throw Missing("force_ruler");
        int person = K((raw <= 6000 ? current.ReadPersonLookupReference(raw)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "force_ruler_pointer");
        if (!K(current.ReadPersonNativeValidity(person), "force_ruler_validity")) return "";
        int repeated = ForcePhysical(force);
        if (repeated is >= 46 and <= 50)
        {
            ushort first = K(current.ReadPersonIdentity(person), "force_reading_person_identity");
            if (unchecked((uint)(first - 5101)) > 99)
            {
                ushort second = K(current.ReadPersonIdentity(person), "force_reading_person_identity");
                if (unchecked((uint)(second - 2062)) > 4)
                    return PersonNameOwner(person).CurrentSurnameSortKey ?? throw Missing("force_first_reading_owner");
            }
        }
        return PersonName(person, true);
    }
    internal bool BeforeForce(CityDomesticCandidate a, CityDomesticCandidate b, bool reversed)
    {
        var left = Force(a); var right = Force(b);
        bool lv = ForceValid(left), rv = ForceValid(right);
        if (!lv) return false; if (!rv) return true;
        if (reversed)
        {
            if (a.PhysicalSlot == b.PhysicalSlot) return false;
            return BeforeForce(b, a, false);
        }
        string x = ForceKey(left), y = ForceKey(right);
        int xn = x.IndexOf('\0'), yn = y.IndexOf('\0');
        if (xn >= 0) x = x[..xn]; if (yn >= 0) y = y[..yn];
        if (x.Length + y.Length + 2 > 1024) throw Missing("force_sort_name_capacity");
        if (string.Equals(x, y, StringComparison.Ordinal))
        { int rp = ForcePhysical(right); return ForcePhysical(left) < rp; }
        return DeploymentResponsibilityNameOrder.Before(x, y);
    }
    internal string? Home(CityDomesticCandidate c)
    {
        int raw = K(current.ReadPersonFirstLocation(c.PhysicalSlot), "first_location");
        bool gate = raw is >= 52 and <= 61;
        int index = raw is >= 1 and <= 51 ? raw : gate ? raw - 51 : 0;
        if (Force51(c)) return "";
        if (SpecialForce(c))
        {
            ushort repeated = K(current.ReadPersonFirstLocation(c.PhysicalSlot), "home_exception_first_location");
            if (repeated is not (>= 1 and <= 61) || !SpecialForce(c)) return "";
        }
        if (!gate && index == 0) return null;
        var selected = gate ? current.ReadGateTableReference(index) : current.ReadCityTableReference(index);
        return LocationName(selected ?? throw Missing("home_base_pointer"));
    }
    private string LocationName(BaseReference selected)
    {
        int physical = K(selected.PhysicalSlot, "location_identity");
        return selected.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(physical)?.Name ?? throw Missing("location_city_name"),
            BaseKind.Gate => state.Gates?.GetValueOrDefault(physical + 51)?.Name ?? throw Missing("location_gate_name"),
            _ => throw Missing("location_base_kind")
        };
    }
    internal string? CurrentLocation(CityDomesticCandidate c)
    {
        var location = PersonPositionRepair.DecodeSecondLocation(
            K(current.ReadPersonSecondLocation(c.PhysicalSlot), "second_location"));
        if (location.Kind == 27)
        {
            _ = current.ReadArmyUnitTableReference(location.Index < 501 ? location.Index : 0)
                ?? throw Missing("current_location_army_pointer");
            throw Missing("current_location_army_name");
        }
        if (location.Kind is not (5 or 6)) return "";
        var selected = location.Kind == 5 ? current.ReadCityTableReference(location.Index < 52 ? location.Index : 0)
            : current.ReadGateTableReference(location.Index < 11 ? location.Index : 0);
        return LocationName(selected ?? throw Missing("current_location_base_pointer"));
    }
    internal bool BeforeLocation(CityDomesticCandidate a, CityDomesticCandidate b, bool currentLocation)
    { throw Missing(currentLocation ? "current_location_compare_composition" : "home_compare_composition"); }
    internal bool BeforeTaskTarget(CityDomesticCandidate a, CityDomesticCandidate b)
    { throw Missing("task_target_compare_composition"); }
    internal string? TaskTarget(CityDomesticCandidate c)
    {
        if (K(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, c.PhysicalSlot), "task_commander_army"))
            throw Missing("task_army_target_display");
        var linked = current.ReadPersonLinkedEntity(c.PhysicalSlot) ?? throw Missing("task_associated_entity");
        if (linked.Identity is not int identity) return "";
        var commands = current.ReadOwnedPersonCommandManager();
        if (commands?.Disappearances?.ContainsKey(identity) == true) return "";
        throw Missing("task_target_virtual_kind_or_target");
    }
    internal CityDomesticPersonTaskRemaining? TaskRemaining(CityDomesticCandidate c)
    {
        var linked = current.ReadPersonLinkedEntity(c.PhysicalSlot) ?? throw Missing("task_associated_entity");
        if (linked.Identity is not int identity) return null;
        if (current.ReadOwnedPersonCommandManager()?.Disappearances?.ContainsKey(identity) == true) return new(true, null);
        throw Missing("task_remaining_value");
    }
    internal SortValue TaskRemainingKey(CityDomesticCandidate c)
    {
        var linked = current.ReadPersonLinkedEntity(c.PhysicalSlot) ?? throw Missing("task_associated_entity");
        if (linked.Identity is null) return SortValue.Empty;
        throw Missing("task_remaining_value");
    }
}
