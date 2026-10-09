/// <summary>Only the current invocation and its immediate predecessor retain
/// module authority. Exported evidence has no authority after eviction.</summary>
internal static class MapPostEffect3Functor1HistoryWindow
{
    internal static void Prune<T>(Dictionary<long,T> records,long currentFrame) where T:class
    {
        foreach(long old in records.Keys.Where(f=>f<currentFrame-1).ToArray())records.Remove(old);
    }
    internal static void RequireOwned<T>(Dictionary<long,T> records,long frame,T actual) where T:class
    {
        if(!records.TryGetValue(frame,out var retained)||!ReferenceEquals(retained,actual))
            throw new InvalidDataException("Historical authority is outside the module's current/previous invocation window.");
    }
}
