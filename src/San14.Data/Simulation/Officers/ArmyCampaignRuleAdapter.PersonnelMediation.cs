using San14.Core;

namespace San14.Data;

public sealed record PersonnelMediationCandidate(int OfficerId, int Physical, string Name,
    bool? Enabled, string? DisabledReason, string? UnresolvedOwner);
public sealed record PersonnelMediationView(int DistrictId,
    IReadOnlyList<IReadOnlyList<PersonnelMediationCandidate>> CandidatesBySlot,
    IReadOnlyList<int> SelectedOfficerIds, bool? IsMarriage,
    int? GoldBefore, int? GoldAfter, int? GoldCost,
    int? BooksBefore, int? BooksAfter, int? BookCost, bool? CanSubmit, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string MediationGoldKey = "simulation.personnel-mediation.gold-cost";
    private const string MediationBooksKey = "simulation.personnel-mediation.book-cost";
    private const string MediationServiceKey = "simulation.personnel-mediation.minimum-service-years";
    private const string MediationAgeKey = "simulation.personnel-mediation.marriage-age-threshold";
    //TC file-backed49588/4957C/495A0/49D50, read offline inmediation-static63.json.
    //Saved overrides have precedence; these are immutable rules, not BSS observations.
    private static readonly IReadOnlyDictionary<string, int> MediationDefaults = new Dictionary<string, int>
        { [MediationGoldKey] = 10000, [MediationBooksKey] = 1, [MediationServiceKey] = 1, [MediationAgeKey] = 18 };

    internal static GameState InstallFreshPersonnelMediationParameters(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw MediationMissing("parameters");
        if (native.Parameters.Language != "TC") return state;
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in MediationDefaults) values.TryAdd(pair.Key, pair.Value);
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }

    public AreaAdministrationQuery<PersonnelMediationView> QueryPersonnelMediation(GameState state,
        int forceId, int districtId, IReadOnlyList<int> selectedIds)
    {
        try { return QueryPersonnelMediationCurrent(state, CaptureOwnedArmyReturnWorld(state).Context, forceId, districtId, selectedIds); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<PersonnelMediationView>.Unknown(error.Message); }
    }

    internal AreaAdministrationQuery<PersonnelMediationView> QueryPersonnelMediationCurrent(GameState state,
        SourceWorldContext current, int forceId, int districtId, IReadOnlyList<int> selectedIds)
    {
        try
        {
            if (selectedIds.Count > 3) throw MediationMissing("selection_slots");
            int[] selected = new int[3];
            for (int i = 0; i < selectedIds.Count; i++) selected[i] = selectedIds[i];
            var source = ReadPersonnelRewardSourceCurrent(state, current, districtId);
            if (source.ForceId != forceId) throw MediationMissing("District_Force_control");
            var all = new List<(int Id, int Physical, string Name)>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw MediationMissing("Person_manager100"))
            {
                int person = MediationNeed(entry, "Person_registry_pointer");
                if (MediationNeed(current.ReadPersonOldForceSlot(person), "candidate_Force") != forceId ||
                    MediationNeed(current.ReadPersonStatus(person), "candidate_status") is < 1 or > 4) continue;
                int id = MediationNeed(current.ReadPersonIdentity(person), "candidate_identity");
                //6D9770's actual three identity bands, before710700's disabled list.
                if (!(id is >= 1 and <= 1000 or >= 3001 and <= 3150 or >= 2001 and <= 2088)) continue;
                var owner = state.Officers.GetValueOrDefault(id) ?? throw MediationMissing("candidate_domain_owner");
                all.Add((id, person, owner.Name));
            }
            all.Sort((a, b) => a.Id.CompareTo(b.Id));
            var rows = new List<IReadOnlyList<PersonnelMediationCandidate>>(3);
            for (int slot = 0; slot < 3; slot++)
            {
                var choices = new List<PersonnelMediationCandidate>(all.Count);
                foreach (var person in all)
                {
                    try
                    {
                        bool enabled = CanSelectMediationCurrent(state, current, person.Physical, slot, selected);
                        if (slot == 2 && selected[0] != 0 && selected[1] != 0 && Sex(selected[0]) != Sex(selected[1])) enabled = false;
                        choices.Add(new(person.Id, person.Physical, person.Name, enabled,
                            enabled ? null : "personnel-mediation.person_ineligible", null));
                    }
                    catch (InvalidDataException error) { choices.Add(new(person.Id, person.Physical, person.Name, null, null, error.Message)); }
                }
                rows.Add(choices.AsReadOnly());
            }
            int gold = BaseCurrencyAdditionSource.ReadCurrentGold(source.Home, current);
            int cost = MediationParameter(state, MediationGoldKey);
            int booksCost = MediationParameter(state, MediationBooksKey);
            if (cost < 0 || booksCost < 0) throw MediationMissing("negative_cost");
            var district = state.Districts.GetValueOrDefault(districtId) ?? throw MediationMissing("District_owner");
            int books = district.OrdersRemaining ?? throw MediationMissing("District14");
            int cap = BasePostLoad.ReadResourceCapacities(BaseCurrencyAdditionSource.CurrencyCity(source.Home, current), current).Gold;
            int afterGold = Math.Clamp(unchecked(gold - cost), 0, cap);
            int afterBooks = Math.Clamp(unchecked(books - booksCost), 0, 200);
            int count = selected.Count(id => id != 0);
            bool? marriage = selected[0] != 0 && selected[1] != 0 ? Sex(selected[0]) != Sex(selected[1]) : null;
            bool canSubmit = count is 2 or 3 && selected[0] != 0 && selected[1] != 0 &&
                (count != 3 || marriage == false) && selected.Where(id => id != 0).Distinct().Count() == count &&
                gold >= cost && books - CommandBookAvailability.Reserved(state, districtId) >= booksCost;
            string? unresolved = null;
            for (int slot = 0; slot < 3 && canSubmit; slot++)
            {
                if (selected[slot] == 0) continue;
                var row = rows[slot].FirstOrDefault(candidate => candidate.OfficerId == selected[slot]);
                if (row?.Enabled is null && row is not null) { unresolved = row.UnresolvedOwner; canSubmit = false; }
                else if (row?.Enabled != true) canSubmit = false;
            }
            return AreaAdministrationQuery<PersonnelMediationView>.Known(new(districtId, rows.AsReadOnly(),
                Array.AsReadOnly(selected), marriage, gold, afterGold, cost, books, afterBooks, booksCost,
                unresolved is null ? canSubmit : null, unresolved));

            OfficerSex Sex(int id) => MediationNeed(state.Officers.GetValueOrDefault(id)?.Sex, "selected_A9");
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<PersonnelMediationView>.Unknown(error.Message); }
    }

    //710700: the selected slot is replaced locally; the other two slots remain
    //real inputs. There is no busy/Army/task exclusion in this native predicate.
    private bool CanSelectMediationCurrent(GameState state, SourceWorldContext current,
        int person, int slot, IReadOnlyList<int> selected)
    {
        if (MediationRelation(state, person, 0xBA) != 0 && MediationRelation(state, person, 0xBC) != 0) return false;
        int ruler = TalentRulerCurrent(current, current.ReadPersonForce(person) ?? throw MediationMissing("candidate_Force_pointer"));
        int id = MediationNeed(current.ReadPersonIdentity(person), "candidate_ID");
        int loyalty = Math.Min(100, (int)MediationNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "candidate_E8"));
        if (loyalty < 100 && id != ruler) return false;
        int year = MediationNeed(current.ReadCurrentYear(), "World34_year");
        int joined = MediationNeed(_governorInputs.ReadCurrentPhysicalWord166(state, person), "candidate_166");
        if (year - joined < MediationParameter(state, MediationServiceKey) && id != ruler) return false;
        int[] others = Enumerable.Range(0, 3).Where(index => index != slot && selected[index] != 0)
            .Select(index => current.ResolveOfficerSource(selected[index]) ?? throw MediationMissing("selected_Person_pointer")).ToArray();
        return others.Length == 0 || MediationCompatibleCurrent(state, current, person, others[0], others.Length == 2 ? others[1] : null);
    }

    //713680 checks the directed native kinship pointer predicate and both
    //raw dislike lists. It does not substitute the aggregate relation score.
    private static bool MediationCompatibleCurrent(GameState state, SourceWorldContext current, int person, int other, int? third)
    {
        int Id(int p) => MediationNeed(current.ReadPersonIdentity(p), "pair_ID");
        int id = Id(person), otherId = Id(other);
        if (id == otherId || third is int same && id == Id(same)) return false;
        if (Kin(person, other) || Kin(other, person)) return false;
        if (third is int last && (Kin(person, last) || Kin(last, person))) return false;
        if (OfficeRelationList(state, current, other, person, 0xD0) ||
            third is int dislike && OfficeRelationList(state, current, dislike, person, 0xD0)) return false;
        if (OfficeRelationList(state, current, person, other, 0xD0) ||
            third is int hated && OfficeRelationList(state, current, person, hated, 0xD0)) return false;
        int sex = Sex(person), otherSex = Sex(other);
        if (third.HasValue) return sex == otherSex && MediationRelation(state, person, 0xBC) == 0;
        if (sex != otherSex && MediationRelation(state, person, 0xBA) != 0 ||
            sex == otherSex && MediationRelation(state, person, 0xBC) != 0) return false;
        int threshold = MediationParameter(state, MediationAgeKey);
        int age = MediationAge(state, current, other);
        if (age >= threshold && sex != otherSex && MediationAge(state, current, person) < threshold) return false;
        if (MediationAge(state, current, other) >= threshold) return true;
        return sex == otherSex;

        int Sex(int p)
        {
            var owner = state.Officers.GetValueOrDefault(Id(p)) ?? throw MediationMissing("pair_owner");
            return (int)MediationNeed(owner.Sex, "pair_A9");
        }
        bool Kin(int a, int b)
        {
            if (MediationRelation(state, a, 0xB2) == MediationRelation(state, b, 0xB2)) return true;
            foreach (int field in new[] { 0xBA, 0xB4, 0xB6 })
            {
                ushort raw = MediationRelation(state, a, field);
                var reference = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                    ?? throw MediationMissing("kin_Person_pointer");
                if (reference.PhysicalSlot == b) return true;
                if (field == 0xBA)
                {
                    ushort sworn = MediationRelation(state, a, 0xBC);
                    if (sworn is >= 1 and <= 6000 && sworn == MediationRelation(state, b, 0xBC)) return true;
                }
            }
            return false;
        }
    }

    internal int[] ReadMediationOrderedOfficerIds(GameState state, SourceWorldContext current, IReadOnlyList<int> ids)
    {
        if (ids.Count is < 2 or > 3 || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count)
            throw MediationMissing("ordering_selection");
        var ordered = ids.ToList();
        int Physical(int id) => current.ResolveOfficerSource(id) ?? throw MediationMissing("ordering_Person");
        int ruler = TalentRulerCurrent(current, current.ReadPersonForce(Physical(ids[0])) ?? throw MediationMissing("ordering_Force"));
        bool marriage = MediationNeed(state.Officers[ids[0]].Sex, "ordering_A9") != MediationNeed(state.Officers[ids[1]].Sex, "ordering_A9");
        if (marriage)
        {
            if (MediationNeed(state.Officers[ordered[0]].Sex, "ordering_A9") == OfficerSex.Female) ordered.Reverse();
            return ordered.ToArray();
        }
        ordered.Sort((a, b) =>
        {
            if (a == ruler) return b == ruler ? 0 : -1;
            if (b == ruler) return 1;
            int first = MediationAge(state, current, Physical(a)), second = MediationAge(state, current, Physical(b));
            if (second < first) return -1;
            first = MediationAge(state, current, Physical(a)); second = MediationAge(state, current, Physical(b));
            return first == second ? a.CompareTo(b) : 1;
        });
        return ordered.ToArray();
    }

    internal CommandResult ApplyPersonnelMediation(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> officerIds, CommandContext context)
    {
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var query = QueryPersonnelMediationCurrent(state, current, force.Id, districtId, officerIds);
            var quote = query.Value ?? throw MediationMissing(query.UnresolvedInput ?? "query");
            if (quote.CanSubmit is null) throw MediationMissing(quote.UnresolvedOwner ?? "qualification");
            if (quote.CanSubmit != true) return CommandResult.Reject("The selected officers or budget cannot be mediated.", "personnel-mediation.qualification");
            var ordered = ReadMediationOrderedOfficerIds(state, current, officerIds);
            var source = ReadPersonnelRewardSourceCurrent(state, current, districtId);
            var working = BaseCurrencyAdditionSource.Add(state, current, source.Home, -quote.GoldCost!.Value,
                city => BasePostLoad.ReadResourceCapacities(city, current).Gold).State;
            int books = quote.BooksAfter!.Value;
            current.SetDistrictRawByte(districtId, 0x14, (byte)books);
            var saved = working.NativeForceUnregisterCurrent ?? throw MediationMissing("District_saved_owner");
            var window = saved.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray() ?? throw MediationMissing("District_saved_window");
            window[4] = (byte)books;
            working = working with { Districts = new(working.Districts) { [districtId] = working.Districts[districtId] with { OrdersRemaining = books } },
                NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10) { [districtId] = window } } };
            var native = working.NativeArmySimulation ?? throw MediationMissing("relation_owner");
            var values = new Dictionary<string, int>(native.Parameters.Int32Values);
            bool marriage = quote.IsMarriage!.Value;
            foreach (int id in ordered)
            {
                int person = current.ResolveOfficerSource(id) ?? throw MediationMissing("write_Person");
                int value = marriage ? ordered[0] == id ? ordered[1] : ordered[0] : ordered[0];
                values[OwnedPersonRelationInputs.PersonFieldKey(person, marriage ? 0xBA : 0xBC)] = Math.Min(value, 6000);
            }
            working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
            _personRelationInputs.BindCurrent(() => working, current);
            var effects = new List<CampaignEffectRequest>();
            working = ApplyPersonnelMediationReportsCurrent(working, current, force.Id, ordered, marriage, effects, state);
            var pending = working.CampaignEffectRequests ?? throw MediationMissing("pending_owner");
            return CommandResult.Accept(working with { CampaignEffectRequests = pending.Append(CampaignEffectSource.PersonnelAdministrationCommands,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw MediationMissing("calendar_owner"), effects) });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private static ushort MediationRelation(GameState state, int person, int field) =>
        MediationNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "relation_" + field.ToString("X"));
    private static int MediationAge(GameState state, SourceWorldContext current, int person) =>
        PersonCurrentAgeSource.Read(state, current, person);
    private static int MediationParameter(GameState state, string key)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw MediationMissing("parameters");
        if (bank.Language != "TC" || bank.RuleVersion != SimulationRuleIdentity.ForLocale("TC") || bank.Float32Values.ContainsKey(key))
            throw MediationMissing(key);
        return bank.Int32Values.TryGetValue(key, out int value) ? value : MediationDefaults[key];
    }
    private static T MediationNeed<T>(T? value, string field) where T : struct => value ?? throw MediationMissing(field);
    private static InvalidDataException MediationMissing(string field) => new("personnel-mediation." + field);
}
