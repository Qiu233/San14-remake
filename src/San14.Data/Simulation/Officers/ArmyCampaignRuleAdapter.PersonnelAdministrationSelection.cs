using System.Collections.Immutable;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<PersonnelAdministrationOfficerSelection> PreparePersonnelRewardOfficerSelection(
        PersonnelRewardDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.UnresolvedOwner is { } missing)
            return AreaAdministrationQuery<PersonnelAdministrationOfficerSelection>.Unknown(missing);
        return PreparePersonnelAdministrationSelection(draft.SelectionSource, draft.ForceId,
            PersonnelAdministrationSelectionKind.Reward, draft.Rows.Select(row => row.OfficerId).ToArray(),
            draft.SelectedOfficerIds, draft.Budget.MaximumSelection,
            draft.Rows.Where(row => row.Eligible == true).Select(row => row.OfficerId).ToArray(),
            draft.Rows.Where(row => row.Eligible != true && row.UnresolvedOwner is not null)
                .ToDictionary(row => row.OfficerId, row => row.UnresolvedOwner!));
    }

    public AreaAdministrationQuery<PersonnelAdministrationOfficerSelection> PreparePersonnelBanishOfficerSelection(
        PersonnelBanishDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.UnresolvedOwner is { } missing)
            return AreaAdministrationQuery<PersonnelAdministrationOfficerSelection>.Unknown(missing);
        return PreparePersonnelAdministrationSelection(draft.SelectionSource, draft.ForceId,
            PersonnelAdministrationSelectionKind.Banish, draft.Rows.Select(row => row.OfficerId).ToArray(),
            draft.SelectedOfficerIds, draft.Rows.Count,
            draft.Rows.Where(row => row.Enabled == true).Select(row => row.OfficerId).ToArray(),
            draft.Rows.Where(row => row.Enabled != true && (row.UnresolvedOwner ?? row.DisabledReason) is not null)
                .ToDictionary(row => row.OfficerId, row => (row.UnresolvedOwner ?? row.DisabledReason)!));
    }

    public AreaAdministrationQuery<PersonnelAdministrationOfficerSelection> PreparePersonnelCreditOfficerSelection(
        PersonnelCreditDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.UnresolvedOwner is { } missing)
            return AreaAdministrationQuery<PersonnelAdministrationOfficerSelection>.Unknown(missing);
        //Credit rows are the real admitted rules.People projection, not a
        //generic all-Person list whose unknown eligibility is made true.
        var ids = draft.Rows.Select(row => row.OfficerId).ToArray();
        return PreparePersonnelAdministrationSelection(draft.SelectionSource, draft.ForceId,
            draft.Rankup ? PersonnelAdministrationSelectionKind.CreditRankup
                : PersonnelAdministrationSelectionKind.CreditGive,
            ids, draft.SelectedOfficerIds, draft.Budget.MaximumSelection, ids, new Dictionary<int, string>());
    }

    private AreaAdministrationQuery<PersonnelAdministrationOfficerSelection> PreparePersonnelAdministrationSelection(
        PersonnelAdministrationSelectionSource? source, int forceId, PersonnelAdministrationSelectionKind kind,
        IReadOnlyList<int> ids, IReadOnlyList<int> initial, int? maximum,
        IReadOnlyList<int> eligible, IReadOnlyDictionary<int, string> disabledReasons)
    {
        try
        {
            if (source is null) throw PersonnelPageMissing("selection_source");
            if (maximum is not int limit || limit < 0) throw PersonnelPageMissing("selection_maximum");
            var reader = new OfficerInformationCurrent(this, source.State, forceId,
                rowPaletteEnabled: false, retainedCurrent: source.Current);
            var pages = reader.AdministrationSelectorPages(ids, kind);
            //All four actual callers retain B0=-1. The existing draft rows
            //already carry their distinct native pre-dialog comparator order.
            pages.SortSelection(initial, selectedFirst: true);
            var messages = new NarrationResourceMessages(
                _repository.LoadMessages(Seed.Language, San14.Data.Formats.MessageKind.Game).Messages);
            string guide = kind == PersonnelAdministrationSelectionKind.Reward
                ? messages.ResolvePersonSelectorHint(ReadHistoryDataText(1, 0x211))
                : messages.ResolvePersonnelAdministrationSelectionGuide(kind switch
                {
                    PersonnelAdministrationSelectionKind.Banish => 0x5884,
                    PersonnelAdministrationSelectionKind.CreditGive => 0x5C55,
                    PersonnelAdministrationSelectionKind.CreditRankup => 0x5C4E,
                    _ => throw PersonnelPageMissing("selection_kind")
                });
            bool banish = kind == PersonnelAdministrationSelectionKind.Banish;
            return AreaAdministrationQuery<PersonnelAdministrationOfficerSelection>.Known(new(pages,
                initial.ToArray(), limit, banish ? 0x273 : 0x32E, guide, RequireSelection: banish)
            {
                Kind = kind, EligibleOfficerIds = eligible.ToArray(), DisabledReasons = disabledReasons,
                AlteredHealth = ids.ToDictionary(id => id, reader.MovementAlteredHealth)
            });
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<PersonnelAdministrationOfficerSelection>.Unknown(error.Message); }
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal OfficerInformationPageSet AdministrationSelectorPages(IReadOnlyList<int> ids,
            PersonnelAdministrationSelectionKind kind)
        {
            var common = OfficerInformationSources.Create(Data);
            var columns = common.SelectMany(group => group.Columns).GroupBy(column => column.Id)
                .ToDictionary(group => group.Key, group => group.First());
            //The actual182 descriptor is the same name presentation as0;
            //only its native-invalid sort branch differs (34D1A0).
            columns[182] = columns[0] with { Id = 182 };
            OfficerInformationGroup Group(int index, int caption, int[] sequence) =>
                new(index, caption, Data(1, caption), sequence.Select(id => columns[id]).ToImmutableArray());
            ImmutableArray<OfficerInformationGroup> groups = kind switch
            {
                PersonnelAdministrationSelectionKind.Reward =>
                    [Group(0, 0x3CD, [0,2,5,6,8,9,10,11,12,20])],
                PersonnelAdministrationSelectionKind.Banish => common,
                PersonnelAdministrationSelectionKind.CreditGive or PersonnelAdministrationSelectionKind.CreditRankup =>
                [
                    Group(0, 0x3CD, [0,24,194,193,3,8,9,10,11,12,196,197]),
                    common[0] with { Index = 1 },
                    Group(2, 0x22B, [182,40,41,42,43,44,45,46,47,48,49,50]),
                    Group(3, 0x241, [0,98,99,26,101]),
                    Group(4, 0x23D, [182,61,62,63,64,65])
                ],
                _ => throw Missing("administration.selection_kind")
            };
            var names = new Dictionary<int, PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (column != 182) return Cell(id, column);
                if (names.TryGetValue(id, out var saved)) return saved;
                var ordinary = Cell(id, 0);
                try
                {
                    saved = InformationNativeValid(ReadPerson(id)) ? ordinary
                        : new(ordinary.Display, new NameKey("", true), null);
                }
                catch (InvalidDataException error) { saved = new(ordinary.Display, null, error.Message); }
                return names[id] = saved;
            }
            OfficerInformationCell Display(int id, int column)
            {
                var cell = Read(id, column);
                //cfg178=0 gates StateAdvantage only. Keep the independent
                //credit/Force/Person presets, inline text and marker colour.
                return cell.Display with { StatePresets = null, SortUnresolvedOwner = cell.SortUnresolvedOwner };
            }
            return new(Data(1, kind == PersonnelAdministrationSelectionKind.Banish ? 0x273 : 0x32E),
                groups, ids, Display, (id, column) => Read(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Read(left, column).Key ?? throw Missing("sort_key"))
                    .Before(Read(right, column).Key ?? throw Missing("sort_key"), ascending))
                { DynamicPaletteEnabled = false };
        }
    }
}
