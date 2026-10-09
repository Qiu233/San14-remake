using Godot;
using San14.Data;

public sealed partial class MapPostEffect3LensExecutionOwner
{
    /// <summary>The exact original Ghost target setter plus the complete
    /// destination produced earlier in this same Lens chain. This helper
    /// supplies no raster, topology, viewport or pixel sampler defaults.</summary>
    public IMapPostEffect3StockGraphicsReadableTargetLease CaptureCurrentGhostTarget(
        MapPostEffect3LensExecutionRecipe recipe,MapPostEffect3TextureParameterState table)
    {
        ArgumentNullException.ThrowIfNull(recipe);ArgumentNullException.ThrowIfNull(table);
        if(!recipe.ExecutesPass||recipe.SourcePassIndex!=34||recipe.GhostDraw is null||
            recipe.SourceSha256!=Bank.Source.Source.Sha256||recipe.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            table.SourceSha256!=recipe.SourceSha256||table.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||table.FrameNumber!=recipe.FrameNumber)
            throw new InvalidDataException("Current Ghost destination requires the actual active source34/table/module/frame.");
        var binding=recipe.OrderedEffectiveBindings.LastOrDefault(b=>
            b.Table==MapPostEffect3BindingTable.TextureParameters&&b.Role==MapPostEffect3BindingRole.RenderTargetWrite&&
            b.RegisterIndex==0);
        var accepted=table.Require(41);
        if(binding is null||binding.ParameterIndex!=41||binding.NativeViewKind!=3||
            accepted.Reference.PhysicalAllocationParameterIndex!=41)
            throw new NotSupportedException("Ghost target requires the exact native factory4 target41 setter and physical allocation.");
        var texture=Bank.RequireTexture(41,accepted.Reference.InstanceIndex);
        if(!texture.Shape.HasRenderTargetPath||texture.Shape.MipLevels!=1||texture.Shape.Depth!=1||
            texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||texture.Shape.Formats.RenderTargetFormat0==0||
            !texture.NativeFormatViews.ContainsKey(texture.Shape.Formats.RenderTargetFormat0))
            throw new InvalidDataException("Actual source Ghost target has no original single-mip2D render-target view.");
        var contents=Bank.CaptureSourceWrittenTextureAliasLease(table,41,recipe.FrameNumber,0,1);
        return new CurrentGhostTarget(this,recipe,table,accepted,texture,contents);
    }

    private sealed record CurrentGhostTarget(MapPostEffect3LensExecutionOwner Owner,
        MapPostEffect3LensExecutionRecipe Recipe,MapPostEffect3TextureParameterState Table,
        MapPostEffect3ResolvedTextureParameter Accepted,MapPostEffect3DeviceTexture Texture,
        IMapPostEffect3ComputeMipTextureLease PriorContents):IMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber=>Recipe.FrameNumber;
        public string ContextOwnerIdentity=>Recipe.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>ContextOwnerIdentity+":source-Lens-factory4-direct-target34";
        public string ViewIdentity=>OwnedFrontendIdentity+":physical41/side"+InstanceIndex+"/native-format"+NativeViewFormat;
        public int? ParameterIndex=>41;
        public int InstanceIndex=>Texture.InstanceIndex;
        public string ProducerOwnerIdentity=>ContextOwnerIdentity;
        public Rid StorageRid=>Texture.StorageRid;
        public Rid TargetViewRid=>Texture.NativeFormatViews[NativeViewFormat];
        public uint NativeViewFormat=>Texture.Shape.Formats.RenderTargetFormat0;
        // Bank creation chooses RenderTargetFormat0 as its primary storage
        // format on this positively required render-target path.
        public RenderingDevice.DataFormat DeviceFormat=>Texture.StorageFormat;
        public Vector2I Extent=>new(checked((int)Texture.Shape.Width),checked((int)Texture.Shape.Height));
        public string SourceEvidence=>Recipe.SourceEvidence+";native-actual-target-setter41-kind3;"+Accepted.Reference.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {
            lock(Owner.lifetime)ObjectDisposedException.ThrowIf(Owner.retiring&&Owner.accepted==0,Owner);
            Table.ValidateCurrent(Accepted);
            if(pass!=34||!ReferenceEquals(Owner.Bank.RequireTexture(41,InstanceIndex),Texture))
                throw new InvalidDataException("Ghost target belongs to another source pass or its physical allocation retired.");
        }
        public void RequireReadableForPass(int pass)
        {RequireCurrentForPass(pass);PriorContents.RequireCurrent();PriorContents.RequireReadableMipRange(0,1);}
    }
}
