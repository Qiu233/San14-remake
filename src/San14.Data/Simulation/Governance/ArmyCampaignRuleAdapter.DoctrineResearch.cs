using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IDoctrineResearchCommandRules
{
    private const string ResearchParameterPrefix = "simulation.doctrine-research.";

    public DoctrineResearchDraft OpenDoctrineResearchDraft(GameState state, int forceId, bool userStrategyHostAvailable)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            return new(CaptureDoctrineResearchRules(state, current, forceId, userStrategyHostAvailable));
        }
        catch (InvalidDataException error) { return new(forceId, error.Message); }
    }

    private DoctrineResearchRules CaptureDoctrineResearchRules(GameState state, SourceWorldContext current,
        int forceId, bool userStrategyHostAvailable)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw ResearchMissing("Force_pointer");
        if (!ResearchNeed(current.ReadForceNativeValidity(force), "Force_validity") ||
            ResearchNeed(force.PhysicalSlot, "Force_identity") != forceId || !state.Forces.TryGetValue(forceId, out var owner))
            throw ResearchMissing("Force_control");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        int district = ResearchNeed(primary.PhysicalSlot, "primary_District");
        var districtOwner = state.Districts.GetValueOrDefault(district) ?? throw ResearchMissing("primary_District_owner");
        int books = districtOwner.OrdersRemaining ?? throw ResearchMissing("primary_District14");
        var treasury = ReadPersonnelHireTreasuryCurrent(current, district);
        if (!ResearchNeed(current.ReadBaseNativeValidity(treasury), "treasury_validity")) throw ResearchMissing("treasury_invalid");
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(treasury, current);
        int goldCapacity = BasePostLoad.ReadResourceCapacities(BaseCurrencyAdditionSource.CurrencyCity(treasury, current), current).Gold;
        int ruler = TalentRulerCurrent(current, force);
        int rulerPhysical = current.ResolveOfficerSource(ruler) ?? throw ResearchMissing("ruler_pointer");
        var rulerOwner = state.Officers.GetValueOrDefault(ruler) ?? throw ResearchMissing("ruler_owner");
        int rawDoctrine = ResearchNeed(OfficerDoctrineRules.ReadDoctrineId(rulerOwner.DoctrineId, rulerOwner.AbilityState), "ruler_doctrine");
        int doctrine = ResearchNeed((current.ReadStaticTableReference(0x78CF8, rawDoctrine < 7 ? rawDoctrine : 0)
            ?? throw ResearchMissing("ruler_doctrine_lookup")).PhysicalSlot, "ruler_doctrine_receiver");
        if (state.ForceCreditReserves is not { } reserves || !reserves.PointsByForce.TryGetValue(forceId, out var points) || points.Length != 6)
            throw ResearchMissing("doctrine_reserve_owner");
        int[] active = owner.PolicyEffects?.ActiveEffectIds ?? throw ResearchMissing("researched_owner");
        int[] pending = owner.PolicyEffects?.SuppressedEffectIds ?? throw ResearchMissing("pending_research_owner");
        var available = owner.DoctrineResearch?.AvailablePanelIds ?? throw ResearchMissing("available_research_owner");
        var baseline = new DoctrineResearchWorkspace(points, active, pending, available);
        var panels = ReadDoctrineResearchDefinitions(current);
        var panelByLayout = panels.Values.OrderBy(row => row.Id).GroupBy(row => row.LayoutSlot)
            .ToDictionary(group => group.Key, group => group.Last().Id);
        int rankRaw = ResearchNeed(current.ReadMoraleWaveForceRank47(forceId), "Force_rank47");
        int rank = ResearchNeed((current.ReadStaticTableReference(0x78490, rankRaw < 13 ? rankRaw : 0)
            ?? throw ResearchMissing("Rank_lookup")).PhysicalSlot, "Rank_receiver");
        var rankRows = _scenario.Snapshot.RankParameters?.Ranks ?? throw ResearchMissing("Rank_catalog");
        if ((uint)rank >= (uint)rankRows.Count) throw ResearchMissing("Rank_identity");
        var rankBytes = Convert.FromHexString(rankRows[rank].RawBytesHex);
        if (rankBytes.Length < 166) throw ResearchMissing("Rank_research_capacity");
        int discount = ResearchParameter(state, "matching-doctrine-discount", 1);
        var names = Enumerable.Range(0, 7).ToDictionary(id => id, id => state.Doctrines.GetValueOrDefault(id)?.Name ?? "");
        var priorities = new Lazy<(int First, int Second)>(() => ReadDoctrineResearchPriorities(state, current, forceId));
        var secondary = new Lazy<int>(() => ReadDoctrineResearchSecondary(state, current, forceId, doctrine));
        return new(forceId, district, ruler, doctrine, discount, books, gold, goldCapacity,
            ResearchParameter(state, "command-book-cost", 1), ResearchParameter(state, "redefine-gold", 1000),
            rankBytes[165], userStrategyHostAvailable && TalentForceIsPlayerCurrent(state, current, force),
            ReadApplicationInformationRestriction(state), baseline, panels, panelByLayout, names,
            id => ReadHistoryDataText(0, checked(0x17b9 + id)), RedefineAllowed, Message, Automatic)
            { ForceName = owner.Name, TitleName = rankRows[rank].Name };

        bool RedefineAllowed(DoctrineResearchWorkspace workspace)
        {
            if (!workspace.Researched.Contains(51)) return true;
            int capacity = _force164Inputs.ReadGovernanceCostLimit(state, forceId) ?? throw ResearchMissing("redefine_base_policy_capacity");
            int used = GovernanceRosterSource.Read(state, forceId, _scenario.Snapshot.DoctrineParameters, []).PolicyCost
                ?? throw ResearchMissing("redefine_used_policy_cost");
            return capacity >= used;
        }
        AreaAdministrationQuery<string> Message(int root, int? amount, string? text)
        {
            try
            {
                return AreaAdministrationQuery<string>.Known(FormatReportMessage(state, current, root,
                    new(String0: text, Integers: amount is int value ? [value] : null),
                    ReportMessageOrigin.DoctrineResearch, null).Text);
            }
            catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
        }
        DoctrineResearchAutomaticSession Automatic(DoctrineResearchWorkspace workspace, Func<int, bool> canResearch, int? previous)
            => DoctrineResearchAutomaticSource.CreateSession(panels.Values.OrderBy(row => row.Id).ToArray(),
                priorities.Value.First, priorities.Value.Second,
                new(() => doctrine, () => secondary.Value, category => Preference(category),
                    id => id is >= 1 and <= 6 ? workspace.Points[id - 1] : throw ResearchMissing("automatic_doctrine_identity"),
                    workspace.Researched.Contains, canResearch),
                new(ResearchParameter(state, "automatic-primary-match-bonus", 3),
                    ResearchParameter(state, "automatic-secondary-match-bonus", 2)), previous);
        int Preference(int category)
        {
            if (!ResearchNeed(current.ReadPersonNativeValidity(rulerPhysical), "automatic_ruler_validity")) return 0;
            return category switch { 3 => Math.Max(Word(0x16A), Word(0x17C)),
                4 => Word(0x17E), 5 => Word(0x16E), 6 => Word(0x180), _ => 0 };
        }
        int Word(int offset) => RulerCaptureReleaseInputs.ReadCurrentWord(state, rulerPhysical, offset)
            ?? throw ResearchMissing("automatic_ruler_preference_" + offset.ToString("X"));
    }

    internal IReadOnlyDictionary<int, DoctrineResearchPanelDefinition> ReadDoctrineResearchDefinitions(SourceWorldContext current)
    {
        var catalog = _repository.LoadDoctrinePanels(_scenario.FixedDataSource.SourcePath).Catalog;
        var result = new Dictionary<int, DoctrineResearchPanelDefinition>();
        for (int raw = 0; raw < 80; raw++)
        {
            int physical = ResearchNeed((current.ReadStaticTableReference(0x85F20, raw)
                ?? throw ResearchMissing("definition_lookup")).PhysicalSlot, "definition_receiver");
            if ((uint)physical >= (uint)catalog.Panels.Count) throw ResearchMissing("definition_identity");
            result.TryAdd(physical, DoctrineResearchSource.Decode(catalog.Panels[physical]));
        }
        return result;
    }

    private (int First, int Second) ReadDoctrineResearchPriorities(GameState state, SourceWorldContext current, int forceId)
    {
        //6F28E0's eight local values: two initial zero slots, then the five
        //actual department cost sums, then one trailing zero slot.
        var values = new int[8];
        for (int group = 0; group < 5; group++)
            for (int ordinal = 0; ordinal < 7; ordinal++)
            {
                string key = BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group, ordinal);
                int id = ResearchSaved(state, key);
                var pointer = current.ReadPersonLookupReference(id < 6001 ? id : 0) ?? throw ResearchMissing("priority_Person_lookup");
                int physical = ResearchNeed(pointer.PhysicalSlot, "priority_Person_receiver");
                if (!ResearchNeed(current.ReadPersonNativeValidity(physical), "priority_Person_validity")) continue;
                if (!PolicyCosts.TryCalculate(state, id, out var cost)) throw ResearchMissing("priority_policy_cost");
                values[group + 2] = unchecked(values[group + 2] + cost.Total);
            }
        int first = 2, second = 3, firstValue = 0, secondValue = 0;
        for (int index = 0; index < values.Length; index++)
            if (firstValue < values[index]) { second = first; secondValue = firstValue; first = index; firstValue = values[index]; }
            else if (secondValue < values[index]) { second = index; secondValue = values[index]; }
        return (first, second);
    }

    private int ReadDoctrineResearchSecondary(GameState state, SourceWorldContext current, int forceId, int primaryDoctrine)
    {
        return DoctrineResearchAutomaticSource.SelectSecondaryDoctrine(primaryDoctrine, People());
        IEnumerable<DoctrineResearchSecondaryOfficer> People()
        {
            foreach (int? slot in current.ReadRegisteredPersonPhysicalSlots() ?? throw ResearchMissing("secondary_Person_registry"))
            {
                int physical = ResearchNeed(slot, "secondary_Person_pointer");
                var force = current.ReadPersonForce(physical) ?? throw ResearchMissing("secondary_Person_force");
                if (force.PhysicalSlot != forceId) continue;
                int status = ResearchNeed(current.ReadPersonStatus(physical), "secondary_Person_status");
                if (status is < 2 or > 4) continue;
                int id = ResearchNeed(current.ReadPersonIdentity(physical), "secondary_Person_identity");
                var officer = state.Officers.GetValueOrDefault(id) ?? throw ResearchMissing("secondary_Person_owner");
                int personDoctrine = ResearchNeed(OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState), "secondary_Person_doctrine");
                yield return new(id, status, ResearchNeed(current.ReadMoraleWavePersonPostE7(physical), "secondary_Person_post"),
                    ResearchNeed(current.ReadPersonFirstLocation(physical), "secondary_Person_home"), personDoctrine,
                    officer.Behavior?.UsesSpecialPersonRules ?? throw ResearchMissing("secondary_Person_special"),
                    () => ResearchNeed(current.ReadPersonForce(physical)?.PhysicalSlot, "secondary_Person_force_identity"),
                    () => ResearchNeed(current.ReadAssignmentForceAdvisor52(forceId), "secondary_Force_advisor") == id);
            }
        }
    }

    private int ResearchParameter(GameState state, string name, int ceValue)
    {
        string key = ResearchParameterPrefix + name;
        if (state.NativeArmySimulation?.Parameters is not { } parameters || _scenario.Metadata.FormatMarker != 0x66)
            throw ResearchMissing("parameters");
        if (parameters.Float32Values.ContainsKey(key)) throw ResearchMissing("parameter_type_" + name);
        int value = parameters.Int32Values.TryGetValue(key, out int saved) ? saved : ceValue;
        if (value < 0) throw ResearchMissing("parameter_negative_" + name);
        return value;
    }
    private static int ResearchSaved(GameState state, string key)
        => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true
            ? value : throw ResearchMissing(key);
    private static T ResearchNeed<T>(T? value, string owner) where T : struct => value ?? throw ResearchMissing(owner);
    private static InvalidDataException ResearchMissing(string owner) => DoctrineResearchSource.Missing(owner);
}
