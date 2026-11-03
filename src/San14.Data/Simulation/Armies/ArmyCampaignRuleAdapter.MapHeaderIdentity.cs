using San14.Core;

namespace San14.Data;

public enum MapHeaderUpdate { Updated, Unchanged, Unresolved }

/// <summary>A reached update of the map header's district/force identity.
/// Known invalid receivers leave the previous identity intact; unknown inputs
/// are distinct from both that no-update result and a hidden list portrait.</summary>
public sealed record MapHeaderIdentity(MapHeaderUpdate Update, int? ForceId = null,
    int? RulerOfficerId = null, OfficerPortraitRoute? Portrait = null,
    ArmyFlagPaneNameSelection? Flag = null, string? UnresolvedInput = null)
{
    public int? DistrictId { get; init; }
    public bool? CommandBooksVisible { get; init; }
    public string? CommandBooksVisibilityIssue { get; init; }
}

public sealed record MapHeaderCommandBooks(MapHeaderUpdate Update, int? Count = null, string? UnresolvedInput = null);
public sealed record MapHeaderProjection(MapHeaderIdentity Identity, MapHeaderCommandBooks CommandBooks)
{
    public MapMainCommandStyle MainCommandStyle { get; init; } = new(null, "map.main_command.style_not_prepared");
    public MapMainCommandPresentation MainCommandPresentation { get; init; } = new([], ["map.main_command.presentation_not_prepared"])
        { Update = MapHeaderUpdate.Unresolved };
}
/// <summary>View-owned presentation values, never part of the saved simulation.</summary>
public sealed record MapHeaderDisplayState(int DistrictId, int? OrderCount);

public sealed partial class ArmyCampaignRuleAdapter
{
    public MapHeaderProjection QueryMapHeader(GameState state, int? districtId,
        MapHeaderDisplayState? displayed, LoadedArmyFlagPaneNames names)
    {
        SourceWorldContext current;
        try
        {
            current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            ForceUnregisterInputs.BindCurrent(state, current);
            //The header's current extension window is not part of the static
            //District identity capture. Borrow saved scalars/reset stores,
            //then project the same live books owner used by the visible header
            //and deployment quote. Old native windows may predate its grant.
            foreach (var (physical, owner) in state.Districts)
            {
                //An explicit native reset remains later than the live Core
                //projection, including an inactive historical District row.
                if (state.NativeForceUnregisterCurrent?.DistrictWindows10.TryGetValue(physical, out var saved) == true &&
                    saved[0] == 0) continue;
                current.SetDistrictRawByte(physical, 0x14, owner.Id == physical &&
                    owner.OrdersRemaining is int books && books is >= 0 and <= byte.MaxValue ? (byte)books : null);
            }
        }
        catch (InvalidDataException error)
        { return new(new(MapHeaderUpdate.Unresolved, UnresolvedInput: error.Message), new(MapHeaderUpdate.Unresolved, UnresolvedInput: error.Message))
            { MainCommandStyle = new(null, error.Message), MainCommandPresentation = new([], [error.Message])
                { Update = MapHeaderUpdate.Unresolved } }; }
        var style = ReadMainCommandStyleCurrent(state, current);
        var commandPresentation = ReadMainCommandPresentationCurrent(state, current);
        var identity = ReadIdentity();
        int? retainedDistrict = identity.Update == MapHeaderUpdate.Updated ? identity.DistrictId : displayed?.DistrictId;
        if (retainedDistrict is not int district)
            return new(identity, identity.Update == MapHeaderUpdate.Unchanged ? new(MapHeaderUpdate.Unchanged)
                : new(MapHeaderUpdate.Unresolved, UnresolvedInput: "map.header.quantity_retained_district"))
            { MainCommandStyle = style, MainCommandPresentation = commandPresentation };
        return new(identity, ReadBooks(district)) { MainCommandStyle = style, MainCommandPresentation = commandPresentation };

        MapHeaderIdentity ReadIdentity()
        {
            try
            {
            int district = districtId ?? throw Missing("player_district");
            if (!Need(current.ReadDistrictNativeValidity(district), "district_validity"))
                return new(MapHeaderUpdate.Unchanged);
            int rawForce = Need(current.ReadDistrictForceId(district), "district_force");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                ?? throw Missing("force_pointer");
            if (!Need(current.ReadForceNativeValidity(force), "force_validity"))
                return new(MapHeaderUpdate.Unchanged);
            int forceSlot = force.PhysicalSlot ?? throw Missing("force_identity");
            ushort ruler = current.ReadForceRecord(forceSlot)?.RawRuler10 ?? throw Missing("ruler_lookup");
            var person = (ruler <= 6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("ruler_pointer");
            if (person.PhysicalSlot is not int physical)
                return new(MapHeaderUpdate.Unchanged);
            if (!Need(current.ReadPersonNativeValidity(physical), "ruler_validity"))
                return new(MapHeaderUpdate.Unchanged);
            int personId = current.ReadPersonIdentity(physical) ?? throw Missing("ruler_identity");
            byte color = current.ReadForceReportColor(force) ?? throw Missing("force_color");
            // 92FD50 repeats validity and selects2198 on its own false branch.
            // It does not call85A230's invalid-person hide behavior. The raw
            // lookup ID and the selected pointer's identity stay independent.
            ushort appearance = Need(current.ReadPersonNativeValidity(physical), "portrait_validity")
                ? current.ReadPersonPortraitAppearance(physical) ?? throw Missing("portrait_appearance") : (ushort)2198;
            var portrait = ReadOfficerPortraitAppearance(state, current, _definitions, personId, appearance, ArmyMapUiSources.Create());
            var flag = names.Resolve(current, force) with { CurrentForceColor49 = color };
            bool? visible = null; string? visibilityIssue = null;
            try
            {
                current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
                visible = BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, new(district), Missing);
            }
            catch (InvalidDataException error) { visibilityIssue = error.Message; }
            return new(MapHeaderUpdate.Updated, forceSlot, personId, portrait, flag, portrait.UnresolvedInput)
            { DistrictId = district, CommandBooksVisible = visible, CommandBooksVisibilityIssue = visibilityIssue };
            }
            catch (InvalidDataException error) { return new(MapHeaderUpdate.Unresolved, UnresolvedInput: error.Message); }
        }

        MapHeaderCommandBooks ReadBooks(int district)
        {
            try
            {
                // Init calls88FC50 directly. Later quantity refresh uses the
                // independent233880 gate, not the visibility's ruler predicate.
                if (displayed is not null)
                {
                    current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
                    if (!BaseCaptureRewardSource.ReadPlayerDistrict(current, new(district), Missing)) return new(MapHeaderUpdate.Unchanged);
                    int next = Count();
                    if (displayed.OrderCount == next) return new(MapHeaderUpdate.Unchanged, next);
                    if (!Need(current.ReadDistrictNativeValidity(district), "quantity_district_validity")) return new(MapHeaderUpdate.Unchanged);
                    return new(MapHeaderUpdate.Updated, Count());
                }
                if (!Need(current.ReadDistrictNativeValidity(district), "quantity_district_validity")) return new(MapHeaderUpdate.Unchanged);
                return new(MapHeaderUpdate.Updated, Count());

                int Count()
                {
                    if (!state.Districts.TryGetValue(district, out var owner) || owner.Id != district) throw Missing("quantity_district_owner");
                    return owner.OrdersRemaining is int count && count is >= 0 and <= byte.MaxValue
                        ? count : throw Missing("quantity_current_count");
                }
            }
            catch (InvalidDataException error) { return new(MapHeaderUpdate.Unresolved, UnresolvedInput: error.Message); }
        }

        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("map.header." + field);
    }
}
