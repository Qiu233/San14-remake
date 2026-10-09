using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Executes the original Eye bloom draws on current caller resources.
/// This owner borrows its module bank/library and owns its wrapper samplers.
/// Histogram, exposure, debug draws and complete Functor execution remain
/// separate operations; completing bloom does not publish a complete frame.</summary>
public sealed partial class OriginalMapPostEffect3EyeBloomExecutionOwner : IAsyncDisposable
{
    public OriginalMapPostEffect3ResourceBank Bank { get; }
    public OriginalMapPostEffect3FullscreenGraphicsExecutor Graphics { get; }
    public LoadedOriginalMapPostEffect3EyeAdaptation2Source Source { get; }
    public OriginalMapPostEffect3EyeAdaptation2Owner Controller { get; }
    public OriginalMapPostEffect3FullscreenCompletedPass? LastCompleted { get; private set; }
    public IOriginalMapPostEffect3ComputeMipTextureLease? LastPublishedMip { get; private set; }
    private readonly List<OriginalMapPostEffect3FullscreenCompletedPass> completedDraws = new();
    public IReadOnlyList<OriginalMapPostEffect3FullscreenCompletedPass> CompletedDraws
    {
        get { lock (lifetime) return Array.AsReadOnly(completedDraws.ToArray()); }
    }
    private readonly LoadedOriginalDeferredSamplerNativeSource native;
    private readonly OriginalMapPostEffect3SourceSamplerOwner managers;
    private readonly Dictionary<string, (Rid Rid, G1TextureSamplerRecipe Recipe)> wrappers = new();
    private readonly SemaphoreSlim serial = new(1, 1);
    private readonly object lifetime = new();
    private int accepted;
    private bool retiring;
    private Task? disposal;

    private OriginalMapPostEffect3EyeBloomExecutionOwner(OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3FullscreenGraphicsExecutor graphics, LoadedOriginalMapPostEffect3EyeAdaptation2Source source,
        LoadedOriginalDeferredSamplerNativeSource native, OriginalMapPostEffect3SourceSamplerOwner managers,
        OriginalMapPostEffect3EyeAdaptation2Owner? actualController=null)
    {
        Bank = bank; Graphics = graphics; Source = source; this.native = native; this.managers = managers;
        Controller = actualController??source.ConstructFreshController(bank.Source.Source, bank.Source.ContextOwnerIdentity);
    }

    public static async Task<OriginalMapPostEffect3EyeBloomExecutionOwner> BuildBorrowedAsync(
        OriginalGameRepository repository, OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3ProgramLibrary programs,string locale="TC")
        =>await BuildCoreAsync(repository,bank,programs,null,locale,null);

    internal static Task<OriginalMapPostEffect3EyeBloomExecutionOwner> BuildForCurrentControlledFrameAsync(
        OriginalGameRepository repository,OriginalMapPostEffect3WorldControlledFactoryFrame frame,
        OriginalMapPostEffect3ProgramLibrary programs)
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(frame.EyeOwner,frame.CpuModule.EyeController))
            throw new InvalidDataException("Current Eye graphics owner must borrow the actual module's retained controller.");
        return BuildCoreAsync(repository,frame.World.Bank,programs,frame.EyeOwner,frame.Eye.Locale,frame.ModuleLifetime!.StaticSources);
    }

    private static async Task<OriginalMapPostEffect3EyeBloomExecutionOwner> BuildCoreAsync(
        OriginalGameRepository repository,OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3ProgramLibrary programs,OriginalMapPostEffect3EyeAdaptation2Owner? actualController,string locale,
        OriginalMapPostEffect3WorldControlledStaticSources? sources)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(programs);
        sources?.RequireRepository(repository);
        var native = sources?.Native??OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale);
        var source = sources?.Eye??OriginalMapPostEffect3ParameterEyeAdaptation2.Load(repository,locale);
        if(native.Locale!=locale||source.Locale!=locale)throw new InvalidDataException("Eye bloom needs the current module original executable locale.");
        var graphics = OriginalMapPostEffect3FullscreenGraphicsExecutor.Create(programs, bank);
        try
        {
            var managers = await OriginalMapPostEffect3SourceSamplerOwner.BuildAsync(native, bank.Source.ContextOwnerIdentity);
            return new(bank, graphics, source, native, managers,actualController);
        }
        catch { await graphics.DisposeAsync(); throw; }
    }

    /// <summary>Each draw needs the caller's actual post-Bind raster, viewport,
    /// depth range and scissor. Color/exposure leases must be current readable
    /// products; this method neither makes a World fixture nor assumes that a
    /// resource allocation contains pixels. CB sides are actual module sides.</summary>
    public async Task<OriginalMapPostEffect3FullscreenCompletedPass> DrawIterationAsync(
        OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3EyeBeforeBindings before,
        IOriginalMapPostEffect3FullscreenDrawContextLease actualAfterBindingContext,
        IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> actualPrefilterReads,
        IReadOnlyDictionary<int, int> actualConstantBufferSides)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(before); ArgumentNullException.ThrowIfNull(actualAfterBindingContext);
        ArgumentNullException.ThrowIfNull(actualPrefilterReads); ArgumentNullException.ThrowIfNull(actualConstantBufferSides);
        var cpu=OriginalRenderThreadSchedulingDiagnostics.BeginCpuTrace("EyeDraw",before.Iteration.SourcePassIndex);
        int ordinal = before.Iteration.NativeCachedPassOrdinal;
        if (ordinal is not (0 or 2 or 3 or 4 or 9 or 10 or 11))
            throw new NotSupportedException("Eye bloom owner requires an actual prefilter/downsample/upsample iteration.");
        frame.RequireCurrent();
        if (!ReferenceEquals(frame.Allocation, Bank.Source) || !ReferenceEquals(table.Allocation, Bank.Source) ||
            table.FrameNumber != frame.FrameNumber || !frame.OrderedIterations.Contains(before.Iteration) ||
            frame.OwnerIdentity != Bank.Source.ContextOwnerIdentity)
            throw new InvalidDataException("Eye bloom draw belongs to another current source module/frame.");
        var sides = new Dictionary<int, int>(actualConstantBufferSides);
        int pass = before.Iteration.SourcePassIndex;
        var selectedCbs = Bank.Source.Passes[pass].ActiveBindings
            .Where(b => b.Role == OriginalMapPostEffect3BindingRole.ConstantBufferRead)
            .Select(b => b.ParameterIndex).Distinct().ToArray();
        var loop = before.BloomLoopConstants ?? throw new InvalidDataException("Selected Eye bloom draw lacks its original loop constants.");
        foreach (int index in selectedCbs.Concat([frame.BloomMainConstants.Declaration.Index, loop.Declaration.Index]).Distinct())
        {
            if (!sides.TryGetValue(index, out int side))
                throw new InvalidDataException("Eye bloom draw lacks the actual CB side: " + index);
            if (!Bank.ConstantBuffers.TryGetValue(index, out var buffers) || side < 0 || side >= buffers.Count)
                throw new InvalidDataException("Eye bloom draw selects an absent native CB allocation side: " + index);
        }
        cpu?.Mark("01-input-current-and-cb-sides");
        lock (lifetime) { ObjectDisposedException.ThrowIf(retiring, this); accepted++; }
        await serial.WaitAsync();
        cpu?.Mark("02-serial-wait");
        try
        {
            RequireLive(); frame.RequireCurrent(); actualAfterBindingContext.RequireCurrentForPass(pass);
            table.ApplyEyeBeforeAlias(frame, before);
            var coverage = OriginalMapPostEffect3EyeGraphicsWriteCoverage.Prepare(Bank.Source, frame, before);
            cpu?.Mark("03-alias-and-footprint");
            var target = await Bank.CaptureEyeMipTargetLeaseAsync(table, frame, before, actualAfterBindingContext);
            cpu?.Mark("04-target-capture");
            await Bank.UploadCompleteConstantBufferAsync(frame.BloomMainConstants, sides[frame.BloomMainConstants.Declaration.Index]);
            await Bank.UploadCompleteConstantBufferAsync(loop, sides[loop.Declaration.Index]);
            cpu?.Mark("05-cb-uploads");

            IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> reads;
            IReadOnlyList<OriginalMapPostEffect3FullscreenTextureOverride> setters = [];
            IOriginalMapPostEffect3ComputeSamplerLease sampler;
            if (ordinal == 0)
            {
                reads = actualPrefilterReads.ToArray();
                foreach (var read in reads) read.RequireCurrent();
                sampler = managers.LeaseSelected(new ManagerSelection(this, frame, before), 2);
            }
            else
            {
                if (actualPrefilterReads.Count != 0)
                    throw new InvalidDataException("Downsample/upsample cannot inherit another pass's prefilter reads.");
                var input = before.RequiresDynamicPixelTextureBinding
                    ? await Bank.CaptureEyeBeforeMipReadLeaseAsync(table, frame, before)
                    : await Bank.CaptureEyeMipAliasReadLeaseAsync(table, 69, pass, 0);
                reads = [input];
                if (before.RequiresDynamicPixelTextureBinding)
                    setters = [OriginalMapPostEffect3FullscreenTextureOverride.CaptureEye(table, frame, before, input)];
                sampler = await LeaseWrapperSamplerAsync(frame, before, input);
            }
            cpu?.Mark("06-read-and-sampler");
            var inputs = new OriginalMapPostEffect3FullscreenFrameInputs(
                OriginalMapPostEffect3FullscreenSourceDraw.CaptureEye(frame, before), table, reads, [sampler],
                selectedCbs.ToDictionary(i => i, i => sides[i]), [new(0, target)], actualAfterBindingContext)
                { TextureOverrides = setters };
            if(actualAfterBindingContext is IOriginalMapPostEffect3EyeCloneBindingContext actualClones)
                actualClones.RequireEyeCloneBindings(frame,before,target,reads);
            cpu?.Mark("07-input-and-clone-bindings");
            var historicalInputs=OriginalMapPostEffect3Functor1SubmissionLedger.FreezeGraphicsInputs(inputs);
            cpu?.Mark("08-freeze-input-json");
            var actualProgram=await Graphics.Library.GetCompiledPassAsync(pass);
            cpu?.Mark("09-compiled-program");
            var completed = await Graphics.DrawAsync(pass, inputs);
            cpu?.Mark("10-fullscreen-draw");
            lastHistoricalGraphicsInputs=(completed,frame,before,actualProgram,historicalInputs);
            LastPublishedMip = Bank.PublishEyeGraphicsCoverage(coverage, Graphics, completed);
            lock (lifetime) completedDraws.Add(completed);
            cpu?.Mark("11-publish-coverage");
            return LastCompleted = completed;
        }
        finally { serial.Release(); lock (lifetime) accepted--; cpu?.Mark("12-release"); }
    }

    private async Task<IOriginalMapPostEffect3ComputeSamplerLease> LeaseWrapperSamplerAsync(
        OriginalMapPostEffect3EyeAdaptation2Frame frame, OriginalMapPostEffect3EyeBeforeBindings before,
        IOriginalMapPostEffect3ComputeSelectedMipTextureLease input)
    {
        int physical = before.SourceTextureParameterIndex ?? throw new InvalidDataException("Eye mip input lacks its physical wrapper.");
        var texture = Bank.RequireTexture(physical, input.InstanceIndex);
        byte[] key = OriginalDeferredSamplerSources.RenderTextureKey(native, texture.Shape.MipLevels);
        // Copy clone0 before changing the physical key.
        // The selected mip views change while retaining the copied sampler key.
        if (input.NativeFirstMipLevel != 0)
            BinaryPrimitives.WriteUInt16LittleEndian(key, (ushort)((BinaryPrimitives.ReadUInt16LittleEndian(key) & 0x03FF) |
                Source.ReadOwnedBloomViewPlan(Bank.Source, physical == 86).NativeSamplerFilterMask));
        string keyHex = Convert.ToHexString(key);
        if (!wrappers.TryGetValue(keyHex, out var cached))
        {
            var recipe = OriginalDeferredSamplerSources.DecodeKey(native, key);
            Rid rid = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => OriginalDeviceSampler.Create(rd, recipe));
            wrappers.Add(keyHex, cached = (rid, recipe));
        }
        return new WrapperSampler(this, frame, before, input, cached.Rid, cached.Recipe, keyHex);
    }

    private void RequireLive() { lock (lifetime) ObjectDisposedException.ThrowIf(retiring && accepted == 0, this); }
    private sealed record ManagerSelection(OriginalMapPostEffect3EyeBloomExecutionOwner Owner,
        OriginalMapPostEffect3EyeAdaptation2Frame Frame, OriginalMapPostEffect3EyeBeforeBindings Before)
        : IOriginalMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber => Frame.FrameNumber;
        public string ContextOwnerIdentity => Frame.OwnerIdentity;
        public int NativeStageOrdinal => 2;
        public uint SourceRegister => 7;
        public OriginalMapPostEffect3ManagerSamplerKind Kind => OriginalMapPostEffect3ManagerSamplerKind.Linear;
        public string SourceEvidence => Before.SourceEvidence + ";actual-Eye-Before-PS7-manager30-linear";
        public void RequireCurrent()
        {
            Owner.RequireLive(); Frame.RequireCurrent();
            if (Before.Iteration.NativeCachedPassOrdinal != 0 || !Frame.OrderedIterations.Contains(Before.Iteration) ||
                !Before.NativeBeforeSamplerRegisters.Contains(7))
                throw new InvalidDataException("Eye manager sampler lacks its current original Before writer.");
        }
    }
    private sealed record WrapperSampler(OriginalMapPostEffect3EyeBloomExecutionOwner Owner,
        OriginalMapPostEffect3EyeAdaptation2Frame Frame, OriginalMapPostEffect3EyeBeforeBindings Before,
        IOriginalMapPostEffect3ComputeSelectedMipTextureLease Input, Rid SamplerRid,
        G1TextureSamplerRecipe Recipe, string KeyHex) : IOriginalMapPostEffect3SourceMipSamplerLease
    {
        public long FrameNumber => Frame.FrameNumber;
        public string ContextOwnerIdentity => Frame.OwnerIdentity;
        public int NativeStageOrdinal => 2;
        public uint SourceRegister => 0;
        public string SourceEvidence => Before.SourceEvidence + ";actual-Eye-cloned-wrapper-PS0-key=" + KeyHex;
        public void RequireCurrent()
        {
            Owner.RequireLive(); Frame.RequireCurrent(); Input.RequireCurrent();
            Input.RequireSourceSelectedView(Before.Iteration.SourcePassIndex, 0);
            if (!Frame.OrderedIterations.Contains(Before.Iteration) || Before.SourceMipLevel != Input.NativeFirstMipLevel)
                throw new InvalidDataException("Eye wrapper sampler no longer belongs to this selected source mip.");
        }
        public void RequireSourceMipSampling(uint filter, uint bias, uint minimum, uint maximum)
        {
            RequireCurrent();
            if (Recipe.Filter != filter || Recipe.MipLodBiasBits != bias || Recipe.MinimumLodBits != minimum || Recipe.MaximumLodBits != maximum)
                throw new InvalidDataException("Eye wrapper sampler differs from its actual source read footprint.");
        }
    }
    public ValueTask DisposeAsync()
    {
        lock (lifetime)
        {
            if (disposal is not null) return new(disposal);
            retiring = true; return new(disposal = RetireAsync());
        }
    }
    private async Task RetireAsync()
    {
        await serial.WaitAsync();
        try
        {
            await Graphics.DisposeAsync(); await managers.DisposeAsync();
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                foreach (var sampler in wrappers.Values) if (sampler.Rid.IsValid) rd.FreeRid(sampler.Rid);
                wrappers.Clear(); return true;
            });
            LastCompleted = null; LastPublishedMip = null; lock (lifetime) completedDraws.Clear();
        }
        finally { serial.Release(); }
    }
}
