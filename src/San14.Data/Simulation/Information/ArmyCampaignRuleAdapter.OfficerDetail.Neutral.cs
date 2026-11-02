using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The captive inspector's empty checked/hover chain selects the
    /// world's actual placeholder. It is never admitted as an OfficerState.</summary>
    public AreaAdministrationQuery<OfficerDetailPublication> QueryArmyReturnCaptiveNeutralDetail(
        GameState state, int playerForceId)
    {
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var placeholder = current.ReadBasePerson()?.Value
                ?? throw OfficerDetailMissing("neutral.placeholder");
            int physical = placeholder.PhysicalSlot;
            if (physical != 0 || placeholder.CharacterId != 0 ||
                current.ReadPersonNativeValidity(physical) is not false)
                throw OfficerDetailMissing("neutral.placeholder_identity");
            _personRelationInputs.BindCurrent(state, current);
            return AreaAdministrationQuery<OfficerDetailPublication>.Known(
                ReadNeutralOfficerDetail(state, current, playerForceId, physical));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OfficerDetailPublication>.Unknown(error.Message); }
    }

    private OfficerDetailPublication ReadNeutralOfficerDetail(GameState state,
        SourceWorldContext current, int playerForceId, int physical)
    {
        var issues = new HashSet<string>(StringComparer.Ordinal);
        AreaAdministrationQuery<T> Q<T>(Func<T> read)
        {
            try { return AreaAdministrationQuery<T>.Known(read()); }
            catch (InvalidDataException error)
            { issues.Add(error.Message); return AreaAdministrationQuery<T>.Unknown(error.Message); }
        }
        static AreaAdministrationQuery<T> K<T>(T value) => AreaAdministrationQuery<T>.Known(value);
        var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions,
            _scenario, Seed.Language, _baseCapacityInputs, _repository);
        var card = cards.ReadCity(0, physical, false, 0);

        // The invalid-person writers clear the text and disable the health
        // compound. Capacity/salary's own value controls are also hidden by
        // their empty result; their captions and the rest of the pane remain.
        var text = K<string?>(null);
        var number = K<int?>(null);
        var entity = K<OfficerDetailEntity?>(null);
        var overview = new OfficerDetailOverview(number, text, text, entity, entity,
            text, entity, entity, text, number, number, number, number, number,
            text, number, text, text, text,
            K<string?>(ReadHistoryDataText(1, 0xc7)), K<string?>(ReadHistoryDataText(1, 0xc8)))
        {
            HealthCode = number, CourtesyName = text, CommandEmphasized = K<bool?>(false),
            LoyaltyVisible = K<bool?>(false), MeritVisible = K<bool?>(false)
        };

        var definition = _scenario.Snapshot.Characters[physical];
        if (definition.Slot != physical || definition.CharacterId != 0)
            throw OfficerDetailMissing("neutral.definition_identity");
        var doctrine = Q<OfficerDetailEntity?>(() => EmptyStaticEntity(0x78cf8,
            definition.RawDoctrineId is byte raw ? raw < 7 ? raw : 0 : throw OfficerDetailMissing("neutral.doctrine"),
            "neutral.doctrine_not_empty"));
        var policy = Q<OfficerDetailEntity?>(() => EmptyStaticEntity(0x78d50,
            definition.RawPolicySlot is byte raw ? raw < 121 ? raw : 0 : throw OfficerDetailMissing("neutral.policy"),
            "neutral.policy_not_empty"));
        var policyCost = policy.Available && policy.Value is null ? number
            : AreaAdministrationQuery<int?>.Unknown(policy.UnresolvedInput ?? "officer-detail.neutral.policy_cost");

        var tactics = ImmutableArray.CreateBuilder<OfficerDetailTactic>(10);
        for (int slot = 0; slot < 10; slot++)
        {
            int? id = null;
            try
            {
                var reference = DeploymentTacticSelectionSource.ReadCurrentSlot(current, 0, physical, slot);
                if (!DetailNeed(current.ReadSkillTacticValidity(reference), "neutral.tactic_validity"))
                { tactics.Add(new(slot, null, null, [], null)); continue; }
                id = DetailNeed(reference.PhysicalSlot, "neutral.tactic_receiver");
                string name = state.Tactics.GetValueOrDefault(id.Value)?.Name
                    ?? throw OfficerDetailMissing("neutral.tactic_name");
                var effects = Q(() => ReadPersonnelLearnEffectsCurrent(state, current, id.Value).ToImmutableArray());
                tactics.Add(new(slot, id, name, effects.Available ? effects.Value : [], effects.UnresolvedInput));
            }
            catch (InvalidDataException error)
            { issues.Add(error.Message); tactics.Add(new(slot, id, null, [], error.Message)); }
        }
        var formations = ImmutableArray.CreateBuilder<OfficerDetailFormation>(10);
        var mask = Q(() => DetailNeed(current.ReadPersonFormationMask164(physical), "neutral.formation_mask"));
        for (int raw = 1; raw <= 10; raw++)
        {
            int requested = raw;
            try
            {
                var reference = current.ReadStaticTableReference(0x77428, requested)
                    ?? throw OfficerDetailMissing("neutral.formation_lookup");
                int id = DetailNeed(reference.PhysicalSlot, "neutral.formation_receiver");
                string name = state.Formations.GetValueOrDefault(id)?.Name
                    ?? throw OfficerDetailMissing("neutral.formation_name");
                var available = mask.Available
                    ? K<bool?>(DeploymentFormationEligibility.Allows(mask.Value, id, 0))
                    : AreaAdministrationQuery<bool?>.Unknown(mask.UnresolvedInput!);
                formations.Add(new(id, name, available));
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
        }
        var military = new OfficerDetailMilitary(doctrine, policy, policyCost,
            tactics.ToImmutable(), formations.ToImmutable()) { PolicyCostBeforeDiscount = policyCost };
        var traits = ImmutableArray.CreateBuilder<OfficerDetailTrait>();
        HexListHandle? temporary = null;
        try
        {
            temporary = current.CreateTemporaryHexList() ?? throw OfficerDetailMissing("neutral.traits_list");
            for (int slot = 0; slot < 9; slot++)
            {
                int raw = DetailNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,
                    physical, slot), "neutral.trait_slot");
                var reference = current.ReadPersonalityTableReference(raw is >= 0 and <= 355 ? raw : 0)
                    ?? throw OfficerDetailMissing("neutral.trait_lookup");
                if (!DetailNeed(current.ReadSkillPersonalityValidity(reference), "neutral.trait_validity") ||
                    !current.AppendSkillTemporaryPersonality(temporary, reference) || traits.Count == 5) continue;
                int at = DetailNeed(reference.PhysicalSlot, "neutral.trait_receiver");
                byte style = DetailNeed(current.ReadPersonalityByte(reference, 0xbe), "neutral.trait_style");
                var tooltip = cards.ReadTraitTooltip(raw);
                if (tooltip.UnresolvedInput is { } issue) issues.Add(issue);
                traits.Add(new(traits.Count, raw, at, current.ReadPersonalityName(reference), style, tooltip, null));
            }
        }
        finally
        { if (temporary is not null) { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); } }
        var relations = ReadOfficerDetailRelationsCurrent(state, current, physical);
        foreach (string issue in relations.UnresolvedOwners) issues.Add(issue);
        if (card.ForceFlag.UnresolvedInput is { } flagIssue) issues.Add(flagIssue);
        ushort flags = DetailNeed(current.ReadArmyReturnPersonFlags196(physical), "neutral.flags");
        return new(0, physical, "", card.Portrait, overview, military, relations,
            card.Abilities.ToImmutableArray(), traits.ToImmutable(), issues.ToImmutableArray())
        {
            IsNeutral = true,
            PageCount = K((flags & 0x4000) != 0 ? 2 : 3),
            StandingPortrait = K(new DeploymentNarrationPortraitRoute(2198, 2198,
                "texture_chara_anime_2198", ["face_2198_b", "face_2198_a", "face_2198_c"])
                { Kind = StandingPortraitKind.ThreePart }),
            ForceFlag = card.ForceFlag,
            Priority = ReadOfficerDetailPriorityCurrent(state, current, playerForceId, physical),
            MiniMap = K<DetailMiniMapValues?>(null)
        };

        OfficerDetailEntity? EmptyStaticEntity(int table, int lookup, string issue)
        {
            var reference = current.ReadStaticTableReference(table, lookup)
                ?? throw OfficerDetailMissing(issue + ".lookup");
            return DetailNeed(reference.PhysicalSlot, issue + ".receiver") == 0
                ? null : throw OfficerDetailMissing(issue);
        }
    }
}
