using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private CommandSelectionColumn[] ItemEditorColumns(ItemEditorCatalog catalog) =>
        [new("name", catalog.Data(0x246), 420), new("kind",catalog.Data(0x401),220),
         new("ability",catalog.Data(0x452),240),new("effect",catalog.Data(0x411),240),new("worth",catalog.Data(0x423),180,true)];
    private CommandSelectionRow ItemEditorRow(ItemEditorCandidate item)
    {
        var catalog = GetItemEditorCatalog();
        return new(item.Id, [new(item.Name,SortUnavailable:true),new(catalog.Kind(item.Values),SortUnavailable:true),
            new(catalog.Ability(item.Values),SortUnavailable:true),new(catalog.Effect(item.Values),SortUnavailable:true),
            new(item.Values.Worth.ToString(),item.Values.Worth)], true);
    }
    private void SelectItems(int title, IReadOnlyList<ItemEditorCandidate> candidates, int maximum,
        Action<IReadOnlyList<int>> selected, string? guide = null)
    {
        if (_repository is null || !IsItemEditorsCurrent || _itemEditorSelector is not null) return;
        var view = new CommandSelectionView(); _itemEditorSelector = view; AddChild(view);
        _itemEditorMenu!.SetBusy(true); _itemEditorMenu.Hide();
        view.Configure(_repository, _ui, title, ItemEditorColumns(GetItemEditorCatalog()), candidates.Select(ItemEditorRow).ToArray(),
            [], maximum, () => IsItemEditorsCurrent && _itemEditorSelector == view && _itemEditorQuestion is null,
            result => RunUiAction(() =>
            {
                if (!IsItemEditorsCurrent || _itemEditorSelector != view) return;
                _itemEditorSelector = null; view.Abandon(); view.QueueFree();
                _itemEditorMenu!.Show(); _itemEditorMenu.SetBusy(false);
                if (result is not null) selected(result);
            }), CurrentListScrollSpeed, guide, requireSelection:true,singleSelection:maximum==1);
    }
    private void OpenItemEditorField(int section)
    {
        if (!CanEditItemPage || _repository is null || _itemEditorWorking is not { } before) return;
        if(section==7)
        {
            AskItemEditorQuestion(_itemEditorsCreated?0x28F2:0x28C1,()=>
            {
                _itemEditorWorking=_itemEditorsCreated?_itemEditorOriginal:
                    before with {Values=GetItemEditorCatalog().Historical(_itemEditorId,ItemEditorLibrary.Empty).Values};
                _itemEditorPage?.Refresh();
            });
            return;
        }
        var page = _itemEditorPage!; page.SetBusy(true); page.Hide();
        bool Admitted() => IsItemEditorsCurrent && _itemEditorPage == page && _itemEditorQuestion is null;
        void Finish(Control child, Action? apply)
        {
            if (_itemEditorChild != child || !IsItemEditorsCurrent || _itemEditorPage != page) return;
            _itemEditorChild = null; RetireItemControl(child); apply?.Invoke(); page.Show(); page.SetBusy(false); page.Refresh();
        }
        if (section is 2 or 3 or 4)
        {
            var child = new ItemEditorFieldView(); _itemEditorChild = child; AddChild(child);
            child.Configure(_repository,_ui,GetItemEditorCatalog(),before.Values,section,Admitted,
                result => RunUiAction(() => Finish(child,result is null?null:()=>_itemEditorWorking=before with {Values=result})));
        }
        else if (section is 0 or 6)
        {
            if (!_itemEditorsCreated) { page.Show(); page.SetBusy(false); return; }
            var child = new ItemEditorTextView(); _itemEditorChild = child; AddChild(child);
            child.Configure(_repository,_ui,section==6,section==6?before.Description:before.Name,Admitted,
                result=>RunUiAction(()=>Finish(child,result is null?null:()=>_itemEditorWorking=section==6?
                    before with {Description=result}:before with {Name=result})));
        }
        else if (section == 1)
        {
            var child = new ItemEditorImageView(); _itemEditorChild = child; AddChild(child);
            child.Configure(_repository,_ui,GetItemEditorCatalog(),_itemExternalImages,before.Values.ImageIndex,Admitted,
                result=>RunUiAction(()=>Finish(child,result is not int image?null:()=>_itemEditorWorking=before with {Values=before.Values with {ImageIndex=image}})));
        }
        else if (section == 5)
        {
            var child = new DeploymentCalculatorDialog(); _itemEditorChild = child; AddChild(child);
            child.Configure(_repository,_ui,before.Values.Worth,0,50,
                result=>RunUiAction(()=>Finish(child,result is not int worth?null:()=>_itemEditorWorking=before with {Values=before.Values with {Worth=worth}})),titleMessage:0x423);
        }
    }
    private void OpenItemImport()
    {
        if (_repository is null || !IsItemEditorsCurrent) return;
        string directory=ProjectSettings.GlobalizePath("user://ITEMDATA");
        ItemExchangeLoadResult publication;
        try { publication=ItemExchangeFiles.LoadCandidates(directory); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException)
        {ItemEditorStorageFailure(error);return;}
        foreach(var failure in publication.Failures) GD.PushWarning(failure.FileName+": "+failure.Reason);
        var candidates=publication.Candidates;
        var rows=candidates.Select(item=>new ItemEditorCandidate(item.Index,item.Item.Name,item.Item.Description,
            item.Item.Values,item.Item.Values,false)).ToArray();
        string guide=ItemMessage(0x2908,50-GetItemEditorStore().Current.Created.Length)+" "+directory;
        var notices=new Queue<(int Message,string Text)>(publication.Failures.Select(failure=>(0x2907,ItemMessage(0x2907,null,failure.FileName))));
        if(publication.LimitReached)notices.Enqueue((0x2909,ItemMessage(0x2909,500)));
        void PresentNext()
        {
            if(!IsItemEditorsCurrent)return;
            if(notices.TryDequeue(out var notice)){ShowItemEditorNoticeText(notice.Message,notice.Text,PresentNext);return;}
            SelectItems(0x7D4,rows,Math.Min(rows.Length,50-GetItemEditorStore().Current.Created.Length),selected=>
        {
            try
            {
                var library=GetItemEditorStore().Current;
                var next=ItemExchangeFiles.Import(library,candidates,selected);
                if(SaveItemLibrary(next)) ShowItemEditorNotice(0x290A,selected.Count);
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException)
            {ItemEditorStorageFailure(error);}
            },guide);
        }
        PresentNext();
    }
    private void ExportItems(ItemEditorLibrary library,IReadOnlyList<int> ids)
    {
        try
        {
            var files=ItemExchangeFiles.Export(ProjectSettings.GlobalizePath("user://ITEMDATA"),library,ids,
                GetItemEditorCatalog().PhysicalImages(library));
            ShowItemEditorNotice(0x2906,files.Count);
        }
        catch(ItemExchangeExportException error)
        {
            ItemEditorStorageFailure(error);
            if(error.WrittenPaths.Count>0)ShowItemEditorNotice(0x2906,error.WrittenPaths.Count);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException)
        {ItemEditorStorageFailure(error);}
    }
    private void ShowItemEditorNotice(int id,int count)=>ShowItemEditorNoticeText(id,ItemMessage(id,count));
    private void ShowItemEditorNoticeText(int id,string message,Action? completed=null)
    {
        if(_repository is null||!IsItemEditorsCurrent||_itemEditorNotice is not null)return;
        var menu=_itemEditorMenu!;
        var view=new DeploymentNarrationView();_itemEditorNotice=view;AddChild(view);menu.SetBusy(true);
        try
        {
            view.ConfigureAnnouncement(_repository,_ui,message,()=>RunUiAction(()=>
            {
                if(!IsItemEditorsCurrent||_itemEditorNotice!=view)return;
                _itemEditorNotice=null;view.Abandon();view.QueueFree();_itemEditorMenu!.SetBusy(false);_itemEditorMenu.Refresh();
                completed?.Invoke();
            }),admitted:()=>IsItemEditorsCurrent&&_itemEditorNotice==view,
                resources:TryBorrowApplicationUiResources(view));
            view.SetMeta("message_record",id);
        }
        catch
        {
            if(_itemEditorNotice==view)
            {
                _itemEditorNotice=null;
                if(_itemEditorMenu==menu&&GodotObject.IsInstanceValid(menu))menu.SetBusy(false);
            }
            view.Abandon();view.QueueFree();throw;
        }
    }
}
