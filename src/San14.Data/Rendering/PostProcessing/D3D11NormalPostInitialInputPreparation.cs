using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11NormalPostInitialConstantBinding(
    string Stage,int Slot,int ParameterIndex,ReadOnlyMemory<byte> Bytes);

public sealed record D3D11NormalPostInitialPass(string Name,int SourcePassIndex,
    IReadOnlyList<D3D11NormalPostInitialConstantBinding> Constants);

/// <summary>Fresh controller numeric inputs. The actual native invocation
/// supplies its Common clock, delta, selected light and current Main lanes
/// before uploading these templates. No exposure or staging pixels are seeded.</summary>
public sealed class D3D11NormalPostInitialConstants
{
    internal D3D11NormalPostInitialConstants(MapPostEffect3AllocationPlan allocation,
        MapPostEffect3OwnedControlledCameraFrame frame,
        MapPostEffect3DepthOfField2Controller controller,
        IReadOnlyList<D3D11NormalPostInitialPass> passes,float heatCoefficient,
        int gatherSize,int enableDof,IReadOnlyList<string> historyPairs)
        =>(Allocation,Frame,Controller,Passes,HeatCoefficient,GatherSize,EnableDof,HistoryPairs)=
            (allocation,frame,controller,passes,heatCoefficient,gatherSize,enableDof,historyPairs);

    public MapPostEffect3AllocationPlan Allocation{get;}
    public MapPostEffect3OwnedControlledCameraFrame Frame{get;}
    public MapPostEffect3DepthOfField2Controller Controller{get;}
    public IReadOnlyList<D3D11NormalPostInitialPass> Passes{get;}
    public float HeatCoefficient{get;}
    public int GatherSize{get;}
    public int EnableDof{get;}
    public IReadOnlyList<string> HistoryPairs{get;}

    public void WriteNativeInputs(string inputRoot)
    {
        string continuous=Path.Combine(inputRoot,"normal-post-continuous-input");
        string initial=Path.Combine(inputRoot,"normal-post-next-input","prior-cpu");
        Directory.CreateDirectory(continuous);
        // These inputs describe the fresh controller before its first Init.
        // Sprite Count10, history16 and staging28 are separate owners.
        File.WriteAllText(Path.Combine(continuous,"continuous-fixed.txt"),FormattableString.Invariant(
            $"{HeatCoefficient:R} {Allocation.ContextWidth} {Allocation.ContextHeight} {GatherSize} {EnableDof} {Controller.Count10} {(Controller.HistoryFlip16?1:0)} {Controller.SpriteSide28} 1 0 0\n"));
        File.WriteAllLines(Path.Combine(continuous,"ordered-required-color-passes.txt"),Passes.Select(p=>p.Name));
        File.WriteAllLines(Path.Combine(continuous,"history-pairs.txt"),HistoryPairs);
        foreach(var pass in Passes)
        {
            WritePass(continuous,pass);
            // prepareNormalPost's initial CB22 gate belongs to this same
            // current first preparation, not a preceding cached invocation.
            if(pass.Name is "108-tone" or "109-fxaa")WritePass(initial,pass);
        }
    }

    private static void WritePass(string root,D3D11NormalPostInitialPass pass)
    {
        string directory=Path.Combine(root,pass.Name);Directory.CreateDirectory(directory);
        foreach(var cb in pass.Constants.DistinctBy(cb=>cb.ParameterIndex))
            File.WriteAllBytes(Path.Combine(directory,$"cb{cb.ParameterIndex}.bin"),cb.Bytes.ToArray());
        File.WriteAllLines(Path.Combine(directory,"cbuffers.txt"),pass.Constants.Select(cb=>
            FormattableString.Invariant($"{cb.Stage} {cb.Slot} cb{cb.ParameterIndex}.bin")).Distinct());
    }
}

/// <summary>Creates all complete numeric buffers for the ordinary fresh
/// Main Post3 slice from its actual source-prepared Camera and RDB module.
/// Source programs and media remain owned by the resource preparation path.</summary>
public static class D3D11NormalPostInitialInputPreparation
{
    public static D3D11NormalPostInitialConstants Prepare(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame currentSeason,
        ReadOnlyMemory<byte>? actualSelectedLightData,long? actualSelectedLightFrameNumber,
        int outputWidth,int outputHeight)
    {
        var camera=currentSeason.CapturePreparedCamera();
        string locale=camera.ExecutableLocale,owner="native-normal-post/initial-numeric";
        long frame=camera.FrameNumber;
        var module=MapFinalOutputPostEffect3Source.Load(repository);
        var parsed=MapPostEffect3ContainerReader.Read(module);
        var allocation=MapPostEffect3AllocationReader.Read(parsed,(uint)outputWidth,(uint)outputHeight,owner);
        var native=MapPostEffect3OwnedControlledCameraSources.Load(repository,locale);
        var transfer=MapSeasonPreparedLutSource.Load(repository,locale)
            .CaptureActualMainPreparation(currentSeason.AcceptedSeasonCamera.Inputs,camera);
        var context=MainMapContextCurrentSource.CreateFresh(repository,locale,owner+"/Main-Q")
            .BeginDeferredPreparation(frame,owner+"/current-preparation");
        var selector=MapPostEffect3LutSelectorSource.Load(repository,locale).ResolveOwnedMain(context);
        var definitions=repository.LoadObjects("KIDSSystemResource",0xD2ECCCDF);
        var hdr=MapPostEffect3CurrentLutLookup.ResolveOwnedSource(repository,camera,true,
            definitions,"KIDSSystemResource","RRPreview",selector).JoinActualMainPreparation(transfer);
        var ldr=camera.LdrLutKey933==0?null:MapPostEffect3CurrentLutLookup.ResolveOwnedSource(
            repository,camera,false,definitions,"KIDSSystemResource","RRPreview",selector).JoinActualMainPreparation(transfer);
        var lens=MapPostEffect3LensPresetSource.LoadForCamera(repository,camera.Source,locale);
        var lookups=MapPostEffect3ApplyFreshLutLookups.ConstructFreshSource(camera,owner,lens,hdr,ldr);
        var hdrRecipe=MapPostEffect3StaticHdrNativeSource.Load(repository,locale).ReadSelectedCurrent(hdr);
        var commonTemplate=repository.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId);
        var common=MapCommonRenderState.FromSettings(
            CommonRenderResources.ImportSettings(commonTemplate),frame,
            owner+"/Settings",owner+"/Common",owner+"/View","fresh-native-post-numeric-owner");
        var aux=MapPostEffect3ControlledAuxSource.Load(repository,locale).CaptureFreshSelectedTemplate(
            commonTemplate,common,MapExposureViewAux.ReadTemplate(commonTemplate,CommonRenderResources.SourceObjectId));
        var callerSource=MapPostEffect3ControlledFreshCallerSource.Load(repository,locale);
        var fresh=callerSource.CaptureFreshCurrent(camera,transfer,lookups);
        var configuration=callerSource.CreateFreshOwnedConfiguration(owner+"/Configuration");
        var final=MapFinalOutputResources.Load(repository);
        byte mode=final.OutputTexture.OutputColorSpace==2?final.OutputTexture.ColorSpace:final.OutputTexture.OutputColorSpace;
        float gamma=MapPostEffect3OwnedDirectCaller.ResolveGammaArgument(
            BinaryPrimitives.ReadSingleLittleEndian(camera.Prefix.Span[0x174..]),mode==1,false);
        var raw=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,owner+"/Fields");
        var caller=new MapPostEffect3ControlledCameraCaller(frame,outputWidth,outputHeight,
            aux.Fxaa3A8|unchecked((int)raw.Word(0x414)),gamma,0,fresh.LdrIntensityXmm9,
            fresh.GatherSizeTypeC0,fresh.AdaptationLock,new(0,0,1,1),new(0,0,1,1),
            configuration.ProjectionConvention,0,"new native owner before first Apply; fresh clock0/delta0; final whole target");
        var execute=new MapPostEffect3ControllerExecuteCaller(frame,fresh.FreshControllerArgument3,0,
            fresh.FreshApplyArgument5,configuration.QualityLevel,1,"fresh owned controlled Main Post3");
        var selectedHdr=MapPostEffect3ControlledSelectedHdrTexture.CaptureActualStaticSelection(hdr,
            new(frame,owner+"/selected-HDR"),hdrRecipe.NativeDescriptorMetadata28Chain,hdrRecipe.SourceEvidence);
        var light=MapPostEffect3ControlledLightLookup.CaptureActualNumericLookup(camera,
            actualSelectedLightData,actualSelectedLightFrameNumber,owner+"/selected-light",
            "same current native season first-subset lookup; actual complete provider LightData or absent");
        var inputs=new MapPostEffect3OwnedControlledCameraInputs(transfer,selector,lookups,selectedHdr,light,
            caller,ReadOnlyMemory<byte>.Empty,aux.SourceEvidence,null,execute,NativeLogDispatch.Sse)
            {ActualAuxFields=aux,ActualFreshCaller=fresh};
        var owned=MapPostEffect3OwnedControlledCameraFrame.Prepare(module,allocation,camera,native,inputs);
        var table=MapPostEffect3TextureParameterState.CreateFreshWithSourceZeroAllocator(allocation,frame,
            allocation.TextureAllocations.ToDictionary(a=>a.Parameter.Index,_=>0),
            MapPostEffect3TextureZeroAllocationSource.Load(repository,locale),"fresh Post3 pointer table; no pixel values");
        table.BindExternal(0,new(frame,owner+"/current-Scene"),"same native current Scene InputColor0");
        var samplers=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        var freshDraw=RenderSamplerSourceAdapters.ReadFreshPostEffect3StockDrawSource(samplers);
        uint contextWord=BitConverter.SingleToUInt32Bits(freshDraw.FreshContext1C48);

        var dofSource=MapPostEffect3ParameterDepthOfField2.Load(repository,locale);
        var dofController=dofSource.ConstructFreshController(owned.State,allocation);
        var dofInputs=dofSource.CaptureCurrentInputs(owned.State,allocation,table,contextWord,
            (uint)configuration.ProjectionConvention,
            [new(table.Require(0),(ushort)outputWidth,(ushort)outputHeight,"current Scene wrapper extent")],
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,"fresh actual native Scene pointer");
        var dof=dofSource.PrepareAndInitialize(owned.State,allocation,dofInputs,dofController);
        table.ApplyDepthOfField2Alias(dof);

        var eyeSource=MapPostEffect3ParameterEyeAdaptation2.Load(repository,locale);
        var eyeOwner=eyeSource.ConstructFreshController(parsed,owner);
        var eye=eyeOwner.PrepareFrame(owned.State,allocation,NativeLogDispatch.Sse);
        var exposure=eyeSource.PrepareExposure(eye,LoadedNativeDoubleLogSource.Load(repository,locale),NativeLogDispatch.Sse);
        table.ApplyEyeResourceInitializationAlias(eyeSource);table.ApplyEyeInitializationAlias(eye);
        var bloom=allocation.TextureAllocations.Single(a=>a.Parameter.Index==85);
        MapPostEffect3Composite2ExternalExtent[] extents=[
            new(table.Require(68),(ushort)outputWidth,(ushort)outputHeight,"same current Scene wrapper"),
            new(table.Require(74),bloom.Width16,bloom.Height16,"actual Eye alias physical85 mip0")];
        var eyeExtents=extents.ToArray();
        var compositeSource=MapPostEffect3ParameterComposite2.Load(repository,locale);
        var compositeInputs=compositeSource.CaptureCurrentInputs(owned.State,allocation,table,contextWord,
            extents,MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,"fresh Composite before Init");
        var composite=compositeSource.Prepare(owned.State,allocation,compositeInputs);
        var manager=RenderSamplerSourceAdapters.ReadPostEffect3ManagerSamplers(samplers);
        var carriers=new MapPostEffect3Composite2ModuleLuts(frame,owner,new(frame,owner+"/linear-carrier"),
            new(frame,owner+"/point-carrier"),BinaryPrimitives.ReadUInt16LittleEndian(manager.LinearKey),manager.SourceEvidence);
        var compositeExecution=MapPostEffect3Composite2ExecutionSource.Load(repository,locale);
        var init=compositeExecution.PrepareInitialization(owned.State,allocation,table,composite,carriers,
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale);
        table.ApplyComposite2Initialization(init);composite.AcceptActualInitialization(init);
        // Native retained surfaces have already published Scene79 when the
        // reusable complete templates are consumed. Re-evaluate its getters
        // after that actual alias publication instead of retaining null1x1.
        extents[0]=new(table.Require(79),(ushort)outputWidth,(ushort)outputHeight,"current Composite Init68->79");
        compositeInputs=compositeSource.CaptureCurrentInputs(owned.State,allocation,table,contextWord,
            extents,MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,"Composite after its actual Init publication");
        composite=compositeSource.Prepare(owned.State,allocation,compositeInputs);

        var passes=new List<D3D11NormalPostInitialPass>();
        foreach(int pass in dof.PassSelection.OrderedFactory7Passes)
            passes.Add(Pass($"{pass:D3}-dof",pass,dof.ConstantBuffer));
        // The next CoC variant has the same complete CB18 writer. Its different
        // texture/history bindings remain actual RDB/pass-owner inputs.
        int cocIndex=passes.FindIndex(p=>p.SourcePassIndex==66);
        if(cocIndex>=0)passes.Insert(cocIndex+1,Pass("067-dof",67,dof.ConstantBuffer));
        var eyeInputs=eyeSource.CaptureCurrentInputs(eye,table,contextWord,eyeExtents,
            MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,"current Scene width/height");
        foreach(var iteration in eye.OrderedIterations.Where(i=>i.IsBloomGraphics))
        {
            var before=eyeOwner.PrepareBefore(eye,iteration,eyeInputs);
            table.ApplyEyeBeforeAlias(eye,before);
            string tag=iteration.SourcePassIndex switch{84=>"prefilter",86=>"down0",88=>"down1",90=>"up",
                _=>throw new NotSupportedException("This normal native slice needs another actual Bloom pass family.")};
            passes.Add(Pass($"{iteration.SourcePassIndex:D3}-bloom-{tag}-{iteration.NativeLoopIndex}",
                iteration.SourcePassIndex,before.BloomLoopConstants!,eye.BloomMainConstants));
        }
        foreach(int pass in composite.PassSelection.OrderedFactory9Passes.Where(p=>p is 100 or 101))
            passes.Add(Pass($"{pass:D3}-add-bloom",pass,composite.ConstantBuffer));
        foreach(var iteration in eye.OrderedIterations.Where(i=>i.IsCompute))
            passes.Add(Pass($"{iteration.SourcePassIndex}-exposure",iteration.SourcePassIndex,
                iteration.SourcePassIndex==102?[]:[exposure]));
        foreach(int pass in composite.PassSelection.OrderedFactory9Passes.Where(p=>p is 108 or 109))
            passes.Add(Pass(pass==108?"108-tone":"109-fxaa",pass,composite.ConstantBuffer));
        var sides=MapPostEffect3TextureZeroAllocationSource.Load(repository,locale).SelectFreshOwnedSides(allocation);
        var history=MapPostEffect3PassStateReader.ReadHistoryFlip(allocation,true).Select(p=>
        {
            int side=(p.Table switch{MapPostEffect3BindingTable.TextureParameters=>sides.TextureInstances,
                MapPostEffect3BindingTable.BufferParameters=>sides.BufferInstances,_=>sides.ConstantBufferInstances})[p.ParameterIndex];
            return FormattableString.Invariant($"{p.Table} {p.ParameterIndex} {side}");
        }).ToArray();
        var tail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,owner+"/tail");
        return new(allocation,owned,dofController,passes.AsReadOnly(),tail.Settings.HeatShimmer.Speed,fresh.GatherSizeTypeC0,
            fresh.GatherSizeTypeC0,Array.AsReadOnly(history));

        D3D11NormalPostInitialPass Pass(string name,int pass,
            params MapPostEffect3ParameterConstantBuffer[] buffers)
        {
            List<D3D11NormalPostInitialConstantBinding> constants=[];
            foreach(var program in allocation.Passes[pass].Pass.ProgramReferences.Where(p=>p.StageIndex is not null))
            {
                var rdef=DxbcResourceDefinitionReader.Read(parsed.GetProgram(program.StageCode,program.StageIndex!.Value).Bytes.Span);
                int mask=program.StageCode switch{5=>1,1=>2,0=>4,_=>throw new NotSupportedException("Unknown normal post program stage.")};
                foreach(var binding in rdef.Bindings.Where(b=>b.InputType==0))
                {
                    var bound=allocation.Passes[pass].ActiveBindings.Last(b=>
                        b.Role==MapPostEffect3BindingRole.ConstantBufferRead&&b.RegisterIndex==binding.BindPoint&&
                        (b.StageMask&mask)!=0);
                    var cb=buffers.Single(b=>b.Declaration.Index==bound.ParameterIndex);
                    var declared=rdef.Buffers.Single(b=>b.Name.Text==binding.Name.Text);
                    if(declared.ByteSize>cb.Bytes.Length)throw new InvalidDataException("Normal post CB writer is shorter than its actual shader declaration.");
                    constants.Add(new(program.StageCode==5?"CS":program.StageCode==1?"VS":"PS",
                        checked((int)binding.BindPoint),cb.Declaration.Index,cb.Bytes));
                }
            }
            return new(name,pass,constants.AsReadOnly());
        }
    }
}
