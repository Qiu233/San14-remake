using San14.Core;

namespace San14.Data;

public sealed record DetailForceAppearance(bool IsValid, string Glyph, AreaMarkerColor Fill,
    AreaMarkerColor Letter, string? DisplayName)
{
    public string? DisplayText => DisplayName is null ? null : DisplayName.Length == 0 ? "" : "■" + DisplayName;
}

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
}
