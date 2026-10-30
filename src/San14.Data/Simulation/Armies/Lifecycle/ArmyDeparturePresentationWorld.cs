using San14.Core;

namespace San14.Data;

public sealed record ArmyDepartureFlagInputs(int ForcePhysical, byte? Color49,
    ArmyFlagPaneNameSelection BannerName, ArmyFlagPaneNameSelection? CarriedKind2Name,
    string? CarriedKind2Unavailable);

/// <summary>A single departure's presentation projection. Other campaign
/// owners are borrowed; only the departing unit and its receiver are projected.
/// The prepared fields were evaluated at departure, before later writers.</summary>
public sealed record ArmyDeparturePresentationWorld(GameState World,
    ArmyGraphicsCurrentInputs? GraphicsInputs, ArmyMapUiCurrentFrame UiFields,
    ArmySimpleOutlineResult? Outline, ArmyDepartureFlagInputs? FlagInputs)
{
    public IReadOnlyList<ArmyDeparturePersonalitySnapshot> Personalities { get; init; } = [];
    public string? GraphicsUnavailable { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private LoadedArmyFlagPaneNames? departureFlagNames;

    /// <summary>Reached personality-view refresh only. Uses the same current
    /// receiver, commander and ordered related-army lists as the business world.</summary>
    public IReadOnlyList<ArmyDeparturePersonalitySnapshot> QueryArmyMapPersonalities(GameState state, int armyId)
    {
        var army = RequireArmyMapPersonalitySource(state, armyId);
        var owned = CaptureOwnedArmyReturnWorld(state);
        return CaptureDeparturePersonalities(state, owned.Context, army);
    }

    internal ArmyDeparturePresentationRequest CaptureArmyDeparturePresentationCurrent(GameState state,
        SourceWorldContext current, UnitState army, int appearance, bool reconcile, bool controller)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw DepartureMissing("source_physical");
        var lifecycle = state.ReadArmyLifecycle() ?? throw DepartureMissing("source_lifecycle");
        var receiver = lifecycle.ReceiverSlots()[physical];
        if (receiver.ActiveUnitId != army.Id || receiver.Commander12 is not ushort commander ||
            receiver.Status10 is not byte status)
            throw DepartureMissing("source_receiver");
        float? mobility = null; string? mobilityMissing = null;
        if (reconcile)
        {
            graphicsMotionMobility ??= new(this, _mobilitySeed, _map, _terrain, _catalog);
            var resolved = graphicsMotionMobility.Resolve(state, army, current);
            mobility = resolved.Receipt?.Value;
            mobilityMissing = resolved.UnresolvedOwner;
        }
        var single = state with { Units = new() { [army.Id] = army } };
        ArmyDepartureLabelSnapshot? label = null;
        ArmyDepartureOutlineSnapshot? outline = null;
        var unavailable = Array.Empty<string>();
        if (controller)
        {
            var frame = new ArmyMapUiCurrentSource(_scenario.Snapshot, _terrain).Capture(single, current);
            var row = frame.Rows.Single();
            unavailable = frame.Unavailable.Select(item => item.Field)
                .Where(field => field != "current.Person_appearanceA6").ToArray();
            byte? district = row.PersonValid == false ? (byte)0 : row.PersonValid == true ? row.PersonDistrictE0 : null;
            if (district is byte raw)
            {
                var style = ArmySimpleOutlineSource.ReadOrdinaryMap(state, current,
                    current.ReadDistrictTableReference(raw <= 51 ? raw : 0));
                if (style.Selection is { } selection)
                    outline = new(selection.UsesOutline, (int)selection.Primary.Kind, selection.Primary.Value,
                        selection.Primary.RgbMultiplier, (int)selection.Disabled.Kind, selection.Disabled.Value,
                        selection.Disabled.RgbMultiplier);
                else unavailable = unavailable.Append(style.Missing ?? "army-departure.source_outline").ToArray();
            }
            label = CaptureDepartureLabel(row with { AppearanceA6 = checked((ushort)appearance) })
                with { MissingFields = unavailable };
        }
        ArmyDepartureFlagSnapshot? flag = null;
        if (reconcile)
        {
            departureFlagNames ??= ArmyFlagPaneNames.Load(_scenario);
            var force = current.ReadArmyUnitForceReference(new(physical)) ?? throw DepartureMissing("source_flag_force");
            int forcePhysical = force.PhysicalSlot ?? throw DepartureMissing("source_flag_force_physical");
            var banner = departureFlagNames.Resolve(current, force);
            string? carried = null; string? carriedMissing = null; bool? carriedValid = null;
            try
            {
                var selected = departureFlagNames.ResolveCarriedKind2(current);
                carried = selected.SourceTextBeforeSingleUnitCopy; carriedValid = selected.ForceValidity;
            }
            catch (InvalidDataException error) { carriedMissing = error.Message; }
            flag = new(forcePhysical, banner.CurrentForceColor49, banner.SourceTextBeforeSingleUnitCopy,
                carried, carriedMissing) { BannerForceValid = banner.ForceValidity, CarriedForceValid = carriedValid };
        }
        return new(army, appearance, reconcile, controller)
        {
            SourceSnapshot = new(receiver.ReceiverId, commander, status, mobility, label, outline, flag)
            {
                GraphicsUnavailable = mobilityMissing,
                CurrentHex = ArmyMarchSourceAdapter.ReadCreatedCurrentHex(state, army),
                Direction = army.NativeDirection36,
                ArmyValid = commander != 0 && status != 0,
                Flags = army.NativeTacticRuntime?.Flags26,
                Abnormal = army.NativeTacticRuntime?.Abnormal23,
                Personalities = controller ? CaptureDeparturePersonalities(state, current, army) : []
            }
        };
    }

    public ArmyDeparturePresentationWorld PrepareArmyDeparturePresentationWorld(GameState current,
        ArmyDeparturePresentationRequest request)
    {
        var source = request.SourceSnapshot ?? throw DepartureMissing("source_snapshot");
        var army = request.ArmyAtSource;
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw DepartureMissing("source_physical");
        var lifecycle = current.ReadArmyLifecycle() ?? throw DepartureMissing("presentation_lifecycle");
        var before = lifecycle.Receivers.Find(source.ReceiverId) ?? throw DepartureMissing("presentation_receiver_identity");
        var slots = lifecycle.Slots.ToArray();
        // The source header is a captured witness. No later ActiveUnitId or
        // commander is used to authenticate a retired/reused receiver.
        slots[physical] = new(physical, source.CommanderId, source.Status, null)
            { ReceiverId = source.ReceiverId };
        var units = new Dictionary<int, UnitState>(current.Units);
        if (lifecycle.ReceiverSlots()[physical].ActiveUnitId is int replacement && replacement != army.Id)
            units.Remove(replacement);
        units[army.Id] = army;
        var transient = (current with { Units = units }).WithArmyLifecycle(lifecycle with
        {
            Source = lifecycle.Source with { Slots = slots },
            Receivers = lifecycle.Receivers.With(before with
                { Phase = ArmyReceiverPhase.Active, ActiveUnitId = army.Id, PendingImage = null })
        });
        ArmyGraphicsCurrentInputs? graphics = source.Mobility is float mobility && source.Direction is byte direction &&
            source.CurrentHex is ushort hex && source.ArmyValid is bool valid && source.Flags is byte flags &&
            source.Abnormal is byte abnormal
            ? new(source.CommanderId, hex, direction, valid, flags, abnormal, mobility) : null;
        var fields = new ArmyMapUiCurrentFrame(source.Label is { } label ? [PrepareDepartureLabel(army.Id, label)] : [],
            (source.Label?.MissingFields ?? []).Select(field => new ArmyMapUiCurrentUnavailable(army.Id, field)).ToArray());
        var outline = source.Outline is { } style ? new ArmySimpleOutlineResult(new(style.UsesOutline,
            new((ArmySimpleOutlineColorKind)style.PrimaryKind, style.PrimaryValue, style.PrimaryMultiplier),
            new((ArmySimpleOutlineColorKind)style.DisabledKind, style.DisabledValue, style.DisabledMultiplier)), null) : null;
        var flag = source.Flag is { } f ? new ArmyDepartureFlagInputs(f.ForcePhysical, f.Color,
            PrepareDepartureFlagName(f.BannerText, f.ForcePhysical, f.Color, f.BannerForceValid),
            f.CarriedText is { } text && f.CarriedForceValid is bool carriedValid
                ? PrepareDepartureFlagName(text, 51, null, carriedValid) : null, f.CarriedMissing) : null;
        return new(transient, graphics, fields, outline, flag)
            { Personalities = source.Personalities, GraphicsUnavailable = source.GraphicsUnavailable };
    }

    private static ArmyFlagPaneNameSelection PrepareDepartureFlagName(string text, int force, byte? color, bool valid)
        => new(text, ArmyFlagPaneColors.CopySourceNameGlyph(text, "captured Army departure name"),
            valid, force, null, null, null, null, null, color,
            "captured Army departure name", "actual departure Force name selection");

    private static ArmyDepartureLabelSnapshot CaptureDepartureLabel(ArmyMapUiCurrentRow row) => new(row.UnitId)
    {
        ArmyPhysicalSlot = row.ArmyPhysicalSlot, ArmyValid = row.ArmyValid, Commander12 = row.Commander12,
        Ready16 = row.Ready16, Morale1A = row.Morale1A, MoraleCapacity = row.MoraleCapacity,
        EffectiveMorale = row.EffectiveMorale, PersonPhysicalSlot = row.PersonPhysicalSlot,
        PersonValid = row.PersonValid, AppearanceA6 = row.AppearanceA6, PersonDistrictE0 = row.PersonDistrictE0,
        DistrictPhysicalSlot = row.DistrictPhysicalSlot, DistrictValid = row.DistrictValid,
        DistrictNumber11 = row.DistrictNumber11, DistrictPredicate233A90 = row.DistrictPredicate233A90,
        ForcePhysicalSlot = row.ForcePhysicalSlot, SourceForceColor49 = row.SourceForceColor49,
        IsCurrentPlayerForce = row.IsCurrentPlayerForce, Army13C = row.Army13C,
        SelectedFormation = row.SelectedFormation, FormationClass7E = row.FormationClass7E,
        IconSpriteId = row.Icon?.SpriteId, IconPalette = row.Icon?.Palette
    };

    private static ArmyMapUiCurrentRow PrepareDepartureLabel(int id, ArmyDepartureLabelSnapshot row) => new(id)
    {
        ArmyPhysicalSlot = row.ArmyPhysicalSlot, ArmyValid = row.ArmyValid, Commander12 = row.Commander12,
        Ready16 = row.Ready16, Morale1A = row.Morale1A, MoraleCapacity = row.MoraleCapacity,
        EffectiveMorale = row.EffectiveMorale, PersonPhysicalSlot = row.PersonPhysicalSlot,
        PersonValid = row.PersonValid, AppearanceA6 = row.AppearanceA6, PersonDistrictE0 = row.PersonDistrictE0,
        DistrictPhysicalSlot = row.DistrictPhysicalSlot, DistrictValid = row.DistrictValid,
        DistrictNumber11 = row.DistrictNumber11, DistrictPredicate233A90 = row.DistrictPredicate233A90,
        ForcePhysicalSlot = row.ForcePhysicalSlot, SourceForceColor49 = row.SourceForceColor49,
        IsCurrentPlayerForce = row.IsCurrentPlayerForce, Army13C = row.Army13C,
        SelectedFormation = row.SelectedFormation, FormationClass7E = row.FormationClass7E,
        Icon = row.IconSpriteId is int sprite && row.IconPalette is int palette ? new(sprite, palette) : null
    };

    private static ArmyDeparturePersonalitySnapshot[] CaptureDeparturePersonalities(GameState state,
        SourceWorldContext current, UnitState army)
    {
        int person = current.ResolveOfficerSource(army.OfficerId) ?? throw DepartureMissing("source_personality_person");
        byte formation = ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army)
            ?? throw DepartureMissing("source_personality_formation");
        int filter = formation is 19 or 20 ? 8 : 2;
        var result = new List<ArmyDeparturePersonalitySnapshot>(9);
        int admitted = 0;
        for (int index = 8; index >= 0; index--)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, index)
                ?? throw DepartureMissing("source_personality_lookup");
            bool valid = current.ReadSkillPersonalityValidity(trait) ?? throw DepartureMissing("source_personality_validity");
            int identity = trait.PhysicalSlot ?? 0;
            if (!valid) { result.Add(new(index, identity, false, false, false, null, null)); continue; }
            byte flags = current.ReadSkillPersonalityFlags(trait) ?? throw DepartureMissing("source_personality_flags");
            bool eligible = (flags & filter) != 0;
            if (!eligible) { result.Add(new(index, identity, true, false, false, null, null)); continue; }
            if (admitted++ >= 5)
            { result.Add(new(index, identity, true, true, null, null, null)); continue; }
            bool active = false;
            var relatedArmy = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                ?? throw DepartureMissing("source_personality_commander_army");
            bool relatedValid = current.ReadArmyUnitNativeValidity(relatedArmy)
                ?? throw DepartureMissing("source_personality_commander_army_validity");
            if (relatedValid)
            {
                //2308D0 selects the first matching commander slot, including
                //duplicates, then reads that Army's descriptor count.
                int first = 0;
                while (first < 9)
                {
                    int lookup = TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, first)
                        ?? throw DepartureMissing("source_personality_related_lookup");
                    if (lookup == identity) break;
                    first++;
                }
                if (first < 9)
                {
                    var fields = current.ReadArmyReturnTraitFields(relatedArmy)
                        ?? throw DepartureMissing("source_personality_related_lists");
                    int count = current.ReadTemporaryHexListCount(new(fields.Descriptors160[first].Identity))
                        ?? throw DepartureMissing("source_personality_related_list_count");
                    active = count > 0;
                }
            }
            if (!active)
            {
                ushort condition = current.ReadPersonalityWord(trait, 0xB4)
                    ?? throw DepartureMissing("source_personality_condition");
                active = condition == ushort.MaxValue ||
                    ((current.ReadPersonTraitMask1BC(person) ?? throw DepartureMissing("source_personality_active_mask")) & (1 << index)) != 0;
            }
            byte style = current.ReadPersonalityByte(trait, 0xBE) ?? throw DepartureMissing("source_personality_style");
            result.Add(new(index, identity, true, true, active, style, null));
        }
        return result.ToArray();
    }
}
