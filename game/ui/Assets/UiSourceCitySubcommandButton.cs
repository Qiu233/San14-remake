using Godot;
using San14.Data;

/// <summary>The city tree's unresized child button. Its v52 constructor uses
/// the whole root for hit input, and the field8=1 caption family in each state.</summary>
internal partial class UiSourceCitySubcommandButton : UiSourceButton
{
    private Action? released;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string caption, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, 0xBD19DBED);
        Size = source.Rectangle(0).Size;
        ActionMode = ActionModeEnum.Release;
        int[] art = [3, 6, 10, 15, 19], text = [4, 8, 12, 16, 21];
        var states = new Control[5];
        var presets = MapTextPresets.Create();
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "State" + state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group);
            source.Art(group, art[state], source.Rectangle(art[state]), assets, shader);
            if (state is 1 or 4)
            {
                int node = state == 1 ? 7 : 20;
                byte alpha = state == 1 ? (byte)255 : (byte)127;
                uint packed = 0x00FFFFFFu | (uint)alpha << 24;
                var color = new AreaMarkerColor(packed, packed);
                source.Art(group, node, source.Rectangle(node), assets, shader,
                    new(color, color, color, color), blend: UiSourceSprite.Blend.Additive);
            }
            var label = source.Text(group, text[state], default, caption, font, presets[28 + state]);
            label.Name = "CommandCaption";
            states[state] = group;
        }
        // Actual setup hides the alternate5/9/13/17/22 text family. The
        // attention marker23 is also explicitly hidden. Neither is duplicated.
        // The changing push glow remains deferred rather than frozen at its entry.
        ConfigureStates(states);
        Pressed += () => { if (!Disabled) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; Disabled = true; }
}
