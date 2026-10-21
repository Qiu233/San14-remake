using San14.Core;

namespace San14.Data;

public sealed record DetailForceAppearance(bool IsValid, string Glyph, AreaMarkerColor Fill,
    AreaMarkerColor Letter, string? DisplayName)
{
    public string? DisplayText => DisplayName is null ? null : DisplayName.Length == 0 ? "" : "■" + DisplayName;
}

/// <summary>One current Force detail publication. Null validity is unknown;
/// a known invalid ruler hides the nameplate, while an invalid flag leaves its name.</summary>
public sealed record DetailForceNameplate(bool? ForceValid, bool? RulerValid, int? RulerId,
    string? RulerName, bool? FlagValid, DetailForceAppearance? FlagAppearance);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One current capture serves all flags in a detail publication.
    /// Only existing raw placeholder values are bound; gameplay-only Person
    /// aftermath and Army command state are not required.</summary>
    public IReadOnlyDictionary<int, DetailForceAppearance?> ReadDetailForces(GameState state,
        LoadedArmyFlagPaneNames names, IEnumerable<int> forceIds) =>
        ReadDetailForces(state, CaptureDetailForceInputs(state), names, forceIds);

    public DetailForceAppearance? ReadCityDetailForce(GameState state, LoadedArmyFlagPaneNames names, int cityId)
    {
        var current = CaptureDetailForceInputs(state);
        int? force = current?.ReadBaseForceSlot(new(BaseKind.City, cityId));
        if (force is not int rawForce) return null;
        int id = (uint)rawForce < 52 ? rawForce : 0;
        return ReadDetailForces(state, current, names, [id]).GetValueOrDefault(id);
    }

    public DetailForceNameplate ReadForceDetailNameplate(GameState state, LoadedArmyFlagPaneNames names, int forceId)
    {
        if ((uint)forceId >= 52)
            throw new InvalidDataException("Detail force identity is outside the source table.");
        var current = CaptureDetailForceInputs(state);
        var force = current?.ReadForceTableReference(forceId);
        bool? forceValid = force is null ? null : current!.ReadForceNativeValidity(force);
        var result = new DetailForceNameplate(forceValid, null, null, null, null, null);
        if (forceValid is not true) return result;

        ushort? ruler = current!.ReadForceRecord(force!.PhysicalSlot!.Value)?.RawRuler10;
        if (ruler is not ushort rawRuler) return result;
        var person = rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler)
            : current.ReadTransferPersonTableReference(0);
        if (person is null) return result;
        if (person.PhysicalSlot is not int physicalPerson)
            return result with { RulerValid = false };
        bool? personValid = current.ReadPersonNativeValidity(physicalPerson);
        result = result with { RulerValid = personValid };
        if (personValid is not true) return result;

        ushort? identity = current.ReadPersonIdentity(physicalPerson);
        result = result with
        {
            RulerId = identity,
            RulerName = identity is ushort rulerId ? state.Officers.GetValueOrDefault(rulerId)?.Name : null
        };
        // 22CB40 returns the current Person's District -> Force reference.
        // Keep it resolved: its physical slot is not a second lookup-table key.
        var flagForce = current.ReadPersonForce(physicalPerson);
        if (flagForce is null) return result;
        bool? flagValid = current.ReadForceNativeValidity(flagForce);
        result = result with { FlagValid = flagValid };
        if (flagValid is not true) return result;

        int flagPhysical = flagForce.PhysicalSlot!.Value;
        // The existing ordinary appearance consumer does not close special
        // forces. Missing flag inputs must not discard a known ruler name.
        if (flagPhysical >= 46 || current.StaticSource is null ||
            current.ReadForceKingdomNameIndex(flagPhysical) is not byte nameIndex) return result;
        if (nameIndex == 0 || nameIndex > 100)
        {
            ushort? flagRuler = current.ReadForceRecord(flagPhysical)?.RawRuler10;
            if (flagRuler is not ushort flagRulerId) return result;
            var fallbackPerson = flagRulerId <= 6000 ? current.ReadPersonLookupReference(flagRulerId)
                : current.ReadTransferPersonTableReference(0);
            if (fallbackPerson is null) return result;
            if (fallbackPerson.PhysicalSlot is int fallbackPhysical)
            {
                bool? fallbackValid = current.ReadPersonNativeValidity(fallbackPhysical);
                if (fallbackValid is null) return result;
                if (fallbackValid.Value)
                {
                    var retained = names.Source.Snapshot.Characters[fallbackPhysical];
                    // The retained glyph-name consumer cannot supply a changed
                    // Person identity or the unclosed empty-name suffix path.
                    if (current.ReadPersonIdentity(fallbackPhysical) != retained.CharacterId ||
                        retained.Name.Length == 0 || retained.Name[0] == '\0') return result;
                }
            }
        }
        return result with
        {
            FlagAppearance = ReadDetailForceAppearance(state, current, names, flagForce, true, MapSpritePalettes.Create())
        };
    }

    private SourceWorldContext? CaptureDetailForceInputs(GameState state)
    {
        var current = _definitions.Capture(state);
        if (current is not null) OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        return current;
    }

    private static IReadOnlyDictionary<int, DetailForceAppearance?> ReadDetailForces(GameState state,
        SourceWorldContext? current, LoadedArmyFlagPaneNames names, IEnumerable<int> forceIds)
    {
        var result = new Dictionary<int, DetailForceAppearance?>();
        var palettes = MapSpritePalettes.Create();
        foreach (int id in forceIds.Distinct())
        {
            if ((uint)id >= 52) throw new InvalidDataException("Detail force identity is outside the source table.");
            result[id] = Read(id);
        }
        return result;

        DetailForceAppearance? Read(int id)
        {
            // The common flag and special-force display-name paths have
            // additional consumers. Keep those unproved rows unavailable.
            if (current is null || id >= 46 || current.ReadForceTableReference(id) is not { } force ||
                current.ReadForceNativeValidity(force) is not bool valid) return null;
            return ReadDetailForceAppearance(state, current, names, force, valid, palettes);
        }
    }

    private static DetailForceAppearance? ReadDetailForceAppearance(GameState state, SourceWorldContext current,
        LoadedArmyFlagPaneNames names, ForceReference force, bool valid, MapSpritePalettes palettes)
    {
        var fillForce = valid ? force : current.ReadForceTableReference(0);
        if (fillForce is null || current.ReadForceReportColor(fillForce) is not byte colour) return null;
        var fill = MapSpritePalettes.ForceFill(colour);
        var letter = palettes.ForceLetter(colour);
        if (!valid) return new(false, "", fill, letter, "");
        int physicalForce = force.PhysicalSlot!.Value;
        if (current.ReadForceKingdomNameIndex(physicalForce) is null) return null;
        var selection = names.Resolve(current, force);
        ushort? ruler = current.ReadForceRecord(physicalForce)?.RawRuler10;
        string? displayName = null;
        if (ruler is ushort rulerId && (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
            : current.ReadTransferPersonTableReference(0)) is { } person)
        {
            if (person.PhysicalSlot is not int physical) displayName = "";
            else if (current.ReadPersonNativeValidity(physical) is bool personValid)
                displayName = !personValid ? "" : current.ReadPersonIdentity(physical) is ushort identity
                    ? state.Officers.GetValueOrDefault(identity)?.Name : null;
        }
        return new(true, selection.NativeShortName, fill, letter, displayName);
    }
}
