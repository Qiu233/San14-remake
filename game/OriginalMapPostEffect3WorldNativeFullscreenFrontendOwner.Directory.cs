using San14.Data;

public sealed partial class OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private OriginalMapPostEffect3Functor1FrontendCursor? directoryCursor;
    internal void AttachDirectoryCursor(OriginalMapPostEffect3Functor1FrontendCursor actual)
    {RequireCurrent();if(directoryCursor is not null)throw new InvalidOperationException("Frontend already owns its source directory cursor.");directoryCursor=actual;}
    internal bool UsesActualDirectoryCursor=>directoryCursor is not null;
    internal void RequireNoPendingForDirectoryReturn(){RequireCurrent();RequireReleasedBeforeNextBind();}
    internal void BeginCompositeSourceBefore(OriginalMapPostEffect3Composite2BeforeBindings before)
    {directoryCursor?.EnterSelectedBefore(before.SourcePassIndex,0);}
    internal IReadOnlyList<OriginalMapPostEffect3Binding?> ApplyCompositeSourceBefore(OriginalMapPostEffect3Composite2BeforeBindings before)
    {
        var cursor=directoryCursor??throw new InvalidOperationException("Actual global cache is absent.");
        cursor.RecordBeforeReturned(before);cursor.ApplyCompositeBefore(before);return cursor.Cache.ReadCountedTargets();
    }
    internal void AcceptCompositeSourceBind(OriginalMapPostEffect3Composite2BeforeBindings before,object actualContext)
    {directoryCursor?.RecordBindReturned(before,actualContext);}
}
