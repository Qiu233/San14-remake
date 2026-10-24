using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The merchant's actual common officer pane. The first selected
/// officer and the independent selection-count caption share no domain state.</summary>
internal partial class CityMerchantOfficerView : Control
{
    private AreaAdministrationOfficerView card = null!;
    private UiSourceText selectionCount = null!;
    private string countFormat = "";
    private Action? choose;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, UiAssets assets,
        Shader shader, MapFont font, PreviewText text, UiSourceLayout merchantSource,
        Action choose)
    {
        this.choose = choose;
        Name = "CityMerchantOfficer";
        Position = merchantSource.Rectangle(19).Position;
        MouseFilter = MouseFilterEnum.Ignore;
        var source = new UiSourceLayout(repository, 0xCB7DEBFC);
        Size = source.Rectangle(0).Size;
        card = new AreaAdministrationOfficerView { Name = "MerchantOfficerCard" };
        AddChild(card);
        card.Configure(repository, assets, shader, font, text,
            () => { if (!abandoned) this.choose?.Invoke(); }, tooltipHost: this);
        // 79FEA0 refreshes the first person's common card, then 8A37A0 writes
        // Data1/0323 to node0x41. It never appends the count to the person's name.
        countFormat = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[0x0323].Text ??
            throw new InvalidDataException("Missing merchant selected-person count format.");
        selectionCount = source.Text(this, 65, default, "", font);
        selectionCount.Name = "MerchantSelectedPersonCount";
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_merchant_mount_node", 19);
        Apply(new(null, null, null, null, null), 0, false);
    }

    internal void Apply(AreaAdministrationOfficerValues values, int selectedCount, bool canChoose)
    {
        if (abandoned) return;
        if (selectedCount < 0) throw new ArgumentOutOfRangeException(nameof(selectedCount));
        // Traits are supplied only by the merchant's real context publication.
        // A null publication stays visibly unknown in the shared card.
        card.Apply(values, canChoose);
        selectionCount.Text = selectedCount > 0
            ? countFormat.Replace("%d", selectedCount.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            : "";
    }

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true;
        choose = null;
        card?.Abandon();
        Hide();
    }
}
