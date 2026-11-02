using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>534CF0 footer18's complete current ForcePersonIterator(mode0)
    /// plus its independent Person validity filter. The one normal capture is
    /// shared by every row, sort key and portrait route. Keep the publication
    /// while browsing or opening an existing OfficerDetailsView.</summary>
    public NewGameForceOfficersPreview CaptureNewGameForceOfficers(GameState state, int forceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rows = new List<NewGameForceOfficerPreview>();
        var issues = new List<string>();
        bool? forceValid = null; bool complete = false;
        string? sortIssue = null;
        IReadOnlyList<NewGameForceOfficerPreview>? sorted = null;
        SourceWorldContext? captured = null;
        NewGameOfficerAbilityProducer? abilityProducer = null;
        if (!ArmyOperationContext.IsNewGamePreview(state))
        { issues.Add("new-game.officers.normal-preview-owner"); return Publish(); }
        if (forceId is < 1 or > 51)
        { forceValid = false; complete = true; sorted = []; return Publish(); }
        try
        {
            var current = captured = CapturePersonPresentationCurrent(state);
            forceValid = current.ReadForceNativeValidity(new(forceId));
            if (forceValid != true)
            {
                if (forceValid is null) issues.Add("new-game.officers.force-validity");
                else { complete = true; sorted = []; }
                return Publish();
            }
            var chain = current.ReadRegisteredPersonPhysicalSlots()
                ?? throw new InvalidDataException("new-game.officers.current-Person-chain");
            abilityProducer = CreateNewGameOfficerAbilities(state,current);
            var offices = _scenario.Snapshot.RankParameters?.Offices.ToDictionary(row => row.Id);
            var naming = ArmyMapUiSources.Create();
            complete = true; int ordinal = 0;
            foreach (int? entry in chain)
            {
                int sourceOrdinal = ordinal++;
                if (entry is not int physical)
                { Incomplete("current-Person-chain-payload"); continue; }
                //334810: actual Force ID1..51, then22CF60 raw District owner,
                //then232F10(mode0) status1/2/3/4. No Core ForceId/Role filter.
                byte? affiliation = current.ReadPersonOldForceSlot(physical);
                if (affiliation is null) { Incomplete("person-force:" + physical); continue; }
                if (affiliation != forceId) continue;
                byte? status = current.ReadPersonStatus(physical);
                if (status is null) { Incomplete("person-status:" + physical); continue; }
                if (status is not (1 or 2 or 3 or 4)) continue;
                bool? valid = current.ReadPersonNativeValidity(physical);
                if (valid is null) { Incomplete("person-validity:" + physical); continue; }
                if (valid == false) continue;
                int? id = current.ReadPersonIdentity(physical);
                var person = id is int actual ? state.Officers.GetValueOrDefault(actual) : null;
                var missing = new Dictionary<string, string>();
                if (id is null) missing[nameof(NewGameForceOfficerPreview.OfficerId)] = "new-game.officers.person-identity";
                if (person is null) missing[nameof(NewGameForceOfficerPreview.Name)] = "new-game.officers.current-person-owner";
                int? officeId = id is int officePerson && person is not null ? abilityProducer.ReadOfficeId(officePerson) : null;
                var office = officeId is int officeKey ? offices?.GetValueOrDefault(officeKey) : null;
                if (office is null) missing[nameof(NewGameForceOfficerPreview.OfficeName)] = "new-game.officers.office-definition";
                var basic = NewGameOfficerAbilityFigures.From(person?.BaseAbilities);
                var abilityRead = id is int abilityPerson && person is not null ? abilityProducer.Resolve(abilityPerson)
                    : new NewGameOfficerAbilityRead(null,"new-game.ability.person-owner");
                var effective = NewGameOfficerAbilityFigures.From(abilityRead.Abilities);
                if (person?.BaseAbilities is null) missing[nameof(NewGameForceOfficerPreview.BaseAbilities)] = "new-game.officers.base-abilities";
                if (abilityRead.Abilities is null) missing[nameof(NewGameForceOfficerPreview.EffectiveAbilities)] = abilityRead.UnresolvedInput ?? "new-game.ability.inputs";
                ushort? merit = id is int creditPerson && person is not null ? abilityProducer.ReadStoredMeritWord(creditPerson) : null;
                if (merit is null) missing[nameof(NewGameForceOfficerPreview.StoredMeritWord)] = "new-game.officers.stored-merit-current-owner";
                int? location = current.ReadPersonFirstLocation(physical);
                string? locationName = location is int place ? state.Cities.GetValueOrDefault(place)?.Name
                    ?? state.Gates.GetValueOrDefault(place)?.Name : null;
                if (location is null) missing[nameof(NewGameForceOfficerPreview.LocationId)] = "new-game.officers.current-first-location";
                OfficerPortraitRoute? portrait = null;
                ushort? appearance = current.ReadPersonPortraitAppearance(physical);
                int? detail = null;
                if (id is int route && person is not null && _definitions.ResolveOfficerSource(route) == physical)
                {
                    detail = route;
                    if (appearance is ushort image)
                        portrait = ReadOfficerPortraitAppearance(state, current, _definitions, route, image, naming);
                }
                if (detail is null) missing[nameof(NewGameForceOfficerPreview.DetailOfficerId)] = "new-game.officers.current-detail-identity";
                if (portrait is null || portrait.Kind == OfficerPortraitRouteKind.Unresolved)
                    missing[nameof(NewGameForceOfficerPreview.PortraitRoute)] = portrait?.UnresolvedInput ?? "new-game.officers.current-portrait-owner";
                rows.Add(new(sourceOrdinal, physical, id, person?.Name, status, officeId, office?.Name,
                    office?.CommandLimit, current.ReadPersonDistrictSlot(physical), location, locationName,
                    basic, effective, merit, appearance, portrait, detail,
                    new ReadOnlyDictionary<string, string>(missing)));
            }
            if (complete) sorted = NewGameForceOfficerSort.Read(rows, out sortIssue);
            else sortIssue = "new-game.officers.sort.incomplete-membership";
        }
        catch (InvalidDataException error) { complete = false; issues.Add(error.Message); }
        return Publish();

        void Incomplete(string field) { complete = false; issues.Add("new-game.officers." + field); }
        NewGameForceOfficersPreview Publish()
        {
            OfficerInformationPageSet? pages = null;
            if (forceValid == true && captured is not null && rows.All(row => row.OfficerId is >= 1 and <= 6000))
            {
                //534CF0 supplies this exact iterator/order to the ordinary
                //pane14. Its two final optional arguments are zero: no
                //gameplay dynamic row palette is requested here.
                int[] ids = (sorted ?? rows).Select(row => row.OfficerId!.Value).ToArray();
                try
                {
                    pages = new OfficerInformationCurrent(this, state, forceId, rowPaletteEnabled: false,
                        retainedCurrent: captured, bindGameplayInputs: false, prestartAbilities: abilityProducer).Pages(ids);
                }
                catch (InvalidDataException error)
                {
                    // Preserve the original incomplete receipt and any rows
                    // already captured when a page input cannot be published.
                    complete = false;
                    issues.Add(error.Message);
                }
            }
            return new(forceId, forceValid, complete, rows.AsReadOnly(), sorted, sortIssue,
                issues.AsReadOnly()) { Pages = pages };
        }
    }
}
