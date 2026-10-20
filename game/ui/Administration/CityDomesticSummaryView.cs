using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CityDomesticSummaryValues(string? CityName, int? Gold, bool? Affordable,
    int? Books, int? RemainingBooks, int? BookCost,
    AreaAdministrationValueChange Troops, AreaAdministrationValueChange Morale,
    int? RecruitmentFee, bool? RecruiterValid, int? TrainingFee, bool? TrainerValid);

/// <summary>The city's source summary mounts. Values belong to the private
/// draft publication; drawing does not query or change simulation state.</summary>
internal partial class CityDomesticSummaryView : Control
{
    private UiSourceText city = null!, gold = null!, books = null!, remaining = null!;
    private readonly List<(UiSourceText Before, UiSourceText After)> changes = [];
    private readonly List<UiSourceText> fees = [];
    private string unknown = "";
    private uint increase, decrease;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, PreviewText text)
    {
        Name = "CityDomesticSummary"; MouseFilter = MouseFilterEnum.Ignore;
        var source = new UiSourceLayout(repository, 0xCF0252EE);
        Size = source.Rectangle(0).Size;
        var presets = MapTextPresets.Create();
        unknown = text.Get("detail_unknown");
        increase = presets[38].LT.CpuPacked; decrease = presets[40].LT.CpuPacked;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var single = new UiSourceLayout(repository, 0xB178BC3C);
        var identity = Group("CityIdentity", 138, single);
        foreach (int node in new[] { 1, 2 }) single.Art(identity, node, single.Rectangle(node), assets, shader);
        single.Text(identity, 3, default, Message(0x0478), font, presets[13]);
        city = single.Text(identity, 4, default, unknown, font);

        foreach (int node in new[] { 147, 148, 149, 154, 155 })
            source.Art(this, node, source.Rectangle(node), assets, shader);
        source.Text(this, 150, default, Message(0x0292), font, presets[13]);
        source.Text(this, 156, default, Message(0x020C), font, presets[13]);
        books = source.Text(this, 151, default, unknown, font);
        remaining = source.Text(this, 152, default, unknown, font);
        gold = source.Text(this, 157, default, unknown, font);

        var change = new UiSourceLayout(repository, 0x6AD3C221);
        foreach (var (mount, caption) in new[] { (142, 0x020E), (144, 0x0225) })
        {
            var group = Group(mount == 142 ? "TroopPreview" : "MoralePreview", mount, change);
            foreach (var node in change.Nodes.Where(n => n.Type is 1 or 3))
                change.Art(group, node.Index, change.Rectangle(node.Index), assets, shader);
            change.Text(group, 3, default, Message(caption), font, presets[13]);
            changes.Add((Number(4), Number(6)));
            UiSourceText Number(int node)
            {
                var value = change.Text(group, node, default, unknown, font, presets[27]);
                value.Configure(font, (MapFontProfile.FromNode(change.Nodes[node], change.Source.Source.ResourceUri)
                    with { RawAlign = 0x21 }).WithPreset(presets[27]));
                return value;
            }
        }
        var fee = new UiSourceLayout(repository, 0xC1F1B300);
        foreach (var (mount, period) in new[] { (143, 0x02AC), (145, 0x047A) })
        {
            var group = Group(mount == 143 ? "RecruitmentFee" : "TrainingFee", mount, fee);
            foreach (var node in fee.Nodes.Where(n => n.Type is 1 or 3))
                fee.Art(group, node.Index, fee.Rectangle(node.Index), assets, shader);
            fee.Text(group, 3, default, Message(0x0403), font, presets[13]);
            fees.Add(fee.Text(group, 4, default, unknown, font, presets[27]));
            fee.Text(group, 5, default, "/" + Message(period), font, presets[27]);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        Control Group(string name, int mount, UiSourceLayout child)
        {
            var group = new Control { Name = name, Position = source.Rectangle(mount).Position,
                Size = child.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group); group.SetMeta("source_mount_node", mount);
            group.SetMeta("source_layout", child.Source.Source.ResourceUri); return group;
        }
        string Message(int id) => messages.Records[id].Text ??
            throw new InvalidDataException($"Missing city domestic caption {id:X4}.");
    }

    internal void Apply(CityDomesticSummaryValues value)
    {
        city.Text = value.CityName ?? unknown;
        if (value.Gold is int amount)
            gold.SetRuns(new UiSourceTextRun(Number(amount), value.Affordable == false ? decrease : null));
        else gold.Text = unknown;
        ApplyBooks(value.Books, value.RemainingBooks, value.BookCost);
        AreaAdministrationValueChange[] values = [value.Troops, value.Morale];
        for (int i = 0; i < changes.Count; i++)
        {
            var change = values[i]; changes[i].Before.Text = Number(change.Before);
            if (change.Before is int before && change.After is int next && before != next)
                changes[i].After.SetRuns(new UiSourceTextRun(Number(next), next > before ? increase : decrease));
            else changes[i].After.Text = Number(change.After);
        }
        fees[0].Text = Fee(value.RecruiterValid, value.RecruitmentFee);
        fees[1].Text = Fee(value.TrainerValid, value.TrainingFee);
    }
    internal void ApplyBooks(int? current, int? after, int? cost)
    {
        books.Text = Number(current);
        if (after is int next && cost is int required)
        {
            if (required < 1) remaining.Text = Number(next) + "(-0)";
            else remaining.SetRuns(new(Number(next) + "("), new("-" + Number(required), decrease), new(")"));
        }
        else remaining.Text = unknown;
    }
    private string Fee(bool? valid, int? amount) => valid switch { false => "", true => Number(amount), _ => unknown };
    private string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
}
