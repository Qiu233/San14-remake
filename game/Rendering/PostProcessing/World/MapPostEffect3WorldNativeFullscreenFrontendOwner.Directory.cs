using San14.Data;

public sealed partial class MapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private MapPostEffect3Functor1FrontendCursor? directoryCursor;
    internal void AttachDirectoryCursor(MapPostEffect3Functor1FrontendCursor actual)
    {RequireCurrent();if(directoryCursor is not null)throw new InvalidOperationException("Frontend already owns its source directory cursor.");directoryCursor=actual;}
    internal bool UsesActualDirectoryCursor=>directoryCursor is not null;
    internal void RequireNoPendingForDirectoryReturn(){RequireCurrent();RequireReleasedBeforeNextBind();}
    internal void BeginCompositeSourceBefore(MapPostEffect3Composite2BeforeBindings before)
    {directoryCursor?.EnterSelectedBefore(before.SourcePassIndex,0);}
    internal IReadOnlyList<MapPostEffect3Binding?> ApplyCompositeSourceBefore(MapPostEffect3Composite2BeforeBindings before)
    {
        var cursor=directoryCursor??throw new InvalidOperationException("Actual global cache is absent.");
        cursor.RecordBeforeReturned(before);cursor.ApplyCompositeBefore(before);return cursor.Cache.ReadCountedTargets();
    }
    internal void AcceptCompositeSourceBind(MapPostEffect3Composite2BeforeBindings before,object actualContext)
    {directoryCursor?.RecordBindReturned(before,actualContext);}
}
