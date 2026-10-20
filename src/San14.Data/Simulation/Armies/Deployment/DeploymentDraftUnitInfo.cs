using San14.Core;

namespace San14.Data;

public sealed record DeploymentFormationPresentation(int FormationId, string Name, byte RawKind,
    ArmyMapIconSelection Icon);
public sealed record DeploymentTacticPresentation(int TacticId, string Name);
public sealed record DeploymentTroopClassification(DeploymentTroopMarker Marker,
    int PrimaryThreshold, int SecondaryThreshold);
public sealed record DeploymentDraftUnitInfo(int? MaximumTroops,
    DeploymentFormationPresentation? LandFormation, DeploymentFormationPresentation? NavalFormation,
    DeploymentTroopClassification? LandTroops, DeploymentTroopClassification? NavalTroops,
    IReadOnlyList<DeploymentTacticPresentation>? LandTactics, IReadOnlyList<DeploymentTacticPresentation>? NavalTactics,
    bool? UsesSpecialNavalTacticText, string? MaximumUnresolvedInput,
    string? LandFormationUnresolvedInput, string? NavalFormationUnresolvedInput,
    string? LandTroopsUnresolvedInput, string? NavalTroopsUnresolvedInput,
    string? LandTacticsUnresolvedInput, string? NavalTacticsUnresolvedInput, string? SpecialTextUnresolvedInput);

internal static class DeploymentTroopClassificationSource
{
    internal static DeploymentTroopClassification Read(GameState state, SourceWorldContext current,
        int personPhysical, int formation, int troops)
    {
        int effect = Need(TacticRemakeCurrentTraits.CommanderEffectSum(current, personPhysical, 14), "threshold_personality14");
        var values = state.NativeArmySimulation?.Parameters.Int32Values;
        if (values is null || !values.TryGetValue(DeploymentDraftFigureRules.TroopThresholdEffectScaleKey, out int scale))
            throw Missing("threshold_effect_scale");
        float reduction = (float)scale * (float)effect;
        reduction /= 100f;
        int first = Threshold(0x80), second = Threshold(0x81);
        var marker = second > 0 && troops >= second ? DeploymentTroopMarker.AtSecondaryThreshold :
            first <= 0 || troops < first ? DeploymentTroopMarker.BelowPrimaryThreshold : DeploymentTroopMarker.Default;
        return new(marker, first, second);
        int Threshold(int offset)
        {
            int raw = unchecked(Need(TacticRemakeCurrentFormations.ReadParameter(state, formation, offset), "current_formation_threshold") * 100);
            return unchecked(raw - BattleUnitSourceMath.TruncateSingleToInt32((float)raw * reduction));
        }
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("deployment.unit_info." + field);
}
