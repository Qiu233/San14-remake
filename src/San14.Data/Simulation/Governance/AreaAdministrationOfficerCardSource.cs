using San14.Core;

namespace San14.Data;

/// <summary>Read-only current-person inputs for the region card. Resource
/// names and effective-ability services are created only when consumed.</summary>
internal sealed class AreaAdministrationOfficerCardSource(GameState state, SourceWorldContext current,
    ArmyOperationContext definitions, LoadedScenario scenario, string language, OwnedBaseCapacityInputs panels, GameResourceRepository repository)
{
    private TacticRemakeCurrentAbilities? _abilities;
    private LoadedArmyFlagPaneNames? _flagNames;
    private ArmyMapUiSources? _portraitNames;
    private AreaAdministrationOfficerTraitTooltip? _traitTooltips;

    internal AreaAdministrationOfficerCard Read(int areaId, long revision, int physical, bool valid, int traitContext = 12) =>
        ReadCard(areaId, revision, physical, valid, traitContext);

    internal AreaAdministrationOfficerCard ReadCity(long revision, int physical, bool valid, int traitContext) =>
        ReadCard(null, revision, physical, valid, traitContext);

    //Transport mounts CUtilPInfo type8, but its actual traitContext is15.
    //The context15 table plus16F880(personality,3) leaf is not the region12
    //filter. Preserve that group as Unknown while sharing other real fields.
    internal AreaAdministrationOfficerCard ReadTransport(long revision, int physical, bool valid) =>
        ReadCard(null, revision, physical, valid, null);

    private AreaAdministrationOfficerCard ReadCard(int? areaId, long revision, int physical, bool valid, int? traitContext)
    {
        if (traitContext.HasValue && traitContext is not (0 or 10 or 12 or 13 or 14)) throw new ArgumentOutOfRangeException(nameof(traitContext));
        int? id = null; OfficerState? owner = null;
        if (valid)
        {
            id = current.ReadPersonIdentity(physical) ?? throw Missing("person_identity");
            owner = state.Officers.GetValueOrDefault(id.Value) ?? throw Missing("person_owner");
            if (definitions.ResolveOfficerSource(id.Value) != physical) throw Missing("person_owner_binding");
        }
        var abilities = new List<AreaAdministrationOfficerAbility>(5);
        for (int dimension = 0; dimension < 5; dimension++)
        {
            var basic = !valid ? Known(0) : Base(owner!, dimension);
            var effective = !valid ? Known(0) : Effective(id!.Value, dimension);
            abilities.Add(new(dimension, basic, effective));
        }
        return new(areaId, revision, id, owner?.Name ?? string.Empty, abilities.AsReadOnly(),
            Portrait(id, physical, owner),
            valid ? traitContext is int context ? Traits(physical, context)
                : AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown("transport.setup.traits-context15-table-and-kind3")
                : AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Known(Array.Empty<AreaAdministrationOfficerTrait>()),
            Flag(valid ? physical : null));
    }

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

    private AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>> Traits(int physical, int traitContext)
    {
        try
        {
            if (traitContext == 0 && language != "TC")
                throw Missing("traits_context0_selected_edition_table");
            var result = new List<AreaAdministrationOfficerTrait>(5);
            for (int slot = 0; slot < 5; slot++)
            {
                int lookup = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, slot)
                    ?? throw Missing("trait_slot." + slot);
                if (lookup is < 1 or > 355) continue;
                var trait = current.ReadPersonalityTableReference(lookup) ?? throw Missing("trait_pointer");
                int identity = current.ReadPersonalityPhysicalSlot(trait) ?? throw Missing("trait_identity");
                //89CB70 context12 does not add a trait-validity gate. In
                //particular B6=0 can still admit a matching second effect.
                byte first = current.ReadPersonalityByte(trait, 0xB6) ?? throw Missing("trait_first_effect");
                if (!Matches(first, traitContext))
                {
                    byte second = current.ReadPersonalityByte(trait, 0xBA) ?? throw Missing("trait_second_effect");
                    if (!Matches(second, traitContext)) continue;
                }
                byte style = current.ReadPersonalityByte(trait, 0xBE) ?? throw Missing("trait_style");
                result.Add(new(identity, current.ReadPersonalityName(trait), style) { LookupTraitId = lookup });
            }
            return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Known(result.AsReadOnly());
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>.Unknown(unknown.Message); }
    }
    internal AreaAdministrationQuery<string?> ReadTraitTooltip(int lookupTraitId) =>
        (_traitTooltips ??= new(current, repository, language)).Read(lookupTraitId);

    //Immutable CE context12 column, paired in TC/SC. Source addresses stay in
    //offline evidence; the game consumes this finite rule and current effects.
    private static bool Matches(byte effect, int context) => context switch
    {
        //TC89CB70's immutable context0 column admits effects1..126
        //except106. The selected edition's other contexts are independent.
        0 => effect is >= 1 and <= 126 && effect != 106,
        10 => effect == 64, 12 => Context12(effect), 13 => effect == 17, 14 => effect == 16, _ => false
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
