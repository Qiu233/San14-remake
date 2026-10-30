using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read the ordinary Search draft without repeating its advice,
    /// route calculation, pairing or App random edit event.</summary>
    public TalentSearchPersonSelection PrepareTalentSearchPersonSelection(TalentUiDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Recruitment) throw new ArgumentException("The Search chooser cannot consume a Recruit draft.", nameof(draft));
        var source = draft.Source;
        var prepared = (source.SelectionRecommendations
            ?? throw new InvalidOperationException("Search candidate preparation has not completed.")).ToDictionary(row => row.OfficerId,
            row => new TalentSearchSelectionPreview(row.Recommended, row.TravelDays, null));
        var ids = source.SelectionRecommendationOrder
            ?? throw new InvalidOperationException("Search candidate order has not been prepared.");
        var reader = new OfficerInformationCurrent(this, source.State, source.ForceId,
            rowPaletteEnabled: true, retainedCurrent: source.Current);
        return reader.TalentSearchPages(ids, draft.SelectedExecutorOfficerIds, draft.MaximumExecutors,
            prepared, automatic: false, PrepareCommanderSelectionGuide());
    }

    /// <summary>Consume the candidates produced by the caller's one explicit
    /// PrepareSelection event. This factory never invokes Recompute.</summary>
    public TalentSearchPersonSelection PrepareTalentAutoSearchPersonSelection(TalentAutoSearchDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.CanPrepareSelection) throw new InvalidOperationException(draft.UnresolvedInput ?? "Automatic Search has no prepared candidates.");
        var source = draft.Source.Page;
        var prepared = draft.Candidates.ToDictionary(row => row.OfficerId,
            row => new TalentSearchSelectionPreview(row.Recommended, row.TravelDays, row.CityId));
        var reader = new OfficerInformationCurrent(this, source.State, draft.Source.ForceId,
            rowPaletteEnabled: true, retainedCurrent: source.Current);
        return reader.TalentSearchPages(draft.Candidates.Select(row => row.OfficerId).ToArray(),
            draft.PreviewSelectedExecutorOfficerIds, draft.MaximumExecutors, prepared,
            automatic: true, PrepareCommanderSelectionGuide());
    }

    //716BA0's installed fast branch is shared by these two real callers.
    //The former Auto local bodies below are retained without numerical edits.
    private Func<GameState, int, AreaReference, bool> CreateTalentSearchFastAdvice(
        SourceWorldContext current, TacticRemakeCurrentAbilities abilities)
    {
        //716BA0 builds2D9750 once per Area before querying all executors.
        //The page's captured command manager and positions stay the owners.
        var hiddenByArea = new Dictionary<int, IReadOnlyList<int>>();
        var manager = current.ReadOwnedPersonCommandManager() ?? throw TalentUiQuerySource.Missing("auto_command_manager");
        var disappearances = manager.Disappearances ?? throw TalentUiQuerySource.Missing("auto_Disappear_manager");
        var excluded = new HashSet<int>();
        foreach (var entry in manager.DispatchOrder())
            if (entry.Kind == PersonnelCommandKind.Disappear && disappearances[entry.Id].MemberIds is { Length: > 0 } members)
            {
                int raw = members[0];
                var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                    ?? throw TalentUiQuerySource.Missing("auto_Disappear_first_member");
                excluded.Add(person.PhysicalSlot ?? throw TalentUiQuerySource.Missing("auto_Disappear_first_pointer"));
            }
        return FastAdvice;

        bool FastAdvice(GameState working, int actor, AreaReference area)
        {
            var force = current.ReadPersonForce(actor) ?? throw TalentUiQuerySource.Missing("auto_Force");
            int forcePhysical = current.ReadForcePhysicalSlot(force) ?? throw TalentUiQuerySource.Missing("auto_Force_pointer");
            ushort raw = current.ReadAssignmentForceAdvisor52(forcePhysical) ?? throw TalentUiQuerySource.Missing("auto_Force52");
            var advisor = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw TalentUiQuerySource.Missing("auto_advisor_lookup");
            if (advisor.PhysicalSlot is not int advisorPhysical || !(current.ReadPersonNativeValidity(advisorPhysical)
                ?? throw TalentUiQuerySource.Missing("auto_advisor_validity"))) return false;
            int accuracy = 0;
            if (current.ReadForceNativeValidity(force) ?? throw TalentUiQuerySource.Missing("auto_Force_validity"))
            {
                int id = current.ReadPersonIdentity(advisorPhysical) ?? throw TalentUiQuerySource.Missing("auto_advisor_identity");
                int first = abilities.ResolveDimension(working, id, 2, (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                    ?? throw TalentUiQuerySource.Missing("auto_advisor_INT");
                int second = abilities.ResolveDimension(working, id, 2, (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                    ?? throw TalentUiQuerySource.Missing("auto_advisor_INT");
                accuracy = Math.Clamp(unchecked(60 + Math.Max(0, first - 70) + Math.Max(0, second - 90)), 0, 100);
            }
            TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("auto_sampler");
            int actorId = current.ReadPersonIdentity(actor) ?? throw TalentUiQuerySource.Missing("auto_actor_identity");
            int areaPhysical = area.PhysicalSlot ?? throw TalentUiQuerySource.Missing("auto_Area_pointer");
            bool accurate = TacticEffectSourceSamplerMath.Sample(100, 10, actorId, areaPhysical, 0, 0, Sampler()) < accuracy;
            //Each edition's actual static switch is1. The real fast path
            //omits the full text formatter and its App-random wording draws.
            var people = TalentSearchSource.ResolvePerson(working, current, actor, area, Array.Empty<int>(), Sampler,
                abilities, -1, Hidden(areaPhysical));
            var receipt = people.Receipt ?? throw TalentUiQuerySource.Missing(people.UnresolvedOwner ?? "auto_Person_trial");
            bool found = receipt.FoundPersonPhysical is int foundPerson &&
                (current.ReadPersonNativeValidity(foundPerson) ?? throw TalentUiQuerySource.Missing("auto_found_Person_validity"));
            if (!found)
            {
                var scenery = TrySearchSceneryCurrent(working, current, actor, area, -1, Sampler);
                if (scenery.UnresolvedOwner is not null) throw new InvalidDataException(scenery.UnresolvedOwner);
                found = scenery.Receipt is not null;
                if (!found)
                {
                    var item = TrySearchItemCurrent(working, current, actor, area, -1, Sampler);
                    if (item.UnresolvedOwner is not null) throw new InvalidDataException(item.UnresolvedOwner);
                    found = item.Receipt is not null;
                }
            }
            return found == accurate;
        }

        IReadOnlyList<int> Hidden(int area)
        {
            if (hiddenByArea.TryGetValue(area, out var known)) return known;
            var people = new List<int>();
            for (int slot = 0; slot < 1851; slot++)
            {
                var person = current.ReadRegistryTableReference(RegistryKind.Person, slot) ?? throw TalentUiQuerySource.Missing("auto_hidden_Person_table");
                int p = person.PhysicalSlot ?? throw TalentUiQuerySource.Missing("auto_hidden_Person_pointer");
                if ((current.ReadPersonStatus(p) ?? throw TalentUiQuerySource.Missing("auto_hidden_PersonE6")) != 8) continue;
                var location = PersonPositionRepair.DecodeSecondLocation(current.ReadPersonSecondLocation(p)
                    ?? throw TalentUiQuerySource.Missing("auto_hidden_PersonE4"));
                int? at = location.Kind switch
                {
                    10 => location.Index,
                    5 => BaseArea(current.ReadCityTableReference(location.Index <= 51 ? location.Index : 0)
                        ?? throw TalentUiQuerySource.Missing("auto_hidden_City")),
                    6 => BaseArea(current.ReadGateTableReference(location.Index <= 10 ? location.Index : 0)
                        ?? throw TalentUiQuerySource.Missing("auto_hidden_Gate")),
                    _ => null
                };
                if (at == area && !excluded.Contains(p)) people.Add(p);
            }
            hiddenByArea.Add(area, people.AsReadOnly()); return hiddenByArea[area];
        }
        int BaseArea(BaseReference receiver)
        {
            var hex = CurrentGateMapQueries.FindHex(receiver, current);
            return hex.PhysicalSlot is null ? 0 : current.ReadHexAreaSlot(hex)
                ?? throw TalentUiQuerySource.Missing("auto_hidden_Base_Hex12");
        }
    }

    private sealed record TalentSearchSelectionPreview(bool Recommended, int TravelDays, int? CityId);

    private sealed partial class OfficerInformationCurrent
    {
        internal TalentSearchPersonSelection TalentSearchPages(IReadOnlyList<int> ids,
            IReadOnlyList<int> initial, int maximum,
            IReadOnlyDictionary<int, TalentSearchSelectionPreview>? previews,
            bool automatic, string guide)
        {
            // The two actual Search callers enable the same ordinary body
            // palette and altered-health callback. Only their columns differ.
            commandBodyPalette = true;
            int[] sequence = automatic
                ? [0,75,3,5,35,11,103,104,128,129,102,142,18,194]
                : [0,75,3,5,35,11,103,104,128,129,102,18,194];
            var common = OfficerInformationSources.Create(Data).SelectMany(group => group.Columns)
                .GroupBy(column => column.Id).ToDictionary(group => group.Key, group => group.First());
            void Add(int id, int caption, int width, int format, bool ascending)
                => common[id] = new(id, caption, Data(1, caption), width, format, ascending, true, true);
            Add(75, 0x44D, 132, 4, false);
            Add(103, 0x51B, 76, 4, false);
            Add(104, 0x51A, 76, 4, false);
            Add(128, 0x81A, 76, 4, false);
            Add(129, 0x81D, 76, 4, false);
            Add(102, 0x26C, 76, 5, true);
            Add(142, 0x474, 132, 4, true);
            ImmutableArray<OfficerInformationGroup> groups =
                [new(0, 0x3CD, Data(1, 0x3CD), sequence.Select(id => common[id]).ToImmutableArray())];
            var cache = new Dictionary<(int Person, int Column), PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (cache.TryGetValue((id, column), out var saved)) return saved;
                try
                {
                    var person = ReadPerson(id);
                    TalentSearchSelectionPreview Preview() => previews?.GetValueOrDefault(id)
                        ?? throw Missing("talent_search.prepared_candidate");
                    bool Positive() => Preview().Recommended;
                    bool Trait(int requested)
                    {
                        for (int slot = 0; slot < 9; slot++)
                            if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,
                                person.PhysicalSlot, slot), "talent_search.trait_slot") == requested) return true;
                        return false;
                    }
                    int CityPhysical()
                    {
                        int cityId = Preview().CityId ?? throw Missing("talent_search.destination_City");
                        var city = BaseLookup.Find(cityId, current);
                        if (city.Kind != BaseKind.City) throw Missing("talent_search.destination_City_kind");
                        return Need(city.PhysicalSlot, "talent_search.destination_City_pointer");
                    }
                    string CityText()
                    {
                        //366800 consumes the assigned City's current name12.
                        int id = Need(current.ReadBaseObjectLink(new BaseReference(BaseKind.City, CityPhysical())),
                            "talent_search.destination_City_identity");
                        return state.Cities.GetValueOrDefault(id)?.Name ?? throw Missing("talent_search.destination_City_name");
                    }
                    saved = column switch
                    {
                        75 => MakeCell(() => Positive() ? Data(1, 0x338) : "", () => new NumberKey(Positive() ? 1 : 0)),
                        103 or 104 or 128 or 129 => TraitCell(column switch {103 => 0x78, 104 => 0x77, 128 => 0xCE, _ => 0xC8}),
                        102 => MakeCell(() => Number(Preview().TravelDays),
                            () => new NumberKey(InformationNativeValid(person) ? Preview().TravelDays : int.MaxValue),
                            statePalette: () => BasicRowPalette(person, 5)),
                        142 => MakeCell(CityText, () => new NameKey(rules.ReadCityDetailNameSortKeyCurrent(current, CityPhysical()))),
                        _ => Cell(id, column)
                    };
                    PreparedCell TraitCell(int requested) => MakeCell(() => Trait(requested) ? Data(1, 0x338) : "",
                        () => new NumberKey(Trait(requested) ? 1 : 0));
                }
                catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
                return cache[(id, column)] = saved;
            }
            var pages = new OfficerInformationPageSet(Data(1, 0x58F), groups, ids,
                (id, column) => Read(id, column).Display with { SortUnresolvedOwner = Read(id, column).SortUnresolvedOwner },
                (id, column) => Read(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Read(left, column).Key ?? throw Missing("talent_search.sort_key"))
                    .Before(Read(right, column).Key ?? throw Missing("talent_search.sort_key"), ascending))
                { DynamicPaletteEnabled = true };
            // B0=-1: retain the prepared caller order within checked groups.
            pages.SortSelection(initial, selectedFirst: true);
            return new(pages, initial.ToArray(), maximum, 0x58F, guide, automatic)
            {
                AlteredHealth = ids.ToDictionary(id => id, MovementAlteredHealth)
            };
        }
    }
}
