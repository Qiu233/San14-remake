using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Only the selected-commander transition calls this preparation.
    /// The caller supplies the committed application stream and retains the
    /// returned speech across redraws; no simulation or CRT random is used.</summary>
    public AreaAdministrationQuery<DeploymentUnitSpeechPublication> PrepareDeploymentUnitSpeech(
        GameState state, int officerId, string locale, Func<int, int> appPresentationRandom)
    {
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        try
        {
            if (locale is not ("TC" or "SC") || state.NativeArmySimulation?.Parameters is not { } parameters ||
                parameters.Language != Seed.Language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw UnitPaneMissing("speech_rule_profile");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            int physical = UnitPanePerson(current, state, officerId);
            var candidates = new List<int>(5);
            for (int slot = 0; slot < 9; slot++)
            {
                //2307D0 first collects valid results of230730 in raw slot
                //order.7D7F40 then applies the speech bit/custom exclusion.
                int lookup = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, slot)
                    ?? throw UnitPaneMissing("speech_trait_slot." + slot);
                var trait = current.ReadPersonalityTableReference(lookup <= 355 ? lookup : 0)
                    ?? throw UnitPaneMissing("speech_trait_pointer");
                if (!(current.ReadSkillPersonalityValidity(trait) ?? throw UnitPaneMissing("speech_trait_validity"))) continue;
                int identity = current.ReadPersonalityPhysicalSlot(trait) ?? throw UnitPaneMissing("speech_trait_identity");
                byte flags = current.ReadSkillPersonalityFlags(trait) ?? throw UnitPaneMissing("speech_trait_flags");
                if (identity is not (>= 256 and <= 355) && (flags & 2) != 0) candidates.Add(identity);
            }
            int selected = candidates.Count == 0 ? -1 : appPresentationRandom(candidates.Count);
            if (selected < -1 || selected >= candidates.Count || candidates.Count > 0 && selected < 0)
                throw UnitPaneMissing("speech_random_range");
            int? traitId = selected >= 0 ? candidates[selected] : null;
            int message = traitId is int chosen ? UnitSpeechMessage(chosen) : 0x53EE;
            _personRelationInputs.BindCurrent(state, current);
            current.EventConditions.CurrentYear = state.NativeMapSeason?.ThirdDate.Year;
            current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion);
            //The original binds Person0 only. It does not substitute the
            //ruler, an arbitrary addressee, an Army, or an Area argument.
            var context = new DeploymentNarrationCurrentPeople(state, current, abilities, officerId, null,
                panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
            string line = new NarrationResourceMessages(_repository.LoadMessages(locale, MessageKind.Game).Messages)
                .ResolveDeploymentUnitSpeech(context, appPresentationRandom, message);
            return AreaAdministrationQuery<DeploymentUnitSpeechPublication>.Known(new(officerId, traitId,
                selected, candidates.Count, message, line));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DeploymentUnitSpeechPublication>.Unknown(error.Message); }
    }

    /// <summary>The unit tab's context1 traits, compacted in source slot order.
    /// The current selected land formation owns the land/water filter.</summary>
    public AreaAdministrationQuery<DeploymentUnitPanePublication> QueryDeploymentUnitPane(
        GameState state, int officerId, int landFormationId)
    {
        try
        {
            if (Seed.Language is not ("TC" or "SC")) throw UnitPaneMissing("locale");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            int physical = UnitPanePerson(current, state, officerId);
            byte flags = TacticRemakeCurrentFormations.ReadParameter(state, landFormationId, 0xA6)
                ?? throw UnitPaneMissing("formation_flags");
            var rows = new List<AreaAdministrationOfficerTrait>(5);
            for (int slot = 0; slot < 9; slot++)
            {
                int lookup = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, slot)
                    ?? throw UnitPaneMissing("trait_slot." + slot);
                if (lookup is < 1 or > 355) continue;
                var trait = current.ReadPersonalityTableReference(lookup) ?? throw UnitPaneMissing("trait_pointer");
                byte first = current.ReadPersonalityByte(trait, 0xB6) ?? throw UnitPaneMissing("trait_first_effect");
                if (!UnitContext1Effect(first))
                {
                    byte second = current.ReadPersonalityByte(trait, 0xBA) ?? throw UnitPaneMissing("trait_second_effect");
                    if (!UnitContext1Effect(second)) continue;
                }
                if (!(current.ReadSkillPersonalityValidity(trait) ?? throw UnitPaneMissing("trait_validity"))) continue;
                byte condition = current.ReadPersonalityByte(trait, 0xA4) ?? throw UnitPaneMissing("trait_condition");
                if (condition == ((flags & 2) == 0 ? 12 : 37)) continue;
                int identity = current.ReadPersonalityPhysicalSlot(trait) ?? throw UnitPaneMissing("trait_identity");
                byte style = current.ReadPersonalityByte(trait, 0xBE) ?? throw UnitPaneMissing("trait_style");
                rows.Add(new(identity, current.ReadPersonalityName(trait), style) { LookupTraitId = lookup });
            }
            var cards = new AreaAdministrationOfficerCardSource(state, current, _definitions, _scenario,
                Seed.Language, _baseCapacityInputs, _repository);
            return AreaAdministrationQuery<DeploymentUnitPanePublication>.Known(new(officerId, landFormationId,
                rows.AsReadOnly()) { TraitTooltip = cards.ReadTraitTooltip });
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DeploymentUnitPanePublication>.Unknown(error.Message); }
    }

    // The immutable TC/SC context1 column, effect1..126. No per-frame original
    // executable or display-name inference participates in the runtime query.
    private static bool UnitContext1Effect(byte effect) => effect is
        >= 1 and <= 14 or >= 19 and <= 31 or 47 or 49 or >= 51 and <= 62 or
        75 or >= 77 and <= 82 or >= 88 and <= 93 or 95 or 96 or 98 or 99 or
        102 or 103 or >= 108 and <= 112 or >= 115 and <= 118 or 124;

    //The original finite trait-to-Game-message table, in its declaration order.
    private static int UnitSpeechMessage(int trait) => trait switch
    {
        0 => 0x53EE,
        1 => 0x5468,
        2 => 0x5476,
        3 => 0x5484,
        4 => 0x53FC,
        5 => 0x5413,
        6 => 0x5427,
        7 => 0x543B,
        8 => 0x544D,
        9 => 0x545D,
        10 => 0x5492,
        11 => 0x5498,
        12 => 0x549E,
        13 => 0x54A4,
        14 => 0x54A8,
        15 => 0x54AC,
        16 => 0x54B2,
        17 => 0x54B7,
        18 => 0x54BC,
        19 => 0x54C1,
        20 => 0x54C5,
        21 => 0x54C9,
        22 => 0x54CC,
        23 => 0x54D2,
        24 => 0x54D5,
        25 => 0x54DA,
        26 => 0x54DD,
        27 => 0x54E1,
        28 => 0x54E4,
        29 => 0x54E7,
        30 => 0x54EA,
        31 => 0x54ED,
        32 => 0x54F0,
        33 => 0x54F3,
        34 => 0x54F6,
        35 => 0x54F9,
        36 => 0x5500,
        37 => 0x5504,
        38 => 0x5508,
        39 => 0x550B,
        40 => 0x550E,
        41 => 0x5511,
        42 => 0x5515,
        43 => 0x5519,
        44 => 0x551C,
        45 => 0x551F,
        46 => 0x5522,
        47 => 0x5525,
        48 => 0x5526,
        49 => 0x552A,
        50 => 0x552D,
        51 => 0x5530,
        52 => 0x5531,
        53 => 0x5535,
        54 => 0x5539,
        55 => 0x553D,
        56 => 0x5540,
        57 => 0x5543,
        58 => 0x5546,
        59 => 0x554A,
        60 => 0x554D,
        61 => 0x5550,
        62 => 0x5553,
        63 => 0x555A,
        64 => 0x555D,
        65 => 0x5560,
        66 => 0x5564,
        67 => 0x5567,
        68 => 0x556B,
        69 => 0x5570,
        70 => 0x5573,
        71 => 0x5577,
        72 => 0x557A,
        73 => 0x557D,
        74 => 0x5580,
        75 => 0x5583,
        76 => 0x5586,
        77 => 0x5589,
        78 => 0x558C,
        80 => 0x558F,
        81 => 0x5592,
        84 => 0x5596,
        85 => 0x559A,
        86 => 0x559D,
        87 => 0x55A1,
        88 => 0x55A5,
        90 => 0x554D,
        91 => 0x55AA,
        92 => 0x55AB,
        93 => 0x55AF,
        94 => 0x55B3,
        95 => 0x55B6,
        96 => 0x55B7,
        97 => 0x55B8,
        98 => 0x55BC,
        99 => 0x55BF,
        105 => 0x55C0,
        106 => 0x55C4,
        107 => 0x55C7,
        108 => 0x55CB,
        142 => 0x55CE,
        143 => 0x55D1,
        144 => 0x55D4,
        150 => 0x55D8,
        151 => 0x55DC,
        152 => 0x55E0,
        153 => 0x55E4,
        154 => 0x55E8,
        155 => 0x55EB,
        156 => 0x55EE,
        157 => 0x55F1,
        158 => 0x55F4,
        159 => 0x55F7,
        160 => 0x55FA,
        161 => 0x5618,
        162 => 0x561B,
        163 => 0x561E,
        164 => 0x5621,
        166 => 0x5627,
        167 => 0x562A,
        168 => 0x562D,
        169 => 0x5636,
        170 => 0x5639,
        171 => 0x563C,
        172 => 0x563F,
        173 => 0x5651,
        174 => 0x5652,
        177 => 0x5653,
        178 => 0x5654,
        183 => 0x5630,
        184 => 0x5633,
        187 => 0x5655,
        188 => 0x5658,
        189 => 0x565B,
        190 => 0x565E,
        191 => 0x5642,
        192 => 0x5645,
        193 => 0x5648,
        194 => 0x564B,
        195 => 0x564E,
        197 => 0x560F,
        198 => 0x5612,
        199 => 0x5615,
        202 => 0x55FD,
        203 => 0x5600,
        204 => 0x5603,
        205 => 0x5606,
        206 => 0x5609,
        207 => 0x560C,
        210 => 0x5661,
        216 => 0x5662,
        217 => 0x5665,
        _ => 0x53EE
    };

    private int UnitPanePerson(SourceWorldContext current, GameState state, int officerId)
    {
        int physical = current.ResolveOfficerSource(officerId) ?? throw UnitPaneMissing("person_receiver");
        if (current.ReadPersonIdentity(physical) != officerId || current.ReadPersonNativeValidity(physical) != true ||
            _definitions.ResolveOfficerSource(officerId) != physical || !state.Officers.ContainsKey(officerId))
            throw UnitPaneMissing("person_owner");
        return physical;
    }
    private static InvalidDataException UnitPaneMissing(string input) => new("deployment.unit-pane." + input);
}

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveDeploymentUnitSpeech(INarrationCurrentContext context, Func<int, int> drawApp32, int root)
    {
        _prefix = "deployment.unit-pane.speech";
        bool previous = _report; _report = true;
        try
        {
            //The source message formatter follows the selected Game record's
            //references normally. This consumer supplies only Person0; an
            //unbound operand still fails through the ordinary context reader.
            return ExecuteMessage(context, drawApp32, root, Get(root, true), _ => false, true, null,
                _ => true, (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }
}
