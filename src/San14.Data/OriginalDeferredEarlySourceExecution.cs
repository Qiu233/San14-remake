namespace San14.Data;

public sealed record OriginalDeferredEarlySourceBin(int GraphNodeIndex, uint LightRecordMask78,
    uint ShaderMode, uint StencilReference, string CurrentLightOwnerIdentity,
    string CurrentPublicationProvenance)
{
    public uint BackendStencilWriteMask => 0;
    public string StencilWriteMaskScope => "native retains its write mask; Equal/all-Keep makes the backend zero mask observationally equivalent";
}

public sealed record OriginalDeferredEarlyWorldSourceBin(OriginalDeferredEarlySourceBin Bin,
    OriginalMapSourceCreatedWorldF103Pass Pass);

/// <summary>Actual early F103 mode gates and the four native inner bins.
/// This describes admitted draws, and does not establish GPU stencil contents,
/// current shader resources, native callback completion or pixel production.</summary>
public static class OriginalDeferredEarlySourceExecution
{
    public const string NativeRecipe = "ce-20261002-F103-early-EAD355/EADDE8/EAED63";

    public static IReadOnlyList<OriginalDeferredEarlySourceBin> ReadBins(
        LoadedOriginalDeferredTexturePlan source, OriginalMainMapContextCurrentPacket currentQ,
        OriginalMapSourceCreatedViewFieldsPacket currentView,
        byte? currentCommonViewAux3F2)
        => ReadCore(source,currentQ,currentView,currentCommonViewAux3F2,false);

    /// <summary>Resolve each physical inner record to its actual ordered World
    /// feature and independent modifier. The single-owner API stays strict.</summary>
    public static IReadOnlyList<OriginalDeferredEarlyWorldSourceBin> ReadWorldBins(
        LoadedOriginalDeferredTexturePlan source,OriginalMainMapContextCurrentPacket currentQ,
        OriginalMapSourceCreatedViewFieldsPacket currentView,byte? currentCommonViewAux3F2,
        OriginalMapSourceCreatedWorldF103Frames world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if(!ReferenceEquals(world.View,currentView)||world.Passes.Count==0)
            throw new InvalidDataException("World early bins require their exact counted View and real ordered pass owners.");
        var bins=ReadCore(source,currentQ,currentView,currentCommonViewAux3F2,true);
        uint exclusion=~(1u<<(((currentCommonViewAux3F2 is null or 0?8:currentCommonViewAux3F2.Value)-1)&31));
        var outer=currentView.Kind1Records.Single(record=>(record.Mask78&exclusion)==0);
        return Array.AsReadOnly(bins.Select(bin=>
        {
            var pass=world.Passes.Single(p=>p.Association.OuterRecord.RecordOwnerIdentity==outer.LightOwnerIdentity&&
                p.Association.InnerRecord.RecordOwnerIdentity==bin.CurrentLightOwnerIdentity&&
                p.Association.EnabledInnerBit==bin.LightRecordMask78);
            pass.Frame.Feature.ValidateFrame(pass.Frame.Camera,pass.Frame.Lighting);
            pass.InnerLighting.ValidateCurrentSourceFrame();
            if(pass.Frame.FrameNumber!=currentQ.FrameNumber||pass.InnerLighting.FrameNumber!=currentQ.FrameNumber||
                pass.InnerLighting.SourceIdentity.MainLightObjectId!=pass.Association.InnerRecord.Source.SourceObjectId||
                !pass.Frame.Feature.SelectedModifierLightData.Span.SequenceEqual(pass.InnerLighting.LocalLightData.Span))
                throw new InvalidDataException("World early association does not own its actual independent inner payload.");
            return new OriginalDeferredEarlyWorldSourceBin(bin,pass);
        }).ToArray());
    }

    private static IReadOnlyList<OriginalDeferredEarlySourceBin> ReadCore(
        LoadedOriginalDeferredTexturePlan source,OriginalMainMapContextCurrentPacket currentQ,
        OriginalMapSourceCreatedViewFieldsPacket currentView,byte? currentCommonViewAux3F2,bool worldOwners)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(currentQ);
        ArgumentNullException.ThrowIfNull(currentView);
        OriginalDeferredEarlySceneConstants.ValidateSource(source);
        currentQ.Source.Validate(currentQ);
        if (currentQ.FrameNumber <= 0 || currentQ.FrameNumber != currentView.Camera.FrameNumber ||
            currentView.Kind1Records.Any(record => record.CurrentCpuPrimitiveCount < 0 ||
                string.IsNullOrWhiteSpace(record.LightOwnerIdentity) || string.IsNullOrWhiteSpace(record.CurrentCountPublicationIdentity)))
            throw new InvalidDataException("Early source bins require complete same-frame owned View and Q publications.");
        // E942E4..EF is the task wrapper's signed Q3F0 admission check.
        // A later BeginCheckSkip publication must not execute early F103.
        if ((currentQ.ContextFlags3F0 & 0x80000000u) != 0)
            return Array.Empty<OriginalDeferredEarlySourceBin>();
        uint mode = source.Node.Index switch { 79 => 0x10u, 80 => 0x40u, _ => 0u };
        bool admitted = source.Node.Index switch
        {
            77 => (currentQ.ContextFlags3F4 & 1) != 0,
            79 => (currentQ.ContextFlags3F0 & 0x20000000) != 0,
            80 => (currentQ.ContextFlags3F0 & 0x04000000) != 0,
            _ => true
        };
        if (!admitted) return Array.Empty<OriginalDeferredEarlySourceBin>();
        uint exclusion = ~(1u << (((currentCommonViewAux3F2 is null or 0 ? 8 : currentCommonViewAux3F2.Value) - 1) & 31));
        var outer = currentView.Kind1Records.Where(record => (record.Mask78 & exclusion) == 0).ToArray();
        if (outer.Length == 0)
            throw new InvalidDataException("Early F103 requires its actual selected outer kind1 cache record before inner-bin preparation.");
        if (outer.Select(record => record.LightOwnerIdentity).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new NotSupportedException("Early single-source lighting requires one resolved kind1 cache generation.");
        // EADC*/EADD*: the inner index is exactly0..3. It is independent
        // of source LightGroupID8 and its outer exclusion maskFFFFFF7F.
        var result = new List<OriginalDeferredEarlySourceBin>();
        for (int group = 0; group < 4; group++)
        {
            uint bit = 1u << group;
            if ((currentView.ViewFlags19AC & bit) == 0) continue;
            foreach (var record in currentView.Kind1Records.Where(record => record.Mask78 == bit))
            {
                if (!worldOwners && record.LightOwnerIdentity != outer[0].LightOwnerIdentity)
                    throw new NotSupportedException("Another current kind1 light requires its own ordered parameter producer.");
                // EADDB3..BF: a registered LightMap overrides the ordinary
                // mode in the stencil partition, while its execution mode0
                // remains unchanged.
                uint stencilMode = source.Node.Index == 77 ? 0xD0u : mode;
                result.Add(new(source.Node.Index, bit, mode, bit | stencilMode, record.LightOwnerIdentity,
                    currentView.SourceProvenance + ";" + record.CurrentCountPublicationIdentity + ";outer-mask=" +
                    exclusion.ToString("X8") + ";" + NativeRecipe));
            }
        }
        return result.AsReadOnly();
    }
}
