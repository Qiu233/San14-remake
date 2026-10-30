using System.Globalization;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed record TalentSceneryDiscoveryPanel(int SceneryId, string Name,
    AreaAdministrationQuery<string> LevelText, AreaAdministrationQuery<string> BonusText,
    AreaAdministrationQuery<bool> BonusEnabled, AreaAdministrationQuery<int?> BonusIcon,
    AreaAdministrationQuery<string> ImageResourceName, AreaAdministrationQuery<string> ImageSpriteName,
    PersonnelPresentationSpeaker Executor)
{
    public string LevelCaption { get; init; } = "";
    public string BonusCaption { get; init; } = "";
    public int ImageKind => 0x1B;
    public int ImageIndex { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One read-only projection when the discovery panel opens. The
    /// persistent landmark effect is independent of the one-time reward.</summary>
    public AreaAdministrationQuery<TalentSceneryDiscoveryPanel> QuerySceneryDiscoveryPanel(
        GameState state, TalentSceneryRewardRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if ((uint)request.SceneryId >= (uint)_landmarkDefinitions.Catalog.Sceneries.Count)
                throw SceneryPanelMissing("Scenery_identity");
            var definition = _landmarkDefinitions.Catalog.Sceneries[request.SceneryId];
            // A single current view supplies both map ownership and the source
            // actor's existing portrait route. No simulation writer is called.
            var current = CapturePersonPresentationCurrent(state);
            current.MaterializeFixedScenery(_landmarkDefinitions);
            current.SetSceneryFlags47(request.SceneryId,
                state.LandmarkInvestments?.GetValueOrDefault(request.SceneryId)?.Flags);
            var scenery = current.ReadSceneryTableReference(request.SceneryId)
                ?? throw SceneryPanelMissing("Scenery_pointer");
            if (current.ReadSceneryNativeValidity(scenery) != true)
                throw SceneryPanelMissing("Scenery_validity");
            var development = state.LandmarkInvestments?.GetValueOrDefault(request.SceneryId)?.Development;
            var level = Read(() => (development ?? throw SceneryPanelMissing("saved_Level54"))
                .Level.ToString(CultureInfo.InvariantCulture));
            var icon = Read<int?>(() => (development ?? throw SceneryPanelMissing("saved_EffectKind55"))
                .EffectKind switch { 1 => 0x1C1, 2 => 0x1A0, 3 => 0x19C, _ => null });
            var bonus = Read(() =>
            {
                var saved = development ?? throw SceneryPanelMissing("saved_EffectKind55");
                return saved.EffectKind is >= 1 and <= 3
                    ? FormatSceneryPanelNumber(ReadHistoryDataText(1, 0x862), saved.Effect)
                    : "";
            });
            var enabled = Read(() =>
            {
                var area = TalentSceneryArea(current, scenery);
                bool valid = current.ReadAreaNativeValidity(area)
                    ?? throw SceneryPanelMissing("Area_validity");
                if (!valid) return false;
                byte areaForce = current.ReadAreaCurrentForceSlot(area)
                    ?? throw SceneryPanelMissing("Area_Force");
                byte sceneryForce = current.ReadConditionSceneryForceSlot(scenery)
                    ?? throw SceneryPanelMissing("Scenery_Force");
                return areaForce == sceneryForce && sceneryForce != 0;
            });
            DeploymentNarrationPortraitRoute? portrait = null;
            string? portraitMissing = null;
            try
            {
                portrait = ReadNarrationPortrait(state, current, _definitions, request.ExecutorOfficerId,
                    request.ExecutorPortraitAppearanceIndex ?? throw SceneryPanelMissing("source_Executor_A6"),
                    requireCurrentAppearance: false);
            }
            catch (InvalidDataException error) { portraitMissing = error.Message; }
            var executor = new PersonnelPresentationSpeaker(request.ExecutorOfficerId, request.ExecutorOfficerId,
                request.ExecutorNameAtSource ?? throw SceneryPanelMissing("source_Executor_name"),
                null, portrait, portraitMissing);
            return AreaAdministrationQuery<TalentSceneryDiscoveryPanel>.Known(new(request.SceneryId,
                definition.Name, level, bonus, enabled, icon,
                AreaAdministrationQuery<string>.Known("texture_stl_scenery_" + definition.RawField48.ToString("D3", CultureInfo.InvariantCulture)),
                AreaAdministrationQuery<string>.Known("sti_scenery_" + definition.RawField48.ToString("D3", CultureInfo.InvariantCulture)), executor)
            {
                ImageIndex = definition.RawField48,
                LevelCaption = ReadHistoryDataText(1, 0x84C),
                BonusCaption = ReadHistoryDataText(1, 0x411)
            });
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<TalentSceneryDiscoveryPanel>.Unknown(error.Message);
        }

        static AreaAdministrationQuery<T> Read<T>(Func<T> read)
        {
            try { return AreaAdministrationQuery<T>.Known(read()); }
            catch (InvalidDataException error) { return AreaAdministrationQuery<T>.Unknown(error.Message); }
        }
    }

    // The actual Data0862 template takes one integer. Reject unsupported
    // format operands rather than displaying an unexpanded native template.
    private static string FormatSceneryPanelNumber(string format, int value)
    {
        var text = new StringBuilder();
        int arguments = 0;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { text.Append(format[index]); continue; }
            if (++index == format.Length) throw SceneryPanelMissing("bonus_format");
            char code = format[index];
            if (code == '%') { text.Append('%'); continue; }
            if (code is not ('d' or 'u') || ++arguments != 1)
                throw SceneryPanelMissing("bonus_format");
            text.Append(value.ToString(CultureInfo.InvariantCulture));
        }
        if (arguments != 1) throw SceneryPanelMissing("bonus_format_operand");
        return text.ToString();
    }

    private static InvalidDataException SceneryPanelMissing(string field) => new("scenery.panel." + field);
}
