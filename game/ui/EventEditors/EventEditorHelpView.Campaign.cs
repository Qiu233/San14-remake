using Godot;
using San14.Data;

internal partial class EventEditorHelpView
{
    private CampaignHelpAdviser? campaignAdviser;
    private Func<int>? currentScrollSpeed;
    private Control campaignRowsHost = null!, campaignScrollHost = null!;
    private UiSourceLayout campaignSelector = null!;
    private IReadOnlyList<EventEditorHelpTopic> campaignTopics = [];
    private readonly List<UiSourceTabButton> campaignTabs = [];
    private readonly Dictionary<int, UiSourceScrollbar> campaignScrollbars = [];
    private UiSourceScrollbar? campaignScrollbar;
    private int campaignCategory;
    private float campaignListHeight;

    internal void BeginCampaignPreparation(Func<bool> admitted, Action returned)
    {
        this.admitted = admitted;
        this.returned = returned;
        ConfigureSourcePage("CampaignHelpPreparation", null, returned);
        ZIndex = 7;
    }

    private void ConfigureCampaignDirectory(UiSourceLayout root, UiSourceLayout selector)
    {
        campaignSelector = selector;
        var list = selector.Rectangle(6);
        list.Position -= new Vector2(108, 0);
        list.Size += new Vector2(108, 0);
        campaignListHeight = list.Size.Y;
        campaignRowsHost = new() { Name = "CampaignHelpRows", Position = list.Position, Size = list.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        directory.AddChild(campaignRowsHost);
        campaignRowsHost.GuiInput += input => campaignScrollbar?.HandleWheel(input);
        campaignScrollHost = new() { Name = "CampaignHelpScrollbars", Size = directory.Size,
            MouseFilter = MouseFilterEnum.Ignore };
        directory.AddChild(campaignScrollHost);

        int count = (int)MathF.Ceiling(list.Size.Y / 50) + 1;
        for (int slot = 0; slot < count; slot++)
        {
            int poolSlot = slot;
            var row = new EventEditorHelpTopicButton { Name = "CampaignHelpRow" + slot,
                InputAdmission = () => CanUse() && topic is null };
            campaignRowsHost.AddChild(row);
            row.Configure(repository, assets, shader, font, "", () =>
            {
                int index = (campaignScrollbar?.Value ?? 0) / 50 + poolSlot;
                if ((uint)index < (uint)campaignTopics.Count) OpenTopic(campaignTopics[index]);
            });
            row.GuiInput += input => campaignScrollbar?.HandleWheel(input);
            rows.Add(row);
        }

        for (int index = 0; index < catalog.Categories.Count; index++)
        {
            int categoryIndex = index;
            var entry = catalog.Categories[index];
            var box = root.Rectangle(8 + index * 2);
            var tab = new UiSourceTabButton { Name = "HelpCategory" + entry.Id,
                Position = box.Position, Size = box.Size, InputAdmission = () => CanUse() && topic is null };
            directory.AddChild(tab);
            uint layout = index == 0 ? 0x172EDC24u : index == catalog.Categories.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u;
            tab.Configure(repository, assets, shader, font, entry.Title, layout);
            tab.Pressed += () => SelectCampaignCategory(categoryIndex);
            campaignTabs.Add(tab);
        }
        PadHint(24, "pad_l1");
        PadHint(27, "pad_r1");
        SelectCampaignCategory(0);

        void PadHint(int node, string icon)
        {
            // Source type21 is a hardware-pad hint, not a mouse button.
            var pad = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_pad_00nsize"));
            var host = new Control { Name = "HelpCategoryPad" + node, Position = root.Rectangle(node).Position,
                Size = pad.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            directory.AddChild(host);
            pad.Art(host, 1, pad.Rectangle(1), assets, shader,
                commonSpriteOverride: "base_pad", commonTexturePack: "texture_pad");
            pad.Art(host, 2, pad.Rectangle(2), assets, shader,
                commonSpriteOverride: icon, commonTexturePack: "texture_pad");
        }
    }

    private void SelectCampaignCategory(int index)
    {
        if ((uint)index >= (uint)catalog.Categories.Count || topic is not null) return;
        if (campaignScrollbar is not null) { campaignScrollbar.CancelDrag(); campaignScrollbar.Hide(); }
        campaignCategory = index;
        var category = catalog.Categories[index];
        campaignTopics = category.Topics;
        if (!campaignScrollbars.TryGetValue(category.Id, out var scrollbar))
        {
            var box = campaignSelector.Rectangle(35);
            scrollbar = new() { Name = "HelpScrollCategory" + category.Id, Position = box.Position, Size = box.Size };
            campaignScrollHost.AddChild(scrollbar);
            int range = Math.Max(0, (int)MathF.Ceiling(campaignTopics.Count * 50 - campaignListHeight));
            scrollbar.Configure(repository, assets, shader, range, currentScrollSpeed?.Invoke() ?? 0,
                _ => RefreshCampaignRows(), currentScrollSpeed: currentScrollSpeed);
            campaignScrollbars.Add(category.Id, scrollbar);
        }
        campaignScrollbar = scrollbar;
        scrollbar.Show();
        for (int tab = 0; tab < campaignTabs.Count; tab++) campaignTabs[tab].Selected = tab == index;
        RefreshCampaignRows();
        SetMeta("source_dictionary_category", category.Id);
    }

    private void RefreshCampaignRows()
    {
        int scroll = campaignScrollbar?.Value ?? 0;
        int first = scroll / 50;
        for (int slot = 0; slot < rows.Count; slot++)
        {
            var row = rows[slot];
            // Disabling cancels a held BaseButton press before another topic
            // takes over this physical row's hit area and release callback.
            row.Disabled = true;
            int index = first + slot;
            bool visible = index < campaignTopics.Count;
            row.Visible = visible;
            if (!visible) continue;
            row.Position = new(0, index * 50 - scroll);
            var entry = campaignTopics[index];
            row.BindTitle(entry.Title, entry.ShowMarker17, entry.ShowMarker18);
            row.SetMeta("help_topic_id", campaignTopics[index].Id);
            row.Disabled = campaignTopics[index].Pages.Length == 0;
        }
    }

    private void AbandonCampaignDirectory()
    {
        foreach (var tab in campaignTabs) { tab.InputAdmission = null; tab.Disabled = true; }
        foreach (var scrollbar in campaignScrollbars.Values) { scrollbar.CancelDrag(); scrollbar.Hide(); }
        campaignAdviser = null;
        currentScrollSpeed = null;
    }
}
