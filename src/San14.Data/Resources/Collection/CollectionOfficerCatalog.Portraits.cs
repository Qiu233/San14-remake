using System.Globalization;

namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    private OfficerEditorPortraitCatalog? editorPortraits;
    private int editorPortraitResourceVersion = -1;
    private San14.Core.RegisteredOfficerLibrary? editorPortraitLibrary;
    public OfficerEditorPortraitCatalog CreateEditorPortraitCatalog() =>
        CreateEditorPortraitCatalog(portraitRepository ?? throw new InvalidDataException("officer-editor.portrait.resource_owner"));
    public OfficerEditorPortraitCatalog CreateEditorPortraitCatalog(GameResourceRepository repository,
        San14.Core.RegisteredOfficerLibrary? registeredLibrary = null)
    {
        if (editorPortraits is not null && editorPortraitResourceVersion == repository.PortraitResourceVersion
            && ReferenceEquals(editorPortraitLibrary, registeredLibrary)) return editorPortraits;
        var content = repository.LoadPortraitContent();
        var userPortraits = repository.LoadUserPortraits();
        var resources = new LocalizedUiSpriteSource(repository);
        var usedNames = registeredLibrary?.Officers.OrderBy(person => person.Id).GroupBy(person => person.PortraitId)
            .ToDictionary(group => group.Key, group => group.First().Name) ?? new Dictionary<int, string>();
        string UsedName(int appearance) => usedNames.GetValueOrDefault(appearance) ?? "";
        var bonusOwners = people.Values.Where(person => person.Id is >= 2001 and <= 2088)
            .OrderBy(person => person.Id).GroupBy(person => (int)person.Appearance)
            .ToDictionary(group => group.Key, group => group.First().Id);
        var bonusAdmission = bonusOwners.ToDictionary(pair => pair.Key, pair => ReadBonusAdmission(pair.Value));
        RegisteredOfficerEditorChoice[] People(int first, int count) => Enumerable.Range(first, count)
            .Where(id => people.TryGetValue(id, out var person) && NativeValid(person))
            .Select(id => new RegisteredOfficerEditorChoice(people[id].Appearance, people[id].Name.DisplayName)).ToArray();
        var npc = Enumerable.Range(4001, 33).Concat(Enumerable.Range(5001, 8))
            .Concat(Enumerable.Range(5010, 25))
            .Where(id => people.TryGetValue(id, out var person) && NativeValid(person))
            .Select(id => new RegisteredOfficerEditorChoice(people[id].Appearance, people[id].Name.DisplayName))
            //The later4031/4032 pass checks pointer presence, not native validity.
            .Concat(Enumerable.Range(4031, 2).Where(people.ContainsKey)
                .Select(id => new RegisteredOfficerEditorChoice(people[id].Appearance, people[id].Name.DisplayName)))
            .Concat(People(4101, 100).DistinctBy(choice => choice.Id)).ToArray();
        var bonus = admitted.Where(entry => entry.Person.Id is >= 2001 and <= 2088)
            .Select(entry => new RegisteredOfficerEditorChoice(entry.Person.Appearance, entry.Person.Name.DisplayName))
            .Concat(Enumerable.Range(2179, 19).Concat(Enumerable.Range(2500, 10))
                .Select(id => new RegisteredOfficerEditorChoice(id, UsedName(id))))
            //5FD1F0 visits the real36FE80 candidates in numerical order.
            //Classified ownership is independent of Person publication bits.
            .Concat(Enumerable.Range(5000, 2000).Where(SelectableSpecial)
                .Where(id => content.ReadAdmission(id) is { Available: true, Value: true })
                .Select(id => new RegisteredOfficerEditorChoice(id, UsedName(id)))).ToArray();
        static bool SelectableSpecial(int id) => id is not (>= 5000 and <= 5099 or >= 5100 and <= 5105
            or >= 5207 and <= 5210 or >= 5212 and <= 5218 or >= 5220 and <= 5222 or 5225);
        int[] captions = [0x55B,0x556,0x557,0x558,0x559,0x55A,0x6D0];
        HistoricalOfficerPortraitCategory Group(int id, IEnumerable<RegisteredOfficerEditorChoice> values,
            params string[] missing)
        {
            var choices = new List<RegisteredOfficerEditorChoice>(); var issues = new HashSet<string>(missing);
            foreach (var choice in values)
            {
                if (choice.Id < 1300 && bonusAdmission.TryGetValue(choice.Id, out bool? allowed) && allowed == false)
                    continue; //Known locked is not an unresolved resource or an unknown admission.
                string? issue = RouteIssue(choice.Id);
                if (issue is null) choices.Add(choice); else issues.Add(issue);
            }
            return new(id, choices.AsReadOnly(), issues.ToArray()) { Caption = Data(1, captions[id]) };
        }
        string? RouteIssue(int id)
        {
            if (id is >= 7000 and <= 7999) return repository.UserPortraitResourceIssue(id);
            if (DlcPortraitDescriptor.ForAppearance(id) is not null) return repository.DlcPortraitResourceIssue(id);
            if (!RegisteredOfficerPortraitCatalog.IsStockAppearance(id))
                return "officer-editor.portrait.stock_route:" + id;
            if (id < 1300)
            {
                if (bonusOwners.TryGetValue(id, out int owner) && bonusAdmission[id] is null)
                    return "officer-editor.portrait.application_admission:" + owner;
            }
            var face = RegisteredOfficerPortraitCatalog.FacePortrait(id);
            var standing = RegisteredOfficerPortraitCatalog.StandingPortrait(id);
            if (!resources.ContainsTexturePack(face.ResourceName))
                return "officer-editor.portrait.face_resource:" + id;
            if (!resources.ContainsTexturePack(standing.ResourceName))
                return "officer-editor.portrait.standing_resource:" + id;
            return null;
        }
        string[] contentIssues = Enumerable.Range(5000, 2000).Where(SelectableSpecial)
            .Select(content.ReadAdmission).Where(result => !result.Available)
            .Select(result => result.UnresolvedInput!).Distinct().ToArray();
        editorPortraitResourceVersion = repository.PortraitResourceVersion;
        editorPortraitLibrary = registeredLibrary;
        return editorPortraits = new([
            Group(0, RegisteredOfficerPortraitCatalog.OrdinaryPortraits
                .Select(choice => new RegisteredOfficerEditorChoice(choice.Id, UsedName(choice.Id)))),
            Group(1, People(1, 316)), Group(2, People(317, 436)), Group(3, People(753, 248)),
            Group(4, npc), Group(5, bonus, contentIssues),
            Group(6, userPortraits.Registered.Select(slot => new RegisteredOfficerEditorChoice(slot.Appearance, UsedName(slot.Appearance))))
        ], repository);
    }
}
