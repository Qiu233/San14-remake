using San14.Data;

public sealed partial class SkyDirectionalAmbientDeviceOwner
{
    private readonly List<NativeMapBorrowerRetirementPackage> replacedSeasonResources = new();
    public Exception? LastSeasonRetirementFailure { get; private set; }

    public sealed class PreparedSeasonResources : IAsyncDisposable
    {
        internal readonly SkyDirectionalAmbientDeviceOwner device;
        internal readonly Sky2TextureBindings previous, bindings;
        internal readonly Sky2TextureBindingSnapshot snapshot;
        internal readonly SkySceneTextures textures;
        internal readonly SkyScene.PreparedSky2Replacement branch;
        private readonly NativeMapBorrowerRetirementPackage retirement;
        internal bool installed,retiring;

        internal PreparedSeasonResources(SkyDirectionalAmbientDeviceOwner device,
            Sky2TextureBindings previous, Sky2TextureBindings bindings,
            Sky2TextureBindingSnapshot snapshot, SkySceneTextures textures,
            SkyScene.PreparedSky2Replacement branch)
        {
            this.device=device;this.previous=previous;this.bindings=bindings;
            this.snapshot=snapshot;this.textures=textures;this.branch=branch;
            retirement=new(device.OwnedSkyIdentity+"/pending-season/"+Guid.NewGuid().ToString("N"),branch,textures);
        }
        public async ValueTask DisposeAsync()
        {
            if(installed)return;
            retiring=true;
            if(!ReferenceEquals(bindings,previous))bindings.Dispose();
            await retirement.DisposeAsync();
        }
    }

    /// <summary>Non-World callers retain their existing compatibility boundary.
    /// The World season loader uses the prepared replacement path below.</summary>
    public void RequireCompatibleSeasonResources(SkySeasonMainCameraSource source)
    {
        ArgumentNullException.ThrowIfNull(source);ObjectDisposedException.ThrowIf(retiring,this);
        textureBindings.RequireCompatiblePackage(SkySeasonPackageTextures.Read(source.Selection.FirstPackage),
            reference=>SkySeasonPackageTextures.LoadSampling(repository,reference));
    }

    public async Task<PreparedSeasonResources?> PrepareSeasonResourcesAsync(SkySeasonMainCameraSource source,
        IReadOnlyList<SkyTextureSource>? atmosphereTextures=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(atmosphereTextures is not null&&atmosphereTextures.Count!=4)
            throw new InvalidDataException("Sky2 atmosphere replacement requires the complete four-entry Plane import.");
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(sourceFault is not null)throw new InvalidOperationException("Failed Sky owner must be retired before season replacement.",sourceFault);
            var bindings=textureBindings.PrepareReplacement(SkySeasonPackageTextures.Read(source.Selection.FirstPackage),
                reference=>SkySeasonPackageTextures.LoadSampling(repository,reference));
            if(bindings is null&&atmosphereTextures is null)return null;
            bindings??=textureBindings;
            var snapshot=ReferenceEquals(bindings,textureBindings)?textureSnapshot:bindings.Capture();
            var selections=Enumerable.Range(0,3).Select(i=>atmosphereTextures is null
                ?textures.Selections["source-sta"+i]:new SkySceneTextureSelection("source-sta"+i,atmosphereTextures[i])).ToList();
            foreach(var role in new[]{SkySamplingRole.Earth,SkySamplingRole.Moon,SkySamplingRole.MilkyWay})
                if(snapshot.GetTexture(role) is { } resource)selections.Add(SkySceneTextureSelection.FromSampling(resource));
            var pendingTextures=new SkySceneTextures(selections);
            SkyScene.PreparedSky2Replacement? branch=null;
            try
            {
                var inputs=new List<Sky2DeviceInput>();
                var registers=shader.ReadResourceDefinitions(ActualContributionPixelProgram).Bindings
                    .Where(b=>b.InputType==2).Select(b=>b.BindPoint);
                foreach(uint register in registers)
                {
                    if(register<3)inputs.Add(await pendingTextures.GetInputAsync(register,"source-sta"+register));
                    else
                    {
                        var role=register switch{3=>SkySamplingRole.Earth,4=>SkySamplingRole.MilkyWay,5=>SkySamplingRole.Moon,
                            _=>throw new NotSupportedException("Unknown Sky2 seasonal texture register.")};
                        if(snapshot.GetTexture(role) is { } resource)
                            inputs.Add(await pendingTextures.GetInputAsync(register,resource.SourceName));
                    }
                }
                branch=await Scene.PrepareSky2ReplacementAsync(shader,compiler,snapshot,inputs);
                return new(this,textureBindings,bindings,snapshot,pendingTextures,branch);
            }
            catch(Exception failure)
            {
                if(!ReferenceEquals(bindings,textureBindings))bindings.Dispose();
                var partial=new NativeMapBorrowerRetirementPackage(OwnedSkyIdentity+"/season-partial/"+Guid.NewGuid().ToString("N"),
                    failure is NativeMapBorrowerConstructionException nested?nested.RetainedPackage:null,branch,pendingTextures);
                try{await partial.DisposeAsync();}
                catch(Exception cleanup){partial.RetainFailure();throw new NativeMapBorrowerConstructionException(failure,cleanup,partial);}
                throw;
            }
        }
        finally{gate.Release();}
    }

    public void RequirePreparedSeasonResources(PreparedSeasonResources? prepared)
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        textureSnapshot.RequireCurrent();
        if(prepared is null)return;
        if(!ReferenceEquals(prepared.device,this)||prepared.installed||prepared.retiring||!ReferenceEquals(prepared.previous,textureBindings))
            throw new InvalidOperationException("Season Sky replacement belongs to another or changed owner.");
        prepared.snapshot.RequireCurrent();prepared.branch.RequireInstallable();
    }

    public void InstallPreparedSeasonResources(PreparedSeasonResources? prepared)
    {
        RequirePreparedSeasonResources(prepared);
        if(prepared is null)return;
        if(!gate.Wait(0))throw new InvalidOperationException("Sky resources are currently borrowed.");
        try
        {
            var previousTextures=textures;
            var previousBindings=textureBindings;
            var previousBranch=Scene.InstallSky2Replacement(prepared.branch);
            textures=prepared.textures;textureBindings=prepared.bindings;textureSnapshot=prepared.snapshot;
            preparedCurrent=null;
            prepared.installed=true;
            if(!ReferenceEquals(previousBindings,textureBindings))previousBindings.Dispose();
            lock(lifetimeGate)
                replacedSeasonResources.Add(new(OwnedSkyIdentity+"/replaced-season/"+Guid.NewGuid().ToString("N"),
                    previousBranch,previousTextures));
        }
        finally{gate.Release();}
    }

    /// <summary>Installation already succeeded. Cleanup failures remain owned
    /// here for retry and never report the new season as uninstalled.</summary>
    public async Task RetireReplacedSeasonResourcesAsync()
    {
        await gate.WaitAsync();
        try
        {
            try{await RetireSeasonResourcesCoreAsync();LastSeasonRetirementFailure=null;}
            catch(Exception failure){LastSeasonRetirementFailure=failure;}
        }
        finally{gate.Release();}
    }

    private async Task RetireSeasonResourcesCoreAsync()
    {
        while(true)
        {
            NativeMapBorrowerRetirementPackage package;
            lock(lifetimeGate)
            {if(replacedSeasonResources.Count==0)return;package=replacedSeasonResources[0];}
            await package.DisposeAsync();
            lock(lifetimeGate)replacedSeasonResources.RemoveAt(0);
        }
    }
}
