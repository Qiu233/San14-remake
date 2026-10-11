using San14.Data;

public sealed record SkyCloudColorFacePacket(ReadOnlyMemory<byte> VertexConstants,
    ReadOnlyMemory<byte> PixelConstants,uint MaximumRaySamples);

/// <summary>Retains the Cloud program and WORK target while its six texture
/// borrowing draws can be replaced between complete World requests.</summary>
public sealed class SkyCloudColorProducer : IAsyncDisposable
{
    public SkyCloudProgram Program { get; }
    public CubeRenderTextureDevice Work { get; }
    public bool NoiseGroupPresent { get; }
    private DrawSet draws;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private bool retiring,needsCurrentFrame;
    private Task? disposal;
    private NativeMapBorrowerRetirementPackage? finalRetirement;

    internal sealed class DrawSet : IAsyncDisposable
    {
        internal readonly IReadOnlyList<SkyCloudColorDraw> Faces;
        private readonly NativeMapBorrowerRetirementPackage retirement;
        internal DrawSet(IReadOnlyList<SkyCloudColorDraw> faces)
        {
            Faces=faces;
            retirement=new("Cloud-draws/"+Guid.NewGuid().ToString("N"),faces.Reverse().Cast<IAsyncDisposable>().ToArray());
        }
        public ValueTask DisposeAsync()=>retirement.DisposeAsync();
    }
    public sealed class PreparedTextureReplacement : IAsyncDisposable
    {
        internal readonly SkyCloudColorProducer producer;
        internal readonly DrawSet previous;
        internal DrawSet? replacement;
        internal bool installed,retiring;
        internal PreparedTextureReplacement(SkyCloudColorProducer producer,DrawSet previous,DrawSet replacement)
        {this.producer=producer;this.previous=previous;this.replacement=replacement;}
        public async ValueTask DisposeAsync()
        {
            if(installed)return;
            retiring=true;
            if(replacement is { } pending){await pending.DisposeAsync();replacement=null;}
        }
    }

    private SkyCloudColorProducer(SkyCloudProgram program,CubeRenderTextureDevice work,bool present,IReadOnlyList<SkyCloudColorDraw> faces)
    {Program=program;Work=work;NoiseGroupPresent=present;draws=new(faces);}

    public static async Task<SkyCloudColorProducer> BuildAsync(SkyCloudColorProgramSelection selection,
        Vkd3dShaderCompiler compiler,CubeRenderTextureDevice work,
        IReadOnlyList<Sky2DeviceInput>? explicitNoiseAndTables,
        IReadOnlyList<SkyCloudColorFacePacket> explicitSixFaces,uint explicitColorWriteMask)
    {
        ArgumentNullException.ThrowIfNull(work);ArgumentNullException.ThrowIfNull(explicitSixFaces);
        var owned=new List<IAsyncDisposable>();
        try
        {
            var program=await SkyCloudProgram.BuildAsync(selection,compiler);owned.Add(program);
            var faces=new List<SkyCloudColorDraw>();
            if(explicitNoiseAndTables is not null)
            {
                var packets=Snapshot(program,explicitSixFaces);
                for(uint face=0;face<6;face++)
                {
                    var packet=packets[(int)face];
                    var draw=await SkyCloudColorDraw.BuildAsync(program,work,
                        SkySceneSchedule.SurfaceId(work.Source.LevelNum,face,0),packet.VertexConstants,packet.PixelConstants,
                        packet.MaximumRaySamples,explicitNoiseAndTables,explicitColorWriteMask);
                    faces.Add(draw);owned.Add(draw);
                }
            }
            return new(program,work,explicitNoiseAndTables is not null,faces.AsReadOnly());
        }
        catch(Exception failure)
        {
            var partial=new NativeMapBorrowerRetirementPackage("Cloud-producer-partial/"+Guid.NewGuid().ToString("N"),owned.AsEnumerable().Reverse().ToArray());
            try{await partial.DisposeAsync();}
            catch(Exception cleanup){partial.RetainFailure();throw new NativeMapBorrowerConstructionException(failure,cleanup,partial);}
            throw;
        }
    }

    public async Task<PreparedTextureReplacement> PrepareTextureReplacementAsync(IReadOnlyList<Sky2DeviceInput> inputs)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!NoiseGroupPresent)throw new NotSupportedException("An absent Cloud producer needs its actual source activation before texture replacement.");
            var faces=new List<SkyCloudColorDraw>();
            try
            {
                for(uint face=0;face<6;face++)
                {
                    var old=draws.Faces[(int)face];var packet=old.Constants;
                    // Retained allocation contents are never submitted as a
                    // new source frame. UpdateFrame must replace all six first.
                    faces.Add(await SkyCloudColorDraw.BuildAsync(Program,Work,
                        SkySceneSchedule.SurfaceId(Work.Source.LevelNum,face,0),packet.VertexBytes,packet.PixelBytes,
                        packet.MaximumRaySamples,inputs,old.ExplicitColorWriteMask));
                }
                return new(this,draws,new(faces.AsReadOnly()));
            }
            catch(Exception failure)
            {
                var partial=new NativeMapBorrowerRetirementPackage("Cloud-draws-partial/"+Guid.NewGuid().ToString("N"),
                    faces.AsEnumerable().Reverse().Cast<IAsyncDisposable>().ToArray());
                try{await partial.DisposeAsync();}
                catch(Exception cleanup){partial.RetainFailure();throw new NativeMapBorrowerConstructionException(failure,cleanup,partial);}
                throw;
            }
        }
        finally{gate.Release();}
    }
    internal void RequirePreparedTextureReplacement(PreparedTextureReplacement replacement)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(!ReferenceEquals(replacement.producer,this)||!ReferenceEquals(replacement.previous,draws)||replacement.installed||replacement.retiring||replacement.replacement is null)
            throw new InvalidOperationException("Cloud draws changed after this texture replacement was prepared.");
    }
    internal IAsyncDisposable InstallTextureReplacement(PreparedTextureReplacement replacement)
    {
        if(!gate.Wait(0))throw new InvalidOperationException("Cloud draws are currently borrowed.");
        try
        {
            RequirePreparedTextureReplacement(replacement);
            var previous=draws;draws=replacement.replacement!;
            needsCurrentFrame=true;replacement.installed=true;replacement.replacement=null;
            return previous;
        }
        finally{gate.Release();}
    }

    public async Task UpdateFrameAsync(IReadOnlyList<SkyCloudColorFacePacket> explicitSixFaces)
    {
        var packets=NoiseGroupPresent?Snapshot(Program,explicitSixFaces):Array.Empty<SkyCloudColorFacePacket>();
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            for(int face=0;face<packets.Length;face++)
                await draws.Faces[face].UpdateConstantsAsync(packets[face].VertexConstants,packets[face].PixelConstants,packets[face].MaximumRaySamples);
            needsCurrentFrame=false;
        }
        finally{gate.Release();}
    }
    public SkySceneFaceProducer? CreateSceneStage()
    {
        RequireCurrentFrame();
        return NoiseGroupPresent?new(Program.Source.Source.ResourceUri+"#native-pass=0",SubmitFaceAsync):null;
    }
    private void RequireCurrentFrame()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(needsCurrentFrame)throw new InvalidOperationException("Replacement Cloud draws require all six actual current face packets.");
    }
    private async Task SubmitFaceAsync(SkySceneFaceContext context)
    {
        if(!ReferenceEquals(context.PrimaryWork,Work)||context.Face>=6||
            context.Target!=Work.Surfaces[(int)SkySceneSchedule.SurfaceId(Work.Source.LevelNum,context.Face,0)])
            throw new InvalidDataException("Cloud scene stage belongs to another WORK resource or surface.");
        await gate.WaitAsync();
        try{RequireCurrentFrame();await draws.Faces[(int)context.Face].DrawAsync();}
        finally{gate.Release();}
    }
    private static SkyCloudColorFacePacket[] Snapshot(SkyCloudProgram program,IReadOnlyList<SkyCloudColorFacePacket> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        if(faces.Count!=6)throw new InvalidDataException("Cloud WORK generation needs six explicit face packets.");
        return faces.Select(packet=>
        {
            var validated=SkyCloudConstants.ReadColor(program.VertexBindings.SourceDefinitions,program.PixelBindings.SourceDefinitions,
                packet.VertexConstants.Span,packet.PixelConstants.Span,packet.MaximumRaySamples);
            return new SkyCloudColorFacePacket(validated.VertexBytes,validated.PixelBytes,validated.MaximumRaySamples);
        }).ToArray();
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetimeGate)
        {
            retiring=true;
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=DisposeCoreAsync();
            return new(disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            finalRetirement??=new("Cloud-producer-retire/"+Guid.NewGuid().ToString("N"),draws,Program);
            await finalRetirement.DisposeAsync();
        }
        finally{gate.Release();}
    }
}
