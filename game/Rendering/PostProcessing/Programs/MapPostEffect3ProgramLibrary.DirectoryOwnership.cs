using San14.Data;

public sealed partial class MapPostEffect3ProgramLibrary
{
    /// <summary>Actual same-library source shader object ownership, including
    /// known-null and other source-pinned backend variants. Public record
    /// construction or a copied/equal RID never establishes this identity.</summary>
    internal void RequireOwnedDirectoryProgram(MapPostEffect3CompiledPass actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            bool owned=passes.Values.Any(p=>ReferenceEquals(p,actual))||nullPasses.Values.Any(p=>ReferenceEquals(p,actual));
            if(!owned||!actual.ShaderRid.IsValid||actual.ContainerSha256!=SourceSha256||
                !shaders.Values.Any(shader=>shader.Rid==actual.ShaderRid)||!ReferenceEquals(Source.Passes[actual.Source.Index],actual.Source))
                throw new InvalidDataException("Directory selection requires this library's actual created shader wrapper; public records are not authority.");
            foreach(var stage in actual.Stages)
                if(!ReferenceEquals(stage.Source,Source.GetProgram(stage.Source.StageCode,stage.Source.StageIndex)))
                    throw new InvalidDataException("Directory wrapper differs from the library's counted original stage.");
        }
    }
}
