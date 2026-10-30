using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;
using System.Globalization;
using System.Collections.Immutable;

public partial class Main
{
    private PersonnelItemPage? _personnelItemPage;
    private GameplaySession? _personnelItemSession;
    private GameState? _personnelItemWorld;
    private MapSceneView? _personnelItemMap;
    private CommandSelectionView? _personnelItemSelector;
    private PersonnelItemPersonSelectionView? _personnelItemTargetSelector;
    private Action? _refreshPersonnelItemPage;
    private bool HasPersonnelItemPage => _personnelItemPage is not null;

    private bool IsPersonnelItemPageCurrent(PersonnelItemPage page) =>
        !_failed && !_shuttingDown && _personnelItemPage == page &&
        ReferenceEquals(_session, _personnelItemSession) && ReferenceEquals(_world, _personnelItemWorld) &&
        ReferenceEquals(_session?.World, _personnelItemWorld) && _residentMap == _personnelItemMap;

    private void AdoptPersonnelItemSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_personnelItemPage is not { } page || _personnelItemSession != session ||
            _personnelItemMap is null || _personnelItemMap != _residentMap ||
            !ReferenceEquals(_personnelItemWorld, previous) ||
            !TryAdoptPersonnelItemConfirmationSpeechWorld(page, session, previous, replacement)) return;
        _personnelItemWorld = replacement;
        if (_personnelItemNarration is { } narration && ReferenceEquals(narration.World, previous))
        {
            narration.World = replacement;
            if (_personnelPresentation == narration.View) _personnelPresentationWorld = replacement;
        }
        // The displayed draft remains selected across an unrelated committed speech.
        // Its current-world values are read again when the child selector returns.
        if (_personnelItemSelector is null && _personnelItemTargetSelector is null && _personnelItemNarration is null) _refreshPersonnelItemPage?.Invoke();
    }

    private void OpenPersonnelItems()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasActionReport || HasPersonnelConfirmation || _detailsView is not null ||
            _repository is null || _campaignComposition is not { } composition ||
            _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.World is not { Phase: TurnPhase.Strategy } world ||
            !ReferenceEquals(world, _world) || PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        CloseCommandWorkspace();
        var page = new PersonnelItemPage(); _personnelItemPage = page;
        _personnelItemSession = session; _personnelItemWorld = world; _personnelItemMap = map;
        int? itemId = null, targetId = null;
        GameOrder? order = null;
        var content = new AreaAdministrationOfficerContent(_repository);
        var sprites = new LocalizedUiSpriteSource(_repository);
        var messages = _repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages;
        string Message(int id, int section = 1) => messages.Sections[section].Records[id].Text
            ?? throw new InvalidDataException($"Missing normal item command text {section}/{id:X}.");
        string Guide() => new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5869);
        ArmyCampaignRuleAdapter Rules() => composition.PrepareArmyQueries(session.World);
        bool Current() => IsPersonnelItemPageCurrent(page);
        AddChild(page);
        try
        {
            page.Configure(_repository, _ui, Current, RequestPersonnelItemBack,
                () => RunUiAction(() => { if (Current() && order is not null) RequestPersonnelOrder(order, page); }),
                () => RunUiAction(ChooseItem), () => RunUiAction(ChooseTarget));
            _refreshPersonnelItemPage = Refresh;
            Refresh();
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
        }
        catch { ClosePersonnelItemPage(); throw; }

        void Refresh()
        {
            if (!Current()) return;
            world = session.World;
            var rules = Rules();
            var budget = rules.QueryPersonnelItemCommandBudget(world, player);
            var text = new Dictionary<int, string>();
            foreach (int node in new[] {17,19,20,24,29,30,32,33,34,37,39,40,41,113,115,116,117,124,126,127}) text[node] = "";
            var visible = new Dictionary<int, bool>();
            foreach (int node in new[] {29,30,31,32,33,34,37,38,39,40,41,112,113,114,115,116,117}) visible[node] = false;
            string unknown = _ui.Get("detail_unknown");
            if (budget.Value is { } books)
            { text[124] = PersonnelItemNumber(books.DistrictBooksBefore); text[126] = PersonnelItemNumber(books.DistrictBooksAfter); text[127] = "(^03-" + PersonnelItemNumber(books.CommandBooksCost) + "^00)"; }
            else { text[124] = text[126] = text[127] = unknown; Warn(budget.UnresolvedInput); }
            ItemInformationPage? selectedItem = itemId is int selected ? rules.QueryItemInformation(world, selected) : null;
            LoadedSprite? face = null;
            if (selectedItem is not null)
            {
                int? abilityKind = world.Items?.GetValueOrDefault(selectedItem.ItemId) is { } owned
                    ? world.ItemDefinitions.GetValueOrDefault(owned.DefinitionId)?.Appearance?.AbilityKind : null;
                text[17] = abilityKind == 0 ? Message(0x18, 0) : selectedItem.Text.GetValueOrDefault(37, unknown);
                text[19] = selectedItem.Text.GetValueOrDefault(42, unknown);
                text[24] = selectedItem.Text.GetValueOrDefault(53, unknown);
                if (selectedItem.HolderValid == true && selectedItem.HolderPortrait is
                    { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: not null, SpriteName: not null } portrait)
                    face = sprites.LoadTexturePackSprite(portrait.ResourceName, portrait.SpriteName);
            }
            var card = rules.QueryPersonnelItemTransferOfficerCard(world, targetId);
            AreaAdministrationOfficerValues target = content.Create(card.Value);
            if (!card.Available) Warn(card.UnresolvedInput);
            order = null;
            PersonnelEvaluation? evaluation = null;
            PersonnelItemSelection? selection = null;
            if (itemId is int id)
            {
                int? holder = world.Items?.GetValueOrDefault(id)?.Holder?.OfficerId;
                bool revoke = targetId == world.Forces[player].RulerId;
                selection = new(id, targetId, revoke);
                if (holder is int previous && targetId is int next && previous != next)
                {
                    var change = new PersonnelChange(revoke ? PersonnelActionKind.RevokeItem : PersonnelActionKind.GrantItem,
                        revoke ? previous : next, id, previous, next);
                    evaluation = session.EvaluatePersonnelChange(change);
                    if (evaluation.Allowed == true && evaluation.Quote is not null)
                        order = revoke ? new RevokeItemOrder(player, id, previous) : new GrantItemOrder(player, id, next);
                }
            }
            var partial = evaluation?.ItemTransferPreview is { } complete
                ? new AreaAdministrationQuery<PersonnelItemCommandPreview>(new(itemId, targetId, complete.AbilityKind,
                    complete.PreviousHolder, complete.NextHolder), null)
                : rules.QueryPersonnelItemCommandPreview(world, player, itemId, targetId);
            if (!partial.Available) Warn(partial.UnresolvedInput);
            var preview = partial.Value;
            if (preview is not null)
            {
                int kind = preview.AbilityKind;
                if (kind != 0 && preview.PreviousHolder is { } previousHolder)
                {
                    foreach (int node in new[] {29,30,31,32,33,34}) visible[node] = true;
                    text[29] = Message(0x183 + kind - 1, 0);
                    int before = PersonnelItemAbility(previousHolder.AbilitiesBefore, kind);
                    int after = PersonnelItemAbility(previousHolder.DisplayAbilitiesAfter, kind);
                    text[30] = PersonnelItemNumber(before); text[32] = PersonnelItemNumber(after);
                    if (before != after)
                    { text[33] = "("; text[34] = "^03-" + PersonnelItemNumber(before - after) + "^00)"; }
                }
                if (preview.PreviousHolder is { } oldHolder) Loyalty(oldHolder, false);
                if (preview.NextHolder is { } nextHolder) Loyalty(nextHolder, true);
                if (target.Abilities is { } abilities && preview.NextHolder is { } next)
                {
                    int[] after = PersonnelItemAbilities(next.DisplayAbilitiesAfter);
                    target = target with { Abilities = abilities.Select((value,index) =>
                        new AreaAdministrationAbilityValue(value.Base, after[index])).ToArray() };
                }
            }
            string guide = evaluation is { Allowed: null } ? _ui.Get("play_personnel_items_unavailable") : Guide();
            page.Apply(new(selection, selectedItem?.Name ?? "", selectedItem?.ImageIndex, face, target, text, visible,
                true, true, order is not null && partial.Available && budget.Available, guide)
            { TargetLoyaltyAfter = preview?.NextHolder?.DisplayLoyaltyAfter, TargetOfficerId = targetId });

            void Loyalty(PersonnelItemOfficerPreview person, bool gain)
            {
                int caption = gain ? 112 : 36, before = gain ? 113 : 37, arrow = gain ? 114 : 38,
                    after = gain ? 115 : 39, opening = gain ? 116 : 40, suffix = gain ? 117 : 41;
                visible[caption] = visible[before] = true;
                text[before] = person.IsRuler ? " - " : person.DisplayLoyaltyBefore is int value ? PersonnelItemNumber(value) : unknown;
                if (!person.IsRuler && !person.LoyaltyChangeVisible) return;
                foreach (int node in new[] {arrow,after,opening,suffix}) visible[node] = true;
                text[opening] = "(";
                text[after] = person.IsRuler ? " - " : person.DisplayLoyaltyAfter is int final ? PersonnelItemNumber(final) : unknown;
                if (!gain && !person.IsRuler && person.DisplayLoyaltyAfter is >= 100)
                { text[opening] = ""; text[suffix] = ""; }
                else text[suffix] = person.IsRuler ? " - )" : person.DisplayLoyaltyChangeMagnitude is int delta
                    ? (gain ? "^02+" : "^03-") + PersonnelItemNumber(delta) + "^00)" : unknown;
            }
        }

        void ChooseItem()
        {
            if (!Current() || _personnelItemSelector is not null || _personnelItemTargetSelector is not null) return;
            var rules = Rules(); var candidates = rules.QueryPersonnelItemCommandCandidates(session.World, player);
            if (!candidates.Available || candidates.Value is null) { Unavailable(candidates.UnresolvedInput); return; }
            var query = rules.QueryPersonnelItemCommandRows(session.World, candidates.Value);
            if (!query.Available || query.Value is null) { Unavailable(query.UnresolvedInput); return; }
            var definitions = PersonnelItemSelectionColumns.Items;
            var columns = definitions.Select(column => new CommandSelectionColumn(column.Id.ToString(), Message(column.Caption),
                column.Width, column.Align == 0x21, column.Align, column.LineArrange, column.InitialAscending)).ToArray();
            IReadOnlyList<CommandSelectionRow> rows = query.Value.Select(row =>
            {
                string[] values = [row.Name,row.KindText,row.AbilityText,row.EffectText,PersonnelItemNumber(row.LoyaltyValue),row.HolderName,row.HolderStatusText];
                return new CommandSelectionRow(row.ItemId, definitions.Select((column,index) =>
                {
                    var cell = row.SortCells[column.Id];
                    return new CommandSelectionCell(values[index], cell.SortKey is null ? cell.SortNumber ?? cell.Number : null,
                        cell.SortKey, SortGroup: cell.NotApplicable ? 2 : 0, SortUnavailable: cell.SortUnresolvedOwner is not null);
                }).ToArray(), true);
            }).ToArray();
            var selector = new CommandSelectionView(); _personnelItemSelector = selector; page.SetBusy(true); AddChild(selector);
            try
            {
                selector.Configure(_repository!, _ui, 0x5AA, columns, rows, itemId is int selected ? [selected] : [], 1,
                    () => Current() && _personnelItemSelector == selector, selected =>
                    {
                        if (_personnelItemSelector != selector) return;
                        _personnelItemSelector = null; RetirePersonnelItemSelector(selector); page.SetBusy(false);
                        if (!Current()) return;
                        if (selected is { Count: > 0 })
                        {
                            itemId = selected[0];
                            if (session.World.Items?.GetValueOrDefault(itemId.Value)?.Holder?.OfficerId == targetId) targetId = null;
                        }
                        Refresh();
                    }, CurrentListScrollSpeed, Guide(), requireSelection: true, singleSelection: true,
                    sourceSort: (key, ascending) =>
                    {
                        int columnId = int.Parse(key, CultureInfo.InvariantCulture);
                        int index = Array.FindIndex(definitions, column => column.Id == columnId);
                        rows = rows.OrderBy(row => row, Comparer<CommandSelectionRow>.Create((a,b) =>
                        {
                            var left=a.Cells[index]; var right=b.Cells[index];
                            int group=(left.SortGroup??0).CompareTo(right.SortGroup??0);
                            if(group != 0) return columnId is 8 or 3 || ascending ? group : -group;
                            int comparison = left.SortKey is not null || right.SortKey is not null
                                ? PersonNameOrder.Compare(left.SortKey ?? "",right.SortKey ?? "")
                                : Nullable.Compare(left.Number,right.Number);
                            return ascending ? comparison : -comparison;
                        })).ToArray();
                        return rows;
                    }, groupCaptions: [Message(0x3CD)], resources: page.SharedResources);
            }
            catch { _personnelItemSelector=null;RetirePersonnelItemSelector(selector);page.SetBusy(false);throw; }
        }

        void ChooseTarget()
        {
            if (!Current() || _personnelItemSelector is not null || _personnelItemTargetSelector is not null) return;
            var candidates = Rules().QueryPersonnelItemTargetCandidates(session.World, player, itemId);
            if (!candidates.Available || candidates.Value is null) { Unavailable(candidates.UnresolvedInput); return; }
            var groups = PersonnelItemSelectionColumns.PersonPages.Select((ids,index) => new OfficerInformationGroup(index,
                PersonnelItemSelectionColumns.PersonCaptions[index], Message(PersonnelItemSelectionColumns.PersonCaptions[index]),
                ids.Select(id => PersonnelItemSelectionColumns.People[id]).Select(column => new OfficerInformationColumn(column.Id,
                    column.Caption, Message(column.Caption), column.Width, column.Align == 0x01 ? 3 : column.Align == 0x21 ? 5 : 4,
                    column.InitialAscending, column.Id != 101, true)).ToImmutableArray())).ToArray();
            var queried = Rules().QueryPersonnelItemTargetPages(session.World, player, candidates.Value, groups);
            if (!queried.Available || queried.Value is not { } pages) { Unavailable(queried.UnresolvedInput);return; }
            AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>> Convert(OfficerInformationPage value) =>
                new(value.Rows.Select(row => new CommandSelectionRow(row.OfficerId,
                    row.Cells.Select((cell,index) => new CommandSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : _ui.Get("detail_unknown"),
                        FirstGlyphColour: cell.MarkerColour?.CpuPacked, SourcePreset: cell.PresetIndex,
                        SortUnavailable: !groups[value.GroupIndex].Columns[index].CanSort || cell.SortUnresolvedOwner is not null)).ToArray(), true)).ToArray(), null);
            AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>> Read(int tab) => Convert(pages.ReadPage(tab));
            AreaAdministrationQuery<IReadOnlyList<CommandSelectionRow>> Sort(int tab, string key, bool ascending)
            {
                int id = key == "name" ? 0 : int.Parse(key, CultureInfo.InvariantCulture);
                var sorted = pages.Sort(tab, id, ascending);
                return sorted.Available && sorted.Value is { } value ? Convert(value) : new(null, sorted.UnresolvedInput);
            }
            var selector = new PersonnelItemPersonSelectionView(); _personnelItemTargetSelector=selector;
            page.SetBusy(true); AddChild(selector);
            try
            {
                selector.Configure(_repository!, _ui, page.SharedResources, targetId is int selected ? [selected] : [],
                    () => Current() && _personnelItemTargetSelector==selector, selected =>
                    {
                        if (_personnelItemTargetSelector != selector) return;
                        _personnelItemTargetSelector=null;selector.Abandon();RemoveChild(selector);selector.QueueFree();page.SetBusy(false);
                        if(!Current())return;
                        if(selected is {Count:>0})targetId=selected[0];
                        Refresh();
                    }, CurrentListScrollSpeed, Read, Sort, Guide());
            }
            catch { _personnelItemTargetSelector=null;selector.Abandon();RemoveChild(selector);selector.QueueFree();page.SetBusy(false);throw; }
        }
        void Unavailable(string? reason) { Warn(reason);page.ApplyHint(_ui.Get("information_partial")); }
        static void Warn(string? reason) { if(reason is not null)GD.PushWarning(reason); }
    }

    private static string PersonnelItemNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static int[] PersonnelItemAbilities(OfficerAbilities value) => [value.Leadership,value.Strength,value.Intelligence,value.Politics,value.Charm];
    private static int PersonnelItemAbility(OfficerAbilities value,int kind) => PersonnelItemAbilities(value)[kind-1];
    private static void RetirePersonnelItemSelector(CommandSelectionView selector)
    { selector.Abandon();if(selector.GetParent() is Node parent)parent.RemoveChild(selector);selector.QueueFree(); }

    private void RequestPersonnelItemBack()
    {
        if (_personnelItemNarration is not null) return;
        if (_personnelItemSelector is { } item) { item.RequestCancel();return; }
        if (_personnelItemTargetSelector is { } target) { target.RequestCancel();return; }
        ClosePersonnelItems();
    }
    private void ClosePersonnelItems() => ClosePersonnelItemPage();
    private void ClosePersonnelItemPage()
    {
        ClosePersonnelItemNarration();
        if (_personnelItemSelector is { } item) { _personnelItemSelector=null;RetirePersonnelItemSelector(item); }
        if (_personnelItemTargetSelector is { } target)
        { _personnelItemTargetSelector=null;target.Abandon();if(target.GetParent() is Node parent)parent.RemoveChild(target);target.QueueFree(); }
        var page = _personnelItemPage; var map = _personnelItemMap;
        if (page is not null && _personnelConfirmationItemParent == page) ClosePersonnelConfirmation(false);
        _personnelItemPage=null;_personnelItemSession=null;_personnelItemWorld=null;_personnelItemMap=null;_refreshPersonnelItemPage=null;
        if (page is not null) { page.Abandon();if(page.GetParent() is Node parent)parent.RemoveChild(page);page.QueueFree(); }
        if (map is not null && map == _residentMap && GodotObject.IsInstanceValid(map)) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && map is not null && map == _residentMap) SetMapFocus(true);
    }
}
