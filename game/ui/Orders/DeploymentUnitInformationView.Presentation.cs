using Godot;
using San14.Data;

internal partial class DeploymentUnitInformationView
{
    private readonly List<AreaAdministrationTraitMarker> unitTraits = [];
    private AreaAdministrationQuery<DeploymentUnitPanePublication>? unitPresentation;
    private UiSourceSimpleTooltip unitTooltip = null!;
    private Control unitSpeechRoot = null!;
    private UiSourceParagraph unitSpeechText = null!;
    private UiSourceText specialNavalText = null!;

    // Called once, after the ordinary unit pane's source/font fields exist.
    internal void ConfigureUnitPresentation(Control tooltipHost)
    {
        var first = source.Rectangle(21); var next = source.Rectangle(23);
        Vector2 delta = next.Position - first.Position;
        for (int index = 0; index < 9; index++)
        {
            int slot = index;
            var marker = new AreaAdministrationTraitMarker { Name = "DeploymentUnitTrait" + index,
                Position = first.Position + new Vector2(index / 5 * delta.X, index % 5 * delta.Y) };
            AddChild(marker); marker.Configure(repository, assets, shader, font, first.Size);
            marker.MouseEntered += () => ShowUnitTraitTooltip(slot);
            marker.MouseExited += () => unitTooltip.Hide();
            marker.GuiInput += input =>
            { if (input is InputEventMouseMotion && unitTooltip.Visible) unitTooltip.UpdatePointer(tooltipHost.GetLocalMousePosition()); };
            marker.Hide(); unitTraits.Add(marker);
        }
        unitTooltip = new UiSourceSimpleTooltip { Name = "DeploymentUnitTraitTooltip" };
        tooltipHost.AddChild(unitTooltip); unitTooltip.Configure(repository, assets, shader, font);
        unitTooltip.Name = "DeploymentUnitTraitTooltip";
        var box = source.Rectangle(4);
        unitSpeechRoot = new Control { Name = "DeploymentUnitCommanderSpeech", Position = box.Position,
            Size = box.Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(unitSpeechRoot);
        var bubble = source.Rectangle(5); bubble.Position -= box.Position;
        source.Art(unitSpeechRoot, 5, bubble, assets, shader);
        var label = source.Rectangle(6); label.Position -= box.Position;
        unitSpeechText = new UiSourceParagraph { Name = "DeploymentUnitSpeechBody",
            Position = label.Position, Size = label.Size };
        unitSpeechRoot.AddChild(unitSpeechText);
        //7D40D0 presets node6 to0. Its temporary width-fit/character-mode
        //setters are superseded by30D290's final3/2, not by clearing flag4.
        unitSpeechText.ConfigureFormattedMessage(font,
            MapFontProfile.FromNode(source.Nodes[6], source.Source.Source.ResourceUri)
                .WithPreset(MapTextPresets.Create()[0]));
        unitSpeechRoot.Hide();
        string caption = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data)
            .Messages.Sections[1].Records[0x06FD].Text!;
        specialNavalText = source.Text(this, 129, default, caption, font, MapTextPresets.Create()[16]);
        specialNavalText.Name = "DeploymentSpecialNavalTactic"; specialNavalText.Hide();
        VisibilityChanged += () =>
        { if (!IsVisibleInTree() && GodotObject.IsInstanceValid(unitTooltip)) unitTooltip.Hide(); };
        TreeExiting += () =>
        { if (GodotObject.IsInstanceValid(unitTooltip)) unitTooltip.QueueFree(); };
    }

    internal void ApplyUnitPresentation(AreaAdministrationQuery<DeploymentUnitPanePublication>? query,
        AreaAdministrationQuery<DeploymentUnitSpeechPublication>? speech, bool? usesSpecialNavalText)
    {
        if (abandoned) return;
        unitPresentation = query; unitTooltip.Hide();
        foreach (var marker in unitTraits) marker.Hide();
        if (query?.Value is { } value)
        {
            for (int index = 0; index < Math.Min(value.Traits.Count, unitTraits.Count); index++)
            {
                var row = value.Traits[index];
                unitTraits[index].Apply(row.LookupTraitId, row.Name, row.Style); unitTraits[index].Show();
            }
        }
        else if (query is not null)
        { unitTraits[0].ApplyUnknown(text.Get("detail_unknown")); unitTraits[0].Show(); }
        specialNavalText.Visible = usesSpecialNavalText == true;
        unitSpeechRoot.Visible = speech is not null;
        unitSpeechText.Apply(speech?.Value?.Text ?? (speech is null ? "" : text.Get("detail_unknown")));
        if (speech?.Value is { } message)
        {
            unitSpeechRoot.SetMeta("officer_id", message.OfficerId);
            unitSpeechRoot.SetMeta("source_message_id", message.MessageId);
            unitSpeechRoot.SetMeta("candidate_index", message.SelectedIndex);
        }
        else
        { unitSpeechRoot.RemoveMeta("officer_id"); unitSpeechRoot.RemoveMeta("source_message_id"); unitSpeechRoot.RemoveMeta("candidate_index"); }
    }

    private void ShowUnitTraitTooltip(int slot)
    {
        unitTooltip.Hide();
        if (abandoned || !IsVisibleInTree() || unitPresentation?.Value is not { } value ||
            slot >= value.Traits.Count || value.TraitTooltip is null) return;
        var query = value.TraitTooltip(value.Traits[slot].LookupTraitId);
        unitTooltip.DisplayText(query.Available ? query.Value : text.Get("detail_unknown"));
    }

    internal void AbandonUnitPresentation()
    {
        unitPresentation = null;
        if (GodotObject.IsInstanceValid(unitTooltip)) unitTooltip.Hide();
    }
}
