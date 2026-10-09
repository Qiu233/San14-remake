using San14.Data;

/// <summary>Sealed source-wrapper identities backed by actual created shader
/// objects in this one library. Source Select availability and the eventual
/// known-null/backend draw variant are separate identities.</summary>
internal sealed class MapPostEffect3Functor1ShaderCatalog
{
    internal sealed class Entry
    {
        internal Entry(MapPostEffect3Functor1ShaderCatalog owner,MapPostEffect3Functor1WrapperRecipe recipe,
            MapPostEffect3CompiledPass actual)=>(Owner,Recipe,ActualOrdinaryProgram)=(owner,recipe,actual);
        internal readonly MapPostEffect3Functor1ShaderCatalog Owner;
        internal readonly MapPostEffect3Functor1WrapperRecipe Recipe;
        internal readonly MapPostEffect3CompiledPass ActualOrdinaryProgram;
        internal MapPostEffect3CompiledStage? SourceCsWrapper=>ActualOrdinaryProgram.Stages.SingleOrDefault(s=>s.Source.StageCode==5);
        internal MapPostEffect3CompiledStage? SourcePsWrapper=>ActualOrdinaryProgram.Stages.SingleOrDefault(s=>s.Source.StageCode==0);
        internal byte EvaluateActualSelectionAl()
        {
            Owner.Library.RequireOwnedDirectoryProgram(ActualOrdinaryProgram);
            // Select the compute program, otherwise the pixel program.
            //The AL follows actual owned pointer availability, not a mask.
            return SourceCsWrapper is not null||SourcePsWrapper is not null?(byte)1:(byte)0;
        }
    }
    private readonly MapPostEffect3WorldControlledFactoryFrame frame;
    private readonly Dictionary<int,Entry> entries=[];
    internal MapPostEffect3ProgramLibrary Library{get;}
    private MapPostEffect3Functor1ShaderCatalog(MapPostEffect3WorldControlledFactoryFrame frame,
        MapPostEffect3ProgramLibrary library)=>(this.frame,Library)=(frame,library);
    internal static async Task<MapPostEffect3Functor1ShaderCatalog> CaptureCreatedAsync(
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3ProgramLibrary library,Action<string>? checkpoint)
    {
        frame.RequireCurrent();
        if(library.SourceSha256!=frame.WrapperDirectory.ModuleSourceSha256||frame.WrapperDirectory.Wrappers.Count!=72)
            throw new InvalidDataException("Program catalog differs from this actual complete directory.");
        var result=new MapPostEffect3Functor1ShaderCatalog(frame,library);
        foreach(var recipe in frame.WrapperDirectory.Wrappers)
        {
            var program=await library.GetCompiledPassAsync(recipe.SourcePassIndex);
            library.RequireOwnedDirectoryProgram(program);
            if(program.Source.Index!=recipe.SourcePassIndex||program.ContainerSha256!=frame.Camera.Allocation.Source.Sha256||
                !program.Source.RawBytes.Span.SequenceEqual(recipe.Pass.Pass.RawBytes.Span))
                throw new InvalidDataException("Actual program catalog did not create the exact source wrapper.");
            var entry=new Entry(result,recipe,program);
            if(entry.EvaluateActualSelectionAl()==0)throw new NotSupportedException("Native Select lacks its actual CS/PS wrapper: "+recipe.SourcePassIndex);
            result.entries.Add(recipe.SourcePassIndex,entry);checkpoint?.Invoke("controlled-source-program"+recipe.SourcePassIndex+"-created");
        }
        return result;
    }
    internal Entry RequireEntry(MapPostEffect3Functor1WrapperRecipe recipe)
    {
        frame.RequireCurrent();
        if(!entries.TryGetValue(recipe.SourcePassIndex,out var entry)||!ReferenceEquals(entry.Recipe,recipe)||!ReferenceEquals(entry.Owner,this))
            throw new InvalidDataException("Select does not belong to this actual frame's shader catalog.");
        Library.RequireOwnedDirectoryProgram(entry.ActualOrdinaryProgram);return entry;
    }
    internal void RequireActualVariant(Entry entry,MapPostEffect3CompiledPass actual)
    {
        if(!ReferenceEquals(entry.Owner,this)||!ReferenceEquals(RequireEntry(entry.Recipe),entry))
            throw new InvalidDataException("Bind used a foreign source Select identity.");
        Library.RequireOwnedDirectoryProgram(actual);
        if(actual.Source.Index!=entry.Recipe.SourcePassIndex||!ReferenceEquals(actual.Source,entry.ActualOrdinaryProgram.Source)||
            !actual.Stages.Select(s=>(s.Source.StageCode,s.Source.StageIndex,s.Source.Sha256)).SequenceEqual(
                entry.ActualOrdinaryProgram.Stages.Select(s=>(s.Source.StageCode,s.Source.StageIndex,s.Source.Sha256))))
            throw new InvalidDataException("Backend variant replaced the source selected shader tuple.");
    }
}
