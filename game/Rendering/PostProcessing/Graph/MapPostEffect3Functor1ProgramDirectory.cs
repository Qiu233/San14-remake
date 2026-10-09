using San14.Data;

/// <summary>Actual shader creation for the complete source directory, including
/// mask-rejected entries. Creation alone does not execute Select/Reset or draw.</summary>
public static class MapPostEffect3Functor1ProgramDirectory
{
    public static async Task<object> PrepareAsync(MapPostEffect3WorldControlledFactoryFrame frame,
        MapPostEffect3ProgramLibrary programs,Action<string>? checkpoint=null)
    {
        frame.RequireCurrent();
        if(programs.SourceSha256!=frame.WrapperDirectory.ModuleSourceSha256||frame.WrapperDirectory.Wrappers.Count!=72)
            throw new InvalidDataException("Functor1 program directory needs its exact current module source.");
        var entries=new List<object>();
        foreach(var recipe in frame.WrapperDirectory.Wrappers)
        {
            frame.RequireCurrent();
            if(recipe.SourcePassIndex!=45+entries.Count)
                throw new InvalidDataException("Functor1 shader creation lost its original complete directory order.");
            var compiled=await programs.GetCompiledPassAsync(recipe.SourcePassIndex);
            bool hasCompute=compiled.Stages.Any(s=>s.Source.StageCode==5);
            bool hasVertex=compiled.Stages.Any(s=>s.Source.StageCode==1)||compiled.StockScreenVertex is not null;
            if(compiled.Source.Index!=recipe.SourcePassIndex||!compiled.ShaderRid.IsValid||(!hasCompute&&!hasVertex))
                throw new InvalidDataException("Functor1 directory entry has no actual compiled compute or vertex program.");
            entries.Add(new{recipe.SourcePassIndex,recipe.FactoryIndex,recipe.SourceDirectoryOrdinal,
                recipe.MaskAdmitsBefore,shaderRid=compiled.ShaderRid.Id,hasCompute,hasVertex,
                embeddedStockVertex=compiled.StockScreenVertex is not null,
                stages=compiled.Stages.Select(s=>new{s.Source.StageCode,s.Source.StageIndex,s.DeviceTranslation.SpirvSha256}).ToArray()});
            checkpoint?.Invoke("controlled-source-program"+recipe.SourcePassIndex+"-created");
        }
        return new{frame.Camera.FrameNumber,sourceFunctor=1,entries,
            actualCompleteSourceProgramsCreated=true,actualFrontendSelectResetExecuted=false,
            wholeWrapperDirectoryReturned=false,selectedFunctorGpuCompleted=false,images=0};
    }
}
