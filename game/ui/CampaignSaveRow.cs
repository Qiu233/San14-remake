using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

/// <summary>The ordinary 832 by 116 save row, with the HanyouL behavior skin.
/// Slot numbers map explicitly from the UI's zero-based array to file slots.</summary>
internal partial class CampaignSaveRow : UiSourceButton
{
    private Rect2 hit;
    private Action? released;
    public override bool _HasPoint(Vector2 point) => hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, PreviewText text, CampaignSaveSlot slot, string scenarioTitle, bool saving, Action released,
        LoadedSprite? resolvedPortrait = null)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, 0x03757250);
        Size = source.Rectangle(0).Size;
        var skin = new UiSourceLayout(repository, 0x53D84BCB).ResizedSubtree(0, Size, true);
        Rect2 box = skin.Rectangle(1); Vector2 centre = box.GetCenter();
        hit = new(new((int)centre.X - (int)(box.Size.X * .5f), (int)centre.Y - (int)(box.Size.Y * .5f)),
            new((int)box.Size.X, (int)box.Size.Y));
        ActionMode = ActionModeEnum.Release;
        Disabled = !saving && !slot.CanLoad;
        var states = new Control[5];
        int[] mainArt = [3, 7, 10, 13, 15];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "State" + state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group); states[state] = group;
            skin.Art(group, mainArt[state], skin.Rectangle(mainArt[state]), assets, shader);
            if (state is 1 or 4)
            {
                int extra = state == 1 ? 8 : 16;
                uint packed = 0x00FFFFFFu | (uint)(state == 1 ? 190 : 127) << 24;
                var colour = new AreaMarkerColor(packed, packed);
                skin.Art(group, extra, skin.Rectangle(extra), assets, shader,
                    new MapCornerColors(colour, colour, colour, colour), blend: UiSourceSprite.Blend.Additive);
            }
        }
        // Normal uses its settled endpoint: designer node5 is hidden there.
        // Push node11 pulses over frames100..130; that animation is deferred.
        var presets = MapTextPresets.Create();
        int displayIndex = slot.Number > LocalCampaignSaves.SlotCount
            ? slot.Number - LocalCampaignSaves.SlotCount - 1 : slot.Number - 1;
        Label(29, displayIndex.ToString(CultureInfo.InvariantCulture), Disabled ? 6 : 3);
        if (slot.Summary is { } summary)
        {
            if (resolvedPortrait is not null)
            {
                var portraitBox = source.Rectangle(3);
                var portrait = new UiSourcePortrait { Name = "SaveRowPortrait",
                    Position = new((int)portraitBox.Position.X, (int)portraitBox.Position.Y),
                    Size = new((int)portraitBox.Size.X, (int)portraitBox.Size.Y) };
                AddChild(portrait); portrait.Configure(repository, assets, shader, resolvedPortrait);
            }
            if (summary.Flag is { } appearance)
            {
                var flag = new UiSourceForceFlag { Name = "SaveRowFlag", Position = source.Rectangle(6).Position };
                AddChild(flag); flag.Configure(repository, assets, shader, font, appearance);
            }
            Label(9, summary.RulerName, 3);
            Label(10, scenarioTitle, 3);
            if (summary.Date is { Year: >= 0 } date)
            {
                int day = unchecked((byte)date.Day);
                string period = text.Get(day < 11 ? "map_hud_early_period" :
                    day < 21 ? "map_hud_middle_period" : "map_hud_late_period");
                Label(11, text.Format("save_game_date", date.Year, unchecked((byte)date.Month), period), 3).Name = "SaveRowDate";
            }
            else if (summary.Date is null)
                Label(11, text.Get("detail_unknown"), 3).Name = "SaveRowDate";
            foreach (int node in new[] { 13, 14, 15, 16, 21, 22, 23, 24 })
                source.Art(this, node, source.Rectangle(node), assets, shader);
            Label(17, Message(0x04DC), 13);
            Label(25, Message(0x04DD), 13);
            Label(18, summary.SavedAt?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? text.Get("save_time_unknown"), 3);
            Label(19, summary.SavedAt?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "", 3);
            Label(26, summary.Elapsed is { } elapsed
                ? text.Format("save_elapsed_months", elapsed.Years, elapsed.Months)
                : text.Get("detail_unknown"), 3).Name = "SaveRowElapsed";
        }
        else Label(28, slot.Kind == CampaignSaveSlotKind.Empty ? Message(0x0622) : text.Get("save_slot_unreadable"), Disabled ? 6 : 3);
        ConfigureStates(states);
        Pressed += () => { if (!Disabled) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_button_layout", skin.Source.Source.ResourceUri);
        SetMeta("file_slot", slot.Number); SetMeta("source_slot_index", displayIndex);
        SetMeta("save_kind", slot.Kind.ToString());

        UiSourceText Label(int node, string value, int preset) => source.Text(this, node, default, value, font, presets[preset]);
        string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing save row caption.");
    }

    internal void Abandon() { released = null; Disabled = true; }
}
