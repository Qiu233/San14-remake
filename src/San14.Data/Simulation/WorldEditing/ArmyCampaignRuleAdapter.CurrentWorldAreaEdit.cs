using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<CurrentWorldAreaEditPublication> QueryCurrentWorldAreaEdit(
        GameState state, int areaId)
    {
        try
        {
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            var area = ReadCurrentWorldEditArea(state, current, areaId);
            return AreaAdministrationQuery<CurrentWorldAreaEditPublication>.Known(new(areaId, area.Name,
                Number(area.Development?.Commerce, 0, 1000, "commerce"),
                Number(area.Development?.Agriculture, 0, 1000, "agriculture"),
                Number(area.Development?.Barracks, 0, 1000, "barracks"),
                Number(area.NativeNormalAftermath?.Quantity52, 5000, 60000, "population"),
                Number(area.Security, 0, 100, "security")));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldAreaEditPublication>.Unknown(error.Message); }

        static CurrentWorldEditNumber Number(int? value, int minimum, int maximum, string name) =>
            new(value, minimum, maximum, value.HasValue ? null : "current-world-edit.area." + name);
    }

    private CommandResult ApplyCurrentWorldAreaEditCurrent(GameState state, EditCurrentWorldAreaOrder order)
    {
        if (order.Values is not { } values)
            return CommandResult.Reject("Area edit values are missing.", "world_edit.area_values");
        if (!InRange(values.Commerce, 0, 1000) || !InRange(values.Agriculture, 0, 1000) ||
            !InRange(values.Barracks, 0, 1000) || !InRange(values.Population, 5000, 60000) ||
            !InRange(values.Security, 0, 100))
            return CommandResult.Reject("Area edit values are outside the source controls.", "world_edit.area_range");
        if (order.AreaId is < 1 or > 500 or >= 339 and <= 343 || !state.Areas.ContainsKey(order.AreaId))
            return CommandResult.Reject("Area is outside the current editable selection.", "world_edit.area_target");

        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _ = ReadCurrentWorldEditArea(state, current, order.AreaId);
        var reference = new AreaReference(order.AreaId);
        var writer = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
        bool changed = false;
        Progress(CeAreaDevelopmentField.Commerce, values.Commerce);
        Progress(CeAreaDevelopmentField.Agriculture, values.Agriculture);
        Progress(CeAreaDevelopmentField.Barracks, values.Barracks);
        var next = writer.State;

        if (values.Population is int population)
        {
            var area = next.Areas[order.AreaId];
            var aftermath = area.NativeNormalAftermath ?? throw WorldEditMissing("area_population_owner");
            int before = WorldEditNeed(aftermath.Quantity52, "area_population");
            if (before != population)
            {
                next = next with { Areas = new(next.Areas) { [order.AreaId] = area with
                    { NativeNormalAftermath = aftermath with { Quantity52 = (ushort)population } } } };
                changed = true;
            }
        }

        if (values.Security is int security)
        {
            var area = next.Areas[order.AreaId];
            int before = WorldEditNeed(area.Security, "area_security");
            if (before != security)
            {
                // The actual Area owner is reached through its representative
                // Hex, not the potentially independent Area.ForceId projection.
                int raw = WorldEditNeed(current.ReadAreaCurrentForceSlot(reference), "area_security_current_force");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0)
                    ?? throw WorldEditMissing("area_security_force_pointer");
                int physical = WorldEditNeed(current.ReadForcePhysicalSlot(force), "area_security_force_identity");
                if (physical is not (>= 46 and <= 50))
                    next = next with { Areas = new(next.Areas) { [order.AreaId] = area with { Security = security } } };
                // The source reaches its edited marker when the requested
                // byte differs, including a special-Force suppressed store.
                changed = true;
            }
        }
        return CommandResult.Accept(changed ? MarkCurrentWorldEdited(next, CurrentWorldEditKind.Area) : next);

        void Progress(CeAreaDevelopmentField field, int? requested)
        {
            if (requested is not int value) return;
            var development = writer.State.Areas[order.AreaId].Development
                ?? throw WorldEditMissing("area_development_owner");
            int before = WorldEditNeed(field switch
            {
                CeAreaDevelopmentField.Commerce => development.Commerce,
                CeAreaDevelopmentField.Agriculture => development.Agriculture,
                _ => development.Barracks
            }, "area_development_" + field);
            if (before == value) return;
            // Edit commits use the mature setter's flag0 branch. No Force
            // development bonus, level-change callback or guessed input is read.
            _normalAftermathInputs.Development.WriteCurrentProgress(reference, field, value, 0, writer);
            changed = true;
        }

        static bool InRange(int? value, int minimum, int maximum) => value is null || value >= minimum && value <= maximum;
    }

    private static AreaState ReadCurrentWorldEditArea(GameState state, SourceWorldContext current, int areaId)
    {
        if (areaId is < 1 or > 500 or >= 339 and <= 343 ||
            !state.Areas.TryGetValue(areaId, out var owner) || owner.Id != areaId)
            throw WorldEditMissing("area_editable_identity");
        var area = current.ReadAreaTableReference(areaId) ?? throw WorldEditMissing("area_pointer");
        if (area.PhysicalSlot != areaId || !WorldEditNeed(current.ReadAreaNativeValidity(area), "area_validity"))
            throw WorldEditMissing("area_not_current");
        return owner;
    }
}
