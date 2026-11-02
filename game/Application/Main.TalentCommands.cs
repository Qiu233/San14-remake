using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private (GameplaySession? Session, GameState? World, int Force, int City, int Hex, TalentCommandView? View) _talentSearchPublication;
    private (GameplaySession? Session, GameState? World, int Force, int City, int? Target, TalentCommandView? View) _talentRecruitPublication;

    private int? SelectedTalentSearchHex(GameState world, int city) =>
        _selectedCell?.Id ?? world.Cities.GetValueOrDefault(city)?.HexId;

    private TalentCommandView TalentSearchView(GameState world, int force, int city, int hex)
    {
        var cached = _talentSearchPublication;
        if (ReferenceEquals(cached.Session, _session) && ReferenceEquals(cached.World, world) &&
            cached.Force == force && cached.City == city && cached.Hex == hex && cached.View is { } publication)
            return publication;
        var view = _campaignComposition!.PrepareArmyQueries(world).QueryTalentSearch(world, force, city, hex);
        _talentSearchPublication = (_session, world, force, city, hex, view);
        return view;
    }
    private TalentCommandView TalentRecruitView(GameState world, int force, int city, int? target)
    {
        var cached = _talentRecruitPublication;
        if (ReferenceEquals(cached.Session, _session) && ReferenceEquals(cached.World, world) &&
            cached.Force == force && cached.City == city && cached.Target == target && cached.View is { } publication)
            return publication;
        var view = _campaignComposition!.PrepareArmyQueries(world).QueryTalentRecruit(world, force, city, target);
        _talentRecruitPublication = (_session, world, force, city, target, view);
        return view;
    }

    private void RefreshNativeTalentControls()
    {
        // The normal Talent pages own their eligibility and selection.
        // Hidden legacy browser buttons have no consumer in this mode.
        if (!DeveloperWorkspaceEnabled || _world?.NativeArmySimulation is null) return;
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || _session is null || _session.HasPendingOperation ||
            _world is not { Phase: TurnPhase.Strategy } world || _playerForceId is not int force ||
            _campaignComposition is null || SelectionId(_playCity) is not int city ||
            !world.Cities.ContainsKey(city) || SelectedTalentSearchHex(world, city) is not int hex)
        { _playTalentSearch.Disabled = true; _playTalentRecruit.Disabled = true; return; }
        var search = TalentSearchView(world, force, city, hex);
        int? target = SelectionId(_playTalentTarget);
        var recruit = TalentRecruitView(world, force, city, target);
        FillTalentChoices(_playTalentTarget, recruit.TargetOfficerIds, world);
        if (SelectionId(_playTalentTarget) != target)
            recruit = TalentRecruitView(world, force, city, SelectionId(_playTalentTarget));
        FillTalentChoices(_playTalentExecutor,
            search.ExecutorOfficerIds.Concat(recruit.ExecutorOfficerIds).Distinct().ToArray(), world);
        int? executor = SelectionId(_playTalentExecutor);
        _playTalentExecutor.Disabled = _playTalentExecutor.ItemCount == 0;
        _playTalentTarget.Disabled = _playTalentTarget.ItemCount == 0;
        _playTalentSearch.Disabled = search.CanSubmit != true || executor is not int searchActor ||
            !search.ExecutorOfficerIds.Contains(searchActor);
        _playTalentRecruit.Disabled = recruit.CanSubmit != true || executor is not int recruitActor ||
            !recruit.ExecutorOfficerIds.Contains(recruitActor) || SelectionId(_playTalentTarget) is not int recipient ||
            !recruit.TargetOfficerIds.Contains(recipient);
        _playTalentSearch.TooltipText = search.CanSubmit is null ? _ui.Get("detail_unknown") : "";
        _playTalentRecruit.TooltipText = recruit.CanSubmit is null ? _ui.Get("detail_unknown") : "";
    }

    private static void FillTalentChoices(OptionButton choices, IReadOnlyList<int> ids, GameState world)
    {
        bool same = choices.ItemCount == ids.Count;
        if (same)
            for (int i = 0; i < ids.Count; i++)
                if (choices.GetItemId(i) != ids[i] || choices.GetItemText(i) != world.Officers[ids[i]].Name)
                { same = false; break; }
        if (same) return;
        int? previous = SelectionId(choices);
        choices.Clear();
        foreach (int id in ids) choices.AddItem(world.Officers[id].Name, id);
        int index = previous is int selected ? choices.GetItemIndex(selected) : -1;
        if (choices.ItemCount > 0) choices.Select(index < 0 ? 0 : index);
    }
}
