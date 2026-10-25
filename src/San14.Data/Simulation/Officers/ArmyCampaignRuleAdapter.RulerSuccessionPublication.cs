using San14.Core;

namespace San14.Data;

public sealed record RulerSuccessionPersonColumn(int ColumnId, string Caption, int Width);
public sealed record RulerSuccessionPersonPage(string Title, string Guide, string EmptyChoiceWarning,
    IReadOnlyList<RulerSuccessionPersonColumn> Columns, IReadOnlyList<PersonnelGroupPersonListRow> Rows);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One owned current for the retained successor selector. Its
    /// pending ordered candidates are the original28C5D0 list, not a new query.</summary>
    public AreaAdministrationQuery<RulerSuccessionPersonPage> QueryRulerSuccessionPersonPage(GameState state, int forceId)
    {
        if (state.PersonDeathContinuation?.PendingSuccession is not { } request || request.ForceId != forceId)
            return AreaAdministrationQuery<RulerSuccessionPersonPage>.Unknown("ruler.succession_pending");
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            _personRelationInputs.BindCurrent(state, current);
            _moraleWaveInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            current.EventConditions.CurrentYear = state.NativeMapSeason?.ThirdDate.Year;
            current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
            int[] ids = [0, 138, 8, 9, 10, 11, 12];
            int[] captions = [0x211, 0x550, 0x238, 0x239, 0x23a, 0x23b, 0x23c];
            var columns = ids.Select((id, index) => new RulerSuccessionPersonColumn(id,
                ReadHistoryDataText(1, captions[index]), id == 0 ? 132 : 76)).ToArray();
            var rows = request.CandidateOfficerIds.Select(id =>
            {
                int physical = current.ResolveOfficerSource(id) ?? throw OfficerDetailMissing("succession_person_receiver");
                var person = state.Officers.GetValueOrDefault(id) ?? throw OfficerDetailMissing("succession_person_owner");
                var reader = new OfficerDetailsCurrentReader(this, state, current, forceId, person, physical);
                return new PersonnelGroupPersonListRow(id, ids.Select(column => reader.ReadSuccessionListCell(column)).ToArray());
            }).ToArray();
            return AreaAdministrationQuery<RulerSuccessionPersonPage>.Known(new(ReadHistoryDataText(1, 0x4ab),
                request.PromptAtSource, request.EmptyChoiceWarningAtSource, columns, rows));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<RulerSuccessionPersonPage>.Unknown(error.Message); }
    }

    private sealed partial class OfficerDetailsCurrentReader
    {
        internal PersonnelGroupPersonCell ReadSuccessionListCell(int column)
        {
            if (column != 138) return ReadGroupListCell(column);
            try
            {
                if (!DetailNeed(current.ReadPersonNativeValidity(physical), "succession_person_validity"))
                    return new(column, "", null, null, null, false, null);
                ushort homeId = DetailNeed(current.ReadPersonFirstLocation(physical), "succession_Home");
                var home = BaseLookup.Find(homeId, current);
                if (!DetailNeed(current.ReadBaseNativeValidity(home), "succession_Home_validity"))
                    return new(column, "", null, null, null, false, null);
                string name = BaseEntity(home).Name;
                //34DE50 sorts the actual nonzero E2 word; display remains
                //360880's raw Base name, independently of that numeric key.
                return new(column, name, null, homeId == 0 ? int.MaxValue : homeId, null, false, null);
            }
            catch (InvalidDataException error) { return new(column, null, null, null, null, false, error.Message); }
        }
    }
}
