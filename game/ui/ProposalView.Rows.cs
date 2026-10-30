using Godot;
using San14.Data;

internal partial class ProposalView
{
    private readonly Dictionary<int, (UiSourceText Home, UiSourceText Target)> officerFields = [];
    private UiSourceBottomButton? selectAll;
    private readonly Dictionary<(string, string), LoadedSprite> portraitCache = [];

    private void BuildFirst()
    {
        officerFields.Clear(); selectAll = null;
        if (page.FirstRows.Count > 6) throw new InvalidDataException("First proposals exceed their six source mounts.");
        var source = Load("04_00_02_00_kessai");
        Art(source, body, 36);
        source.Text(body, 37, default, Message(0x213), chrome.Font, MapTextPresets.Create()[13]);
        source.Text(body, 38, default, page.RulerName, chrome.Font);
        int[] mounts = [10, 23, 14, 27, 18, 31];
        for (int i = 0; i < page.FirstRows.Count; i++)
        {
            var value = page.FirstRows[i]; bool left = i % 2 == 0;
            var rowSource = Load(left ? "04_00_02_00_kessai_offset_02" : "04_00_02_00_kessai_offset_03");
            var row = Mounted(rowSource, source.Rectangle(mounts[i]).Position, "FirstProposal" + value.Id);
            Art(rowSource, row, 1);
            int variant = left ? 1 : 0;
            rowSource.Text(row, 4 + variant, default, value.ProposerName ?? "", chrome.Font);
            if (ReadPortrait(value) is { } face) Portrait(rowSource, row, 2 + variant, face, 3);
            Art(rowSource, row, 8 + variant);
            Paragraph(rowSource, row, 9 + variant, value.Summary);
            var commandBox = rowSource.Rectangle(6 + variant);
            var command = new UiSourceCommandButton { Name = "AdoptProposal" + value.Id,
                Position = commandBox.Position, Size = commandBox.Size, InputAdmission = CanUse };
            row.AddChild(command);
            command.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, value.Name,
                () => Run(() => selected?.Invoke(value.Id)));
            command.Disabled = !value.CanAdopt || value.Adopted && !value.OpensOfficerPage;
            DetailButton(rowSource, row, 7 + variant, value.Id);
            Stamp(rowSource, row, left ? 18 : 15, value.Adopted && !value.OpensOfficerPage);
            row.SetMeta("proposal_id", value.Id); row.SetMeta("proposal_kind", value.Kind);
            if (value.UnresolvedOwner is { } missing) row.SetMeta("unresolved_owner", missing);
        }
        SetMeta("proposal_count", page.FirstRows.Count);
    }

    private void BuildOfficer()
    {
        officerFields.Clear();
        if (page.OfficerRows.Count > 12) throw new InvalidDataException("Officer proposals exceed their twelve source mounts.");
        var source = Load("04_00_02_01_kessai_list");
        var rowSource = Load("04_00_02_01_kessai_offset_00");
        int[] mounts = [7, 19, 9, 21, 11, 23, 13, 25, 15, 27, 17, 29];
        for (int i = 0; i < page.OfficerRows.Count; i++)
        {
            var value = page.OfficerRows[i];
            var row = Mounted(rowSource, source.Rectangle(mounts[i]).Position, "OfficerProposal" + value.Id);
            Art(rowSource, row, 2); Art(rowSource, row, 10); Art(rowSource, row, 15);
            var hover = new Control { Name = "ProposalHover", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            row.AddChild(hover); Art(rowSource, hover, 18); Art(rowSource, hover, 19);
            var press = new Control { Name = "ProposalPress", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            row.AddChild(press); Art(rowSource, press, 21); Art(rowSource, press, 22);
            var hit = new ProposalRowHit { Name = "ToggleProposal" + value.Id,
                Position = rowSource.Rectangle(1).Position, Size = rowSource.Rectangle(1).Size,
                ActionMode = BaseButton.ActionModeEnum.Release, Admission = CanUse,
                Disabled = !value.CanAdopt || value.Adopted };
            row.AddChild(hit);
            hit.MouseEntered += () => hover.Visible = !hit.Disabled;
            hit.MouseExited += () => { hover.Hide(); press.Hide(); };
            hit.ButtonDown += () => { if (!hit.Disabled) press.Show(); };
            hit.ButtonUp += press.Hide;
            hit.Pressed += () => Run(() => { if (!hit.Disabled) selected?.Invoke(value.Id); });
            rowSource.Text(row, 5, default, value.ProposerName ?? "", chrome.Font);
            var home = rowSource.Text(row, 6, default, value.HomeCityName ?? "", chrome.Font);
            var target = rowSource.Text(row, 7, default, value.TargetName ?? "", chrome.Font);
            target.Visible = value.TargetName is not null;
            officerFields.Add(value.Id, (home, target));
            rowSource.Text(row, 14, default, value.Name, chrome.Font);
            Paragraph(rowSource, row, 11, value.Summary);
            if (ReadPortrait(value) is { } face) Portrait(rowSource, row, 3, face, 2);
            var check = rowSource.Art(row, 16, rowSource.Rectangle(16), chrome.Assets, chrome.SpriteShader);
            check.Visible = selection.Contains(value.Id);
            selectionWriters.Add(value.Id, enabled => check.Visible = enabled);
            DetailButton(rowSource, row, 12, value.Id);
            Stamp(rowSource, row, 27, value.Adopted);
            row.SetMeta("proposal_id", value.Id); row.SetMeta("proposal_kind", value.Kind);
            if (value.UnresolvedOwner is { } missing) row.SetMeta("unresolved_owner", missing);
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        selectAll = new UiSourceBottomButton { Name = "SelectAllProposals",
            Position = footer.Rectangle(14, new(0, 904)).Position, InputAdmission = CanUse };
        body.AddChild(selectAll);
        selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x3A),
            () => Run(() => SelectAllRequested?.Invoke()));
        ApplySelectAll(page.OfficerRows.Any(row => row.CanAdopt && !row.Adopted),
            page.OfficerRows.Where(row => row.CanAdopt && !row.Adopted).All(row => selection.Contains(row.Id)));
        SetMeta("proposal_count", page.OfficerRows.Count);
    }

    /// <summary>The presentation query supplies the actual home/target names;
    /// neither is inferred from the proposer or current player selection.</summary>
    internal void ApplyOfficerFields(int proposalId, string? homeCity, string? target)
    {
        if (abandoned || !officerFields.TryGetValue(proposalId, out var fields)) return;
        fields.Home.Text = homeCity ?? ""; fields.Target.Text = target ?? "";
    }

    internal void ApplySelectAll(bool enabled, bool clearSelection)
    {
        if (abandoned || selectAll is null) return;
        selectAll.Disabled = !enabled; selectAll.ApplyCaption(Message(clearSelection ? 0x3B : 0x3A));
    }

    private void DetailButton(UiSourceLayout source, Control parent, int node, int id)
    {
        var button = new UiSourceFunctionButton { Name = "ProposalMapDetail" + id,
            Position = source.Rectangle(node).Position, InputAdmission = CanUse };
        parent.AddChild(button);
        button.Configure(repository, chrome.Assets, chrome.SpriteShader, "000_cmn_icon_map_mode",
            () => Run(() => DetailRequested?.Invoke(id)));
    }

    private void BuildBudget()
    {
        var root = Load(officerPage ? "04_00_02_01_kessai_list" : "04_00_02_00_kessai");
        var source = Load("04_00_02_00_kessai_offset_00");
        var box = root.Rectangle(officerPage ? 6 : 2);
        var header = new Control { Name = "ProposalBudget", Position = box.Position,
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        body.AddChild(header);
        Art(source, header, 1); Art(source, header, 2); Art(source, header, 5); Art(source, header, 10);
        source.Text(header, 3, default, Message(0x292), chrome.Font, MapTextPresets.Create()[13]);
        source.Text(header, 8, default, Message(0x20C), chrome.Font, MapTextPresets.Create()[13]);
        int? actionCost = officerPage ? page.ActionPointCost : 0;
        var chosen = page.OfficerRows.Where(row => selection.Contains(row.Id)).ToArray();
        long? goldCost = officerPage
            ? chosen.All(row => row.Cost.HasValue) ? chosen.Sum(row => (long)row.Cost!.Value) : null
            : 0;
        string Number(long? number) => number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");
        source.Text(header, 4, default, Number(page.AvailableActionPoints), chrome.Font);
        source.Text(header, 6, default, Number(page.AvailableActionPoints is int ap && actionCost is int cost ? ap - cost : null), chrome.Font);
        source.Text(header, 7, default, actionCost is > 0 ? "(-" + Number(actionCost) + ")" : "", chrome.Font);
        source.Text(header, 9, default, Number(page.AvailableGold), chrome.Font);
        source.Text(header, 11, default, Number(page.AvailableGold is long money && goldCost is long expense ? Math.Max(0, money - expense) : null), chrome.Font);
        source.Text(header, 12, default, goldCost is > 0 ? "(-" + Number(goldCost) + ")" : "", chrome.Font);
    }

    private LoadedSprite? ReadPortrait(ProposalRow row)
    {
        if (portrait is not null) return portrait(row);
        if (row.Portrait is not { Kind: OfficerPortraitRouteKind.StockNamed,
            ResourceName: { } pack, SpriteName: { } name }) return null;
        if (!portraitCache.TryGetValue((pack, name), out var sprite))
        {
            sprite = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(pack, name);
            portraitCache.Add((pack, name), sprite);
        }
        return sprite;
    }

    private void Paragraph(UiSourceLayout source, Control parent, int node, string value)
    {
        var box = source.Rectangle(node);
        var paragraph = new UiSourceParagraph { Name = "ProposalSummary", Position = box.Position, Size = box.Size };
        parent.AddChild(paragraph);
        paragraph.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
            with { WidthFitMode = 3, RawCharacterMode = 2, RawLineArrange = 1, RawAlign = 0 });
        paragraph.Apply(value);
    }

    private void Stamp(UiSourceLayout source, Control parent, int mount, bool adopted)
    {
        if (!adopted) return;
        var stamp = Load("04_00_02_00_kessai_offset_10");
        var position = source.Rectangle(mount).Position;
        stamp.Art(parent, 2, stamp.Rectangle(2, position), chrome.Assets, chrome.SpriteShader);
        stamp.Text(parent, 3, position, Message(0x688), chrome.Font, MapTextPresets.Create()[13]);
    }

    private void Portrait(UiSourceLayout source, Control parent, int node, LoadedSprite sprite, int kind)
    {
        var box = source.Rectangle(node);
        var picture = new UiSourcePortrait { Name = "ProposalPortrait", Position = box.Position, Size = box.Size };
        parent.AddChild(picture);
        switch (kind)
        {
            case 2: picture.ConfigureFace2(repository, chrome.Assets, chrome.SpriteShader, sprite); break;
            case 3: picture.ConfigureFace3(repository, chrome.Assets, chrome.SpriteShader, sprite); break;
            case 4: picture.Configure(repository, chrome.Assets, chrome.SpriteShader, sprite); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}

/// <summary>The officer row has its own authored hover and push windows.</summary>
internal partial class ProposalRowHit : UiSourceHitButton
{
    internal Func<bool>? Admission { get; init; }
    public override bool _HasPoint(Vector2 point) => Admission?.Invoke() == true && base._HasPoint(point);
}
