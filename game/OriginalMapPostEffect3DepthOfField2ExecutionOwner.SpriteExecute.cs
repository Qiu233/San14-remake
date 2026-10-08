using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3DepthOfField2ExecutionOwner
{
    private async Task ExecuteComputeAsync(OriginalMapPostEffect3DepthOfField2BeforeBindings before)
    {
        RequireCurrent();var actual=invocation as IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease
            ??throw new InvalidDataException("DOF2 selected compute lacks its actual sprite invocation producer.");
        int pass=before.SourcePassIndex;
        if(pass is not(57 or 58)||before.OrderedDynamicBindings.Count!=0||before.RequiresNativeIndirectDraw)
            throw new NotSupportedException("DOF2 source compute profile requires its original CS45/46 graph.");
        await actual.ApplyBeforeAndBindComputeAsync(before);RequireCurrent();
        var nulls=OriginalMapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,TextureParameters,pass,5);
        var compiled=await Compute.Library.GetCompiledPassAsync(pass,nulls);
        var reads=new List<IOriginalMapPostEffect3ComputeTextureLease>();
        foreach(var descriptor in compiled.Stages.Single().Bindings.Bindings.Where(b=>b.DescriptorKind=="Texture"))
        {
            var row=Bank.Source.Passes[pass].ActiveBindings.Last(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&(b.StageMask&1)!=0&&b.RegisterIndex==descriptor.Source.BindPoint);
            if(row.ParameterIndex is not(59 or 62))throw new InvalidDataException("DOF2 original CS46 texture input differs.");
            reads.Add(Bank.CaptureSourceWrittenTextureAliasLease(TextureParameters,row.ParameterIndex,Prepared.FrameNumber,0,1));
        }
        var cbs=Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead)
            .Select(b=>b.ParameterIndex).Distinct().ToDictionary(i=>i,i=>cbSides[i]);
        var outputSides=Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Table==OriginalMapPostEffect3BindingTable.BufferParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite).Select(b=>b.ParameterIndex).Distinct().ToDictionary(i=>i,i=>bufferSides[i]);
        if(pass==58&&currentArguments is null)throw new InvalidDataException("DOF2 CS46 needs its actual completed CS45 raw32B counter domain.");
        var completed=await Compute.DispatchAsync(pass,new(Prepared.FrameNumber,Array.AsReadOnly(reads.ToArray()),[],cbs,new Dictionary<int,int>())
            {TextureParameters=TextureParameters,OutputBufferInstances=outputSides,CurrentBuffers=pass==58?[currentArguments!]:[],
                DepthOfField2Prepared=Prepared,DepthOfField2Before=before});
        currentArguments=Bank.PublishDepthOfField2ArgumentCoverage(Compute,completed,before);
        ModuleResources.PublishArguments(this,completed,currentArguments);
        if(pass==58)await CaptureSpritePrefixesAsync(completed);
        computes.Add(completed);await actual.AcceptCompletedComputeAsync(before,Compute,completed);
    }
    private async Task ExecuteScatterAsync(OriginalMapPostEffect3DepthOfField2BeforeBindings before)
    {
        RequireCurrent();var actual=invocation as IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease
            ??throw new InvalidDataException("DOF2 scatter requires its actual current native invocation.");
        var executor=scatter??throw new InvalidDataException("DOF2 original scatter executor is absent.");
        int parameter=before.SourcePassIndex==62?7:6;
        if(!spritePrefixes.TryGetValue(parameter,out var prefix)||currentArguments is null)
            throw new InvalidDataException("DOF2 scatter needs the actual completed source CS46 sprite prefix/counters.");
        var view=Bank.ReadDepthOfField2ScatterTargetView(TextureParameters,Prepared,before);
        var context=await invocation.ApplyBeforeAndBindGraphicsAsync(before,[view]);
        var read=await Bank.CaptureDepthOfField2SpriteBufferReadAsync(prefix);
        var prior=before.RequiresNativeClearMaskAndColorOverride?null:ModuleResources.CaptureAccumulation(this,before,view);
        var completed=await executor.DrawAsync(new(State,Prepared,before,TextureParameters,view,context,prefix,read,currentArguments,cbSides[18])
            {PriorDestination=prior});
        submitted.Add(completed);var pixels=Bank.PublishDepthOfField2ScatterCoverage(executor,completed);
        ModuleResources.RecordAccumulation(this,completed,pixels);
        await actual.AcceptCompletedScatterAsync(executor,completed);
    }
}
