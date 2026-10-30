using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>90AA90's ordinary current-world scenery detail. This does not
    /// discover the site, start an investment, or use a Collection world.</summary>
    public SceneryInformationPage QuerySceneryInformation(GameState state, int sceneryId)
    {
        var fields = new Dictionary<int, string>();
        var page = new SceneryInformationPage(sceneryId, "", fields);
        try
        {
            var current = CaptureSceneryMapCurrent(state);
            var scenery = InvestmentScenery(current, sceneryId);
            if (!IK(current.ReadSceneryNativeValidity(scenery), "information_validity"))
                return page with { UnresolvedOwner = "scenery.information.invalid_site" };
            var definition = _landmarkDefinitions.Catalog.Sceneries[sceneryId];
            var owner = InvestmentOwner(state, sceneryId);
            var development = owner.Development ?? throw InvestmentMissing("information_development");
            var area = TalentSceneryArea(current, scenery);
            bool areaValid = IK(current.ReadAreaNativeValidity(area), "information_Area_validity");
            int areaId = IK(area.PhysicalSlot, "information_Area_identity");
            int forceId = IK(current.ReadConditionSceneryForceSlot(scenery), "information_Force");
            var force = current.ReadForceTableReference(forceId < 52 ? forceId : 0)
                ?? throw InvestmentMissing("information_Force_pointer");
            bool validForce = IK(current.ReadForceNativeValidity(force), "information_Force_validity");
            var flagForce = validForce ? force : current.ReadForceTableReference(0)
                ?? throw InvestmentMissing("information_Force0_pointer");
            int color = IK(current.ReadForceReportColor(flagForce), "information_Force_color");
            fields[12] = "";
            fields[16] = "";
            if (validForce)
            {
                _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                fields[12] = _mapInformationNames.Resolve(current, force).NativeShortName;
                fields[16] = ReadSceneryRulerLabel(state, current, force, forceId);
            }
            fields[13] = definition.Name;
            // This detail uses22A060's actual Area->City, independently of
            //22AA10's investment ownership gate.
            int cityId = areaValid ? IK(current.ReadAreaCitySlot(area), "information_Area_city") : 0;
            var city = current.ReadCityTableReference(cityId < 52 ? cityId : 0)
                ?? throw InvestmentMissing("information_City_pointer");
            fields[20] = areaValid && IK(current.ReadBaseNativeValidity(city), "information_City_validity")
                ? state.Cities.GetValueOrDefault(cityId)?.Name ?? throw InvestmentMissing("information_City_name") : "";
            fields[23] = areaValid ? state.Areas.GetValueOrDefault(areaId)?.Name
                ?? throw InvestmentMissing("information_Area_name") : "";
            fields[30] = FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x384), development.Level);
            var date = state.NativeMapSeason?.ThirdDate ?? throw InvestmentMissing("information_ThirdDate");
            int day = unchecked(date.Day - 31 + (date.Month + date.Year * 12) * 30);
            int elapsed = unchecked(owner.InvestmentDays - owner.CompletionDay + day);
            bool noDays = owner.Phase is 1 or 4 || owner.InvestmentDays == 0;
            fields[31] = noDays ? ReadHistoryDataText(1, 0x353) : elapsed.ToString(CultureInfo.InvariantCulture);
            fields[33] = noDays ? ReadHistoryDataText(1, 0x353) : owner.InvestmentDays.ToString(CultureInfo.InvariantCulture);
            fields[40] = FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x862), development.Effect);
            bool alternate = false;
            string status = "";
            if (areaValid)
            {
                if (forceId == 0) status = ReadHistoryDataText(1, 0x89F);
                else if (IK(current.ReadAreaCurrentForceSlot(area), "information_Area_force") != forceId)
                { alternate = true; status = ReadHistoryDataText(1, 0x89E); }
                else status = ReadHistoryDataText(0, 0x1760 + owner.Phase);
            }
            fields[45] = alternate ? "" : status; fields[50] = alternate ? status : "";
            fields[56] = ReadHistoryDataText(0, 0x16F6 + sceneryId);
            ushort hex = IK(current.ReadSceneryHexIndex(scenery), "information_hex");
            var bases = ReadDetailMiniMapBases(current);
            byte? miniColor = ReadMiniMapColor(current, forceId);
            //85B2B0 receives the original elapsed/duration pair; only the
            //actual gauge consumer clamps its displayed fill.
            int progress = owner.Phase == 1 || owner.InvestmentDays == 0 ? 0
                : owner.Phase == 4 ? 100 : elapsed;
            int maximum = owner.Phase is 1 or 4 || owner.InvestmentDays == 0 ? 100 : owner.InvestmentDays;
            return page with
            {
                Name = definition.Name, ImageIndex = definition.RawField48,
                EffectKind = development.EffectKind, ForceColorIndex = color,
                ForceNameColor = validForce ? MapSpritePalettes.Create().ForceLetter(color) : null,
                UseAlternateStatePanel = alternate, ProgressCurrent = progress, ProgressMaximum = maximum,
                MiniMap = bases is not null && miniColor is byte actualColor
                    ? new(bases, DetailMiniMapProjection.Project(DetailMiniMapIconKind.Area, sceneryId, hex, actualColor)) : null
            };
        }
        catch (InvalidDataException error) { return page with { UnresolvedOwner = error.Message }; }
    }

    private string ReadSceneryRulerLabel(GameState state, SourceWorldContext current, ForceReference force, int physical)
    {
        string name;
        if (physical is >= 46 and <= 50)
            name = state.Cities.GetValueOrDefault(physical + 1)?.Name ?? throw InvestmentMissing("information_special_City_name");
        else
        {
            ushort raw = IK(current.ReadForceRecord(physical)?.RawRuler10, "information_ruler");
            var person = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
            if (person is null) throw InvestmentMissing("information_ruler_pointer");
            if (person.PhysicalSlot is not int at || !IK(current.ReadPersonNativeValidity(at), "information_ruler_validity")) return "";
            int identity = IK(current.ReadPersonIdentity(at), "information_ruler_identity");
            name = state.Officers.GetValueOrDefault(identity)?.Name ?? throw InvestmentMissing("information_ruler_name");
        }
        return ReadHistoryDataText(1, 0x571) + name;
    }
}
