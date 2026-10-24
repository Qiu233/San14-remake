using Godot;
using San14.Data;

internal partial class MusicPreviewButtonView : UiSourceButton
{
    private Control play = null!, stop = null!;
    private Action? released;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(MusicEditLayoutSource.LoadPreviewButton(repository));
        var states = new[] { 3, 5, 8, 11, 13 }.Select(node =>
            source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        play = source.Art(this, 17, source.Rectangle(17), assets, shader, commonSpriteOverride: "000_cmn_icon_advance_play");
        stop = source.Art(this, 17, source.Rectangle(17), assets, shader, commonSpriteOverride: "000_cmn_icon_advance_stop");
        ConfigureStates(states); SetActive(false);
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
    }
    internal void SetActive(bool active) { play.Visible = !active; stop.Visible = active; Selected = active; }
    internal void Abandon() { released = null; InputAdmission = null; Disabled = true; }
}
