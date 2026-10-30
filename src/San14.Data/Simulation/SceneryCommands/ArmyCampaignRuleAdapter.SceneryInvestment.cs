using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed record SceneryHexCommandPage(int CityId, bool? InvestmentAllowed,
    bool? TargetAllowed, bool? DetailAllowed, string? UnresolvedOwner)
{
    public string Name { get; init; } = "";
    public int? HexId { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter : IPlayableSceneryInvestmentRules
{
    private SceneryInvestmentSource? sceneryInvestmentSource;
    private SceneryInvestmentSource InvestmentSource => sceneryInvestmentSource ??=
        new(_repository.LoadExperienceData(ExperienceVariant.ExCe));

    public SceneryInvestmentDraft OpenSceneryInvestmentDraft(GameState state, int forceId, int districtId,
        int? cityId = null, int? sceneryId = null) => new(this, state, forceId, districtId, cityId, sceneryId);

    //6DADB0 is the ordinary City command's admission, separate from opening
    //the editor. Its caller supplies the existing camera-level gate.
    private bool CanCityInvestSceneryCurrent(GameState state, SourceWorldContext current, int cityId)
    {
        if (IK(ReadApplicationInformationRestriction(state), "City_external_selection")) return false;
        var city = current.ReadCityTableReference(cityId) ?? throw InvestmentMissing("City_pointer");
        if (!IK(current.ReadBaseNativeValidity(city), "City_validity")) return false;
        var district = current.ReadCityDistrictReference(city) ?? throw InvestmentMissing("City_District_pointer");
        if (!IK(current.ReadDistrictNativeValidity(district), "City_District_validity")) return false;
        int districtId = IK(district.PhysicalSlot, "City_District_identity");
        int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining
            ?? throw InvestmentMissing("City_District_orders14");
        if (books - CommandBookAvailability.Reserved(state, districtId) <= 0) return false;
        current.MaterializeFixedScenery(_landmarkDefinitions);
        for (int id = 0; id < 101; id++)
            current.SetSceneryFlags47(id, state.LandmarkInvestments?.GetValueOrDefault(id)?.Flags);
        return InvestmentCandidates(state, current, cityId, 0).Count != 0;
    }

    internal SceneryInvestmentProjection QuerySceneryInvestmentDraft(GameState state, int forceId,
        int districtId, int cityId, int? sceneryId)
    {
        var cities = new List<SceneryInvestmentCityRow>();
        var sites = new List<SceneryInvestmentSiteRow>();
        var page = new SceneryInvestmentPage(forceId, districtId, cityId, sceneryId);
        try
        {
            var current = CaptureSceneryMapCurrent(state);
            if (sceneryId is int selected && cityId == 0)
                cityId = InvestmentCity(current, InvestmentScenery(current, selected));
            if (state.Cities.TryGetValue(cityId, out var city))
            {
                var receiver = current.ReadCityTableReference(city.Id) ?? throw InvestmentMissing("City_pointer");
                if (IK(current.ReadBaseNativeValidity(receiver), "City_validity") &&
                    IK(current.ReadBaseForceSlot(receiver), "City_Force") == forceId &&
                    IK(current.ReadBaseDistrictSlot(receiver), "City_District") == districtId)
                    cities.Add(new(city.Id, city.Name, city.Gold));
            }
            page = page with { CityId = cityId };
            if (cityId == 0)
                return new(page with { CanConfirm = false, CanContinue = false, CanSelectScenery = false }, cities, sites);
            var selectedCity = state.Cities.GetValueOrDefault(cityId) ?? throw InvestmentMissing("selected_City");
            if (selectedCity.ForceId != forceId || selectedCity.DistrictId != districtId)
                return new(page with { CanConfirm = false, CanContinue = false, CanSelectScenery = false }, cities, sites);
            int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining
                ?? throw InvestmentMissing("District_orders14");
            var candidates = InvestmentCandidates(state, current, cityId, 0);
            foreach (int candidate in candidates)
                sites.Add(InvestmentSiteRow(state, current, candidate));
            page = page with { CityName = selectedCity.Name, CityGold = selectedCity.Gold,
                Books = books, CanSelectScenery = candidates.Count != 0, CanConfirm = false, CanContinue = false };
            if (sceneryId is not int id) return new(page, cities, sites);
            var scenery = InvestmentScenery(current, id);
            if (!candidates.Contains(id)) return new(page, cities, sites);
            var owner = InvestmentOwner(state, id);
            var development = owner.Development ?? throw InvestmentMissing("Scenery_development");
            var area = TalentSceneryArea(current, scenery);
            int areaId = IK(area.PhysicalSlot, "Scenery_Area_identity");
            var price = InvestmentPrice(state, current, scenery, development.Level);
            int availableBooks = checked((int)(books - CommandBookAvailability.Reserved(state, districtId)));
            bool directDistrict = InvestmentDistrictControl(current, districtId, forceId);
            int afterGold = Math.Clamp(unchecked(selectedCity.Gold - price.Gold), 0,
                BasePostLoad.ReadGoldCapacity(new(BaseKind.City, cityId), current));
            var miniBases = ReadDetailMiniMapBases(current);
            ushort hex = IK(current.ReadSceneryHexIndex(scenery), "Scenery_hex44");
            byte? color = ReadMiniMapColor(current, forceId);
            page = page with
            {
                SceneryName = _landmarkDefinitions.Catalog.Sceneries[id].Name,
                ProvinceName = state.Areas.GetValueOrDefault(areaId)?.Name ?? throw InvestmentMissing("Scenery_Area_name"),
                CurrentLevel = development.Level, NextLevel = development.Level + 1,
                CurrentEffect = development.Effect,
                NextEffect = unchecked(development.Effect + InvestmentSource.ReadIncrement(state, development.Level)),
                EffectKind = development.EffectKind, Duration = price.Days,
                AfterGold = afterGold, GoldDelta = price.Gold, AfterBooks = Math.Clamp(books - 1, 0, 200), BookDelta = 1,
                SceneryIllustrationIndex = _landmarkDefinitions.Catalog.Sceneries[id].RawField48,
                CanConfirm = directDistrict && availableBooks >= 1,
                CanContinue = directDistrict && availableBooks > 1 && InvestmentCandidates(state, current, cityId, price.Gold).Count > 1,
                MiniMap = miniBases is not null && color is byte sourceColor
                    ? new(miniBases, DetailMiniMapProjection.Project(DetailMiniMapIconKind.Area, areaId, hex, sourceColor)) : null
            };
            return new(page, cities, sites);
        }
        catch (InvalidDataException missing)
        {
            return new(page with { CanConfirm = null, CanContinue = null, CanSelectScenery = null,
                UnresolvedOwner = missing.Message }, cities, sites);
        }
    }

    public SceneryHexCommandPage QuerySceneryHexCommands(GameState state, int forceId, int sceneryId)
    {
        int cityId = 0;
        string name = "";
        int? hexId = null;
        bool detailAvailable = false;
        try
        {
            var current = CaptureSceneryMapCurrent(state);
            var scenery = InvestmentScenery(current, sceneryId);
            if (!IK(current.ReadSceneryNativeValidity(scenery), "hex_Scenery_validity"))
                return new(0, false, false, false, null);
            detailAvailable = true;
            name = _landmarkDefinitions.Catalog.Sceneries[sceneryId].Name;
            hexId = IK(current.ReadSceneryHexIndex(scenery), "hex_Scenery44");
            cityId = InvestmentCity(current, scenery);
            int owner = IK(current.ReadConditionSceneryForceSlot(scenery), "hex_Scenery_Force");
            int player = IK((current.ReadCurrentPlayerForce() ?? throw InvestmentMissing("hex_player_pointer")).PhysicalSlot,
                "hex_player_identity");
            bool observer = (IK(current.ReadWorldTransferServiceFlags(), "hex_World16A8") & 0x100) != 0;
            bool? target = observer ? false : ArmyLogisticsPreparation.ReadNativeRelationGate(state, player, owner);
            if (target == true) target = SceneryTargetCommandAvailable(state, current);
            bool invest = false;
            if (!observer && forceId == player && owner == player && state.Cities.TryGetValue(cityId, out var city) &&
                city.DistrictId is int districtId)
            {
                var district = current.ReadDistrictTableReference(districtId) ?? throw InvestmentMissing("hex_District_pointer");
                int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining
                    ?? throw InvestmentMissing("hex_District_orders");
                invest = BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, InvestmentMissing) &&
                    books - CommandBookAvailability.Reserved(state, districtId) >= 1 &&
                    !IK(ReadApplicationInformationRestriction(state), "hex_external_selection") &&
                    InvestmentCandidates(state, current, cityId, 0).Contains(sceneryId);
            }
            //72FF30 leaves the detail entry's enabled field at1. Environment
            //bit10 controls its guide marker, not its command permission.
            return new(cityId, invest, target, true, target is null ? "scenery.hex.target_relation" : null)
                { Name = name, HexId = hexId };
        }
        catch (InvalidDataException missing)
        { return new(cityId, null, null, detailAvailable ? true : null, missing.Message) { Name = name, HexId = hexId }; }
    }

    //7CDBC0 is the additional target-button gate after the menu is built.
    private static bool SceneryTargetCommandAvailable(GameState state, SourceWorldContext current)
    {
        var force = current.ReadCurrentPlayerForce() ?? throw InvestmentMissing("target_player_pointer");
        var district = ForcePrimaryDistrictSource.Resolve(current, force);
        if (!IK(current.ReadDistrictNativeValidity(district), "target_primary_District_validity")) return false;
        int id = IK(district.PhysicalSlot, "target_primary_District_identity");
        int books = state.Districts.GetValueOrDefault(id)?.OrdersRemaining
            ?? throw InvestmentMissing("target_primary_District_orders14");
        if (books - CommandBookAvailability.Reserved(state, id) <= 0) return false;
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw InvestmentMissing("target_Army60_registry");
        int count = IK(current.ReadTemporaryHexListCount(new(descriptor.Identity)), "target_Army60_count");
        return count <= 499;
    }

    public CommandResult PrepareSceneryInvestment(GameState state, ForceState force,
        InvestSceneryOrder order, CommandContext context)
    {
        try
        {
            if (state.Phase != TurnPhase.Strategy || !force.IsActive || force.Id != order.ForceId ||
                context.DelegatedDistrictId is not null)
                return CommandResult.Reject("Scenery investment requires direct strategy control.", "scenery.investment_scope");
            var current = CaptureSceneryMapCurrent(state);
            var scenery = InvestmentScenery(current, order.SceneryId);
            int cityId = InvestmentCity(current, scenery);
            var city = state.Cities.GetValueOrDefault(cityId);
            if (city?.ForceId != force.Id || city.DistrictId is not int districtId)
                return CommandResult.Reject("The scenery has no eligible owned city.", "scenery.investment_city");
            var district = state.Districts.GetValueOrDefault(districtId) ?? throw InvestmentMissing("District_owner");
            int books = district.OrdersRemaining ?? throw InvestmentMissing("District_orders14");
            if (!InvestmentDistrictControl(current, districtId, force.Id) ||
                books - CommandBookAvailability.Reserved(state, districtId) < 1 ||
                !InvestmentCandidates(state, current, cityId, 0).Contains(order.SceneryId))
                return CommandResult.Reject("The scenery cannot currently receive investment.", "scenery.investment_qualification");
            var owner = InvestmentOwner(state, order.SceneryId);
            var price = InvestmentPrice(state, current, scenery,
                (owner.Development ?? throw InvestmentMissing("Scenery_development")).Level);
            var date = state.NativeMapSeason?.ThirdDate ?? throw InvestmentMissing("ThirdDate34");
            int now = unchecked(date.Day - 31 + (date.Month + date.Year * 12) * 30);
            int gold = Math.Clamp(unchecked(city.Gold - price.Gold), 0,
                BasePostLoad.ReadGoldCapacity(new(BaseKind.City, cityId), current));
            var working = state with
            {
                LandmarkInvestments = new(state.LandmarkInvestments!)
                {
                    [order.SceneryId] = owner with { Phase = 2,
                        AssignedOfficerId = unchecked((ushort)Math.Max(price.Days, 0)),
                        CompletionDay = Math.Max(unchecked(now + price.Days), 0) }
                },
                Cities = new(state.Cities) { [cityId] = city with { Gold = gold } },
                Districts = new(state.Districts) { [districtId] = district with { OrdersRemaining = Math.Clamp(books - 1, 0, 200) } }
            };
            var name = _landmarkDefinitions.Catalog.Sceneries[order.SceneryId].Name;
            var text = FormatTalentReportCurrent(working, current, 0x5C8A, new(String0: name));
            var requests = new CampaignEffectRequest[]
            {
                new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 4, null, null, null, null) },
                new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) }
            };
            var pending = text.State.CampaignEffectRequests ?? throw InvestmentMissing("presentation_owner");
            return CommandResult.Accept(text.State with { CampaignEffectRequests = pending.Append(
                CampaignEffectSource.SceneryInvestmentCommands,
                text.State.NativeClock?.UpdateOrdinal ?? throw InvestmentMissing("source_revision"), requests) });
        }
        catch (InvalidDataException missing) { return CommandResult.RuleUnavailable(missing.Message, missing.Message); }
    }

    //6DCEF0 retains physical table order. It is not a valid-entity registry
    //rebuild and does not select the first candidate on the player's behalf.
    private List<int> InvestmentCandidates(GameState state, SourceWorldContext current, int cityId, int reservedGold)
    {
        var candidates = new List<int>();
        var city = state.Cities.GetValueOrDefault(cityId) ?? throw InvestmentMissing("City_owner");
        for (int id = 0; id < 101; id++)
        {
            var scenery = current.ReadSceneryTableReference(id) ?? throw InvestmentMissing("Scenery_table");
            if (!IK(current.ReadSceneryNativeValidity(scenery), "Scenery_validity") ||
                (IK(current.ReadSceneryFlags47(scenery), "Scenery_flags47") & 1) == 0) continue;
            if (InvestmentCity(current, scenery) != cityId ||
                IK(current.ReadConditionSceneryForceSlot(scenery), "Scenery_Force") != city.ForceId) continue;
            var owner = InvestmentOwner(state, id);
            var development = owner.Development ?? throw InvestmentMissing("Scenery_development");
            if (owner.Phase != 1 || development.Level is < 1 or > 5 || owner.InvestmentDays != 0 || owner.CompletionDay >= 1) continue;
            int cost = InvestmentPrice(state, current, scenery, development.Level).Gold;
            if (unchecked(city.Gold - reservedGold - cost) >= 0) candidates.Add(id);
        }
        return candidates;
    }

    private (int Gold, int Days) InvestmentPrice(GameState state, SourceWorldContext current, SceneryReference scenery, int level)
    {
        var area = TalentSceneryArea(current, scenery);
        var writer = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
        int sum = 0;
        foreach (var field in new[] { CeAreaDevelopmentField.Agriculture, CeAreaDevelopmentField.Barracks, CeAreaDevelopmentField.Commerce })
            sum += _normalAftermathInputs.Development.ReadLevel(IK(writer.ReadAreaProgress(area, field), "Area_development_" + field));
        return InvestmentSource.Read(state, level, sum);
    }

    private SceneryInvestmentSiteRow InvestmentSiteRow(GameState state, SourceWorldContext current, int id)
    {
        var scenery = InvestmentScenery(current, id);
        var owner = InvestmentOwner(state, id);
        var development = owner.Development ?? throw InvestmentMissing("Scenery_development");
        var price = InvestmentPrice(state, current, scenery, development.Level);
        string name = _landmarkDefinitions.Catalog.Sceneries[id].Name;
        int cityId = InvestmentCity(current, scenery);
        int area = IK(TalentSceneryArea(current, scenery).PhysicalSlot, "Scenery_Area_identity");
        int force = IK(current.ReadConditionSceneryForceSlot(scenery), "Scenery_Force");
        string effect = development.EffectKind is >= 1 and <= 3
            ? FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x842 + development.EffectKind), development.Effect)
            : ReadHistoryDataText(1, 0x353);
        return new(id, name, development.Level, price.Gold, price.Days)
        {
            CanSelect = true,
            Cells = [name, state.Cities[cityId].Name, state.Areas[area].Name,
                state.Forces.GetValueOrDefault(force)?.Name ?? throw InvestmentMissing("Scenery_Force_name"),
                development.Level.ToString(CultureInfo.InvariantCulture),
                effect, ReadHistoryDataText(0, 0x1760 + owner.Phase)]
        };
    }

    private static SceneryReference InvestmentScenery(SourceWorldContext current, int id) =>
        id is >= 0 and <= 100 ? current.ReadSceneryTableReference(id) ?? throw InvestmentMissing("Scenery_pointer")
            : throw InvestmentMissing("Scenery_identity");
    private static LandmarkInvestmentState InvestmentOwner(GameState state, int id) =>
        state.LandmarkInvestments?.GetValueOrDefault(id) ?? throw InvestmentMissing("Scenery_owned_state");
    private static int InvestmentCity(SourceWorldContext current, SceneryReference scenery) =>
        IK(TalentSceneryCity(current, scenery).PhysicalSlot, "Scenery_City_pointer");
    private static bool InvestmentDistrictControl(SourceWorldContext current, int districtId, int forceId)
    {
        var district = current.ReadDistrictTableReference(districtId) ?? throw InvestmentMissing("District_pointer");
        if (!IK(current.ReadDistrictNativeValidity(district), "District_validity") ||
            IK(current.ReadDistrictForceId(districtId), "District_Force") != forceId) return false;
        ushort leader = IK(current.ReadDistrictLeader(districtId), "District_leader12");
        var person = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0)
            ?? throw InvestmentMissing("District_leader_pointer");
        return person.PhysicalSlot is int physical && IK(current.ReadPersonNativeValidity(physical), "District_leader_validity") &&
            IK(current.ReadPersonStatus(physical), "District_leader_status") == 1;
    }
    private static T IK<T>(T? value, string field) where T : struct => value ?? throw InvestmentMissing(field);
    private static InvalidDataException InvestmentMissing(string field) => new("scenery.investment." + field);
}
