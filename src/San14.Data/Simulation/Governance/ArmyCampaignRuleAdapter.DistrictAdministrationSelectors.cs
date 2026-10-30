using System.Collections.Immutable;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<DistrictLeaderSelection> PrepareDistrictLeaderSelection(DistrictAdministrationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            if (draft.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
            var source = draft.SelectionSource ?? throw DistrictPageMissing("selection_source");
            var reader = new OfficerInformationCurrent(this, source.State, draft.ForceId,
                rowPaletteEnabled: false, retainedCurrent: source.Current);
            //740740's admitted chain is already owned by this draft. Opening
            //6A1B80 uses stable descending sum of five effective abilities.
            var ids = reader.DistrictLeaderOrder(draft.Leaders.Select(row => row.OfficerId).ToArray());
            int? initial = draft.LeaderId;
            if (initial is int old)
            {
                int physical = source.Current.ResolveOfficerSource(old) ?? throw DistrictPageMissing("selection_old_leader");
                if (!(source.Current.ReadPersonNativeValidity(physical) ?? throw DistrictPageMissing("selection_old_leader_validity")))
                    initial = null;
            }
            var pages = reader.DistrictLeaderPages(ids);
            pages.SortSelection(initial is int leader ? [leader] : [], selectedFirst: true);
            var messages = DistrictAdministrationMessages();
            return AreaAdministrationQuery<DistrictLeaderSelection>.Known(new(pages, initial,
                messages.ResolvePersonSelectorHint(ReadHistoryDataText(1, 0x211)))
            {
                EligibleOfficerIds = ids,
                AlteredHealth = ids.Distinct().ToDictionary(id => id, reader.MovementAlteredHealth)
            });
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DistrictLeaderSelection>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<DistrictCitySelection> PrepareDistrictCitySelection(DistrictAdministrationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            if (draft.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
            var source = draft.SelectionSource ?? throw DistrictPageMissing("selection_source");
            var reader = new CityInformationCurrent(this, source.State, draft.ForceId, source.Current);
            var pages = reader.DistrictCityPages(draft.Cities.Select(row => row.Id).ToArray());
            var initial = draft.SelectedCityIds.ToArray();
            pages.SortSelection(initial, selectedFirst: true);
            return AreaAdministrationQuery<DistrictCitySelection>.Known(new(pages, initial,
                draft.Cities.Where(row => row.Available == true).Select(row => row.Id).ToArray(),
                draft.LockedSelectedCityIds.ToArray(), DistrictAdministrationMessages()
                    .ResolveDistrictAdministrationGuide(draft.Reorganize ? 0x593F : 0x590E))
            { ReadAdmission = draft.QueryCityListSelection });
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DistrictCitySelection>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<string> ResolveDistrictAdministrationGuide(int root)
    {
        try { return AreaAdministrationQuery<string>.Known(DistrictAdministrationMessages().ResolveDistrictAdministrationGuide(root)); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }

    private NarrationResourceMessages DistrictAdministrationMessages() => new(
        _repository.LoadMessages(Seed.Language, San14.Data.Formats.MessageKind.Game).Messages);

    private sealed partial class OfficerInformationCurrent
    {
        internal int[] DistrictLeaderOrder(IReadOnlyList<int> ids)
        {
            return ids.Select(id =>
            {
                var person = ReadPerson(id); int total = 0;
                for (int dimension = 0; dimension < 5; dimension++)
                    total = unchecked(total + InformationAbility(person, dimension));
                return (Id: id, Total: total);
            }).OrderByDescending(row => row.Total).Select(row => row.Id).ToArray();
        }

        internal OfficerInformationPageSet DistrictLeaderPages(IReadOnlyList<int> ids)
        {
            var columns = OfficerInformationSources.Create(Data).SelectMany(group => group.Columns)
                .GroupBy(column => column.Id).ToDictionary(group => group.Key, group => group.First());
            int[][] sequences = [[0,3,21,8,9,10,11,12,6,194,28,29], [0,61,62,63,64,65], [0,29,28,24,30,31,32,33]];
            int[] captions = [0x3CD,0x23D,0x451];
            var groups = sequences.Select((sequence, index) => new OfficerInformationGroup(index,
                captions[index], Data(1, captions[index]), sequence.Select(id => columns[id]).ToImmutableArray())).ToImmutableArray();
            OfficerInformationCell Display(int id, int column)
            {
                var cell = Cell(id, column);
                //cfg178=0 gates slot56 StateAdvantage. Independent column
                //callbacks and callback2 health remain distinct consumers.
                return cell.Display with { StatePresets = null, SortUnresolvedOwner = cell.SortUnresolvedOwner };
            }
            return new(Data(1,0x32E), groups, ids, Display,
                (id, column) => Cell(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Cell(left, column).Key ?? throw Missing("sort_key"))
                    .Before(Cell(right, column).Key ?? throw Missing("sort_key"), ascending))
                { DynamicPaletteEnabled = false };
        }
    }
}
