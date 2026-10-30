using System.Collections.ObjectModel;
using System.Globalization;

namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    private int detailPortraitResourceVersion = -1;
    private static readonly int[] BasicNodes = [4,37,8,2,25,14,32,26,22,29,35,15,33,27,23,30,108,36,17,
        50,51,52,53,54,59,64,66,60,65,67,101,71,84,72,73,74,75,76,77,78,79,80,81,85,86,87,88,89,90,91,92,93,94,98,99,0];
    private static readonly int[] ProfileNodes = [4,8,2,14,15,99,17,28,35,43,58,29,36,44,52,59,65,24,31,39,
        48,54,61,26,33,41,50,56,63,69,71,72,73,74,76,77,78,79,83,85,86,87,88,90,91,92,93,97,98];

    public int GetVariantCount(int officerId)
    {
        var person = RequireOfficer(officerId);
        return PersonPortraitTransitionCatalog.Rules.Any(rule => rule.PortraitWhenFalse == person.Appearance ||
            rule.PortraitWhenTrue == person.Appearance) ? 2 : 1;
    }

    public CollectionOfficerExpansion ReadExpansion(int officerId, int variant = 0)
    {
        var person = RequireOfficer(officerId);
        ushort appearance = Appearance(person, variant);
        if (DlcPortraitDescriptor.ForAppearance(appearance) is not null)
        {
            if (portraitRepository is null)
                return new(officerId, variant, GetVariantCount(officerId), null, "collection.officer.portrait_repository");
            var available = portraitRepository.ReadDlcPortraitAvailability(appearance);
            if (!available.Available) return new(officerId, variant, GetVariantCount(officerId), null, available.UnresolvedInput);
            if (!available.Value)
                return new(officerId, variant, GetVariantCount(officerId), new("texture_chara_anime_2000", "face_2000_a", 9, 2000));
            var descriptor = portraitRepository.RequireDlcPortrait(appearance, 9);
            return new(officerId, variant, GetVariantCount(officerId), new(descriptor.ResourceUri,
                descriptor.SpriteName(appearance, 9, 1), 9, appearance));
        }
        if (appearance is >= 7000 and <= 7999 && portraitRepository is null)
            return new(officerId, variant, GetVariantCount(officerId), null, "collection.officer.portrait_repository");
        if (appearance is >= 7000 and <= 7999 && portraitRepository is not null
            && portraitRepository.LoadUserPortraits().HasResource(appearance, 9))
            return new(officerId, variant, GetVariantCount(officerId), new(
                GameResourceId.File(UserPortraitRegistration.RelativePath(appearance - 7000, 9)).ToString(), "portrait", 9, appearance));
        if (appearance is >= 7000 and <= 7999)
            return new(officerId, variant, GetVariantCount(officerId), new("texture_chara_anime_2000", "face_2000_a", 9, 2000));
        var route = Portrait(person, appearance);
        if (route.Kind != OfficerPortraitRouteKind.StockNamed || route.ResolvedAppearanceIndex is not int resolved)
            return new(officerId, variant, GetVariantCount(officerId), null, route.UnresolvedInput);
        string number = resolved.ToString("D4", CultureInfo.InvariantCulture) + (route.SpriteName?.EndsWith("b_ctr", StringComparison.Ordinal) == true ? "b" : "");
        return new(officerId, variant, GetVariantCount(officerId),
            new("texture_chara_anime_" + number, "face_" + number + "_a", 9, resolved));
    }

    public CollectionOfficerDetail ReadOfficer(int officerId, int variant = 0)
    {
        int resourceVersion = portraitRepository?.PortraitResourceVersion ?? -1;
        if (detailPortraitResourceVersion != resourceVersion)
        {
            details.Clear(); detailPortraitResourceVersion = resourceVersion;
        }
        if (details.TryGetValue((officerId, variant), out var existing)) return existing;
        var person = RequireOfficer(officerId);
        ushort appearance = Appearance(person, variant);
        var portrait = Portrait(person, appearance);
        var issues = new List<string>();
        DeploymentNarrationPortraitRoute? standing = null;
        if (DeploymentNarrationPortraitRoute.IsAnimatedAppearance(appearance))
            standing=DeploymentNarrationPortraitRoute.Animated(appearance);
        else if (DlcPortraitDescriptor.ForAppearance(appearance) is not null && portraitRepository is not null)
        {
            var available = portraitRepository.ReadDlcPortraitAvailability(appearance);
            if (!available.Available) issues.Add(available.UnresolvedInput!);
            else standing = available.Value ? portraitRepository.ResolveDlcStandingPortrait(appearance)
                : RegisteredOfficerPortraitCatalog.StandingPortrait(2000) with { CurrentAppearanceIndex = appearance };
        }
        else if (appearance is >= 7000 and <= 7999 && portraitRepository is not null)
            standing = portraitRepository.LoadUserPortraits().HasResource(appearance, 9)
                ? portraitRepository.ResolveUserStandingPortrait(appearance)
                : RegisteredOfficerPortraitCatalog.StandingPortrait(2000) with { CurrentAppearanceIndex = appearance };
        else if (portrait.Kind == OfficerPortraitRouteKind.StockNamed && portrait.ResolvedAppearanceIndex is int resolved)
        {
            string number = resolved.ToString("D4", CultureInfo.InvariantCulture) + (portrait.SpriteName?.EndsWith("b_ctr", StringComparison.Ordinal) == true ? "b" : "");
            string stem = "face_" + number;
            standing = new(appearance, resolved, "texture_chara_anime_" + number, [stem + "_b", stem + "_a", stem + "_c"]);
        }
        else if (portrait.UnresolvedInput is string missing) issues.Add(missing);
        var pages = new List<CollectionOfficerDetailPage> { Basic(person) };
        if ((person.Flags & 0x4000) == 0) pages.Add(Profile(person));
        var expansion = ReadExpansion(officerId, variant);
        var actions = new[]
        {
            new CollectionOfficerDetailAction(0, Data(1, 0x70), true),
            new CollectionOfficerDetailAction(1, Data(1, 0x6F), expansion.UnresolvedOwner is null) { UnresolvedOwner = expansion.UnresolvedOwner },
            new CollectionOfficerDetailAction(2, Data(1, 0x73), true),
            new CollectionOfficerDetailAction(3, Data(1, 0xC4), GetVariantCount(officerId) > 1)
        };
        return details[(officerId, variant)] = new(officerId, person.Name.DisplayName, portrait, pages.AsReadOnly())
        {
            StandingPortrait = standing, CourtesyName = person.Dialogue.CourtesyName,
            VoiceSpeaker = new(officerId, person.Dialogue.VoiceType, person.Dialogue.SpecialVoiceType),
            Actions = Array.AsReadOnly(actions), UnresolvedOwners = issues.AsReadOnly()
        };
    }

    private Person RequireOfficer(int id) => admitted.FirstOrDefault(entry => entry.Person.Id == id).Person
        ?? throw new ArgumentOutOfRangeException(nameof(id), "Not an admitted Collection officer.");
    private ushort Appearance(Person person, int variant)
    {
        int count = GetVariantCount(person.Id);
        if ((uint)variant >= (uint)count) throw new ArgumentOutOfRangeException(nameof(variant));
        if (variant == 0) return person.Appearance;
        var pair = PersonPortraitTransitionCatalog.Rules.First(rule => rule.PortraitWhenFalse == person.Appearance || rule.PortraitWhenTrue == person.Appearance);
        return checked((ushort)(pair.PortraitWhenFalse == person.Appearance ? pair.PortraitWhenTrue : pair.PortraitWhenFalse));
    }

    private OfficerPortraitRoute Portrait(Person person, ushort appearance)
    {
        string? issue = null;
        int resolvedAppearance = appearance;
        if (PortraitContentCatalogue.IsClassified(appearance))
        {
            var content = portraitRepository?.LoadPortraitContent().ReadAdmission(appearance);
            if (content is null || !content.Available) issue = content?.UnresolvedInput ?? "collection.officer.portrait_repository";
            else if (!content.Value) resolvedAppearance = 2000;
        }
        if (issue is null && appearance < 1300)
        {
            var matching = people.Values.Where(other => other.Id is >= 2001 and <= 2088 && other.Appearance == appearance)
                .OrderBy(other => other.Id).FirstOrDefault();
            if (matching is not null && ReadBonusAdmission(matching.Id) != true)
                issue = "collection.officer.portrait_application_unlock";
        }
        if (issue is null && resolvedAppearance == appearance && DlcPortraitDescriptor.ForAppearance(appearance) is not null)
        {
            if (portraitRepository is null) issue = "collection.officer.portrait_repository";
            else
            {
                var available = portraitRepository.ReadDlcPortraitAvailability(appearance);
                if (!available.Available) issue = available.UnresolvedInput;
                else if (!available.Value) resolvedAppearance = 2000;
                else
                {
                    var dlc = portraitRepository.ResolveDlcFacePortrait(appearance);
                    return new(person.Id, OfficerPortraitRouteKind.StockNamed, appearance, appearance,
                        dlc.ResourceName, dlc.SpriteName, null, null);
                }
            }
        }
        if (issue is null && appearance is >= 7000 and <= 7999)
        {
            if (portraitRepository is null) issue = "collection.officer.portrait_repository";
            else if (!portraitRepository.LoadUserPortraits().HasResource(appearance, 10)) resolvedAppearance = 2000;
            else
            {
                var user = portraitRepository.ResolveUserFacePortrait(appearance);
                return new(person.Id, OfficerPortraitRouteKind.StockNamed, appearance, appearance,
                    user.ResourceName, user.SpriteName, null, null);
            }
        }
        if (issue is null && resolvedAppearance is not (>= 0 and <= 1310 or >= 2000 and <= 2199 or >= 2500 and <= 2509 or >= 3000 and <= 3005 or >= 5000 and <= 5399 or >= 6000 and <= 6214))
            issue = "collection.officer.unregistered_portrait_range";
        if (issue is not null) return new(person.Id, OfficerPortraitRouteKind.Unresolved, appearance, null, null, null, null, issue);
        var names = ArmyMapUiSources.Create().PortraitNames(checked((ushort)resolvedAppearance));
        return new(person.Id, OfficerPortraitRouteKind.StockNamed, appearance, names.ResolvedAppearanceIndex,
            names.ResourceName, names.SpriteName, null, null);
    }

    private CollectionOfficerDetailPage Basic(Person person)
    {
        int N(int logical) => BasicNodes[logical];
        var fields = new Dictionary<int, string>();
        (int Logical, int Caption)[] labels = [(5,0x55F),(24,0x244),(25,0x241),(26,0x386),(31,0x234),(32,0x22B),(53,0x411)];
        foreach (var label in labels) fields[N(label.Logical)] = Data(1, label.Caption);
        fields[N(16)] = Sex(person); fields[N(27)] = Doctrine(person);
        fields[N(29)] = PolicyCost(person).ToString(CultureInfo.InvariantCulture);
        // Logical field 30 is the policy marker root (101); its source text is child 104.
        fields[104] = Policy(person) is { } policy ? policy.Name + (unresolvedPolicyLevels.Contains(person.Id) ? "" : " " + Data(1, 0x384)
            .Replace("%d", person.PolicyCost.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) : "";
        var hidden = new[] {8,7,9,6,10,14,13,15,12,17,54,28,4,18,2}.Select(N).ToList();
        hidden.Add(105); // The policy marker's focus subtree starts hidden.
        if (person.Id is >= 1 and <= 1000)
            fields[N(11)] = YearSpan(person);
        else { hidden.Add(N(11)); hidden.Add(N(5)); }
        var abilities = person.Abilities.Select((value, index) => new CollectionOfficerAbility(index, Data(1,0x238+index),value,-1)).ToArray();
        var traits = person.Traits.Select((id,slot) => (id,slot)).Where(entry => entry.id != 0)
            .Select(entry => new CollectionOfficerTrait(entry.slot,entry.id,skills.TraitName(entry.id),skills.TraitStyle(entry.id),
                skills.TraitDescription(entry.id),N(19+entry.slot))).ToArray();
        for (int slot = 0; slot < 5; slot++) if (person.Traits[slot] == 0) hidden.Add(N(19+slot));
        var tactics = new List<CollectionOfficerTactic>();
        for (int slot = 0; slot < 10; slot++)
        {
            int id = person.Tactics[slot];
            if (id is <= 0 or >= 201 || !skills.TacticAvailable(id)) { hidden.Add(N(33+slot)); continue; }
            var effects = new string[2];
            for (int index = 0; index < 2; index++)
            { int opcode = combat.Tactics[id].RawBytes[80+index]; effects[index] = opcode is >=1 and <=23 ? Data(0,0x151+opcode) : ""; }
            tactics.Add(new(slot,id,combat.Tactics[id].Name,Array.AsReadOnly(effects),N(33+slot))
                { Balloon = ReadTacticBalloon(combat.Tactics[id]) });
        }
        var formations = Enumerable.Range(1,10).Select(id =>
            new CollectionOfficerFormation(id,combat.Formations[id].Name,Formation(person,id),N(43+id-1))).ToArray();
        return new(0,"02_07_01_busho1",new ReadOnlyDictionary<int,string>(fields))
        {
            Caption=Data(1,0x3CD),NameplateNodeIndex=N(0),PortraitNodeIndex=N(3),AbilityChartNodeIndex=N(1),
            HiddenNodes=hidden.AsReadOnly(),CaptionNodes=labels.Select(label=>N(label.Logical)).ToArray(),
            Abilities=Array.AsReadOnly(abilities),Traits=Array.AsReadOnly(traits),Tactics=tactics.AsReadOnly(),Formations=Array.AsReadOnly(formations),
            UnresolvedOwners=unresolvedPolicyLevels.Contains(person.Id)?["collection.officer.policy_level_credit_bonus"]:[]
        };
    }

    private CollectionOfficerDetailPage Profile(Person person)
    {
        int N(int logical) => ProfileNodes[logical];
        var fields = new Dictionary<int,string>(); var hidden = new List<int> { N(6) };
        var related = new List<CollectionOfficerRelatedPortrait>(); var issues = new List<string>();
        (int Logical,int Caption)[] labels=[(3,0x55F),(7,0x42C),(8,0x42D),(9,0x42E),(10,0x2BF),(29,700),(38,0x2BD),(47,0x297)];
        foreach(var label in labels) fields[N(label.Logical)]=Data(1,label.Caption);
        fields[N(5)]=Sex(person);
        Person? Related(int id) => id is >=5101 and <=5200 ? specialPeople.GetValueOrDefault(id) : people.GetValueOrDefault(id);
        bool UnresolvedSpecial(int id) => id is >=5101 and <=5200 &&
            SpecialDirectory.Any(entry => entry.OfficerId == id && entry.UnresolvedOwner is not null);
        void Relative(int id,int textLogical,int portraitLogical,bool specialDirectory,bool requireValid=true)
        {
            if(specialDirectory && UnresolvedSpecial(id))
            {
                issues.Add("collection.officer.related_special_person_directory");
                related.Add(new(N(portraitLogical),null){UnresolvedOwner="collection.officer.related_special_person_directory"});
                return;
            }
            var value = specialDirectory ? Related(id) : people.GetValueOrDefault(id);
            if(value is null||requireValid&&!NativeValid(value))
            {fields[N(textLogical)]="";hidden.Add(N(portraitLogical));return;}
            fields[N(textLogical)]=value.Name.DisplayName;
            var route=Portrait(value,value.Appearance);
            related.Add(new(N(portraitLogical),route){UnresolvedOwner=route.UnresolvedInput});
        }
        Relative(person.Relations.B4,11,17,true); Relative(person.Relations.B6,12,18,true); Relative(person.Relations.BA,13,19,true);
        var oath = Related(person.Relations.BC) is { } anchor&&NativeValid(anchor)
            ? people.Values.Concat(specialPeople.Values).DistinctBy(other=>other.Id).Where(other=>other.Relations.BC==person.Relations.BC)
                .OrderBy(other=>other.Id==person.Relations.BC?0:1).ThenByDescending(other=>Math.Max(0,prototypeYear-other.Dialogue.BirthYear+1))
                .ThenBy(other=>other.Slot).Take(3).ToArray() : [];
        for(int index=0;index<3;index++) Relative(index<oath.Length?oath[index].Id:0,14+index,20+index,true,false);
        if(UnresolvedSpecial(person.Relations.BC)) issues.Add("collection.officer.sworn_special_person_directory");
        void Affections(ushort[] ids,int firstLogical)
        {
            var values=new List<Person>();var pairs=new HashSet<int>();
            foreach(int id in ids)
            {
                if(UnresolvedSpecial(id)){issues.Add("collection.officer.affection_special_person_directory");continue;}
                var other = Related(id);
                if(other is null||!NativeValid(other))continue;
                if(id is >=5101 and <=5108&&other.Status is not(7 or 9)&&!pairs.Add((id-5101)/2))continue;
                values.Add(other);
            }
            var ordered=values.OrderBy(other=>other.Id is >=5101 and <=5200?0:1).ToArray();
            for(int index=0;index<8;index++)fields[N(firstLogical+index)]=index<ordered.Length?ordered[index].Name.DisplayName:"";
        }
        Affections(person.Relations.C0,30); Affections(person.Relations.D0,39);
        // Long biography text is outside the explicitly required restoration.
        fields[N(48)]="";
        if(person.Id is >=1 and <=1000)fields[N(4)]=YearSpan(person);
        else{hidden.Add(N(3));hidden.Add(N(4));}
        return new(1,"02_07_01_busho2",new ReadOnlyDictionary<int,string>(fields))
        {
            Caption=Data(1,0x297),NameplateNodeIndex=N(0),PortraitNodeIndex=N(2),HiddenNodes=hidden.AsReadOnly(),
            CaptionNodes=labels.Select(label=>N(label.Logical)).ToArray(),ParagraphNodes=[N(48)],
            RelatedPortraits=related.AsReadOnly(),UnresolvedOwners=issues.AsReadOnly()
        };
    }
    private string YearSpan(Person person) => Year(person).Length==0||DeathYear(person).Length==0 ? "-" :
        Year(person)+" ～ "+DeathYear(person);
}
