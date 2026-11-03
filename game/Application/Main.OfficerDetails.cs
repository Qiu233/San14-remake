using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;

public partial class Main
{
    private OfficerDetailPublication ReadCurrentOfficerDetail(GameState world, int officer)
    {
        var rules = (_campaignComposition ?? throw new InvalidOperationException("Officer details require the current campaign.")).PrepareArmyQueries(world);
        var result = rules.QueryOfficerDetail(world, _session?.PlayerForceId ?? -1, officer);
        return result.Available && result.Value is { } publication ? publication :
            throw new InvalidDataException(result.UnresolvedInput ?? "officer-detail.current_publication");
    }

    private void ConfigureCurrentOfficerDetail(OfficerDetailsView view, GameState owner, int officer,
        Action close, Func<bool> hostCurrent, Action<GameState>? adoptWorld = null,
        IReadOnlyList<int>? selectionIds = null,
        Action<OfficerDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null)
    {
        var session = _session ?? throw new InvalidOperationException("The officer detail session is missing.");
        var composition = _campaignComposition ?? throw new InvalidOperationException("The officer detail rules are missing.");
        var map = _residentMap;
        var publication = ReadCurrentOfficerDetail(owner, officer);
        GameState current = owner;
        RetainCommandDetailSpeechWorld(view, next => current = next);
        int currentOfficer = officer;
        var selectionRules = composition.PrepareArmyQueries(owner);
        var selection = PrepareDetailSelection(selectionRules, owner, InformationListKind.Officers, officer, selectionIds)
            with { InitialNameSortAscending = initialNameSortAscending };
        bool Admitted() => !_failed && !_shuttingDown && !IsReturningToTitle &&
            IsInspectionWorldCurrent(session, current) &&
            ReferenceEquals(_residentMap, map) && hostCurrent();
        Action<int>? priority = session.PlayerForceId is int player ? choice => RunInspectionUiAction(current, () =>
        {
            if (!Admitted() || (!_advancingPeriod && session.HasPendingOperation)) return;
            var before = current;
            bool duringPeriod = _advancingPeriod;
            var order = new SetOfficerPriorityOrder(player, currentOfficer, choice);
            var result = duringPeriod ? session.ApplyBorrowedOfficerPriority(current, order)
                : session.Issue(order, new(_presentationRandom.State));
            if (!result.Accepted)
            {
                _playMessage.Text = _ui.Format("play_order_rejected", result.MissingRuleKey is { } missing
                    ? _ui.CommandError(missing) : result.ErrorCode is { } code ? _ui.CommandError(code) : result.Error ?? "?");
                return;
            }
            current = result.State!;
            // The checkpoint's owner, all retained inspectors and their real
            // sequence publications adopt one accepted result before returning.
            if (duringPeriod)
            {
                if (!TryAdoptPeriodMenuWorld(before, current))
                    throw new InvalidOperationException("The officer inspector's period borrow has retired.");
                AdoptCommandDetailsSpeechWorld(session, before, current);
            }
            else if (ReferenceEquals(_commandDetailsWorld, before)) _commandDetailsWorld = current;
            adoptWorld?.Invoke(current);
            if (!duringPeriod)
            {
                if (current.CampaignAppRandom32 is { } random) _presentationRandom.InstallCommittedState(random.State);
                SynchronizeApplicationCrtRandom(current);
            }
            _playMessage.Text = _ui.Get("play_order_accepted");
            var updated = composition.PrepareArmyQueries(current).QueryOfficerDetail(current, player, currentOfficer);
            if (updated.Available && updated.Value is { } next) view.ApplyCurrent(next);
            else
            {
                close();
                GD.PushWarning(updated.UnresolvedInput ?? "officer-detail.after_priority");
                _playMessage.Text = _ui.Get("detail_unknown");
            }
            if (!duringPeriod) RefreshCurrentPlayWorld();
        }) : null;
        view.TransitionFailure = ReportPlayFailure;
        view.Configure(_repository!, publication, _ui, () =>
        {
            if (!Admitted()) return;
            returnSelection?.Invoke(view.ReadSelectionReturn());
            close();
        }, Admitted, priority,
            selection: selection, select: id => RunInspectionUiAction(current, () =>
            {
                if (!Admitted() || (!_advancingPeriod && session.HasPendingOperation)) return;
                var next = ReadCurrentOfficerDetail(current, id);
                view.SelectCurrent(next);
                currentOfficer = id;
            }), resources: TryBorrowApplicationUiResources(view));
    }
}
