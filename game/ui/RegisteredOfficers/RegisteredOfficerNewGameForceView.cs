using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The four original new-force cards over a reversible new-game
/// draft. Candidate and ownership rules remain in the data draft; this page
/// never mutates a running world.</summary>
internal partial class RegisteredOfficerNewGameForceView : Control
{
    private sealed record Card(Control Root, UiSourceCommandButton Create,
        Button Delete, UiSourceCommandButton[] Commands, UiSourceText[] Values,
        Control ColourHost, Control PortraitHost, UiSourceText Number)
    {
        internal int? DrawnColour { get; set; }
        internal int? DrawnPortrait { get; set; }
    }

    private RegisteredOfficerNewGameDraft draft = null!;
    private OfficerEditorPortraitCatalog? portraits;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private AdministrationPageChrome chrome = null!;
    private UiSourceLayout parts = null!;
    private UiSourceLayout portraitLayout = null!;
    private LocalizedUiSpriteSource portraitSprites = null!;
    private readonly Dictionary<int, LoadedSprite> loadedPortraits = [];
    private readonly List<Card> cards = [];
    private Func<bool>? admitted;
    private Func<int> scrollSpeed = () => 3;
    private Action? changed, returned;
    private Action<IReadOnlyList<int>, Action<int?>>? chooseCity;
    private CommandSelectionView? selector;
    private DeploymentTargetConfirmation? confirmation;
    private (int Index, int Rank)? pendingRank;
    private bool abandoned, busy, picking;

    internal void Configure(RegisteredOfficerNewGameDraft draft, GameResourceRepository repository,
        PreviewText text, Func<bool> admitted, Action changed, Action returned,
        Func<int>? scrollSpeed = null, Action<IReadOnlyList<int>, Action<int?>>? chooseCity = null,
        OfficerEditorPortraitCatalog? portraits=null)
    {
        this.draft = draft; this.repository = repository; this.text = text;this.portraits=portraits;
        draft.LoadChoices(repository);
        this.admitted = admitted; this.changed = changed; this.returned = returned;
        this.chooseCity = chooseCity;
        if (scrollSpeed is not null) this.scrollSpeed = scrollSpeed;
        Name = "RegisteredOfficerNewGameForces"; ZIndex = 7;
        MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x004C, RequestBack, RequestBack, returnOnly: true);
        chrome.ApplyDefaultHint(GameMessage(0x27D4));
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_02_edit_new"));
        parts = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_02_offset_00"));
        portraitLayout = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_02_offset_03"));
        portraitSprites = new(repository);
        int[] captions = [0x213, 0x206, 0x205, 0x4D, 0x21B, 0x3C8];
        int[] mounts = [8, 13, 18, 23, 30, 35];
        int[] valueNodes = [10, 15, 20, 27, 32, 37];
        for (int index = 0; index < 4; index++)
        {
            int cardIndex = index;
            Rect2 box = source.Rectangle(2 + index * 4);
            var root = new Control { Name = "NewForceCard" + index, Position = box.Position,
                Size = box.Size, MouseFilter = MouseFilterEnum.Ignore };
            chrome.Canvas.AddChild(root);
            parts.Blur(root, 1, parts.Rectangle(1));
            parts.Art(root, 3, parts.Rectangle(3), chrome.Assets, chrome.SpriteShader);
            var portraitHost = new Control { Name = "NewForceRulerPortrait", Position = parts.Rectangle(4).Position,
                Size = parts.Rectangle(4).Size, MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(portraitHost);
            var commands = new UiSourceCommandButton[6];
            var values = new UiSourceText[6];
            for (int field = 0; field < 6; field++)
            {
                int command = field;
                commands[field] = Command(root, parts.Rectangle(mounts[field]), Message(captions[field]),
                    () => Edit(cardIndex, command));
                values[field] = parts.Text(root, valueNodes[field], default, "", chrome.Font);
            }
            var colourHost = new Control { Name = "NewForceColour", Position = parts.Rectangle(25).Position,
                Size = parts.Rectangle(25).Size, MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(colourHost);
            // The colour swatch is the source flag pane with the selected
            // source palette; it is not a generated colour-name label.
            var create = Command(root, parts.Rectangle(42), Message(0x4C), StartCreate);
            parts.Art(root, 39, parts.Rectangle(39), chrome.Assets, chrome.SpriteShader);
            parts.Art(root, 40, parts.Rectangle(40), chrome.Assets, chrome.SpriteShader);
            var delete = new Button { Name = "ClearNewForce" + index, Flat = true,
                Position = parts.Rectangle(38).Position, Size = parts.Rectangle(38).Size, Text = "" };
            root.AddChild(delete); delete.Pressed += () => Remove(cardIndex);
            var number = parts.Text(root, 47, default, (index + 1).ToString(), chrome.Font);
            cards.Add(new(root, create, delete, commands, values, colourHost, portraitHost, number));
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Refresh();
    }

    private UiSourceCommandButton Command(Control parent, Rect2 box, string caption, Action action)
    {
        var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
        parent.AddChild(button);
        button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, action);
        return button;
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing new-force caption.");
    private string GameMessage(int id) => RegisteredOfficerNewGameMessages.Format(repository, text.SourceLocale, id, draft);
    private bool PageAdmitted() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private bool CanEdit() => PageAdmitted() && selector is null && confirmation is null && !picking;
    private string CityName(int id) => draft.Cities.FirstOrDefault(city => city.Slot == id)?.Name ?? "";

    internal void Refresh()
    {
        if (abandoned || chrome is null) return;
        var forces = draft.Selection.NewForces;
        for (int index = 0; index < cards.Count; index++)
        {
            Card card = cards[index]; bool existing = index < forces.Length;
            card.Root.Visible = existing || index == forces.Length;
            card.Create.Visible = !existing;
            card.Create.Disabled = !CanEdit() || !draft.CanFoundForce;
            card.Delete.Visible = existing; card.Delete.Disabled = !CanEdit();
            foreach (var command in card.Commands) { command.Visible = existing; command.Disabled = !CanEdit(); }
            foreach (var value in card.Values) value.Visible = existing;
            card.ColourHost.Visible = existing; card.PortraitHost.Visible = existing; card.Number.Visible = true;
            if (!existing) continue;
            var force = forces[index];
            card.Values[0].SetKnownColourLine(draft.Officer(force.RulerOfficerId).Name);
            card.Values[1].SetKnownColourLine(CityName(force.CityId));
            card.Values[2].SetKnownColourLine((force.AdditionalCityIds.Length + 1).ToString());
            card.Values[3].SetKnownColourLine("");
            card.Values[4].SetKnownColourLine(draft.Ranks.FirstOrDefault(choice => choice.Id == force.RankId)?.Text ?? "");
            card.Values[5].SetKnownColourLine(draft.KingdomText(force.KingdomId));
            card.Commands[5].Disabled = !CanEdit() || force.RankId != 1;
            ApplyPortrait(card, draft.Officer(force.RulerOfficerId));
            if (card.DrawnColour == force.ColourId) continue;
            foreach (Node child in card.ColourHost.GetChildren()) { card.ColourHost.RemoveChild(child); child.QueueFree(); }
            var colour = MapSpritePalettes.ForceFill(force.ColourId);
            Rect2 flag = parts.Rectangle(26); flag.Position -= card.ColourHost.Position;
            parts.Art(card.ColourHost, 26, flag, chrome.Assets, chrome.SpriteShader,
                new MapCornerColors(colour, colour, colour, colour));
            card.DrawnColour = force.ColourId;
        }
        chrome.ApplyReturn(!busy);
    }

    private void ApplyPortrait(Card card, San14.Core.RegisteredOfficer officer)
    {
        if (card.DrawnPortrait == officer.PortraitId) return;
        if (!loadedPortraits.TryGetValue(officer.PortraitId, out var loaded))
        {
            var route = RegisteredOfficerNewGamePortraits.Resolve(officer,portraits);
            loaded = portraitSprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName);
            loadedPortraits.Add(officer.PortraitId, loaded);
        }
        var group = LcskSpriteNodeReader.Read(portraitLayout.Source.Layout, portraitLayout.Nodes[1]).Groups.Single();
        if (group.ComponentCount != 3 || group.Parts.Count != 3 ||
            group.Parts[0].RawBytes[6] != 0 || group.Parts[0].RawBytes[7] != 0 || group.Parts[0].RawBytes[8] != 0 ||
            group.Parts[1].RawBytes[6] != 3 || group.Parts[1].RawBytes[7] != 1 || group.Parts[1].RawBytes[8] != 0 ||
            group.Parts[2].RawBytes[6] != 3 || group.Parts[2].RawBytes[7] != 0 || group.Parts[2].RawBytes[8] != 0)
            throw new InvalidDataException("New-force card portrait requires its source three-texture pane.");
        foreach (Node child in card.PortraitHost.GetChildren())
        { card.PortraitHost.RemoveChild(child); child.QueueFree(); }
        Rect2 box = portraitLayout.Rectangle(1);
        var image = new UiSourceSprite { Name = "NewForceRulerImage", Position = box.Position, Size = box.Size };
        card.PortraitHost.AddChild(image);
        Texture2D texture = MapPortraitQuad.UsesSrgbView(repository, chrome.Assets, loaded.Definition.Name, loaded)
            ? chrome.Assets.SrgbSpritePart(loaded.Definition.Name, group.Parts[0].Uv, loaded)
            : chrome.Assets.SpritePart(loaded.Definition.Name, group.Parts[0].Uv, loaded);
        Texture2D Mask(LcskSpriteNodePart part) => MapPortraitQuad.UsesSrgbView(repository, chrome.Assets, part.SpriteName)
            ? chrome.Assets.SrgbLayoutSpritePart(portraitLayout.Source, part.SpriteName, part.Uv)
            : chrome.Assets.SpritePart(part.SpriteName, part.Uv);
        image.Configure(chrome.SpriteShader, texture, UiSourceSprite.Blend.ThreeTexturePortrait);
        image.SetClipping(texture, Mask(group.Parts[1]), new(Vector2.Zero, box.Size),
            Mask(group.Parts[2]), new(Vector2.Zero, box.Size));
        portraitLayout.Art(card.PortraitHost, 3, portraitLayout.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        card.PortraitHost.SetMeta("source_layout", portraitLayout.Source.Source.ResourceUri);
        card.PortraitHost.SetMeta("portrait_sprite", loaded.Definition.Name);
        card.DrawnPortrait = officer.PortraitId;
    }

    private void Edit(int index, int field)
    {
        if (!CanEdit() || index >= draft.Selection.NewForces.Length) return;
        var force = draft.Selection.NewForces[index];
        switch (field)
        {
            case 0:
                ChooseRuler(force.RulerOfficerId, ruler =>
                { draft.UpdateForce(index, ruler, force.CityId); Publish(); });
                break;
            case 1:
                ChooseCity(index, true, force.CityId, city =>
                {
                    void Apply() { draft.UpdateForce(index, force.RulerOfficerId, city); Publish(); }
                    if (draft.ForceCapitalChangeNeedsConfirmation(index, city))
                        Confirm(0x27DE, yes => { if (yes) Apply(); });
                    else Apply();
                });
                break;
            case 2:
                ChooseCities(index);
                break;
            case 3:
                Choose(0x4D, Enumerable.Range(50, 10).Select(id =>
                    new CommandSelectionRow(id, [new((id - 49).ToString())], true)).ToArray(),
                    [force.ColourId], 1, true, selected =>
                    { draft.SetForceColour(index, selected[0]); Publish(); });
                break;
            case 4:
                Choose(0x21B, draft.Ranks.Select(choice => ChoiceRow(choice.Id, choice.Text)).ToArray(),
                    [force.RankId], 1, true, selected =>
                    {
                        draft.SetForceRank(index, selected[0]);
                        if (selected[0] == 1 && draft.Selection.NewForces[index].KingdomId == 0)
                        {
                            pendingRank = (index, force.RankId);
                            ChooseKingdom(index, force.RankId);
                        }
                        else Publish();
                    }, GameMessage(0x27DF));
                break;
            case 5:
                if (force.RankId == 1) ChooseKingdom(index);
                break;
        }
    }

    private void StartCreate()
    {
        if (!CanEdit() || !draft.CanFoundForce) return;
        ChooseRuler(null, ruler => ChooseCity(-1, true, null, city =>
        { draft.FoundForce(ruler, city); Publish(); }));
    }

    private void ChooseRuler(int? current, Action<int> selected)
    {
        var rows = draft.Selection.AdoptedOfficerIds.Select(id => new CommandSelectionRow(id,
            [CommandSelectionCell.PersonName(draft.Officer(id).Name, draft.Officer(id).FamilyNameSortKey + draft.Officer(id).GivenNameSortKey),
                new(draft.Age(id).ToString(), draft.Age(id))],
            draft.IsAdult(id) && (!draft.IsRuler(id) || current == id))).ToArray();
        Choose(0x32E, rows, current is int value ? [value] : [], 1, true,
            result => selected(result[0]), GameMessage(0x27D8),
            [new("name", Message(0x51), 360), new("age", Message(0x4E7), 180, true)]);
    }

    private void ChooseCity(int index, bool capital, int? current, Action<int> selected)
    {
        if (chooseCity is not null)
        {
            var eligible = draft.Cities.Where(city => draft.CanChooseForceCity(index, city.Slot, capital)).Select(city => city.Slot).ToArray();
            picking = true; chrome.Hide(); Refresh();
            chooseCity(eligible, city =>
            {
                if (abandoned) return;
                picking = false; chrome.Show(); Refresh();
                if (city is int id) { if (!eligible.Contains(id)) throw new InvalidDataException("registered_officers.new_game.force_city_picker_result"); selected(id); }
            });
            return;
        }
        var rows = draft.Cities.Where(city => city.Slot is >= 1 and <= 51).Select(city =>
        {
            bool enabled = draft.CanChooseForceCity(index, city.Slot, capital);
            return new CommandSelectionRow(city.Slot, [new(city.Name)], enabled,
                enabled ? null : GameMessage(capital ? 0x27DA : 0x27DD));
        }).ToArray();
        Choose(0x206, rows, current is int value ? [value] : [], 1, true,
            result => selected(result[0]), GameMessage(capital ? 0x27D9 : 0x27DB));
    }

    private void ChooseCities(int index)
    {
        if (chooseCity is not null)
        {
            PickNext();
            return;
            void PickNext()
            {
                if (abandoned) return;
                var forceNow = draft.Selection.NewForces[index];
                var eligible = draft.Cities.Where(city => draft.CanChooseForceCity(index, city.Slot, false))
                    .Select(city => city.Slot).ToArray();
                picking = true; chrome.Hide(); Refresh();
                chooseCity(eligible, city =>
                {
                    if (abandoned) return;
                    picking = false; chrome.Show(); Refresh();
                    if (city is not int id) return;
                    if (!eligible.Contains(id)) throw new InvalidDataException("registered_officers.new_game.force_city_picker_result");
                    var selected = forceNow.AdditionalCityIds.Contains(id)
                        ? forceNow.AdditionalCityIds.Remove(id) : forceNow.AdditionalCityIds.Add(id);
                    void Apply() { draft.SetForceCities(index, selected); Publish(); PickNext(); }
                    if (draft.ForceCitiesChangeNeedsConfirmation(index, selected))
                        Confirm(0x27DE, yes => { if (yes) Apply(); else PickNext(); });
                    else Apply();
                });
            }
        }
        var force = draft.Selection.NewForces[index];
        var rows = draft.Cities.Where(city => city.Slot is >= 1 and <= 51 && city.Slot != force.CityId)
            .Select(city =>
            {
                bool enabled = draft.CanChooseForceCity(index, city.Slot, false);
                return new CommandSelectionRow(city.Slot, [new(city.Name)], enabled, enabled ? null : GameMessage(0x27DD));
            }).ToArray();
        Choose(0x205, rows, force.AdditionalCityIds, rows.Length, false, result =>
        {
            void Apply() { draft.SetForceCities(index, result); Publish(); }
            if (draft.ForceCitiesChangeNeedsConfirmation(index, result))
                Confirm(0x27DE, yes => { if (yes) Apply(); });
            else Apply();
        }, GameMessage(0x27DB));
    }

    private void ChooseKingdom(int index, int? restoreRank = null)
    {
        var force = draft.Selection.NewForces[index];
        var used = draft.Selection.NewForces.Where((_, slot) => slot != index).Select(item => item.KingdomId).ToHashSet();
        var rows = draft.Kingdoms.Where(choice => !used.Contains(choice.Id))
            .Select(choice => ChoiceRow(choice.Id, choice.Text)).ToArray();
        Choose(0x3C8, rows, [force.KingdomId], 1, true, result =>
        { draft.SetForceKingdom(index, result[0]); pendingRank = null; Publish(); }, GameMessage(0x27E1), cancelled: () =>
        {
            if (restoreRank is int original) { draft.SetForceRank(index, original); pendingRank = null; Publish(); }
        });
    }

    private static CommandSelectionRow ChoiceRow(int id, string caption) => new(id, [new(caption)], true);

    private void Choose(int title, IReadOnlyList<CommandSelectionRow> rows, IReadOnlyList<int> initial,
        int maximum, bool single, Action<IReadOnlyList<int>> accepted, string? guide = null,
        IReadOnlyList<CommandSelectionColumn>? columns = null, Action? cancelled = null)
    {
        if (abandoned) return;
        selector = new(); AddChild(selector);
        selector.Configure(repository, text, title, columns ?? [new("name", Message(title), 900)],
            rows, initial, maximum, PageAdmitted, result =>
            {
                var old = selector; selector = null;
                if (old is not null) { old.Abandon(); RemoveChild(old); old.QueueFree(); }
                if (abandoned) return;
                if (result is not null) accepted(result); else cancelled?.Invoke();
                Refresh();
            }, scrollSpeed, guide, requireSelection: single, singleSelection: single);
        Refresh();
    }

    private void Remove(int index)
    {
        if (!CanEdit() || index >= draft.Selection.NewForces.Length) return;
        Confirm(0x27E2, yes =>
        {
            if (!yes) return;
            var force = draft.Selection.NewForces[index];
            if (draft.Selection.Placements.Count(entry => entry.Serving &&
                (entry.CityId == force.CityId || force.AdditionalCityIds.Contains(entry.CityId))) > 1)
                Confirm(0x27E3, confirmed => { if (confirmed) Clear(); });
            else Clear();
        });
        void Clear() { draft.RemoveForce(index); Publish(); }
    }

    private void Confirm(int message, Action<bool> closed)
    {
        confirmation = new(); AddChild(confirmation);
        confirmation.ConfigureGameMessage(repository, text, message, result =>
        {
            var old = confirmation; confirmation = null;
            if (old is not null) { old.Abandon(); RemoveChild(old); old.QueueFree(); }
            if (abandoned) return;
            closed(result); Refresh();
        });
        Refresh();
    }

    private void Publish() { Refresh(); changed?.Invoke(); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void RequestBack()
    {
        if (selector is not null) { selector.RequestCancel(); return; }
        if (confirmation is not null) { confirmation.RequestCancel(); return; }
        if (CanEdit()) returned?.Invoke();
    }
    internal void Abandon()
    {
        if (pendingRank is { } pending)
        { draft.SetForceRank(pending.Index, pending.Rank); pendingRank = null; }
        abandoned = true; admitted = null; changed = null; returned = null; chooseCity = null;
        selector?.Abandon(); confirmation?.Abandon();
        foreach (var card in cards)
        { card.Create.Abandon(); foreach (var command in card.Commands) command.Abandon(); card.Delete.Disabled = true; }
        chrome?.Abandon(); Hide();
    }
}
