using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //83C100 retains the actual pair Force pointer across the real1F0BC0
    //wait. The caller owns that wait and the invocation's gain/loss cursor.
    internal (GameState State, string Text) PrepareStoryStateControlNoticeCurrent(
        GameState state, SourceWorldContext current, int province, int force, bool gained,
        List<CampaignEffectRequest> effects)
    {
        var retained = new ForceReference(force);
        int argument = StoryStateControlNeed(current.ReadForcePhysicalSlot(retained), "Force60");
        string provinceName = ReadStoryStateControlProvinceNameCurrent(current, province);
        var formatted = FormatReportMessage(state, current, gained ? 0x328C : 0x3291,
            new(Force0: argument, String0: provinceName), ReportMessageOrigin.StoryStateControl, null);
        effects.Add(new(null, null) { MonthlyMaintenance = new(
            MonthlyMaintenanceOperation.NarrationText, null, null, formatted.Text) });
        return (formatted.State, formatted.Text);
    }

    //Call only after the actual announcement completes. History suppression
    //belongs to WriteHistory; it must not suppress the preceding announcement
    //or the independently player-gated Chronicle command.
    internal GameState CompleteStoryStateControlNoticeCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int province, int force, bool gained,
        string text, List<CampaignEffectRequest> effects)
    {
        var retained = new ForceReference(force);
        var district = ForcePrimaryDistrictSource.Resolve(current, retained);
        var reporter = new PersonDeathHistorySelection(state, current, pool, SearchAbilities(current),
            _governorInputs, (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)).Select(district);
        int reporterId = StoryStateControlNeed(current.ReadPersonIdentity(
            StoryStateControlNeed(reporter.PhysicalSlot, "reporter_pointer")), "reporter_Person10");
        int day = (ReadArmyDestructionSampler(state, current)
            ?? throw StoryStateControlMissing("report_World450")).Day450;
        //The source resolves22E930 again after selecting its reporter.
        district = ForcePrimaryDistrictSource.Resolve(current, retained);
        int districtId = StoryStateControlNeed(current.ReadDistrictPhysicalSlot(district), "District60");
        state = WriteHistory(state, current, new(1, districtId, 0, day, text, string.Empty,
            reporterId, MarkBit0: 1, MarkBit5: 1), effects);

        if (!TalentForceIsPlayerCurrent(state, current, retained)) return state;
        day = (ReadArmyDestructionSampler(state, current)
            ?? throw StoryStateControlMissing("chronicle_World450")).Day450;
        string Name(OfficerState person, bool full) =>
            (person.CurrentSurname ?? throw StoryStateControlMissing("Person_surname")) +
            (full ? person.CurrentGivenName ?? throw StoryStateControlMissing("Person_given_name") : string.Empty);
        string header = HistoryWriter.CustomFirstName(state, current, retained, Name);
        int color = StoryStateControlNeed(current.ReadForceReportColor(retained), "Force49");
        //Label0 is independently22EE20, never the custom-first header.
        string provinceName = ReadStoryStateControlProvinceNameCurrent(current, province);
        string fallback = HistoryWriter.FallbackName(state, current, retained, Name);
        return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
            new(gained ? 300 : 292, 1, day, header, color, gained ? 37u : 35u,
                [fallback, provinceName, string.Empty], [48400, 48400, 48400, 48400]),
            ReadApplicationGameplay()?.UserSelectionServicePresent);
    }

    //30CEB0 returns an undecorated province name, without a native-validity
    //test.0/out-of-range returns the real empty string. Preserve the current
    //table lookup before selecting the immutable definition's name field.
    private string ReadStoryStateControlProvinceNameCurrent(SourceWorldContext current, int province)
    {
        if (province is < 1 or > 22) return string.Empty;
        var selected = current.ReadConditionStateTableSlot(province)
            ?? throw StoryStateControlMissing("Province_lookup");
        int physical = StoryStateControlNeed(selected.PhysicalSlot, "Province_pointer");
        var states = _growthQueries.Definitions.Catalog.States;
        if ((uint)physical >= (uint)states.Count || states[physical].Slot != physical ||
            states[physical].RawBytes.Length != 32) throw StoryStateControlMissing("Province_definition");
        return S14ForceNameDefinitionReader.ReadLiteralUnits(states[physical].RawBytes.AsSpan(24, 6));
    }

    private static T StoryStateControlNeed<T>(T? value, string field) where T : struct =>
        value ?? throw StoryStateControlMissing(field);
    private static InvalidDataException StoryStateControlMissing(string field) =>
        new("story.state-control.report." + field);
}
