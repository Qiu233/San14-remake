using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record AreaAdministrationValueChange(int? Before, int? After);
// Original caption says "occupied land", but this number is the appointed
// officer's occupation gain, not the region's currently owned hex count.
internal sealed record AreaAdministrationSummaryValues(string AreaName, string CityName,
    int? Population, int? OccupationGain, bool HighlightOccupationGain,
    AreaAdministrationValueChange GoldIncome, AreaAdministrationValueChange FoodIncome,
    AreaAdministrationValueChange TroopIncome, AreaAdministrationValueChange PublicOrder);

/// <summary>The region panel's four identity fields and four before/after fields.
/// The caller supplies a single preview publication; drawing never runs rules or
/// temporarily installs the proposed appointments into the campaign world.</summary>
internal partial class AreaAdministrationSummaryView : Control
{
    private readonly List<UiSourceText> identity = [];
    private readonly List<(UiSourceText Before, UiSourceText After)> comparisons = [];
    private MapFont font = null!;
    private MapFontProfile occupationProfile = null!;
    private MapTextPresets presets = null!;
    private string unknown = "";
    private uint increaseColour, decreaseColour;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, PreviewText text)
    {
        this.font = font;
        Name = "AreaAdministrationSummary";
        MouseFilter = MouseFilterEnum.Ignore;
        var panel = new UiSourceLayout(repository, 0xC4464DC9);
        Size = panel.Rectangle(0).Size;
        var single = new UiSourceLayout(repository, 0xB178BC3C);
        var change = new UiSourceLayout(repository, 0xB8DDF15B);
        presets = MapTextPresets.Create();
        unknown = text.Get("detail_unknown");
        increaseColour = presets[38].LT.CpuPacked;
        decreaseColour = presets[40].LT.CpuPacked;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        int[] identityMounts = [98, 99, 100, 158];
        int[] identityCaptions = [0x0477, 0x0478, 0x0298, 0x047B];
        for (int i = 0; i < identityMounts.Length; i++)
        {
            var group = Group("Identity" + i, identityMounts[i], single);
            foreach (int node in new[] { 1, 2 })
                single.Art(group, node, single.Rectangle(node), assets, shader);
            single.Text(group, 3, default, Message(identityCaptions[i]), font, presets[13]);
            var value = single.Text(group, 4, default, unknown, font, presets[0]);
            var profile = MapFontProfile.FromNode(single.Nodes[4], single.Source.Source.ResourceUri)
                with { RawAlign = 0x11 };
            value.Configure(font, profile.WithPreset(presets[0]));
            identity.Add(value);
            if (i == 3) occupationProfile = profile;
        }
        int[] changeMounts = [159, 160, 161, 162];
        int[] changeCaptions = [0x0254, 0x0255, 0x0256, 0x0253];
        for (int i = 0; i < changeMounts.Length; i++)
        {
            var group = Group("Comparison" + i, changeMounts[i], change);
            foreach (int node in new[] { 1, 2, 4 })
                change.Art(group, node, change.Rectangle(node), assets, shader);
            var caption = change.Text(group, 6, default, Message(changeCaptions[i]), font, presets[13]);
            caption.Configure(font, (MapFontProfile.FromNode(change.Nodes[6], change.Source.Source.ResourceUri)
                with { RawAlign = 0x11 }).WithPreset(presets[13]));
            comparisons.Add((Number(3), Number(5)));

            UiSourceText Number(int node)
            {
                var value = change.Text(group, node, default, unknown, font, presets[0]);
                value.Configure(font, (MapFontProfile.FromNode(change.Nodes[node], change.Source.Source.ResourceUri)
                    with { RawAlign = 0x21 }).WithPreset(presets[0]));
                return value;
            }
        }
        SetMeta("source_layout", panel.Source.Source.ResourceUri);

        Control Group(string name, int mount, UiSourceLayout source)
        {
            var group = new Control { Name = name, Position = panel.Rectangle(mount).Position,
                Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group);
            group.SetMeta("source_mount_node", mount);
            group.SetMeta("source_layout", source.Source.Source.ResourceUri);
            return group;
        }
        string Message(int id) => messages.Records[id].Text
            ?? throw new InvalidDataException($"Missing regional administration caption {id:X4}.");
    }

    internal void Apply(AreaAdministrationSummaryValues values)
    {
        identity[0].Text = values.AreaName;
        identity[1].Text = values.CityName;
        identity[2].Text = Number(values.Population);
        identity[3].Configure(font, occupationProfile.WithPreset(presets[values.HighlightOccupationGain ? 66 : 0]));
        identity[3].Text = Number(values.OccupationGain);
        identity[3].QueueRedraw();
        AreaAdministrationValueChange[] changes = [values.GoldIncome, values.FoodIncome,
            values.TroopIncome, values.PublicOrder];
        for (int i = 0; i < changes.Length; i++)
        {
            var (before, after) = comparisons[i];
            var value = changes[i];
            before.Text = Number(value.Before);
            if (value.Before is not int current || value.After is not int proposed || current == proposed)
                after.Text = Number(value.After);
            else
            {
                // Original 7826E0 uses %5d for the two incomes, %d for troops,
                // and %3d for public order. Its ^02/^03 changes only body colour.
                string number = proposed.ToString(CultureInfo.InvariantCulture);
                int width = i switch { 0 or 1 => 5, 3 => 3, _ => 0 };
                after.SetRuns(new UiSourceTextRun(number.PadLeft(width),
                    proposed > current ? increaseColour : decreaseColour));
            }
        }
    }

    private string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
}
