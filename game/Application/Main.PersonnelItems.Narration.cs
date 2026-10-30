using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class PersonnelItemNarrationWorkspace(GameplaySession session, GameState world,
        MapSceneView map, PersonnelItemPage page, GameOrder order, PersonnelItemTransferAnnouncement announcement)
    {
        internal readonly GameplaySession Session = session;
        internal GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly PersonnelItemPage Page = page;
        internal readonly GameOrder Order = order;
        internal readonly PersonnelItemTransferAnnouncement Announcement = announcement;
        internal readonly CancellationTokenSource Lifetime = new();
        internal DeploymentNarrationView? View;
        internal bool Running = true;
        internal bool OwnsVoice;
    }
    private PersonnelItemNarrationWorkspace? _personnelItemNarration;

    private bool IsPersonnelItemNarrationCurrent(PersonnelItemNarrationWorkspace owner) =>
        _personnelItemNarration == owner && IsPersonnelItemPageCurrent(owner.Page) &&
        ReferenceEquals(owner.Session.World, owner.World) && _residentMap == owner.Map;

    private void BeginPersonnelItemTransfer(GameOrder order, PersonnelItemPage page)
    {
        if (!IsPersonnelItemPageCurrent(page) || _personnelItemNarration is not null ||
            _session is not { PlayerForceId: int player } session || _personnelItemMap is not { } map ||
            _campaignComposition is not { } composition || _repository is null || HasPersonnelPresentation) return;
        int item, target;
        switch (order)
        {
            case GrantItemOrder grant: item=grant.ItemId;target=grant.RecipientOfficerId;break;
            case RevokeItemOrder revoke when session.World.Forces[player].RulerId is int ruler:
                item=revoke.ItemId;target=ruler;break;
            default: throw new InvalidOperationException("The item page can only submit an actual item transfer.");
        }
        // This normal authorized command prepares the source-selected line and
        // commits its real App32 state. Custody and books remain unchanged.
        var previousWorld=session.World;
        var prepared=session.Issue(new PreparePersonnelItemTransferOrder(player,item,target),new(_presentationRandom.State));
        if(!prepared.Accepted)
        {
            page.ApplyHint(_ui.Format("play_order_rejected", prepared.MissingRuleKey is { } missing
                ? _ui.CommandError(missing) : prepared.ErrorCode is { } code ? _ui.CommandError(code) : prepared.Error ?? "?"));
            return;
        }
        var announcement=prepared.PersonnelItemTransferAnnouncement
            ?? throw new InvalidDataException("personnel.item_narration.prepared_announcement");
        var world=session.World;
        _personnelItemWorld=world;
        map.AdoptSpeechWorld(previousWorld,world);
        if(world.CampaignAppRandom32 is { } random)_presentationRandom.InstallCommittedState(random.State);
        SynchronizeApplicationCrtRandom(world);
        var owner=new PersonnelItemNarrationWorkspace(session,world,map,page,order,announcement);
        _personnelItemNarration=owner;page.SetBusy(true);
        map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);HideDeploymentHud();
        _=Dispatch();

        async Task Dispatch()
        {
            try
            {
                var audio=_sceneAudioBinding??throw new InvalidOperationException("personnel.item_narration.scene_audio");
                // The original queues SE4/6 before entering its real EventView.
                // Wait for the common manager's admission, not sound completion.
                var sound=announcement.Requests[0].TalentCommandPresentation;
                if(sound is not {Operation:TalentCommandPresentationOperation.SoundCue,SoundId:int soundId})
                    throw new InvalidDataException("personnel.item_narration.sound_request");
                await audio.PlayNormalSoundAsync(soundId,owner.Lifetime.Token);
                if(!IsPersonnelItemNarrationCurrent(owner))return;
                var line=announcement.Requests[1].TalentCommandPresentation;
                if(line is not {Operation:TalentCommandPresentationOperation.OfficerText,TextAtSource:not null,RecruitConversation:{ } conversation})
                    throw new InvalidDataException("personnel.item_narration.officer_request");
                var actors=composition.PrepareArmyQueries(world).QueryRecruitConversationSpeakers(world,conversation);
                bool executorSpeaking=line.OfficerId==conversation.ExecutorOfficerId;
                var actor=executorSpeaking?actors.Value?.Executor:actors.Value?.Target;
                var standing=actor?.StandingPortrait;
                IReadOnlyList<LoadedSprite> layers=[];
                if(standing is not null)layers=standing.LoadSprites(new LocalizedUiSpriteSource(_repository).LoadTexturePackSprite);
                else GD.PushWarning(actor?.PortraitUnresolvedInput??actors.UnresolvedInput??"personnel.item_narration.standing_portrait");
                var view=new DeploymentNarrationView {KeyboardInput=HandleGameplayNarrationKeyboard};owner.View=view;
                _personnelPresentation=view;_personnelPresentationSession=session;
                _personnelPresentationWorld=owner.World;_personnelPresentationMap=map;
                AddChild(view);
                view.Configure(_repository,_ui,executorSpeaking?conversation.ExecutorNameAtSource:conversation.TargetNameAtSource,
                    line.TextAtSource,layers,Completed,()=>_preferences.AutomaticMessageAdvance,
                    ()=>IsPersonnelItemNarrationCurrent(owner)&&CanAdvanceNarration);
                view.Name="PersonnelItemTransferNarration";view.ZIndex=95;
                if(line.Voice is { } voice&&_preferences.Voice.Enabled)
                { audio.PlayPersonnelNarrationVoice(voice);owner.OwnsVoice=true; }
            }
            catch(OperationCanceledException)when(owner.Lifetime.IsCancellationRequested) { }
            catch(Exception error)
            {
                if(_personnelItemNarration==owner)
                { ClosePersonnelItemNarration();if(IsPersonnelItemPageCurrent(page)){RestorePersonnelItemParent(owner);page.ApplyHint(_ui.Get("information_partial"));} }
                GD.PushWarning(error.Message);
            }
            finally
            { owner.Running=false;if(_personnelItemNarration!=owner)owner.Lifetime.Dispose(); }
        }

        void Completed()=>RunUiAction(()=>
        {
            if(!IsPersonnelItemNarrationCurrent(owner))return;
            ClosePersonnelItemNarration();
            // 1F0AD0 waits for EventView return; only now does the real transfer
            // run once, followed by the ordinary page's success exit.
            bool accepted=ApplyPlayableOrder(order,ClosePersonnelItemPage);
            if(!accepted&&IsPersonnelItemPageCurrent(page))
            {RestorePersonnelItemParent(owner);_refreshPersonnelItemPage?.Invoke();page.ApplyHint(_playMessage.Text);}
        });
    }

    private void RestorePersonnelItemParent(PersonnelItemNarrationWorkspace owner)
    {
        if(!IsPersonnelItemPageCurrent(owner.Page))return;
        owner.Page.SetBusy(false);owner.Map.SetDetailsOpen(true,keepMapVisible:true);
        SetMapFocus(true);HideDeploymentHud();
    }

    private void ClosePersonnelItemNarration()
    {
        var owner=_personnelItemNarration;_personnelItemNarration=null;
        if(owner is null)return;
        owner.Lifetime.Cancel();
        if(owner.OwnsVoice)_sceneAudioBinding?.StopNarrationVoice();
        if(owner.View is { } view)
        {
            if(_personnelPresentation==view)ClosePersonnelPresentation();
            else {view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        }
        if(!owner.Running)owner.Lifetime.Dispose();
    }
}
