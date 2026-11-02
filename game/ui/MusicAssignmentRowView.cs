using Godot;
using San14.Data;

internal partial class MusicAssignmentRowView : UiSourceButton
{
    private Action? released;
    private MusicPreviewButtonView change = null!;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string context, string music, Action released)
    {
        this.released = released;
        var source = assets.SourceLayout("02_12_00_offset_00");
        var states = new Control[5];
        int[][] layers = [[2, 8, 9], [2, 6, 7, 8, 9], [12, 13, 8, 9], [2, 8, 9], [2, 8, 9]];
        for (int index = 0; index < states.Length; index++)
        {
            var state = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = Size }; AddChild(state); states[index] = state;
            foreach (int node in layers[index]) source.Art(state, node, source.Rectangle(node), assets, shader);
        }
        source.Text(this, 3, default, context, font);
        source.Text(this, 10, default, music, font);
        ConfigureStates(states);
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        var changeBox = source.Rectangle(4);
        change = new MusicPreviewButtonView { Position = changeBox.Position, Size = changeBox.Size,
            InputAdmission = () => !Disabled && SourceInputAdmitted };
        AddChild(change); change.Configure(repository, assets, shader, () => this.released?.Invoke());
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Abandon() { released = null; InputAdmission = null; Disabled = true; change.Abandon(); }
}
