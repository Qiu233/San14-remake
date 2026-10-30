using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>925FD0's ordinary hovered-Hex route, after the host's real Scene
    /// selection/filter has accepted that Hex. No label hit or clicked selection is substituted.</summary>
    public static AreaAdministrationQuery<int?> ResolveSceneryMapSummaryReceiver(
        SourceWorldContext current, HexReference? hoveredHex)
    {
        ArgumentNullException.ThrowIfNull(current);
        try
        {
            if (hoveredHex?.PhysicalSlot is null) return AreaAdministrationQuery<int?>.Known(null);
            byte raw = IK(current.ReadHexScenerySlot(hoveredHex), "summary_Hex_Scenery1E");
            var scenery = current.ReadSceneryTableReference(raw < 101 ? raw : 0)
                ?? throw InvestmentMissing("summary_Scenery_pointer");
            if (!IK(current.ReadSceneryNativeValidity(scenery), "summary_Scenery_validity"))
                return AreaAdministrationQuery<int?>.Known(null);
            if ((IK(current.ReadSceneryFlags47(scenery), "summary_Scenery_flags47") & 1) == 0)
                return AreaAdministrationQuery<int?>.Known(null);
            int physical = IK(scenery.PhysicalSlot, "summary_Scenery_identity");
            return AreaAdministrationQuery<int?>.Known(physical);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<int?>.Unknown(error.Message); }
    }

    /// <summary>92A280's current compact summary. Reuses the normal Scenery
    /// detail's owned current, name, phase, development and formatting helpers;
    /// it does not execute discovery/investment or open the full detail page.</summary>
    public SceneryMapSummaryPage QuerySceneryMapSummary(GameState state, int sceneryId)
    {
        try { return ReadSceneryMapSummaryCurrent(state, CaptureSceneryMapCurrent(state), sceneryId); }
        catch (InvalidDataException error) { return new(sceneryId, null, new Dictionary<int,string>()) { UnresolvedOwner = error.Message }; }
    }

    internal SceneryMapSummaryPage ReadSceneryMapSummaryCurrent(GameState state, SourceWorldContext current, int sceneryId)
    {
        var fields = new Dictionary<int, string>();
        var page = new SceneryMapSummaryPage(sceneryId, null, fields);
        try
        {
            var scenery = InvestmentScenery(current, sceneryId);
            bool valid = IK(current.ReadSceneryNativeValidity(scenery), "summary_Scenery_validity");
            if (!valid) return page with { ReceiverValid = false };
            if ((IK(current.ReadSceneryFlags47(scenery), "summary_Scenery_flags47") & 1) == 0)
                return page with { ReceiverValid = false };
            var definition = _landmarkDefinitions.Catalog.Sceneries[sceneryId];
            page = page with { ReceiverValid = true, ImageIndex = definition.RawField48 };
            fields[12] = definition.Name;

            var owner = InvestmentOwner(state, sceneryId);
            var development = owner.Development ?? throw InvestmentMissing("summary_development");
            bool maximum = development.Level >= 6;
            bool hasEffect = development.EffectKind is >= 1 and <= 3;
            page = page with { MaximumLevel = maximum, EffectKind = development.EffectKind, EffectVisible = hasEffect };
            fields[28] = maximum ? "" : FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x384), development.Level);
            fields[30] = maximum ? ReadHistoryDataText(1, 0x30A) : "";
            fields[36] = hasEffect ? FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x862), development.Effect) : "";
            if (maximum) page = page with { ProgressCurrent = 1, ProgressMaximum = 1 };
            else if (owner.Phase == 1 || owner.InvestmentDays == 0)
                page = page with { ProgressCurrent = 0, ProgressMaximum = owner.InvestmentDays };
            else
            {
                var date = state.NativeMapSeason?.ThirdDate ?? throw InvestmentMissing("summary_ThirdDate");
                int day = unchecked(date.Day - 31 + (date.Month + date.Year * 12) * 30);
                int elapsed = unchecked(owner.InvestmentDays - owner.CompletionDay + day);
                page = page with { ProgressCurrent = Math.Clamp(elapsed, 0, owner.InvestmentDays),
                    ProgressMaximum = owner.InvestmentDays };
            }

            var area = TalentSceneryArea(current, scenery);
            bool areaValid = IK(current.ReadAreaNativeValidity(area), "summary_Area_validity");
            fields[18] = ""; fields[22] = ""; fields[40] = "";
            if (areaValid)
            {
                int areaId = IK(area.PhysicalSlot, "summary_Area_identity");
                fields[22] = state.Areas.GetValueOrDefault(areaId)?.Name ?? throw InvestmentMissing("summary_Area_name");
                int rawCity = IK(current.ReadAreaCitySlot(area), "summary_Area_city35");
                var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
                    ?? throw InvestmentMissing("summary_City_pointer");
                if (IK(current.ReadBaseNativeValidity(city), "summary_City_validity"))
                {
                    int cityId = IK(city.PhysicalSlot, "summary_City_identity");
                    fields[18] = state.Cities.GetValueOrDefault(cityId)?.Name ?? throw InvestmentMissing("summary_City_name");
                }
                byte sceneryForce = IK(current.ReadConditionSceneryForceSlot(scenery), "summary_Scenery_force");
                //3649B0 uses89F for both neutral and a different owning Area;
                //the full detail's alternate89E panel is a different caller.
                fields[40] = sceneryForce != 0 &&
                    IK(current.ReadAreaCurrentForceSlot(area), "summary_Area_force") == sceneryForce
                    ? ReadHistoryDataText(0, 0x1760 + owner.Phase) : ReadHistoryDataText(1, 0x89F);
            }

            int forceSlot = ReadLandmarkMapOwner(current, scenery)
                ?? throw InvestmentMissing("summary_owner_22CBC0");
            var force = current.ReadForceTableReference(forceSlot)
                ?? throw InvestmentMissing("summary_Force_pointer");
            int color = IK(current.ReadForceReportColor(force), "summary_Force_color49");
            _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
            fields[11] = _mapInformationNames.Resolve(current, force).NativeShortName;
            return page with { ForceColorIndex = color, ForceNameColor = MapSpritePalettes.Create().ForceLetter(color) };
        }
        catch (InvalidDataException error) { return page with { UnresolvedOwner = error.Message }; }
    }
}
