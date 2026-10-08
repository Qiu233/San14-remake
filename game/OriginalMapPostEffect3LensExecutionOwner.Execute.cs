using San14.Data;

public sealed partial class OriginalMapPostEffect3LensExecutionOwner
{
    private async Task<OriginalMapPostEffect3LensCompletedExecution> ExecuteAcceptedAsync(OriginalMapPostEffect3LensExecutionFrame frame)
    {
        await serial.WaitAsync();var submitted=new List<OriginalMapPostEffect3LensSubmittedPass>();
        try
        {
            LastCompleted=null;LastSubmittedPrefix=[];ValidateFrame(frame);
            foreach(var cb in frame.Initialization.ConstantBuffers)
                await Bank.UploadCompleteConstantBufferAsync(cb,frame.ActualConstantBufferInstances[cb.Declaration.Index]);
            foreach(var buffer in OriginalMapPostEffect3LensBufferUploads.Prepare(Bank.Source,frame.Initialization))
                await Bank.UploadCompleteLensBufferAsync(buffer,frame.ActualBufferInstances[buffer.Declaration.Parameter.Index]);
            if(frame.ActualFinishedMiddle is null)frame.TextureParameters.ApplyLensFlareAliases(frame.Initialization);
            foreach(int index in Enumerable.Range(20,24))
            {
                ValidateFrame(frame);
                var recipe=OriginalMapPostEffect3LensExecutionRecipe.Prepare(frame.Scalars,Bank.Source,frame.Initialization,
                    frame.Lens,index,new(Bank.Source.Passes[index].DispatchExtent??new(0,0,0),
                        "actual-counted-pass-setup-dispatch;factory4-native-order20..43"));
                if(!recipe.ExecutesPass){Record(new(index,false,null,null,recipe.SourceEvidence));continue;}
                if(index==34)
                {
                    var sprite=await Bank.PrepareBufferShaderReadViewAsync(2,frame.ActualBufferInstances[2],recipe.FrameNumber);
                    var star=ReadTexture(frame,recipe,36);
                    var input=frame.SourceInputs.PrepareGhostDraw(recipe,sprite,star);
                    if(!ReferenceEquals(input.TextureParameters,frame.TextureParameters)||
                        input.CurrentSpriteBuffer.StorageRid!=sprite.StorageRid||input.CurrentSpriteBuffer.ShaderReadRid!=sprite.ShaderReadRid||
                        input.CurrentStarBurst.StorageRid!=star.StorageRid)
                        throw new InvalidDataException("Lens source Ghost draw replaced the actually produced Sprite/StarBurst/table.");
                    var actual=await Ghost.DrawAsync(recipe,input);
                    RetainCompletedNativeSamplerWrites(recipe,null,actual);
                    if(actual.TextureWrite is not null)Bank.RetainLensGhostTextureCoverage(recipe,Ghost,actual);
                    Record(new(index,true,null,actual,recipe.SourceEvidence));
                    continue;
                }
                var compiled=await Programs.GetCompiledPassAsync(index);
                var inputs=ComputeInputs(frame,recipe,compiled);
                var completed=await Compute.DispatchAsync(index,inputs);
                RetainCompletedNativeSamplerWrites(recipe,completed,null);
                if(index is 30 or 31)
                {
                    var proof=OriginalMapPostEffect3LensWriteCoverage.Prepare(Bank.Source,recipe);
                    Bank.PublishLensBufferCoverage(proof,Compute,completed);
                    if(index==30)await Bank.PrepareBufferShaderReadViewAsync(2,frame.ActualBufferInstances[2],recipe.FrameNumber);
                }
                else if(index==32)
                    Bank.PublishLensTextureCoverage(OriginalMapPostEffect3LensWriteCoverage.Prepare(Bank.Source,recipe),Compute,completed);
                else if(index==41)
                    Bank.PublishToneInfoRegionCoverage(inputs.ToneInfoSourceValueInfluence??
                        throw new InvalidDataException("Actual fresh ToneInfo region value-influence proof absent."),Compute,completed);
                else
                {
                    foreach(var proof in OriginalMapPostEffect3LensTextureWriteCoverage.Prepare(Bank.Source,recipe,completed.SourceNullShaderResources))
                        Bank.PublishLensTextureCoverage(proof,Compute,completed);
                }
                Record(new(index,true,completed,null,recipe.SourceEvidence));
            }
            ValidateFrame(frame);
            var outputs=frame.Initialization.TextureAliases.Select(a=>frame.TextureParameters.Require(a.DestinationParameterIndex)).ToArray();
            LastCompleted=new(frame.Initialization.FrameNumber,Bank.Source.ContextOwnerIdentity,submitted.AsReadOnly(),Array.AsReadOnly(outputs),
                frame.Initialization.SourceEvidence+";actual-ordered-Lens20..43;source-Before-skips-explicit;no-Host/Functor0-completion-claim");
            return LastCompleted;
        }
        finally{serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
        void Record(OriginalMapPostEffect3LensSubmittedPass pass)
        {submitted.Add(pass);LastSubmittedPrefix=Array.AsReadOnly(submitted.ToArray());}
    }
}
