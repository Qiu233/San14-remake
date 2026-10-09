using System.Security.Cryptography;
using San14.Data;

/// <summary>Published only by the scene after every actual ordered late draw
/// returns. It is available inside AfterDeferred, before final presentation.
/// It proves submission of the retained roster, not a GPU fence or a native
/// PostEffect target setter. Scene failure/next attempt/retirement invalidates it.</summary>
public sealed partial class NativeMapWorldDeferredSubmittedFrame
{
    private readonly NativeMapScene scene;
    private readonly IReadOnlyList<SubmittedPass> passes;
    internal NativeMapWorldDeferredSubmittedFrame(NativeMapScene owner,
        NativeMapFrameContext context, DeferredPixelProgram program,
        IReadOnlyList<SubmittedPass> submitted)
    {
        scene=owner;Context=context;Program=program;
        passes=Array.AsReadOnly(submitted.ToArray());
        if(passes.Count==0)throw new InvalidDataException("A late publication needs actual nonempty draw submissions.");
    }
    public NativeMapFrameContext Context{get;}
    public RenderTargetFrame Target=>Context.OutputFrame;
    public DeferredPixelProgram Program{get;}
    public int SubmittedPassCount=>passes.Count;
    public string SourceEvidence=>Program.Source.Source.ResourceUri+"@"+Program.Source.Source.DecodedSha256+
        ";actual-ordered-late-draws="+passes.Count+";frame="+Context.FrameNumber+
        ";pixel="+Program.UnspecializedTranslation.SourceProgramSha256;

    public void RequireCurrent(NativeMapFrameContext actualContext)
        =>scene.RequireCurrentWorldDeferredSubmission(this,actualContext);

    internal void RequireExactOrderedPackets(DeferredPixelProgram actualProgram,
        IReadOnlyList<NativeDeferredPacket> packets)
    {
        RequireCurrent(Context);
        if(!ReferenceEquals(actualProgram,Program)||packets.Count!=passes.Count)
            throw new InvalidDataException("Submitted late roster has another source program or pass count.");
        for(int i=0;i<packets.Count;i++)passes[i].RequireExact(packets[i]);
    }

    internal sealed partial class SubmittedPass
    {
        private readonly IReadOnlyDictionary<uint,string> hashes;
        private readonly IReadOnlyList<DeferredDeviceBinding> inputs;
        private readonly DeferredRasterState raster;
        private readonly DeferredTranslucencyViewportInputs? viewport;
        internal SubmittedPass(DeferredDraw actualDraw,NativeDeferredPacket packet)
        {
            hashes=actualDraw.ConstantBytes.ToDictionary(p=>p.Key,p=>Hash(p.Value));
            inputs=Array.AsReadOnly(actualDraw.Inputs.ToArray());
            raster=packet.Raster;viewport=packet.TemporaryTranslucencyViewport;
            RequireExact(packet);
        }
        internal void RequireExact(NativeDeferredPacket packet)
        {
            if(packet.Raster!=raster||packet.TemporaryTranslucencyViewport!=viewport||
                !packet.Inputs.SequenceEqual(inputs)||packet.Constants.Count!=hashes.Count||
                hashes.Any(p=>!packet.Constants.TryGetValue(p.Key,out var bytes)||Hash(bytes.Span)!=p.Value))
                throw new InvalidDataException("Actual submitted late pass differs from its source constants/resources/raster/viewport/order.");
        }
        private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    }
}
