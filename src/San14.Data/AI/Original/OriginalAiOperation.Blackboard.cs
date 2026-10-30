using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    /// <summary>Called at the actual actor boundary. It is not a query-time
    /// fingerprint or a copy of the game World.</summary>
    public OriginalAiBlackboardSnapshot CaptureBlackboard() => CaptureBlackboard(null);

    internal OriginalAiBlackboardSnapshot CaptureBlackboard(OriginalAiSharedListRuntime? sharedLists)
    {
        var bindings = ImmutableArray.CreateBuilder<OriginalAiBlackboardBinding>();
        var lists = ImmutableArray.CreateBuilder<OriginalAiBlackboardList>();
        var aliases = new Dictionary<CandidateList, int>(ReferenceEqualityComparer.Instance);
        var keys = new HashSet<uint>(integers.Keys);
        keys.UnionWith(entities.Keys); keys.UnionWith(candidateLists.Keys);
        keys.UnionWith(candidateNullEntities); keys.UnionWith(hexes.Keys);
        keys.UnionWith(troops.Keys); keys.UnionWith(tradeCountries.Keys);
        keys.UnionWith(hireOffers.Keys); keys.UnionWith(personSkills.Keys); keys.UnionWith(regions.Keys); keys.UnionWith(definitions.Keys);
        foreach (uint key in keys)
        {
            San14.Core.OriginalAiBlackboardValue value;
            if (integers.TryGetValue(key, out int integer)) value = new OriginalAiIntegerValue(integer);
            else if (entities.TryGetValue(key, out var entity)) value = EncodeEntity(entity);
            else if (candidateLists.TryGetValue(key, out var list))
            {
                if (!aliases.TryGetValue(list, out int id))
                {
                    id = sharedLists?.Identify(list) ?? aliases.Count; aliases.Add(list, id);
                    lists.Add(EncodeBlackboardList(id, list));
                }
                value = sharedLists is null ? new OriginalAiListValue(id) : new OriginalAiSharedListValue(id);
            }
            else if (candidateNullEntities.Contains(key)) value = new OriginalAiNullObjectValue();
            else if (TryDefinition(key, out var definition)) value = EncodeDefinition(definition);
            else if (TryRegionReference(key, out var region)) value = new OriginalAiRegionValue(region.PhysicalSlot);
            else if (TryHex(key, out var hex)) value = new OriginalAiHexValue(hex.Id);
            else if (TryTroops(key, out var group)) value = new OriginalAiMilitaryGroupValue(group.GroupId);
            else if (TryTradeCountry(key, out var country)) value = new OriginalAiTradeCountryValue(country.Id);
            else if (TryHireOffer(key, out var offer)) value = new OriginalAiHireOfferValue(offer.TemplateId);
            else if (TryPersonSkill(key, out var skill)) value = EncodeSkill(skill);
            // An auxiliary dictionary can retain a shadowed old tag. The
            // active tag above is the one the native readers can observe.
            else continue;
            bindings.Add(new(key, value));
        }
        return new(bindings.ToImmutable(), lists.ToImmutable());
    }

    public ImmutableArray<OriginalAiBlackboardEvaluation> CaptureEvaluations()
    {
        var evaluations = ImmutableArray.CreateBuilder<OriginalAiBlackboardEvaluation>();
        foreach (var item in candidateEvaluations) evaluations.Add(new(EncodeEntity(item.Key), item.Value));
        foreach (var item in hexEvaluations) evaluations.Add(new(new OriginalAiHexValue(item.Key.Id), item.Value));
        foreach (var item in hireOfferEvaluations) evaluations.Add(new(new OriginalAiHireOfferValue(item.Key.TemplateId), item.Value));
        foreach (var item in personSkillEvaluations) evaluations.Add(new(EncodeSkill(item.Key), item.Value));
        foreach (var item in definitionEvaluations) evaluations.Add(new(EncodeDefinition(item.Key), item.Value));
        return evaluations.ToImmutable();
    }

    /// <summary>Restore an actually saved context once, before invoking its
    /// delegate. Unknown legacy state must be handled by the owning bank.</summary>
    public void RestoreBlackboard(OriginalAiBlackboardSnapshot snapshot) => RestoreBlackboard(snapshot, null);

    internal void RestoreBlackboard(OriginalAiBlackboardSnapshot snapshot, OriginalAiSharedListRuntime? sharedLists)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Bindings.IsDefault || snapshot.Lists.IsDefault)
            throw BlackboardMissing("snapshot_arrays");
        var restoredLists = new Dictionary<int, CandidateList>();
        var savedListIds = new HashSet<int>();
        var localIds = snapshot.Bindings.Select(binding => binding.Value).OfType<OriginalAiListValue>()
            .Select(value => value.ListId).ToHashSet();
        foreach (var saved in snapshot.Lists)
        {
            if (saved.Values.IsDefault || saved.ListId < 0 || !savedListIds.Add(saved.ListId))
                throw BlackboardMissing("list_identity");
            if (sharedLists is null || localIds.Contains(saved.ListId))
                restoredLists.Add(saved.ListId, DecodeBlackboardList(saved));
        }
        ClearBlackboard();
        var seen = new HashSet<uint>();
        foreach (var binding in snapshot.Bindings)
        {
            if (!seen.Add(binding.Key)) throw BlackboardMissing("duplicate_key");
            uint key = binding.Key;
            switch (binding.Value)
            {
                case OriginalAiIntegerValue value: integers.Add(key, value.Value); break;
                case OriginalAiNullObjectValue: candidateNullEntities.Add(key); break;
                case OriginalAiEntityValue value: entities.Add(key, new(value.EntityKind, value.Id)); break;
                case OriginalAiHexValue value: hexes.Add(key, new(value.Id)); break;
                case OriginalAiRegionValue value: regions.Add(key, new(value.Id)); break;
                case OriginalAiFacilityDefinitionValue value: definitions.Add(key, new(OriginalAiDefinitionKind.Facility, value.Id)); break;
                case OriginalAiGemDefinitionValue value: definitions.Add(key, new(OriginalAiDefinitionKind.Gem, value.Id)); break;
                case OriginalAiMilitaryGroupValue value: troops.Add(key, new(value.Id)); break;
                case OriginalAiTradeCountryValue value: tradeCountries.Add(key, new(value.Id)); break;
                case OriginalAiHireOfferValue value: hireOffers.Add(key, new(value.Id)); break;
                case OriginalAiFormationValue value: personSkills.Add(key, new(OriginalAiPersonSkillKind.Formation, value.Id)); break;
                case OriginalAiTacticValue value: personSkills.Add(key, new(OriginalAiPersonSkillKind.Tactic, value.Id)); break;
                case OriginalAiListValue value:
                    candidateLists.Add(key, restoredLists.GetValueOrDefault(value.ListId) ?? throw BlackboardMissing("list_alias")); break;
                case OriginalAiSharedListValue value:
                    if (!savedListIds.Contains(value.ListId)) throw BlackboardMissing("shared_list_row");
                    candidateLists.Add(key, sharedLists?.Resolve(value.ListId) ?? restoredLists.GetValueOrDefault(value.ListId)
                        ?? throw BlackboardMissing("shared_list_alias")); break;
                default: throw BlackboardMissing("binding_type");
            }
        }
    }

    internal static OriginalAiBlackboardList EncodeBlackboardList(int id, CandidateList list)
    {
        var kind = EncodeListKind(list);
        IEnumerable<San14.Core.OriginalAiBlackboardValue> values = kind switch
        {
            OriginalAiBlackboardListKind.HireOffers => list.HireOfferValues.Select(value => new OriginalAiHireOfferValue(value.TemplateId)),
            OriginalAiBlackboardListKind.Hexes => list.HexValues.Select(value => new OriginalAiHexValue(value.Id)),
            OriginalAiBlackboardListKind.MilitaryGroups => list.TroopsValues.Select(value => new OriginalAiMilitaryGroupValue(value.GroupId)),
            OriginalAiBlackboardListKind.Formations or OriginalAiBlackboardListKind.Tactics => list.SkillValues.Select(EncodeSkill),
            OriginalAiBlackboardListKind.FacilityDefinitions or OriginalAiBlackboardListKind.GemDefinitions => list.DefinitionValues.Select(EncodeDefinition),
            _ => list.Values.Select(EncodeEntity)
        };
        return new(id, kind, values.ToImmutableArray());
    }

    internal static CandidateList DecodeBlackboardList(OriginalAiBlackboardList saved)
    {
        if (saved.ListId < 0 || saved.Values.IsDefault) throw BlackboardMissing("list_identity");
        var list = new CandidateList(DecodeListKind(saved.Kind));
        list.HoldsHireOffers = saved.Kind == OriginalAiBlackboardListKind.HireOffers;
        foreach (var value in saved.Values)
        {
            if (list.HoldsHireOffers && value is OriginalAiHireOfferValue offer) list.HireOfferValues.Add(new(offer.Id));
            else if (list.Kind == CandidateListKind.Hex && value is OriginalAiHexValue hex) list.HexValues.Add(new(hex.Id));
            else if (list.Kind == CandidateListKind.Troops && value is OriginalAiMilitaryGroupValue group) list.TroopsValues.Add(new(group.Id));
            else if (list.Kind == CandidateListKind.Formation && value is OriginalAiFormationValue formation)
                list.SkillValues.Add(new(OriginalAiPersonSkillKind.Formation, formation.Id));
            else if (list.Kind == CandidateListKind.Tactic && value is OriginalAiTacticValue tactic)
                list.SkillValues.Add(new(OriginalAiPersonSkillKind.Tactic, tactic.Id));
            else if (list.Kind == CandidateListKind.Facility && value is OriginalAiFacilityDefinitionValue facility)
                list.DefinitionValues.Add(new(OriginalAiDefinitionKind.Facility, facility.Id));
            else if (list.Kind == CandidateListKind.Gem && value is OriginalAiGemDefinitionValue gem)
                list.DefinitionValues.Add(new(OriginalAiDefinitionKind.Gem, gem.Id));
            else if (value is OriginalAiEntityValue entity && CandidateValueMatches(new(entity.EntityKind, entity.Id), list.Kind))
                list.Values.Add(new(entity.EntityKind, entity.Id));
            else throw BlackboardMissing("list_value_type");
        }
        return list;
    }

    public void RestoreEvaluations(ImmutableArray<OriginalAiBlackboardEvaluation> evaluations)
    {
        if (evaluations.IsDefault) throw BlackboardMissing("evaluation_array");
        candidateEvaluations.Clear(); hexEvaluations.Clear(); hireOfferEvaluations.Clear(); personSkillEvaluations.Clear(); definitionEvaluations.Clear();
        foreach (var value in evaluations)
            switch (value.Receiver)
            {
                case OriginalAiEntityValue receiver: candidateEvaluations.Add(new(receiver.EntityKind, receiver.Id), value.Value); break;
                case OriginalAiHexValue receiver: hexEvaluations.Add(new(receiver.Id), value.Value); break;
                case OriginalAiHireOfferValue receiver: hireOfferEvaluations.Add(new(receiver.Id), value.Value); break;
                case OriginalAiFormationValue receiver:
                    personSkillEvaluations.Add(new(OriginalAiPersonSkillKind.Formation, receiver.Id), value.Value); break;
                case OriginalAiTacticValue receiver:
                    personSkillEvaluations.Add(new(OriginalAiPersonSkillKind.Tactic, receiver.Id), value.Value); break;
                case OriginalAiFacilityDefinitionValue receiver:
                    definitionEvaluations.Add(new(OriginalAiDefinitionKind.Facility, receiver.Id), value.Value); break;
                case OriginalAiGemDefinitionValue receiver:
                    definitionEvaluations.Add(new(OriginalAiDefinitionKind.Gem, receiver.Id), value.Value); break;
                default: throw BlackboardMissing("evaluation_type");
            }
    }

    public void ClearBlackboard()
    {
        integers.Clear(); entities.Clear(); candidateLists.Clear(); candidateNullEntities.Clear();
        hexes.Clear(); troops.Clear(); tradeCountries.Clear(); hireOffers.Clear(); personSkills.Clear(); regions.Clear(); definitions.Clear();
        // Receiver evaluations are shared policy state; B26CC0 clears only
        // blackboard values and their lists, not the referenced objects.
    }

    private static OriginalAiEntityValue EncodeEntity(OriginalAiEntity value) => new(value.Kind, value.Id);
    private static San14.Core.OriginalAiBlackboardValue EncodeDefinition(OriginalAiDefinitionReference value) => value.Kind switch
    {
        OriginalAiDefinitionKind.Facility => new OriginalAiFacilityDefinitionValue(value.PhysicalSlot),
        OriginalAiDefinitionKind.Gem => new OriginalAiGemDefinitionValue(value.PhysicalSlot),
        _ => throw BlackboardMissing("definition_kind")
    };
    private static San14.Core.OriginalAiBlackboardValue EncodeSkill(OriginalAiPersonSkillReference value) => value.Kind switch
    {
        OriginalAiPersonSkillKind.Formation => new OriginalAiFormationValue(value.PhysicalSlot),
        OriginalAiPersonSkillKind.Tactic => new OriginalAiTacticValue(value.PhysicalSlot),
        _ => throw BlackboardMissing("skill_kind")
    };
    private static OriginalAiBlackboardListKind EncodeListKind(CandidateList value) => value.Kind switch
    {
        CandidateListKind.Officer => value.HoldsHireOffers ? OriginalAiBlackboardListKind.HireOffers : OriginalAiBlackboardListKind.Officers,
        CandidateListKind.Area => OriginalAiBlackboardListKind.Areas, CandidateListKind.Foothold => OriginalAiBlackboardListKind.Footholds,
        CandidateListKind.Force => OriginalAiBlackboardListKind.Forces, CandidateListKind.Army => OriginalAiBlackboardListKind.Armies,
        CandidateListKind.District => OriginalAiBlackboardListKind.Districts, CandidateListKind.Hex => OriginalAiBlackboardListKind.Hexes,
        CandidateListKind.Caution => OriginalAiBlackboardListKind.CautionRelations, CandidateListKind.Troops => OriginalAiBlackboardListKind.MilitaryGroups,
        CandidateListKind.Formation => OriginalAiBlackboardListKind.Formations, CandidateListKind.Tactic => OriginalAiBlackboardListKind.Tactics,
        CandidateListKind.CautionGroup => OriginalAiBlackboardListKind.CautionGroups,
        CandidateListKind.MapObject => OriginalAiBlackboardListKind.MapObjects,
        CandidateListKind.Landmark => OriginalAiBlackboardListKind.Landmarks,
        CandidateListKind.Facility => OriginalAiBlackboardListKind.FacilityDefinitions,
        CandidateListKind.Gem => OriginalAiBlackboardListKind.GemDefinitions,
        _ => throw BlackboardMissing("list_kind")
    };
    private static CandidateListKind DecodeListKind(OriginalAiBlackboardListKind value) => value switch
    {
        OriginalAiBlackboardListKind.Officers or OriginalAiBlackboardListKind.HireOffers => CandidateListKind.Officer,
        OriginalAiBlackboardListKind.Areas => CandidateListKind.Area, OriginalAiBlackboardListKind.Footholds => CandidateListKind.Foothold,
        OriginalAiBlackboardListKind.Forces => CandidateListKind.Force, OriginalAiBlackboardListKind.Armies => CandidateListKind.Army,
        OriginalAiBlackboardListKind.Districts => CandidateListKind.District, OriginalAiBlackboardListKind.Hexes => CandidateListKind.Hex,
        OriginalAiBlackboardListKind.CautionRelations => CandidateListKind.Caution, OriginalAiBlackboardListKind.MilitaryGroups => CandidateListKind.Troops,
        OriginalAiBlackboardListKind.Formations => CandidateListKind.Formation, OriginalAiBlackboardListKind.Tactics => CandidateListKind.Tactic,
        OriginalAiBlackboardListKind.CautionGroups => CandidateListKind.CautionGroup,
        OriginalAiBlackboardListKind.MapObjects => CandidateListKind.MapObject,
        OriginalAiBlackboardListKind.Landmarks => CandidateListKind.Landmark,
        OriginalAiBlackboardListKind.FacilityDefinitions => CandidateListKind.Facility,
        OriginalAiBlackboardListKind.GemDefinitions => CandidateListKind.Gem,
        _ => throw BlackboardMissing("saved_list_kind")
    };
    private static InvalidDataException BlackboardMissing(string key) => new("original_ai.blackboard." + key);
}
