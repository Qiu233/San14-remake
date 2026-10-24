using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Known totals prepared for the income report's numeric child.
/// This is independent of the provisional monthly settlement receipts.</summary>
internal sealed record IncomeReportNumbers(int Gold, int GoldIncome, int GoldExpense,
    int Food, int FoodIncome, int FoodExpense, int Soldiers, int SoldierChange,
    int ActionPoints, int ActionPointIncome, bool ShowActionPointIncome,
    bool HighlightGoldExpense);

/// <summary>The original four numeric rows. The report owner supplies fully
/// resolved totals and the actual tutorial-dependent display decisions.</summary>
internal partial class IncomeReportNumbersView : Control
{
    private readonly Dictionary<int, UiSourceText> values = new();
    private UiSourceText goldExpense = null!;
    private MapFont font = null!;
    private MapFontProfile expenseProfile = null!;
    private MapTextPresets presets = null!;
    private string absent = null!;
    private uint increase, decrease;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string locale, IncomeReportNumbers numbers)
    {
        Configure(repository, assets, shader, font, locale);
        Apply(numbers);
    }

    // The native child is built before selecting a district. A known-empty
    // district list leaves its numeric strings clear, without fabricated totals.
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string locale)
    {
        this.font = font;
        MouseFilter = MouseFilterEnum.Ignore;
        var source = new UiSourceLayout(repository, 0x8A0BAA70);
        Size = source.Rectangle(0).Size;
        presets = MapTextPresets.Create();
        var palettes = MapSpritePalettes.Create();
        increase = presets[38].LT.CpuPacked;
        decrease = presets[40].LT.CpuPacked;
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        string Message(int id) => messages[id].Text
            ?? throw new InvalidDataException("Missing income report caption: " + id);
        absent = Message(0x353);
        foreach (var node in source.Nodes)
        {
            if (node.Type is 1 or 3)
            {
                // The caller applies colour preset10 to the four row panes.
                MapCornerColors? colours = node.Index is 8 or 17 or 26 or 35
                    ? palettes[10]
                    : null;
                source.Art(this, node.Index, source.Rectangle(node.Index), assets, shader, colours);
            }
        }
        foreach (var (node, message) in new (int, int)[]
            { (2, 0x20F), (4, 0x210), (9, 0x20C), (18, 0x20D), (27, 0x20E), (36, 0x835) })
            source.Text(this, node, default, Message(message), font, presets[13]);
        foreach (int node in new[] { 10, 11, 12, 13, 19, 20, 21, 22, 28, 29, 30, 31, 37, 38, 39, 40 })
            values.Add(node, source.Text(this, node, default, "", font));
        goldExpense = values[13];
        expenseProfile = MapFontProfile.FromNode(source.Nodes[13], source.Source.Source.ResourceUri);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Apply(IncomeReportNumbers numbers)
    {
        Plain(10, numbers.Gold);
        Difference(11, unchecked(numbers.GoldIncome - numbers.GoldExpense),
            numbers.GoldIncome.CompareTo(numbers.GoldExpense));
        Plain(12, numbers.GoldIncome);
        // The source only writes preset66 when this condition is true. A
        // later ordinary refresh does not restore the original preset.
        if (numbers.HighlightGoldExpense)
        {
            goldExpense.Configure(font, expenseProfile.WithPreset(presets[66]));
            goldExpense.QueueRedraw();
        }
        Plain(13, numbers.GoldExpense);
        Plain(19, numbers.Food);
        Difference(20, unchecked(numbers.FoodIncome - numbers.FoodExpense),
            numbers.FoodIncome.CompareTo(numbers.FoodExpense));
        Plain(21, numbers.FoodIncome);
        Plain(22, numbers.FoodExpense);
        Plain(28, numbers.Soldiers);
        Difference(29, numbers.SoldierChange, numbers.SoldierChange.CompareTo(0));
        Plain(30, numbers.SoldierChange);
        values[31].Text = absent;
        Plain(37, numbers.ActionPoints);
        if (numbers.ShowActionPointIncome)
        {
            // The original nonpositive branch tests the soldier change when
            // choosing red versus neutral, while printing the AP income.
            int colour = numbers.ActionPointIncome > 0 ? 1 : numbers.SoldierChange < 0 ? -1 : 0;
            Difference(38, numbers.ActionPointIncome, colour);
            Plain(39, numbers.ActionPointIncome);
        }
        else
        {
            values[38].Text = "";
            values[39].Text = absent;
        }
        values[40].Text = absent;
    }

    private void Plain(int node, int number) => values[node].Text = number.ToString(CultureInfo.InvariantCulture);

    private void Difference(int node, int number, int colour)
    {
        string signed = number >= 0 ? "+" + number.ToString(CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
        values[node].SetRuns(new("("), new(signed, colour > 0 ? increase : colour < 0 ? decrease : null), new(")"));
    }
}
