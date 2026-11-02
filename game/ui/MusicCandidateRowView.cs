using Godot;
using San14.Data;

internal partial class MusicCandidateRowView : Control
{
    private UiSourceButton body = null!;
    private MusicPreviewButtonView preview = null!;
    private Action? selected;
    internal event Action<InputEvent>? ScrollInput;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string name, string number, Func<bool> admitted, Action selected, Action previewRequested)
    {
        this.selected = selected;
        var source = assets.SourceLayout("02_07_04_offset_00");
        body = new UiSourceButton { Size = Size, InputAdmission = admitted }; AddChild(body);
        body.GuiInput += input => ScrollInput?.Invoke(input);
        var states = new Control[5];
        int[][] layers = [[2], [2, 6, 7], [9, 10], [2], [2]];
        for (int index = 0; index < states.Length; index++)
        {
            var state = new Control { Size = Size, MouseFilter = MouseFilterEnum.Ignore }; body.AddChild(state); states[index] = state;
            foreach (int node in layers[index]) source.Art(state, node, source.Rectangle(node), assets, shader);
        }
        body.ConfigureStates(states);
        source.Text(body, 3, default, name, font);
        source.Art(body, 11, source.Rectangle(11), assets, shader);
        source.Text(body, 12, default, number, font);
        body.Pressed += () => { if (!body.Disabled && body.SourceInputAdmitted) this.selected?.Invoke(); };
        var box = source.Rectangle(4);
        preview = new MusicPreviewButtonView { Position = box.Position, Size = box.Size, InputAdmission = admitted };
        AddChild(preview); preview.Configure(repository, assets, shader, previewRequested);
        preview.GuiInput += input => ScrollInput?.Invoke(input);
    }
    internal void Apply(bool isSelected, bool isPreviewing) { body.Selected = isSelected; preview.SetActive(isPreviewing); }
    internal void Abandon() { selected = null; ScrollInput = null; body.InputAdmission = null; body.Disabled = true; preview.Abandon(); }
}
