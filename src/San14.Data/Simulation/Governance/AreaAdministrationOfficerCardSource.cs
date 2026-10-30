using San14.Core;

namespace San14.Data;

/// <summary>Read-only current-person inputs for the region card. Resource
/// names and effective-ability services are created only when consumed.</summary>
internal sealed class AreaAdministrationOfficerCardSource(GameState state, SourceWorldContext current,
    ArmyOperationContext definitions, LoadedScenario scenario, string language, OwnedBaseCapacityInputs panels, GameResourceRepository repository,
    OwnedArmyGovernorComparatorInputs? personGovernor = null, bool? userSelectionServicePresent = null)
{
    private TacticRemakeCurrentAbilities? _abilities;
    private LoadedArmyFlagPaneNames? _flagNames;
    private ArmyMapUiSources? _portraitNames;
    private AreaAdministrationOfficerTraitTooltip? _traitTooltips;

    internal CityDomesticPersonCellSource CreatePersonCells() =>
        new(state, current, definitions, personGovernor, scenario: scenario, repository: repository, language: language,
            userSelectionServicePresent: userSelectionServicePresent);

    internal AreaAdministrationOfficerCard Read(int areaId, long revision, int physical, bool valid, int traitContext = 12) =>
        ReadCard(areaId, revision, physical, valid, traitContext);

    internal AreaAdministrationOfficerCard ReadCity(long revision, int physical, bool valid, int traitContext) =>
        ReadCard(null, revision, physical, valid, traitContext);

    //Transport mounts CUtilPInfo type8 with the independent trait context15.
    //Its normal effect column and dynamic kind3 flag are evaluated below.
    internal AreaAdministrationOfficerCard ReadTransport(long revision, int physical, bool valid) =>
        ReadCard(null, revision, physical, valid, 15);

    private AreaAdministrationOfficerCard ReadCard(int? areaId, long revision, int physical, bool valid, int? traitContext)
    {
        if (traitContext.HasValue && traitContext is not (0 or 10 or 12 or 13 or 14 or 15 or 17 or 18 or 19 or 21)) throw new ArgumentOutOfRangeException(nameof(traitContext));
        int? id = null; OfficerState? owner = null;
        if (valid)
        {
            id = current.ReadPersonIdentity(physical) ?? throw Missing("person_identity");
            owner = state.Officers.GetValueOrDefault(id.Value) ?? throw Missing("person_owner");
            if (definitions.ResolveOfficerSource(id.Value) != physical) throw Missing("person_owner_binding");
        }
        var abilities = ReadAbilities(owner, id, valid);
        return new(areaId, revision, id, owner?.Name ?? string.Empty, abilities,
            Portrait(id, physical, owner),
            valid ? traitContext is int context ? Traits(physical, context)
                : AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown("transport.setup.traits-context15-table-and-kind3")
                : AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Known(Array.Empty<AreaAdministrationOfficerTrait>()),
            Flag(valid ? physical : null));
    }

    internal IReadOnlyList<AreaAdministrationOfficerAbility> ReadAbilities(OfficerState? owner, int? id, bool valid)
    {
        var abilities = new List<AreaAdministrationOfficerAbility>(5);
        for (int dimension = 0; dimension < 5; dimension++)
        {
            var basic = !valid ? Known(0) : Base(owner!, dimension);
            var effective = !valid ? Known(0) : Effective(id!.Value, dimension);
            abilities.Add(new(dimension, basic, effective));
        }
        return abilities.AsReadOnly();
    }
    internal AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>> ReadDeploymentTraits(int physical) =>
        Traits(physical, 0, 9, true);

    private static AreaAdministrationQuery<int?> Base(OfficerState owner, int dimension)
    {
        int? value = owner.BaseAbilities is not { } a ? null : dimension switch
        { 0 => a.Leadership, 1 => a.Strength, 2 => a.Intelligence, 3 => a.Politics, _ => a.Charm };
        return value is >= 0 and <= 255 ? Known(value.Value) : Unknown<int?>("base_ability." + dimension);
    }
    private AreaAdministrationQuery<int?> Effective(int id, int dimension)
    {
        try
        {
            _abilities ??= new(current, scenario, language, SimulationRuleIdentity.ForLocale(language));
            int? value = _abilities.ResolveDimension(state, id, dimension, (force, effect) => panels.ReadPanelModifier(state, force, effect));
            return value is int known ? Known(known) : Unknown<int?>("effective_ability." + dimension);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<int?>.Unknown(unknown.Message); }
    }

    private OfficerPortraitRoute Portrait(int? id, int physical, OfficerState? owner)
    {
        _portraitNames ??= ArmyMapUiSources.Create();
        if (id is null)
            //8A59E0 passes2198 to bank10 even for an invalid person. This is
            //different from deployment85A230's invalid-person Hidden gate.
            return ArmyCampaignRuleAdapter.ReadOfficerPortraitAppearance(state, current, definitions, 0, 2198, _portraitNames);
        if (owner!.PortraitAppearanceIndex is not ushort appearance)
            return Unresolved("appearance");
        if (current.ReadPersonPortraitAppearance(physical) != appearance)
            return Unresolved("appearance_binding");
        return ArmyCampaignRuleAdapter.ReadOfficerPortraitAppearance(state, current, definitions, id.Value, appearance, _portraitNames);
        OfficerPortraitRoute Unresolved(string input) => new(id.Value, OfficerPortraitRouteKind.Unresolved,
            owner.PortraitAppearanceIndex, null, null, null, null, Missing(input).Message);
    }

    private AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>> Traits(int physical, int traitContext, int slotCount = 5, bool requireNativeValidity = false)
    {
        try
        {
            if (language is not ("TC" or "SC"))
                throw Missing("traits_context0_selected_edition_table");
            var result = new List<AreaAdministrationOfficerTrait>(slotCount);
            for (int slot = 0; slot < slotCount; slot++)
            {
                int lookup = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, slot)
                    ?? throw Missing("trait_slot." + slot);
                if (lookup is < 1 or > 355) continue;
                var trait = current.ReadPersonalityTableReference(lookup) ?? throw Missing("trait_pointer");
                int identity = current.ReadPersonalityPhysicalSlot(trait) ?? throw Missing("trait_identity");
                //89CB70 context12 does not add a trait-validity gate. In
                //particular B6=0 can still admit a matching second effect.
                byte first = current.ReadPersonalityByte(trait, 0xB6) ?? throw Missing("trait_first_effect");
                if (!MatchesCurrent(first, traitContext, identity, trait))
                {
                    byte second = current.ReadPersonalityByte(trait, 0xBA) ?? throw Missing("trait_second_effect");
                    if (!MatchesCurrent(second, traitContext, identity, trait)) continue;
                }
                if (requireNativeValidity && !(current.ReadSkillPersonalityValidity(trait) ?? throw Missing("trait_native_validity"))) continue;
                byte style = current.ReadPersonalityByte(trait, 0xBE) ?? throw Missing("trait_style");
                result.Add(new(identity, current.ReadPersonalityName(trait), style) { LookupTraitId = lookup });
            }
            return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Known(result.AsReadOnly());
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown(unknown.Message); }
    }
    private bool MatchesCurrent(byte effect, int context, int personalityPhysical, PersonalityReference trait)
    {
        if (context != 15) return Matches(effect, context, personalityPhysical);
        //TC/SC context15 admits these immutable effect codes first. Only
        //a normal effect whose table entry failed reaches dynamic kind3.
        if (effect is < 1 or > 126) return false;
        if (effect is 32 or 94 or 102) return true;
        byte flags = current.ReadSkillPersonalityFlags(trait) ?? throw Missing("trait_kind3_flags");
        return (flags & 8) != 0;
    }

    internal AreaAdministrationQuery<string?> ReadTraitTooltip(int lookupTraitId) =>
        (_traitTooltips ??= new(current, repository, language)).Read(lookupTraitId);

    //Immutable CE context12 column, paired in TC/SC. Source addresses stay in
    //offline evidence; the game consumes this finite rule and current effects.
    private static bool Matches(byte effect, int context,int personalityPhysical) => context switch
    {
        //The independently extracted TC/SC context0 columns admit
        //effects1..126 except106. No display-name inference is involved.
        0 => effect is >= 1 and <= 126 && effect != 106,
        10 => effect == 64, 12 => Context12(effect), 13 => effect == 17, 14 => effect == 16,
        // Immutable TC/SC columns18/19/21. Context19 additionally admits
        // physical Personality88, but only inside the source effect1..126 gate.
        17 => effect is 113 or 114,
        18 => effect == 119,
        19 => effect is 120 or 121 || effect is >=1 and <=126 && personalityPhysical==88,
        21 => false,
        _ => false
    };
    private static bool Context12(byte effect) => effect is 15 or >= 33 and <= 42 or 100 or 123 or 125 or 126;

    private AreaAdministrationQuery<AreaAdministrationOfficerFlag> Flag(int? physical)
    {
        try
        {
            var force = physical is int person
                ? current.ReadPersonForce(person) ?? throw Missing("person_force")
                : current.ReadForceTableReference(0) ?? throw Missing("empty_force_pointer");
            bool valid = physical is not null && (current.ReadForceNativeValidity(force) ?? throw Missing("force_validity"));
            var fillForce = valid ? force : current.ReadForceTableReference(0) ?? throw Missing("empty_force_pointer");
            byte color = current.ReadForceReportColor(fillForce) ?? throw Missing("force_color");
            var fill = MapSpritePalettes.ForceFill(color);
            if (!valid) return AreaAdministrationQuery<AreaAdministrationOfficerFlag>.Known(new(0, "", fill, null));
            int id = force.PhysicalSlot ?? throw Missing("force_identity");
            if (id >= 46) throw Missing("special_force_flag");
            _flagNames ??= ArmyFlagPaneNames.Load(scenario);
            var selection = _flagNames.Resolve(current, force);
            string glyph = selection.NativeShortName;
            if (selection.SelectionPath == "ordinary person prefix")
            {
                int rulerPhysical = selection.SelectedPersonPhysicalSlot ?? throw Missing("force_ruler_pointer");
                int rulerId = current.ReadPersonIdentity(rulerPhysical) ?? throw Missing("force_ruler_identity");
                var ruler = state.Officers.GetValueOrDefault(rulerId) ?? throw Missing("force_ruler_owner");
                if (definitions.ResolveOfficerSource(rulerId) != rulerPhysical) throw Missing("force_ruler_binding");
                if (ruler.Name.Length == 0 || ruler.Name[0] == '\0') throw Missing("force_empty_name_suffix");
                glyph = ruler.Name[..1]; //22F060 prefix precedes309700's suffix.
            }
            return AreaAdministrationQuery<AreaAdministrationOfficerFlag>.Known(new(id, glyph, fill, MapSpritePalettes.Create().ForceLetter(color)));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationOfficerFlag>.Unknown(unknown.Message); }
    }

    private static AreaAdministrationQuery<int?> Known(int value) => AreaAdministrationQuery<int?>.Known(value);
    private static AreaAdministrationQuery<T> Unknown<T>(string input) => AreaAdministrationQuery<T>.Unknown(Missing(input).Message);
    private static InvalidDataException Missing(string input) => new("area-draft.card_" + input);
}
