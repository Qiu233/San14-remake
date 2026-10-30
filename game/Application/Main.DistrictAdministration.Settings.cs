using System.Collections.Immutable;
using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void ApplyDistrictSetup(DistrictAdministrationWorkspace owner)
    {
        var draft = owner.Draft; var summary = draft.Summary; var settings = draft.Settings;
        var choices = new List<DistrictSetupChoice>();
        if (settings is not null)
        {
            bool editable = draft.CanEditSettings;
            foreach (var policy in draft.Policies)
                choices.Add(new(DistrictSetupField.Policy, policy.Id, settings.PolicyId == policy.Id, editable && policy.Available == true));
            foreach (var priority in draft.Priorities)
                choices.Add(new(DistrictSetupField.Priority, priority.Id, settings.PriorityId == priority.Id, editable && (priority.Id == 1 || draft.CanChangePriority) && priority.Available == true));
            Pair(DistrictSetupField.Attack, settings.Attack, draft.CanAllowAttack, draft.CanDisallowAttack);
            Pair(DistrictSetupField.Transport, settings.Transport, draft.CanAllowTransport, draft.CanDisallowTransport);
            Pair(DistrictSetupField.Recruitment, settings.RecruitOfficers, true, draft.CanDisallowRecruitOfficers);
            Pair(DistrictSetupField.Enlistment, settings.RecruitTroops, true, draft.CanDisallowRecruitTroops);
            Pair(DistrictSetupField.Merchant, settings.Merchant, true, draft.CanDisallowMerchant);
            Pair(DistrictSetupField.Demolition, settings.Demolition, true, draft.CanDisallowDemolition);
            Pair(DistrictSetupField.IntelligenceUse, settings.Military.Enabled, true, draft.CanDisableMilitary);
            foreach (var facility in draft.Facilities)
                choices.Add(new(DistrictSetupField.Facilities, facility.Id, settings.AllowedFacilityIds.Contains(facility.Id),
                    editable && draft.FacilitiesEditable && facility.Available == true, facility.Name));
            choices.Add(new(DistrictSetupField.FacilitiesAll, 0, false, editable && draft.FacilitiesEditable,
                PersonnelDataText(draft.Facilities.Any(option => settings.AllowedFacilityIds.Contains(option.Id)) ? 0x3B : 0x3A)));
            foreach (var (field, selected) in new[] { (DistrictSetupField.GoldUse, settings.Resources.GoldUsageId),
                (DistrictSetupField.FoodUse, settings.Resources.FoodUsageId), (DistrictSetupField.TroopsUse, settings.Resources.TroopsUsageId) })
                foreach (var option in draft.ResourceUsageChoices)
                    choices.Add(new(field, option.Id, selected == option.Id, editable && (settings.PolicyId != 1 || option.Id == 0) && option.Available == true));
            foreach (var mission in draft.Missions)
                choices.Add(new(DistrictSetupField.IntelligenceMissions, mission.Id, settings.Military.AllowedMissionIds.Contains(mission.Id),
                    editable && draft.MissionsEditable && mission.Available == true));
            choices.Add(new(DistrictSetupField.IntelligenceMissionsAll, 0, false, editable && draft.MissionsEditable && settings.PolicyId is 2 or 3,
                PersonnelDataText(settings.Military.AllowedMissionIds.Count > 0 ? 0x3B : 0x3A)));
            choices.Add(new(DistrictSetupField.IntelligenceBudget, 0, false, draft.CanEditIntelligence));
            choices.Add(new(DistrictSetupField.IntelligenceBudget, 1, false, editable && draft.MilitaryIntelligenceTransfer > 0));
            void Pair(DistrictSetupField field, bool selected, bool allow, bool disallow)
            { choices.Add(new(field, 1, selected, editable && allow)); choices.Add(new(field, 0, !selected, editable && disallow)); }
        }
        IReadOnlyDictionary<int, string>[] fields =
        [
            new Dictionary<int, string> { [15] = Number(summary?.OfficerCount), [20] = Number(summary?.Troops) },
            new Dictionary<int, string>(), new Dictionary<int, string>(),
            new Dictionary<int, string> { [13] = Number(draft.IntelligenceBefore), [15] = Number(draft.IntelligenceAfter),
                [16] = settings?.Military.Enabled == true && draft.TransferCost > 0 ? "(^02+" + Number(draft.TransferCost) + "^00)" : "" }
        ];
        owner.Setup.Apply(new(summary?.Name ?? "", fields, choices, summary?.CityCaption ?? "", summary?.LeaderCaption ?? "",
            summary?.TargetCaption ?? "", true, draft.CanEditLeader, draft.CanEditTarget,
            draft.CanConfirm, draft.CanContinue, DistrictAdministrationGuide(owner)) { DistrictNumber=draft.Number });
        var presentation=draft.QuerySetupPresentation();
        owner.Setup.ApplyPresentation(presentation.Available?presentation.Value:null,presentation.UnresolvedInput);
    }

    private void ChangeDistrictSetting(DistrictAdministrationWorkspace owner, DistrictSetupField field, int value)
    {
        if (!CanEditDistrictAdministration(owner) || owner.Draft.Settings is not { } settings || !owner.Draft.CanEditSettings) return;
        var draft = owner.Draft;
        if (field == DistrictSetupField.Policy) draft.SetPolicy(value);
        else if (field == DistrictSetupField.IntelligenceBudget)
        { if (value == 1) draft.SetMilitaryIntelligenceTransfer(0); else OpenDistrictIntelligenceAmount(owner); }
        else
        {
            var updated = field switch
            {
                DistrictSetupField.Priority => settings with { PriorityId = value },
                DistrictSetupField.Attack => settings with { Attack = value != 0 },
                DistrictSetupField.Transport => settings with { Transport = value != 0 },
                DistrictSetupField.Recruitment => settings with { RecruitOfficers = value != 0 },
                DistrictSetupField.Enlistment => settings with { RecruitTroops = value != 0 },
                DistrictSetupField.Merchant => settings with { Merchant = value != 0 },
                DistrictSetupField.Demolition => settings with { Demolition = value != 0 },
                DistrictSetupField.Facilities => settings with { AllowedFacilityIds = Toggle(settings.AllowedFacilityIds, value) },
                DistrictSetupField.FacilitiesAll => settings with { AllowedFacilityIds = ToggleFacilities() },
                DistrictSetupField.GoldUse => settings with { Resources = settings.Resources with { GoldUsageId = value } },
                DistrictSetupField.FoodUse => settings with { Resources = settings.Resources with { FoodUsageId = value } },
                DistrictSetupField.TroopsUse => settings with { Resources = settings.Resources with { TroopsUsageId = value } },
                DistrictSetupField.IntelligenceUse => settings with { Military = settings.Military with { Enabled = value != 0 } },
                DistrictSetupField.IntelligenceMissions => settings with { Military = settings.Military with { AllowedMissionIds = Toggle(settings.Military.AllowedMissionIds, value) } },
                DistrictSetupField.IntelligenceMissionsAll => settings with { Military = settings.Military with
                    { AllowedMissionIds = settings.Military.AllowedMissionIds.Count > 0 ? ImmutableHashSet<int>.Empty
                        : draft.Missions.Where(option => option.Available == true).Select(option => option.Id).ToImmutableHashSet() } },
                _ => settings
            };
            draft.SetSettings(updated);
            ImmutableHashSet<int> ToggleFacilities()
            {
                var shown = draft.Facilities.Where(option => option.Available == true).Select(option => option.Id).ToArray();
                return shown.Any(settings.AllowedFacilityIds.Contains)
                    ? settings.AllowedFacilityIds.Except(shown) : settings.AllowedFacilityIds.Union(shown);
            }
        }
        RefreshDistrictAdministration(owner);
        static ImmutableHashSet<int> Toggle(ImmutableHashSet<int> values, int value) => values.Contains(value) ? values.Remove(value) : values.Add(value);
    }
    private void OpenDistrictIntelligenceAmount(DistrictAdministrationWorkspace owner)
    {
        if (_repository is null || !CanEditDistrictAdministration(owner) || !owner.Draft.CanEditIntelligence) return;
        var options = owner.Draft.QueryMilitaryIntelligenceAmountOptions();
        if (options.UnresolvedOwner is { } missing) { DistrictAdministrationUnavailable(missing); return; }
        if (options.Minimum is not int minimum || options.Maximum is not int maximum || !options.CanChooseOther) return;
        var menu = new MerchantAmountMenuView(); owner.Amount = menu; AddChild(menu); owner.Setup.Hide();
        try
        {
            menu.Configure(_repository, _ui, options.PresetAmounts, options.Current, minimum, maximum,
                () => IsDistrictAdministrationCurrent(owner) && owner.Amount == menu, result => RunUiAction(() =>
                {
                    if (!IsDistrictAdministrationCurrent(owner) || owner.Amount != menu) return;
                    owner.Amount = null; menu.Abandon(); menu.QueueFree();
                    if (result is int amount) owner.Draft.SetMilitaryIntelligenceTransfer(amount);
                    RefreshDistrictAdministration(owner);
                }), titleMessage: 0x8A8, calculatorPosition: new(860, 352), quantityIconPrefix: "000_cmn_icon_sel_gunjou_",
                calculatorTitleMessage: 0x20D, hintText: "", quantityIconTexturePack: "texture_ce_common");
        }
        catch { owner.Amount = null; menu.Abandon(); menu.QueueFree(); RefreshDistrictAdministration(owner); throw; }
    }
}
