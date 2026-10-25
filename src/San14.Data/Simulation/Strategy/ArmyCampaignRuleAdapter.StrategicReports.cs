using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal HistoryTextResult FormatStrategicCommandReport(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands)
    {
        //22EFD0 resolves the two object definition names before30DCF0 stores
        //them in the ordinary String0/1 slots. There is no definition VM token.
        if (operands.ObjectDefinition0 is int first)
            operands = operands with { String0 = ReadStrategicObjectDefinitionNameCurrent(current, first) };
        if (operands.ObjectDefinition1 is int second)
            operands = operands with { String1 = ReadStrategicObjectDefinitionNameCurrent(current, second) };
        return FormatReportMessage(state, current, root, operands,
            ReportMessageOrigin.StrategicCommands, null, _personnelNarrationVoice);
    }

    internal string ReadStrategicObjectDefinitionNameCurrent(SourceWorldContext current, int definitionId)
    {
        var definition = current.ReadObjectDefinitionReference(definitionId is >= 0 and <= 30 ? definitionId : 0)
            ?? throw new InvalidDataException("strategic.report.definition_lookup");
        bool valid = current.ReadObjectDefinitionValidity(definition)
            ?? throw new InvalidDataException("strategic.report.definition_validity");
        if (!valid) return string.Empty;
        int physical = definition.PhysicalSlot ?? throw new InvalidDataException("strategic.report.definition_pointer");
        var rows = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions;
        if ((uint)physical >= (uint)rows.Count) throw new InvalidDataException("strategic.report.definition_resource");
        return rows[physical].Name;
    }

    internal HistoryTextResult FormatStrategicCommandReportCurrent(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands) => FormatStrategicCommandReport(state, current, root, operands);

    // The ordinary event narration and conditional officer subtitle remain
    // separate requests. Formatting has already selected any actual voice.
    internal static CampaignEffectRequest CreateStrategicCommandPresentation(SourceWorldContext current,
        GameState sourceState, int executorId, HistoryTextResult formatted, bool conditional = false) =>
        CreateSingleOfficerPresentation(current, sourceState, executorId, formatted,
            conditional ? TalentCommandPresentationOperation.ConditionalOfficerText :
                TalentCommandPresentationOperation.OfficerText, "strategic.report");
}
