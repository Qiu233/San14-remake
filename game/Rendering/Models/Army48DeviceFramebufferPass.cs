using Godot;
using San14.Data;

/// <summary>The explicit backend keeps all six source node48 color views and
/// E13 in one framebuffer. Each unchanged original pixel shader supplies its
/// actual outputs; unwritten physical attachments are not referenced or
/// written. They stay allocated with their existing contents.
/// This is a device render-pass description, not a changed source binding.</summary>
public sealed class Army48DeviceFramebufferPass
{
    private Army48DeviceFramebufferPass(LoadedRenderGraphTargets source,IReadOnlyList<uint> outputs)
    {
        Source=source;
        var selected=outputs.ToHashSet();
        ColorAttachmentIndices=Array.AsReadOnly(Enumerable.Range(0,source.ColorTargets.Count)
            .Select(index=>selected.Contains((uint)index)?index:checked((int)RDFramebufferPass.AttachmentUnused)).ToArray());
        UnwrittenAttachmentIndices=Array.AsReadOnly(Enumerable.Range(0,source.ColorTargets.Count)
            .Where(index=>!selected.Contains((uint)index)).ToArray());
        ShaderOutputMask=selected.Aggregate(0u,(mask,index)=>mask|(1u<<checked((int)index)));
    }
    public LoadedRenderGraphTargets Source{get;}
    public IReadOnlyList<int> ColorAttachmentIndices{get;}
    public IReadOnlyList<int> UnwrittenAttachmentIndices{get;}
    public int DepthAttachmentIndex=>6;
    public int PhysicalAttachmentCount=>7;
    public uint ShaderOutputMask{get;}

    public static Army48DeviceFramebufferPass Prepare(ArmyWorldGraphicsTargetSource actualSource,
        IReadOnlyList<uint> actualShaderOutputLocations)
    {
        ArgumentNullException.ThrowIfNull(actualSource);
        ArgumentNullException.ThrowIfNull(actualShaderOutputLocations);
        if(actualShaderOutputLocations.Count==0||actualShaderOutputLocations.Any(index=>index>=6)||
            actualShaderOutputLocations.Distinct().Count()!=actualShaderOutputLocations.Count)
            throw new InvalidDataException("Army device subpass needs the actual distinct shader outputs within its six source slots.");
        return new(actualSource.Source,actualShaderOutputLocations);
    }
    internal void RequireSource(LoadedRenderGraphTargets actualSource)
    {
        if(!ReferenceEquals(Source,actualSource)||actualSource.Node.Index!=48||actualSource.ColorTargets.Count!=6||
            actualSource.DepthTarget is null||actualSource.DepthObjectNameHash!=0xE13DDB4F||actualSource.DepthSurfaceId!=0)
            throw new InvalidDataException("Army device subpass requires its actual six-color/E13 node48 declaration.");
    }
    internal RDFramebufferPass CreateDevicePass()=>new()
    {
        ColorAttachments=ColorAttachmentIndices.ToArray(),
        // VkRenderPassCreateInfo2 None-10916 requires an earlier dependent
        // use for preserve attachments. This single subpass has none. Truly
        // unused attachments are unreferenced, so their pixels are untouched.
        PreserveAttachments=Array.Empty<int>(),
        DepthAttachment=DepthAttachmentIndex
    };
}
