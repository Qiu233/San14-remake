using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The merchant's source page. All amounts and capabilities are
/// published by its draft; this view never reads the simulation.</summary>
internal partial class CityMerchantView : Control
{
    private AdministrationPageChrome chrome = null!;
    private CityMerchantOfficerView officer = null!;
    private UiSourceRadioButton buy = null!, sell = null!;
    private UiSourceCommandButton reset = null!;
    private readonly Dictionary<int, UiSourceText> values = [];
    private Action? returned, accepted, chooseOfficers, clear;
    private Action<CityMerchantMode>? chooseAmount;
    private string unknown = "", priceFormat = "", buyFormat = "", sellFormat = "";
    private uint increase, decrease;
    private CityMerchantPage? page;
    private AreaAdministrationOfficerValues card = new(null, null, null, null, null);
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Action returned, Action accepted, Action chooseOfficers,
        Action<CityMerchantMode> chooseAmount, Action reset)
    {
        this.returned = returned; this.accepted = accepted; this.chooseOfficers = chooseOfficers;
        this.chooseAmount = chooseAmount; clear = reset;
        Name = "CityMerchant"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new() { Name = "MerchantChrome" }; AddChild(chrome);
        chrome.Configure(repository, text, 0x02B0, RequestBack,
            () => { if (!abandoned && !busy) this.accepted?.Invoke(); });
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        var source = new UiSourceLayout(repository, 0xC8EDC19A);
        var panel = new Control { Name = "MerchantPanel", Position = new(460, 229),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Stop };
        chrome.Canvas.AddChild(panel);
        foreach (var node in source.Nodes.Where(n => n.Type is 1 or 3))
        {
            if (Replaced(node.Index)) continue;
            source.Art(panel, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        }
        bool Replaced(int index)
        {
            for (int? current = index; current is int i; current = source.Nodes[i].ParentIndex)
                if (i is 19 or 90 or 91 or 92) return true;
            return false;
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing merchant caption {id:X4}.");
        var presets = MapTextPresets.Create();
        increase = presets[38].LT.CpuPacked; decrease = presets[40].LT.CpuPacked;
        unknown = text.Get("detail_unknown");
        priceFormat = Message(0x03BC); buyFormat = Message(0x03C0); sellFormat = Message(0x03C1);
        foreach (var (node, caption) in new[] { (4, 0x0478), (10, 0x06F1), (128, 0x06F2),
            (17, 0x0541), (86, 0x0637), (101, 0x020C), (110, 0x020D), (119, 0x0292) })
            source.Text(panel, node, default, Message(caption), chrome.Font, presets[13]);
        foreach (int node in new[] { 7, 13, 131, 89, 104, 106, 107, 113, 115, 116, 122, 124, 125 })
        {
            var field = source.Text(panel, node, default, unknown, chrome.Font);
            field.Name = "MerchantValue" + node; values.Add(node, field);
        }
        officer = new(); panel.AddChild(officer);
        officer.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text, source,
            () => { if (!abandoned && !busy) this.chooseOfficers?.Invoke(); });
        buy = Radio(90, CityMerchantMode.Buy, UiSourceRadioSegment.Left, 0x02B1);
        sell = Radio(91, CityMerchantMode.Sell, UiSourceRadioSegment.Right, 0x02B2);
        var box = source.Rectangle(92);
        this.reset = new() { Name = "MerchantReset", Position = box.Position, Size = box.Size };
        panel.AddChild(this.reset);
        this.reset.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x029F),
            () => { if (!abandoned && !busy) clear?.Invoke(); });
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        UiSourceRadioButton Radio(int node, CityMerchantMode mode, UiSourceRadioSegment segment, int caption)
        {
            var rectangle = source.Rectangle(node);
            var button = new UiSourceRadioButton { Name = mode == CityMerchantMode.Buy ? "MerchantBuy" : "MerchantSell",
                Position = rectangle.Position, Size = rectangle.Size, Disabled = true };
            panel.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, Message(caption),
                () => { if (!abandoned && !busy) this.chooseAmount?.Invoke(mode); });
            return button;
        }
    }

    internal void Apply(CityMerchantPage publication, AreaAdministrationOfficerValues officerCard, string? hint)
    {
        if (abandoned) return;
        page = publication; card = officerCard;
        values[7].Text = publication.CityName;
        values[13].Text = Price(publication.BuyPrice); values[131].Text = Price(publication.SellPrice);
        values[89].Text = Format(publication.Mode == CityMerchantMode.Buy ? buyFormat : sellFormat, publication.Amount);
        values[104].Text = Number(publication.Gold); values[106].Text = Number(publication.AfterGold);
        values[113].Text = Number(publication.Food); values[115].Text = Number(publication.AfterFood);
        values[122].Text = Number(publication.Books); values[124].Text = Number(publication.AfterBooks);
        Delta(values[107], publication.GoldDelta); Delta(values[116], publication.FoodDelta);
        Delta(values[125], -publication.CommandCost, negativeZero: true);
        chrome.ApplyDefaultHint(hint ?? unknown);
        RefreshAdmission();
    }
    private void RefreshAdmission()
    {
        if (page is not { } value || abandoned) return;
        officer.Apply(card, value.SelectedOfficerIds.Count, !busy && value.Candidates.Count > 0);
        buy.Selected = value.Mode == CityMerchantMode.Buy; sell.Selected = value.Mode == CityMerchantMode.Sell;
        buy.Disabled = busy || value.CanBuy != true; sell.Disabled = busy || value.CanSell != true;
        reset.Disabled = busy;
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && value.CanConfirm == true, true);
    }
    internal void SetBusy(bool value) { if (abandoned) return; busy = value; RefreshAdmission(); }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (!abandoned && !busy) returned?.Invoke(); }
    private string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
    private string Price(int? value) => value is int price ? Format(priceFormat, 10, price) : unknown;
    private static string Format(string template, params int[] arguments)
    {
        string result = template;
        foreach (int argument in arguments)
        {
            int position = result.IndexOf("%d", StringComparison.Ordinal);
            if (position < 0) throw new InvalidDataException("Merchant number format has no integer placeholder.");
            result = result[..position] + argument.ToString(CultureInfo.InvariantCulture) + result[(position + 2)..];
        }
        return result;
    }
    private void Delta(UiSourceText field, int? delta, bool negativeZero = false)
    {
        if (delta is not int value) { field.Text = unknown; return; }
        if (value == 0) { field.Text = negativeZero ? "(-0)" : "(+0)"; return; }
        field.SetRuns(new("("), new(value > 0 ? "+" + Number(value) : Number(value), value > 0 ? increase : decrease), new(")"));
    }
    public override void _Process(double delta) { if (!abandoned && chrome is not null) chrome.ApplyHoverHint(null); }
    internal void Abandon()
    {
        abandoned = true; returned = accepted = chooseOfficers = clear = null; chooseAmount = null; page = null;
        officer?.Abandon(); buy?.Abandon(); sell?.Abandon(); reset?.Abandon(); chrome?.Abandon();
        SetProcess(false); Hide();
    }
}
