using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

namespace San14.Client.Gameplay;

/// <summary>One read-only government publication. Preview reference identities
/// remain the page's keys; all current-person fields use their actual lookup.</summary>
internal static class GovernanceRosterPresentation
{
    internal static GovernanceRosterPageModel Enrich(GameState world, int forceId,
        GovernanceRosterPageModel model, ArmyCampaignRuleAdapter? rules, DetailForceAppearance? flag,
        Func<int, int?>? actualIdentity = null, S14DoctrineCatalog? doctrines = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(model);
        int? Actual(GovernanceRosterOfficer person) => actualIdentity is null ? person.Id : actualIdentity(person.Id);
        var force = world.Forces.GetValueOrDefault(forceId);
        var ruler = force?.RulerId is int rulerId ? world.Officers.GetValueOrDefault(rulerId) : null;
        var actualIds = model.AdvisorCandidates
            .Concat(model.Advisor is { } advisor ? [advisor] : [])
            .Concat(model.Departments.SelectMany(group => group.Members.Concat(group.Candidates)))
            .Select(Actual).OfType<int>().Concat(ruler is not null ? [ruler.Id] : [])
            .Where(world.Officers.ContainsKey).Distinct().ToArray();
        var values = ReadPersonValues(world, forceId, rules, actualIds);
        var portraitRoutes=rules?.QueryOfficerPortraitRoutes(world,actualIds);
        var effectSlots = new Dictionary<int, IReadOnlyList<int>?>();

        GovernanceRosterOfficer Person(GovernanceRosterOfficer person, int? actual)
        {
            if (actual is not int id || !world.Officers.TryGetValue(id, out var current)) return person;
            var policy = current.PolicyId is int policyId ? world.Policies.GetValueOrDefault(policyId) : null;
            bool knownCost = PolicyCosts.TryCalculate(world, id, out var cost);
            return person with
            {
                NativeValid=portraitRoutes?.GetValueOrDefault(id) is {} route
                    ?route.Kind==OfficerPortraitRouteKind.Hidden?false:route.CurrentAppearanceIndex is not null?true:person.NativeValid
                    :person.NativeValid,
                AppearanceIndex = current.PortraitAppearanceIndex,
                FaceResourceName=portraitRoutes?.GetValueOrDefault(id) is {Kind:OfficerPortraitRouteKind.StockNamed} face?face.ResourceName:null,
                FaceSpriteName=portraitRoutes?.GetValueOrDefault(id) is {Kind:OfficerPortraitRouteKind.StockNamed} sprite?sprite.SpriteName:null,
                PolicyDescription = policy?.Description,
                PolicyIcon = policy?.DepartmentCode is int category ? PolicyIcon(category) : null,
                PolicyId = current.PolicyId,
                PolicyLevel = values.GetValueOrDefault(id).Level,
                PolicyEffectSlots = PolicySlots(current.PolicyId),
                PolicyCost = person.PolicyCost == 0 ? 0 : knownCost ? cost.Total : person.PolicyCost,
                CostDiscount = person.PolicyCost != 0 && knownCost && cost.DoctrineDiscountApplied,
                Intelligence = values.GetValueOrDefault(id).Intelligence,
                ServiceStatus = OfficerServiceStatusSourceAdapter.Encode(current.ServiceStatus)
            };
        }
        GovernanceRosterOfficer Existing(GovernanceRosterOfficer person) => Person(person, Actual(person));

        IReadOnlyList<int>? PolicySlots(int? policyId)
        {
            if (policyId is not int id) return null;
            if (effectSlots.TryGetValue(id, out var saved)) return saved;
            var parameters = world.NativeArmySimulation?.Parameters;
            if (parameters is null) { effectSlots[id] = null; return null; }
            var slots = new int[8];
            for (int ordinal = 0; ordinal < slots.Length; ordinal++)
            {
                string key = BattleRemakeCurrentForcePolicies.PolicyEffectSlotKey(id, ordinal);
                if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int value) ||
                    value is < 0 or > byte.MaxValue)
                { effectSlots[id] = null; return null; }
                slots[ordinal] = value;
            }
            effectSlots[id] = slots; return slots;
        }

        GovernanceRosterOfficer? rulerView = null;
        if (ruler is not null)
        {
            var policy = ruler.PolicyId is int policyId ? world.Policies.GetValueOrDefault(policyId) : null;
            // A ruler comes from Force.RulerId, not the read-only preview's
            // raw-reference namespace.
            int? pending=null;
            string key=BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId,0x16A);
            if(world.NativeArmySimulation?.Parameters is {} parameters&&!parameters.Float32Values.ContainsKey(key)&&
                parameters.Int32Values.TryGetValue(key,out int value)&&value is >=0 and <=ushort.MaxValue)pending=value;
            rulerView = Person(new(ruler.Id, ruler.Name, policy?.Name, null, pending, false), ruler.Id);
        }
        int? doctrineId=ruler is null?null:OfficerDoctrineRules.ReadDoctrineId(ruler.DoctrineId,ruler.AbilityState);
        var doctrine=doctrines?.Doctrines.FirstOrDefault(row=>row.Slot==doctrineId);
        IReadOnlyList<GovernanceRosterOfficer?>? Slots(GovernanceRosterDepartment group)
        {
            if(group.SlotMembers is {} authored)return authored.Select(person=>person is null?null:Existing(person)).ToArray();
            if(world.NativeArmySimulation?.Parameters is not {} parameters)return null;
            var rows=new GovernanceRosterOfficer?[7];
            for(int ordinal=0;ordinal<rows.Length;ordinal++)
            {
                string key=BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId,group.Group,ordinal);
                if(parameters.Float32Values.ContainsKey(key)||!parameters.Int32Values.TryGetValue(key,out int id))return null;
                if(id==0)continue;
                var person=group.Members.Concat(group.Candidates).FirstOrDefault(row=>row.Id==id);
                if(person is not null)rows[ordinal]=Existing(person);
            }
            return rows;
        }
        var portrait = ReadRulerPortrait(world, rules, ruler?.Id);
        return model with
        {
            Advisor = model.Advisor is { } originalAdvisor ? Existing(originalAdvisor) : null,
            AdvisorCandidates = model.AdvisorCandidates.Select(Existing).ToArray(),
            Departments = model.Departments.Select(group => group with
            {
                Members = group.Members.Select(Existing).ToArray(),
                Candidates = group.Candidates.Select(Existing).ToArray(),
                MaximumCapacity=doctrine?.RawBytes is {Length:>=101} raw?raw[96+group.Group]:group.MaximumCapacity,
                SlotMembers=Slots(group)
            }).ToArray(),
            Ruler = rulerView,
            RulerResourceName = portrait.Resource,
            RulerSpriteName = portrait.Sprite,
            RulerPortraitUnresolvedInput = portrait.Issue,
            DoctrineId = doctrineId,
            DoctrineProgress = OwnedForce164Inputs.ReadProgress(world, forceId),
            Flag = flag,
            CommandBooksAvailable = AvailableBooks(world, forceId),
            NewAppointmentPending=rules?.ReadGovernanceNewAppointmentPending(world)
        };
    }

    private static IReadOnlyDictionary<int, (int? Intelligence, int? Level)> ReadPersonValues(GameState world, int forceId,
        ArmyCampaignRuleAdapter? rules, int[] ids)
    {
        var result = new Dictionary<int, (int? Intelligence, int? Level)>();
        if (rules is null || ids.Length == 0) return result;
        try
        {
            // One retained current reader for every actually displayed person.
            // Repeated department/adviser appearances reuse this publication.
            var pages = rules.PrepareGovernanceOfficerInformationPages(world, forceId, ids,
                advisor: true, initialSort: false);
            int intelligence = pages.Groups[0].Columns.Select((value, index) => (value.Id, index))
                .Single(value => value.Id == 10).index;
            int level = pages.Groups[0].Columns.Select((value, index) => (value.Id, index))
                .Single(value => value.Id == 69).index;
            foreach (var row in pages.ReadPage(0).Rows)
            {
                int? Number(int column)
                {
                    var cell = row.Cells[column];
                    return cell.UnresolvedOwner is null && int.TryParse(cell.Text,
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;
                }
                result[row.OfficerId] = (Number(intelligence), Number(level));
            }
        }
        catch (InvalidDataException) { /* Unknown remains an absent value. */ }
        return result;
    }

    //77E710 passes Policy+A0 to8AAB20. DepartmentCode is the typed current
    //view initialized by S14Policy.RawTailBytes[0], not a PolicyId-derived icon.
    private static string PolicyIcon(int category) => category switch
    {
        1 => "000_cmn_icon_ctg_shien",
        2 => "000_cmn_icon_ctg_bouryaku",
        3 => "000_cmn_icon_ctg_naisei",
        4 => "000_cmn_icon_ctg_jinji",
        _ => "000_cmn_icon_ctg_battle"
    };

    private static int? AvailableBooks(GameState world, int forceId)
    {
        if(world.Forces.GetValueOrDefault(forceId)?.RulerId is not int ruler ||
            world.Officers.GetValueOrDefault(ruler)?.DistrictId is not int districtId ||
            world.Districts.GetValueOrDefault(districtId) is not {} district)return null;
        //77C8A0 follows the actual ruler's District lookup, as does the
        //formal governance charge. District number1 is not that relationship.
        return district.OrdersRemaining;
    }

    private static (string? Resource, string? Sprite, string? Issue) ReadRulerPortrait(GameState world,
        ArmyCampaignRuleAdapter? rules, int? rulerId)
    {
        if (rulerId is not int id) return (null, null, null);
        if (rules is null) return (null, null, "governance.ruler_portrait.current_route");
        try
        {
            var route = rules.QueryOfficerPortraitRoutes(world, [id], 11).GetValueOrDefault(id);
            if (route?.Kind == OfficerPortraitRouteKind.Hidden) return (null, null, null);
            if (route is not { Kind: OfficerPortraitRouteKind.StockNamed,
                ResourceName: { } resource, SpriteName: { } sprite })
                return (null, null, route?.UnresolvedInput ?? "governance.ruler_portrait.current_route");
            return (resource, sprite, null);
        }
        catch (InvalidDataException error) { return (null, null, error.Message); }
    }
}
