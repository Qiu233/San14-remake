using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public TalentAutoSearchDraft OpenTalentAutoSearchDraft(GameState state, int forceId, int districtId,
        CampaignAppRandomState? appRandom = null)
    {
        if (Seed.Language != "TC") throw new InvalidDataException("talent.auto.SC_parameters");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindAreaAdministrationScalars(state, current);
        //716BA0's fast advice reaches280D60. The Army-return capture has
        //table identities, but does not import fixed Scenery44 positions.
        //Bind that resource once for this page; discovery47 stays owned by
        //the saved campaign, independently of its opening scenario flags.
        current.MaterializeFixedScenery(_landmarkDefinitions);
        for (int slot = 0; slot < 101; slot++)
            current.SetSceneryFlags47(slot, state.LandmarkInvestments?.GetValueOrDefault(slot)?.Flags);
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
            Seed.Language, _baseCapacityInputs, _repository);
        var queryState = appRandom is null ? state : state with { CampaignAppRandom32 = appRandom };
        var abilities = SearchAbilities(current);
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
        var page = new TalentUiQuerySource(queryState, current, forceId, districtId, false,
            (working, actor, target) => ResolveTalentAdvisorSampleCurrent(working, current, actor, target, TalentOrderKind.Search,
                () => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("sampler")),
            working => ReadArmyDestructionSampler(working, current) ?? throw TalentUiQuerySource.Missing("sampler"),
            id => ReadTalentOfficerCard(state, current, cards, id),
            (working, id, dimension) => abilities.ResolveDimension(working, id, dimension,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                ?? throw TalentUiQuerySource.Missing("auto_ability"));
        var source = new TalentAutoSearchQuerySource(page, current, forceId, districtId,
            (working, id) => abilities.ResolveDimension(working, id, 3,
                (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect))
                ?? throw TalentUiQuerySource.Missing("auto_Politics"), FastAdvice);
        return new(source, state.TalentAutoSearchPreferences);

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
            //TC's static AutoSearch switch is1:716BA0 calls2F5CB0, without
            //the2F39D0 text formatter or its App-random wording choices.
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
}
