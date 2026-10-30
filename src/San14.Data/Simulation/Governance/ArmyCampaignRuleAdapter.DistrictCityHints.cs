using San14.Core;

namespace San14.Data;

public enum DistrictCityHintKind { Default, City, Decide, CloseList, SelectAll, CancelStage }

public sealed record DistrictCityHints(bool Reorganize, int? RulerCityId, string GuideText)
{
    internal Func<DistrictCityHintKind, int?, IReadOnlyList<int>, AreaAdministrationQuery<string>>? ReadHint { private get; init; }
    public AreaAdministrationQuery<string> QueryHint(DistrictCityHintKind kind, int? cityId, IReadOnlyList<int> localIds)
    {
        ArgumentNullException.ThrowIfNull(localIds);
        return (ReadHint ?? throw new InvalidOperationException("district.City_hint_owner"))(kind, cityId, localIds);
    }
}

public sealed partial class DistrictAdministrationDraft
{
    /// <summary>7AA7E0/7A91B0 accept only a native-valid returned Person.
    /// An invalid return leaves the previous leader and settings intact.</summary>
    public bool SelectLeaderFromList(int officerId)
    {
        try
        {
            var source = SelectionSource ?? throw new InvalidDataException("district.selection_source");
            int physical = source.Current.ResolveOfficerSource(officerId)
                ?? throw new InvalidDataException("district.selected_leader_Person_pointer");
            if (!(source.Current.ReadPersonNativeValidity(physical)
                ?? throw new InvalidDataException("district.selected_leader_Person_validity"))) return false;
            return SelectLeader(officerId);
        }
        catch (InvalidDataException error) { LastError = error.Message; return false; }
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<DistrictCityHints> PrepareDistrictCityHints(DistrictAdministrationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            var source = draft.SelectionSource ?? throw DistrictPageMissing("City_hint_source");
            var current = source.Current;
            var player = current.ReadCurrentPlayerForce() ?? throw DistrictPageMissing("City_hint_current_player_Force");
            bool playerValid = DistrictPageNeed(current.ReadForceNativeValidity(player), "City_hint_player_validity");
            int? rulerCity = playerValid ? DistrictAdministrationRulerCity(current, player) : null;
            bool editing = draft.Reorganize, primary = draft.SelectedDistrict?.IsPrimary == true;
            int defaultRoot = editing ? 0x593F : 0x590E;
            var messages = DistrictAdministrationMessages();
            var text = new Dictionary<int,string>();
            string Read(int root)
            {
                if (!text.TryGetValue(root, out string? value))
                    text.Add(root, value = messages.ResolveDistrictAdministrationGuide(root));
                return value;
            }

            //234920/3346B0/232F10 mode0: one pass over the captured Person
            //registry, preserving the actual status1..4 membership predicate.
            //2304B0 does not add a native-valid gate;70F200 explicitly does.
            var peopleHomes = new HashSet<int>();
            var candidateHomes = new HashSet<int>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw DistrictPageMissing("City_hint_Person_registry"))
            {
                int person = DistrictPageNeed(entry, "City_hint_Person_pointer");
                int home = DistrictPageNeed(current.ReadPersonFirstLocation(person), "City_hint_Person_homeE2");
                if (home is < 1 or > 61) continue;
                if (DistrictPageNeed(current.ReadPersonStatus(person), "City_hint_Person_statusE6") is < 1 or > 4) continue;
                peopleHomes.Add(home);
                if (DistrictPageNeed(current.ReadPersonNativeValidity(person), "City_hint_Person_validity") &&
                    !DuelAftermathSpecialPerson(current, person) &&
                    (DistrictPageNeed(current.ReadArmyReturnPersonFlags196(person), "City_hint_Person_flags15E") & 0x4000) == 0)
                    candidateHomes.Add(home);
            }

            //22D110 associates Gate -> parent City without a Force filter.
            //Do not substitute the engine leader-candidate helper's distinct
            //same-Force Gate scope for this source UI predicate.
            var gatePeopleCities = new HashSet<BaseReference>();
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw DistrictPageMissing("City_hint_Gate_registry"))
            {
                var gate = entry ?? throw DistrictPageMissing("City_hint_Gate_pointer");
                int home = DistrictPageNeed(current.ReadBaseObjectLink(gate), "City_hint_Gate4E");
                if (peopleHomes.Contains(home)) gatePeopleCities.Add(CurrentGateMapQueries.FindCity(gate, current));
            }

            var facts = new Dictionary<int,DistrictCityHintFacts>();
            foreach (var row in draft.Cities)
            {
                var city = current.ReadCityTableReference(row.Id) ?? throw DistrictPageMissing("City_hint_City_pointer");
                bool valid = DistrictPageNeed(current.ReadBaseNativeValidity(city), "City_hint_City_validity");
                int raw = DistrictPageNeed(current.ReadBaseForceSlot(city), "City_hint_City_Force");
                var owner = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0)
                    ?? throw DistrictPageMissing("City_hint_Force_pointer");
                int home = DistrictPageNeed(current.ReadBaseObjectLink(city), "City_hint_City4E");
                facts.Add(row.Id, new(valid, playerValid && owner == player,
                    rulerCity == row.Id, peopleHomes.Contains(home) || gatePeopleCities.Contains(city),
                    valid && candidateHomes.Contains(home)));
            }

            AreaAdministrationQuery<string> Query(DistrictCityHintKind kind, int? cityId, IReadOnlyList<int> selected)
            {
                try
                {
                    if (selected.Distinct().Count() != selected.Count || selected.Any(id => !facts.ContainsKey(id)))
                        throw DistrictPageMissing("City_hint_checked_membership");
                    int root = kind switch
                    {
                        DistrictCityHintKind.Default => defaultRoot,
                        DistrictCityHintKind.City => cityId is int id && facts.TryGetValue(id, out var city) && city.Valid
                            ? !city.Own ? 0x5940 : city.Ruler ? editing ? 0x5941 : 0x590F
                                : selected.Contains(id) ? editing ? 0x5943 : 0x5912 : editing ? 0x5942 : 0x5911
                            : defaultRoot,
                        DistrictCityHintKind.Decide => !selected.Any(id => facts[id].HasPeople) ? 0x5910
                            : !selected.Any(id => facts[id].HasCandidate) ? 0x591C
                            : editing && primary ? 0x594B : 0x594D,
                        DistrictCityHintKind.CloseList => 0x5950,
                        DistrictCityHintKind.CancelStage => editing ? 0x5945 : 0x2733,
                        DistrictCityHintKind.SelectAll => selected.Count == 0 ||
                            selected.Count == 1 && primary && facts[selected[0]].Ruler ? 0x594F : 0x5944,
                        _ => throw DistrictPageMissing("City_hint_kind")
                    };
                    return AreaAdministrationQuery<string>.Known(Read(root));
                }
                catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
            }
            return AreaAdministrationQuery<DistrictCityHints>.Known(new(editing,rulerCity,Read(defaultRoot)) { ReadHint = Query });
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DistrictCityHints>.Unknown(error.Message); }
    }

    private sealed record DistrictCityHintFacts(bool Valid, bool Own, bool Ruler, bool HasPeople, bool HasCandidate);
}
