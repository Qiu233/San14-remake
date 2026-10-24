using System.Globalization;
using Godot;
using San14.Data;

internal partial class ArmyDetailsView
{
    private ArmyDetailsTacticMarker? focusedTactic;

    private void RenderParameters(UiSourceLayout source, UnitSourceParameters parameters)
    {
        focusedTactic = null;
        var owner = body;
        var presets = MapTextPresets.Create();
        string unknown = text.Get("detail_unknown");
        var effectBox = source.Rectangle(126);
        var effects = new UiSourceParagraph { Name = "ArmyTacticEffects", Position = effectBox.Position, Size = effectBox.Size };
        body.AddChild(effects);
        effects.Configure(font!, MapFontProfile.FromNode(source.Nodes[126], source.Source.Source.ResourceUri).WithPreset(presets[0]));
        effects.Apply("");
        var waiting = source.Text(body, 119, default,
            parameters.WaitingDays?.ToString(CultureInfo.InvariantCulture) ?? unknown, font!, presets[0]);
        var unit = source.Text(body, 120, default, parameters.WaitingVisible == true ? Message(0x46A) : "", font!, presets[0]);
        waiting.Visible = unit.Visible = parameters.WaitingVisible != false;
        for (int index = 0; index < 3; index++)
        {
            UnitSourceTactic? tactic = parameters.Tactics is { } list && index < list.Count ? list[index] : null;
            var box = source.Rectangle(114 + index);
            var marker = new ArmyDetailsTacticMarker { Name = "ArmyTactic" + index, Position = box.Position, Size = box.Size };
            body.AddChild(marker);
            marker.Configure(repository, assets!, shader!, font!,
                parameters.Tactics is null ? unknown : tactic?.Name ?? "", tactic is not null,
                () =>
                {
                    if (closed || body != owner) return;
                    focusedTactic = marker;
                    effects.Apply(parameters.Tactics is null ? unknown : tactic is null ? "" : tactic.EffectsText ?? unknown);
                },
                () =>
                {
                    if (body != owner || focusedTactic != marker) return;
                    focusedTactic = null; effects.Apply("");
                });
            marker.SetMeta("tactic_id", tactic?.TacticId ?? -1);
            marker.SetMeta("tactics_known", parameters.Tactics is not null);
        }
    }
}
